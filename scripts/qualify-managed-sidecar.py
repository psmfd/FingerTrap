#!/usr/bin/env python3
"""Run a selected real Pi through the published test host's shared PTY/RPC entry.

The product has no managed selector. Build FingerTrap.Sidecar.Qualification to
exercise the same Program, RpcSurface and native companion with internal inputs.
"""
import argparse
import base64
import concurrent.futures
import json
import os
from pathlib import Path
import queue
import shutil
import signal
import subprocess
import tempfile
import threading

from pi_startup_fixture import Fixture, digest, probe_extension, reap, records, seed, snapshot, wait_until


class Host:
    def __init__(self, executable, selection, home):
        home.mkdir(parents=True, exist_ok=True)
        self.notifications, self.errors, self.messages = [], [], queue.Queue()
        self.counter = 0
        env = {"HOME": str(home), "USERPROFILE": str(home), "PATH": "/usr/bin:/bin",
            "XDG_CONFIG_HOME": str(home / ".config"), "FT_ISOLATION_SENTINEL": "synthetic-parent-only",
            "NODE_OPTIONS": "--no-warnings", "LANG": "en_US.UTF-8"}
        self.child = subprocess.Popen([str(executable), *selection], env=env, cwd=home,
            stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE, start_new_session=True)
        self.readers = [threading.Thread(target=self.read, daemon=True), threading.Thread(target=self.read_errors, daemon=True)]
        for reader in self.readers:
            reader.start()

    def read(self):
        try:
            stream = self.child.stdout
            while True:
                length = None
                while line := stream.readline():
                    if line in (b"\r\n", b"\n"):
                        break
                    if line.lower().startswith(b"content-length:"):
                        length = int(line.split(b":", 1)[1])
                if length is None:
                    break
                if length > 1024*1024:
                    raise ValueError("oversized response")
                value = json.loads(stream.read(length))
                if "id" in value:
                    self.messages.put(value)
                else:
                    self.notifications.append(value)
        except (ValueError, OSError) as error:
            self.errors.append(str(error))
        finally:
            self.messages.put({"eof": True})

    def read_errors(self):
        for line in self.child.stderr:
            if len(self.errors) < 30:
                self.errors.append(line.decode(errors="replace")[:500])

    def send(self, method, params, identifier=None):
        value = {"jsonrpc": "2.0", "method": method, "params": params}
        if identifier is not None:
            value["id"] = identifier
        body = json.dumps(value).encode()
        self.child.stdin.write(f"Content-Length: {len(body)}\r\n\r\n".encode() + body)
        self.child.stdin.flush()

    def call(self, method, params):
        self.counter += 1
        self.send(method, params, self.counter)
        value = self.messages.get(timeout=25)
        if value.get("id") != self.counter:
            raise RuntimeError("unexpected response or host exit: " + str(value))
        return value

    def require(self, method, params):
        value = self.call(method, params)
        if "error" in value:
            raise RuntimeError(method + ": " + str(value["error"]))
        return value.get("result")

    def close(self):
        if self.child.poll() is None:
            self.send("shutdown", {})
            reap(self.child)
        self.child.stdin.close()
        for reader in self.readers:
            reader.join(timeout=2)
        self.child.stdout.close()
        self.child.stderr.close()


def settings(home, profile):
    definition = json.dumps({"version": 2, "pi": {"mode": "managed", "profileId": profile, "runtimeId": "pi-managed-candidate"}})
    # Match .NET's ApplicationData convention in each supported qualifier OS.
    location = home / ("Library/Application Support/fingertrap/settings.json" if os.uname().sysname == "Darwin"
        else ".config/fingertrap/settings.json")
    seed(location, definition)
    return location


