#!/usr/bin/env python3
"""Run exact local qualification artifacts in a disposable, offline Tart clone.

Uses the reviewed FingerTrap base/helper and controller lock. No host shares,
GitHub registration or credentials; the stopped base and other VMs are preserved.
Requires host permissions to create/run/delete the uniquely generated clone.
"""
import argparse
import fcntl
import json
import os
from pathlib import Path
import signal
import stat
import subprocess
import tarfile
import tempfile
import time
import uuid

from pi_startup_fixture import digest

BASE = "ft-actions-base-20261020"
SOFTNET = Path("/Library/PrivilegedHelperTools/fingertrap-softnet/softnet")
SOFTNET_SHA256 = "ab333619fc8bd7277837545e49a771baa994c01c3e8c14904ae4cc4c1f37269e"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--runtime", required=True, type=Path)
    parser.add_argument("--binary-sha256", required=True)
    parser.add_argument("--host-directory", required=True, type=Path)
    parser.add_argument("--host-sha256", required=True)
    parser.add_argument("--source-revision", required=True)
    parser.add_argument("--host-revision", required=True)
    parser.add_argument("--report-directory", required=True, type=Path)
    args = parser.parse_args()
    if digest(args.runtime / "pi") != args.binary_sha256:
        parser.error("Pi artifact digest differs")
    if digest(args.host_directory / "FingerTrap.Sidecar.Qualification") != args.host_sha256:
        parser.error("Host artifact digest differs")
    for value, size in [(args.source_revision, 40), (args.host_revision, 40),
                        (args.binary_sha256, 64), (args.host_sha256, 64)]:
        if len(value) != size or any(c not in "0123456789abcdef" for c in value):
            parser.error("Expected lowercase full hexadecimal identity")
    info, parent = SOFTNET.lstat(), SOFTNET.parent.lstat()
    if (not stat.S_ISREG(info.st_mode) or info.st_uid != 0
            or not info.st_mode & stat.S_ISUID or info.st_mode & 0o022
            or not stat.S_ISDIR(parent.st_mode) or parent.st_uid != 0
            or parent.st_mode & 0o022 or digest(SOFTNET) != SOFTNET_SHA256):
        parser.error("Reviewed privileged Softnet helper is required")
    # A minimal host child environment keeps operator credentials out of Tart.
    env = {key: os.environ[key] for key in ("HOME", "TMPDIR", "USER", "LOGNAME") if key in os.environ}
    env.update(PATH=str(SOFTNET.parent) + ":/opt/homebrew/bin:/usr/bin:/bin:/usr/sbin:/sbin",
               TART_NO_AUTO_PRUNE="1")
    root = Path(__file__).resolve().parent.parent
    report_dir = args.report_directory.resolve()
    report_dir.mkdir(parents=True, exist_ok=True)
    state = {"vm": "ft-s3-qualification-" + uuid.uuid4().hex[:12], "base": BASE,
             "sourceRevision": args.source_revision, "hostRevision": args.host_revision,
             "binarySha256": args.binary_sha256, "hostSha256": args.host_sha256,
             "hostShares": [], "githubRegistration": False, "outboundIpv4Blocked": "0.0.0.0/0",
             "passed": False, "cleanupVerified": False}

    def record(phase):
        state["phase"] = phase
        temporary = report_dir / "state.tmp"
        temporary.write_text(json.dumps(state, indent=2) + "\n")
        temporary.replace(report_dir / "state.json")
        print(phase, flush=True)

    def command(argv, **kwargs):
        return subprocess.run(argv, env=env, check=True, capture_output=True, **kwargs)

    def guest(script, timeout=60):
        return command(["tart", "exec", "-i", state["vm"], "/bin/bash", "-s"],
                       input=script.encode(), timeout=timeout).stdout

    def interrupted(signum, _frame):
        raise InterruptedError("Qualification interrupted: " + str(signum))

    signal.signal(signal.SIGTERM, interrupted)
    signal.signal(signal.SIGINT, interrupted)
    lock_dir = root / ".artifacts/tart-runner"
    lock_dir.mkdir(parents=True, exist_ok=True)
    created, process = False, None
    with (lock_dir / "controller.lock").open("a") as lock, tempfile.TemporaryDirectory(prefix="ft-s3-tart-") as temp:
        fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        archive = Path(temp) / "inputs.tar"
        with tarfile.open(archive, "w") as tar:
            tar.add(args.runtime.resolve(), arcname="runtime")
            tar.add(args.host_directory.resolve(), arcname="host")
            for name in ("pi_startup_fixture.py", "qualify-pi-startup.py", "qualify-managed-sidecar.py"):
                tar.add(root / "scripts" / name, arcname="scripts/" + name)
        state["inputArchiveSha256"] = digest(archive)
        record("creating")
        try:
            command(["tart", "clone", BASE, state["vm"]], timeout=120)
            created = True
            with (report_dir / "vm.log").open("wb") as log:
                process = subprocess.Popen(["tart", "run", "--no-graphics", "--no-audio", "--no-clipboard",
                    "--net-softnet", "--net-softnet-block=0.0.0.0/0", state["vm"]],
                    env=env, stdout=log, stderr=subprocess.STDOUT)
                record("booting")
                deadline = time.monotonic() + 180
                while time.monotonic() < deadline:
                    if process.poll() is not None:
                        raise RuntimeError("VM stopped while booting; inspect vm.log")
                    try:
                        guest("/usr/bin/python3 --version\n", timeout=5)
                        break
                    except (subprocess.CalledProcessError, subprocess.TimeoutExpired):
                        time.sleep(2)
                else:
                    raise TimeoutError("VM boot timed out")
                state["guestIdentity"] = guest("/usr/bin/sw_vers\n/usr/bin/uname -m\n/usr/bin/python3 --version\n").decode()
                state["tartVersion"] = command(["tart", "--version"]).stdout.decode().strip()
                # Archive travels through the guest agent; no shared directory.
                with archive.open("rb") as inputs:
                    command(["tart", "exec", "-i", state["vm"], "/bin/bash", "-c",
                             "mkdir -p /tmp/qualification; tar -xf - -C /tmp/qualification"],
                            stdin=inputs, timeout=120)
                record("qualifying")
                script = f"""set -uo pipefail
cd /tmp/qualification
mkdir -p reports
/usr/bin/python3 scripts/qualify-pi-startup.py --binary /tmp/qualification/runtime/pi --sha256 {args.binary_sha256} --source-revision {args.source_revision} --report reports/direct.json > reports/direct.log 2>&1
direct=$?
/usr/bin/python3 scripts/qualify-managed-sidecar.py --host /tmp/qualification/host/FingerTrap.Sidecar.Qualification --host-sha256 {args.host_sha256} --runtime /tmp/qualification/runtime --binary-sha256 {args.binary_sha256} --source-revision {args.source_revision} --report reports/sidecar.json > reports/sidecar.log 2>&1
sidecar=$?
printf '%s %s\\n' "$direct" "$sidecar"
"""
                state["guestExitCodes"] = guest(script, timeout=360).decode().strip()
                # Read only known report files; do not extract guest-controlled paths.
                for name in ("direct.json", "direct.log", "sidecar.json", "sidecar.log"):
                    result = command(["tart", "exec", state["vm"], "cat", "/tmp/qualification/reports/" + name], timeout=30)
                    (report_dir / name).write_bytes(result.stdout)
                state["passed"] = (state["guestExitCodes"] == "0 0" and
                    all(json.loads((report_dir / name).read_text())["passed"] for name in ("direct.json", "sidecar.json")))
        except Exception as error:
            state["error"] = type(error).__name__ + ": " + str(error)
            raise
        finally:
            record("cleaning")
            try:
                if created:
                    command(["tart", "stop", state["vm"]], timeout=30)
                    if process:
                        process.wait(timeout=30)
                    command(["tart", "delete", state["vm"]], timeout=60)
                listing = command(["tart", "list", "--format", "json"]).stdout.decode()
                state["cleanupVerified"] = state["vm"] not in listing
                state["basePreserved"] = BASE in listing
                if not state["cleanupVerified"] or not state["basePreserved"]:
                    raise RuntimeError("Clone cleanup/base preservation not verified")
            except Exception as error:
                state["cleanupError"] = str(error)
                state["passed"] = False
                raise
            finally:
                record("complete" if state["passed"] and state["cleanupVerified"] else "failed")
    return 0 if state["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