def run_profile(executable, root, app, artifact, descriptor, descriptor_sha, revision, profile):
    fixture = Fixture.create(root)
    profile_root = app / "profiles" / profile
    profile_root.mkdir(parents=True)
    # The host intentionally retains its pre-S4 profile startup-config guard.
    for name in ("SYSTEM.md", "APPEND_SYSTEM.md"):
        (fixture.agent / name).unlink()
    shutil.move(fixture.agent, profile_root / "agent")
    shutil.move(fixture.home, profile_root / "home")
    fixture.agent, fixture.home = profile_root / "agent", profile_root / "home"
    for name in ("config", "data", "state", "local", "roaming"):
        (fixture.home / name).mkdir()
    fixture.before = {str(path): snapshot(path) for path in (fixture.project, fixture.agent, fixture.home)}
    host_home = root / "operator-home"
    settings_path = settings(host_home, profile)
    seed(host_home / ".pi/agent/auth.json", '{"synthetic":"must-not-inherit"}')
    host = Host(executable, [str(app), str(artifact), str(descriptor), descriptor_sha, revision, str(fixture.tools), str(settings_path)], host_home)
    checks, errors, pids = {}, [], set()
    def observed(mode):
        return fixture.agent / f"probe-{mode}.jsonl"
    def count(mode, kind):
        return sum(item["kind"] == kind for item in records(observed(mode)))
    def pty_send(text):
        host.require("pty/write", {"sessionId": "pty", "dataBase64": base64.b64encode((text+"\r").encode()).decode()})
    try:
        host.require("rpc/spawn", {"sessionId": "rpc", "cwd": str(fixture.project)})
        host.require("pty/spawn", {"sessionId": "pty", "cwd": str(fixture.project), "cols": 120, "rows": 40, "kind": "pi"})
        for mode in ("rpc", "pty"):
            wait_until(lambda: count(mode, "start") >= 1, host.child)
        checks["bothTransportsStarted"] = True
        # Use explicit extension commands through the regular prompt/terminal API.
        host.require("rpc/prompt", {"sessionId": "rpc", "message": "/ft-probe-new"})
        pty_send("/ft-probe-new")
        for mode in ("rpc", "pty"):
            wait_until(lambda: count(mode, "load") == 2, host.child)
            wait_until(lambda: any(item.get("reason") == "new" for item in records(observed(mode))), host.child)
        host.require("rpc/prompt", {"sessionId": "rpc", "message": "/ft-probe-reload"})
        pty_send("/ft-probe-reload")
        for mode in ("rpc", "pty"):
            wait_until(lambda: count(mode, "load") == 3, host.child)
            wait_until(lambda: any(item.get("reason") == "reload" for item in records(observed(mode))), host.child)
        host.require("rpc/abort", {"sessionId": "rpc"})
        checks["cancelAndState"] = host.require("rpc/getState", {"sessionId": "rpc"})["success"] is True
        for mode in ("rpc", "pty"):
            fixture.observed = observed(mode)
            checks.update({mode+"-"+key: value for key,value in fixture.checks(prompt="You are a coding assistant.").items()})
            starts = [item for item in records(observed(mode)) if item["kind"] == "start"]
            checks[mode+"-profileRoots"] = all(item["home"] == str(fixture.home) and item["agent"] == str(fixture.agent)
                and item["sessionDir"] == str(fixture.agent / "sessions") and item["profile"] == profile for item in starts)
            checks[mode+"-noParentEnvironment"] = all("sentinel" not in item and "nodeOptions" not in item for item in starts)
            pids.update(item["pid"] for item in starts)
        # Raw overrides/resume must fail before another child is created.
        for name, options in (("override", {"env": {"HOME": str(root)}}), ("resume", {"sessionPath": "/synthetic/session.jsonl"})):
            value = host.call("rpc/spawn", {"sessionId": name, "cwd": str(fixture.project), **options})
            checks["reject-"+name] = "error" in value
        host.require("rpc/kill", {"sessionId": "rpc"})
        checks["ptySurvivesRpcShutdown"] = all(pid != 0 for pid in pids) and count("pty", "load") == 3
        pty_send("/ft-probe-ready")
        wait_until(lambda: count("pty", "ready") == 1, host.child)
        host.require("pty/kill", {"sessionId": "pty"})
    except (RuntimeError, TimeoutError, queue.Empty, OSError, KeyError, TypeError) as error:
        checks["lifecycleCompleted"] = False
        errors.append(type(error).__name__+": "+str(error))
    finally:
        for mode in ("rpc", "pty"):
            pids.update(item["pid"] for item in records(observed(mode)) if item["kind"] == "start")
        host.close()
    def dead(pid):
        try:
            os.kill(pid, 0)
            return False
        except ProcessLookupError:
            return True
    try:
        wait_until(lambda: all(dead(pid) for pid in pids), timeout=10)
        checks["allChildrenReaped"] = bool(pids)
    except TimeoutError:
        checks["allChildrenReaped"] = False
        for pid in pids:
            if not dead(pid):
                os.kill(pid, signal.SIGKILL)
    checks["hostCleanExit"] = host.child.returncode == 0
    return {"profile": profile, "checks": checks, "passed": all(checks.values()), "diagnostics": errors+host.errors}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--host", type=Path, required=True)
    parser.add_argument("--host-sha256", required=True)
    parser.add_argument("--runtime", type=Path, required=True)
    parser.add_argument("--binary-sha256", required=True)
    parser.add_argument("--source-revision", required=True)
    parser.add_argument("--report", type=Path, required=True)
    args = parser.parse_args()
    if digest(args.host) != args.host_sha256 or digest(args.runtime / "pi") != args.binary_sha256:
        parser.error("independently selected executable digest mismatch")
    if len(args.source_revision) != 40:
        parser.error("exact committed source revision required")
    companion = args.host.parent / "libporta_pty.dylib"
    if not companion.exists():
        companion = args.host.parent / "libporta_pty.so"
    with tempfile.TemporaryDirectory(prefix="ft-managed-sidecar-") as scratch:
        root = Path(scratch).resolve()
        artifact, app = root / "runtime", root / "app"
        shutil.copytree(args.runtime, artifact)
        app.mkdir()
        (artifact / "qualification-probe.mjs").write_text(probe_extension(
            'process.env.PI_CODING_AGENT_DIR + "/probe-" + (process.argv.includes("rpc") ? "rpc" : "pty") + ".jsonl"'))
        descriptor = root / "runtime.json"
        inventory = [{"path": str(path.relative_to(artifact)), "sha256": digest(path)} for path in sorted(artifact.rglob("*")) if path.is_file()]
        descriptor.write_text(json.dumps({"version":2, "runtimeId":"pi-managed-candidate", "forkRevision":args.source_revision,
            "startupPolicy":"managed-v1", "platform":"darwin" if os.uname().sysname == "Darwin" else "linux", "architecture":"arm64" if os.uname().machine == "arm64" else "x64",
            "executable":"pi", "files":inventory, "extensions":["qualification-probe.mjs"]}))
        selected_digest = digest(descriptor)
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
            futures = [pool.submit(run_profile, args.host, root / profile, app, artifact, descriptor,
                selected_digest, args.source_revision, profile) for profile in ("alpha", "beta")]
            profiles = [future.result() for future in futures]
        report = {"sourceRevision":args.source_revision, "binarySha256":args.binary_sha256,
            "qualificationHostSha256":args.host_sha256, "nativeCompanionSha256":digest(companion),
            "descriptorSha256":selected_digest, "inventory":inventory, "profiles":profiles,
            "passed":all(profile["passed"] for profile in profiles)}
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(json.dumps(report,indent=2)+"\n")
        print(json.dumps({key:value for key,value in report.items() if key != "inventory"},indent=2))
        raise SystemExit(0 if report["passed"] else 1)


if __name__ == "__main__":
    main()
