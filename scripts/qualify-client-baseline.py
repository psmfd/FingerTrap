#!/usr/bin/env python3
"""Verify CF-1 inputs, stage an owned Tart guest, and retain observed results.

No provider credentials are accepted. GUI observations are recorded explicitly;
this harness never declares maintainer acceptance. Raw run files stay ignored.
"""

import argparse
import fcntl
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import signal
import stat
import subprocess
import time
import uuid
import zipfile
from datetime import datetime, timezone

ROOT = Path(__file__).resolve().parents[1]
EVIDENCE = ROOT / "docs/migration/evidence/client-baseline"
BASE = "ghcr.io/cirruslabs/macos-sequoia-base@sha256:3f4d14a5ffb9efd3bda2ae0184fd4bc2773d924ff8b7565f958761420ec41a0c"
RUNTIME_TAG = "v0.85.1-psmfd.1"
RUNTIME_SOURCE = "e7d09c9dc5fca409f46a5476669e3c0404054d69"
RUNTIME_ARCHIVE_SHA = "e0e0331d478af7724fa24d780a8d4abc0033c6f5ee7c0d45dddae6f922e5c4cb"
RUNTIME_BINARY_SHA = "811d65137d4272b30a6b2bd9a4093218b8f94bb8f55a9db9ccf8d443c4d4b2a5"
CASES = tuple(f"CF1-{n:02}" for n in range(1, 13))
SOFTNET = Path("/Library/PrivilegedHelperTools/fingertrap-softnet/softnet")
SOFTNET_SHA = "ab333619fc8bd7277837545e49a771baa994c01c3e8c14904ae4cc4c1f37269e"


def digest(path):
    h = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def verify_digest(path, expected):
    if not re.fullmatch(r"[0-9a-f]{64}", expected) or digest(path) != expected:
        raise ValueError(f"Digest mismatch: {Path(path).name}")


def save(path, value):
    temporary = path.with_suffix(".tmp")
    temporary.write_text(json.dumps(value, indent=2) + "\n")
    temporary.replace(path)


def safe_unzip(archive, destination):
    """Bound extraction and reject traversal, duplicates, special files and links."""
    with zipfile.ZipFile(archive) as z:
        members, seen, total = z.infolist(), set(), 0
        if len(members) > 10000:
            raise ValueError("Too many archive members")
        for member in members:
            name = member.filename.rstrip("/")
            path = PurePosixPath(name)
            mode = member.external_attr >> 16
            if (
                not name
                or path.is_absolute()
                or ".." in path.parts
                or "\\" in name
                or ":" in name
                or name in seen
                or stat.S_ISLNK(mode)
                or (stat.S_IFMT(mode) not in (0, stat.S_IFREG, stat.S_IFDIR))
            ):
                raise ValueError("Unsafe archive member: " + member.filename)
            seen.add(name)
            total += member.file_size
            if total > 1024 * 1024 * 1024:
                raise ValueError("Archive exceeds extraction budget")
        destination = Path(destination)
        if destination.exists():
            raise ValueError("Extraction destination must be new")
        destination.mkdir(parents=True)
        for member in members:
            target = destination / member.filename
            if member.is_dir():
                target.mkdir(parents=True, exist_ok=True)
            else:
                target.parent.mkdir(parents=True, exist_ok=True)
                with z.open(member) as source, target.open("xb") as output:
                    shutil.copyfileobj(source, output)
                target.chmod(0o755 if member.external_attr >> 16 & 0o111 else 0o644)


def command(argv, **kwargs):
    env = {
        k: os.environ[k]
        for k in ("HOME", "TMPDIR", "USER", "LOGNAME")
        if k in os.environ
    }
    env.update(
        PATH=str(SOFTNET.parent) + ":/opt/homebrew/bin:/usr/bin:/bin:/usr/sbin:/sbin",
        TART_NO_AUTO_PRUNE="1",
    )
    return subprocess.run(
        argv, env=env, check=True, capture_output=True, timeout=60, **kwargs
    ).stdout


def prepare(archive, runtime):
    reference = json.loads((EVIDENCE / "artifact-reference.json").read_text())
    verify_digest(archive, reference["archiveDigest"].removeprefix("sha256:"))
    verify_digest(runtime, RUNTIME_ARCHIVE_SHA)
    run_id = (
        datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ-") + uuid.uuid4().hex[:12]
    )
    run = ROOT / ".artifacts/client-baseline" / run_id
    run.mkdir(parents=True, mode=0o700)
    state = {
        "kind": "fingertrap-client-baseline",
        "runId": run_id,
        "runtimeTag": RUNTIME_TAG,
        "runtimeSource": RUNTIME_SOURCE,
        "runtimeArchiveSha256": RUNTIME_ARCHIVE_SHA,
        "runtimeBinarySha256": RUNTIME_BINARY_SHA,
        "artifactId": reference["artifactId"],
        "sourceHead": reference["sourceHead"],
        "buildCommit": reference["buildCommit"],
        "sourceTree": reference["sourceTree"],
        "archiveSha256": reference["archiveDigest"].removeprefix("sha256:"),
        "base": BASE,
        "vm": "ft-client-baseline-" + run_id.lower(),
        "created": False,
        "cleanupVerified": False,
        "phase": "verifying",
        "attempts": [],
        "events": [],
    }
    save(run / "state.json", state)
    try:
        safe_unzip(archive, run / "share")
        share = run / "share"
        for name, key in [
            ("FingerTrap-apple-silicon.app.zip", "appArchiveDigest"),
            ("FingerTrap_0.1.0_aarch64.dmg", "dmgDigest"),
        ]:
            verify_digest(share / name, reference[key].removeprefix("sha256:"))
        build = json.loads((share / "build.json").read_text())
        if (
            build["commit"] != reference["buildCommit"]
            or build["target"] != "aarch64-apple-darwin"
        ):
            raise ValueError("App build provenance differs")
        state["build"] = build
        differences = (
            command(["git", "diff", "--name-only", reference["sourceHead"], "HEAD"])
            .decode()
            .splitlines()
        )
        dirty = command(["git", "diff", "HEAD", "--name-only"]).decode().splitlines()
        package_paths = (
            "src-ui/",
            "src-tauri/",
            "src-sidecar/",
            "packages/",
            ".github/workflows/apple-silicon-candidate.yml",
        )
        if any(path.startswith(package_paths) for path in differences + dirty):
            raise ValueError("Package-relevant inputs differ; build a new candidate")
        state["sourceDifferencesFromCheckout"] = differences
        safe_unzip(share / "FingerTrap-apple-silicon.app.zip", run / "app")
        binaries = []
        for name in ("fingertrap", "fingertrap-sidecar", "libporta_pty.dylib"):
            binary = run / "app/FingerTrap.app/Contents/MacOS" / name
            architecture = (
                command(["/usr/bin/file", "-b", str(binary)]).decode().strip()
            )
            if "arm64" not in architecture or "x86_64" in architecture:
                raise ValueError("Expected ARM64 binary: " + name)
            binaries.append(
                {"name": name, "sha256": digest(binary), "architecture": architecture}
            )
        state["binaries"] = binaries
        state["appArchiveSha256"] = digest(share / "FingerTrap-apple-silicon.app.zip")
        state["dmgSha256"] = digest(share / "FingerTrap_0.1.0_aarch64.dmg")
        shutil.copyfile(runtime, share / "pi-production.tar.gz")
        # These are fixed reviewed scripts, not operator configuration or credentials.
        shutil.copyfile(ROOT / "scripts/smoke-pty.py", share / "smoke-pty.py")
        state["phase"] = "inputs-verified"
    except Exception as error:
        state["events"].append({"phase": "prepare", "error": str(error)})
        state["phase"] = "failed"
        raise
    finally:
        save(run / "state.json", state)
        print(str(run), flush=True)
    return run


def load(run):
    run = Path(run).resolve()
    state = json.loads((run / "state.json").read_text())
    if (
        state.get("kind") != "fingertrap-client-baseline"
        or not re.fullmatch(r"\d{8}T\d{6}Z-[0-9a-f]{12}", state.get("runId", ""))
        or run.name != state["runId"]
        or state.get("vm") != "ft-client-baseline-" + state["runId"].lower()
        or state.get("base") != BASE
        or state.get("runtimeTag") != RUNTIME_TAG
    ):
        raise ValueError("Invalid owned run identity")
    return run, state


def record(run, case, status, observation, evidence, observer, mode):
    run, state = load(run)
    if case not in CASES or status not in ("PASS", "FAIL", "BLOCKED", "NOT RUN"):
        raise ValueError("Unknown case/status")
    if not observation.strip():
        raise ValueError("Actual observation is required")
    refs = []
    for value in evidence:
        path = (run / value).resolve()
        if (
            not path.is_relative_to(run)
            or not path.is_file()
            or path.stat().st_size == 0
        ):
            raise ValueError("Evidence must be a nonempty file inside the run")
        refs.append({"path": str(path.relative_to(run)), "sha256": digest(path)})
    if status == "PASS" and (
        not refs
        or not observer.strip()
        or (case != "CF1-01" and mode != "observed-gui")
    ):
        raise ValueError("PASS requires evidence, observer and actual GUI observation")
    state["attempts"].append(
        {
            "case": case,
            "status": status,
            "observation": observation,
            "evidence": refs,
            "observer": observer,
            "mode": mode,
            "utc": datetime.now(timezone.utc).isoformat(),
        }
    )
    save(run / "state.json", state)


def summary(state):
    latest = {case: {"case": case, "status": "NOT RUN"} for case in CASES}
    for attempt in state["attempts"]:
        latest[attempt["case"]] = attempt
    complete = all(a["status"] != "NOT RUN" for a in latest.values())
    return {
        "runId": state["runId"],
        "cases": list(latest.values()),
        "baselineCaptured": complete,
        "readyForMaintainerAcceptance": complete
        and state["cleanupVerified"]
        and all(a["status"] == "PASS" for a in latest.values()),
        "baselineAccepted": False,
        "cleanupVerified": state["cleanupVerified"],
        "attempts": state["attempts"],
        "events": state["events"],
    }


def vm_config(name):
    if not re.fullmatch(r"ft-client-baseline-\d{8}t\d{6}z-[0-9a-f]{12}", name):
        raise ValueError("Expected an owned baseline clone name")
    # Tart get includes mutable running state and disk allocation. The clone's
    # persisted hardware/MAC configuration is stable across boot and shutdown.
    return json.loads((Path.home() / ".tart/vms" / name / "config.json").read_text())


def cleanup(run, state):
    if not state.get("created") or not state.get("ownedConfig"):
        raise ValueError("Refusing cleanup without owned clone receipt")
    listing = json.loads(command(["tart", "list", "--format", "json"]))
    names = {v["Name"] for v in listing}
    if state["vm"] in names:
        if vm_config(state["vm"]) != state["ownedConfig"]:
            raise ValueError("Refusing cleanup: clone identity changed")
        command(["tart", "stop", state["vm"]])
        command(["tart", "delete", state["vm"]])
    after = json.loads(command(["tart", "list", "--format", "json"]))
    after_names = {v["Name"] for v in after}
    preserved = set(state["originalVms"]).issubset(after_names)
    state["cleanupVerified"] = state["vm"] not in after_names and preserved
    state["originalVmsPreserved"] = preserved
    state["phase"] = "cleaned" if state["cleanupVerified"] else "cleanup-failed"
    save(run / "state.json", state)
    if not state["cleanupVerified"]:
        raise RuntimeError("Owned cleanup/original VM preservation failed")


def guest_session(run, hold_seconds, graphics=False):
    run, state = load(run)
    if state["phase"] != "inputs-verified" or state["created"]:
        raise ValueError("Guest requires a fresh input-verified run")
    verify_digest(
        run / "share/FingerTrap-apple-silicon.app.zip", state["appArchiveSha256"]
    )
    verify_digest(run / "share/FingerTrap_0.1.0_aarch64.dmg", state["dmgSha256"])
    verify_digest(run / "share/pi-production.tar.gz", RUNTIME_ARCHIVE_SHA)
    info, parent = SOFTNET.lstat(), SOFTNET.parent.lstat()
    if (
        not stat.S_ISREG(info.st_mode)
        or info.st_uid != 0
        or not info.st_mode & stat.S_ISUID
        or info.st_mode & 0o022
        or parent.st_uid != 0
        or parent.st_mode & 0o022
    ):
        raise ValueError("Reviewed Softnet helper permissions differ")
    verify_digest(SOFTNET, SOFTNET_SHA)
    lock_path = ROOT / ".artifacts/tart-runner/controller.lock"
    lock_path.parent.mkdir(parents=True, exist_ok=True)
    process = None
    with lock_path.open("a") as lock:
        fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        listing = json.loads(command(["tart", "list", "--format", "json"]))
        if any(v["Running"] for v in listing):
            raise ValueError("Standalone acceptance cannot overlap a running VM")
        state["originalVms"] = [v["Name"] for v in listing]
        if BASE not in state["originalVms"] or state["vm"] in state["originalVms"]:
            raise ValueError("Pinned clean base missing or clone name already exists")
        # Tart get only accepts local clones, not cached OCI names. Inspect the
        # fixed cache entry without modifying it; clone still uses its OCI digest.
        cache = Path.home() / ".tart/cache/OCIs" / BASE.replace("@", "/")
        state["baseConfig"] = json.loads((cache / "config.json").read_text())
        state["baseManifestSha256"] = digest(cache / "manifest.json")
        state["tartVersion"] = command(["tart", "--version"]).decode().strip()
        state["hostOs"] = command(["/usr/bin/sw_vers"]).decode().strip()
        state["phase"] = "cloning"
        save(run / "state.json", state)
        try:
            command(["tart", "clone", BASE, state["vm"]])
            state["created"] = True
            state["ownedConfig"] = vm_config(state["vm"])
            save(run / "state.json", state)
            with (run / "vm.log").open("wb") as log:
                # Offline during clean launch; no host home, clipboard or credentials.
                env = {
                    k: os.environ[k]
                    for k in ("HOME", "TMPDIR", "USER", "LOGNAME")
                    if k in os.environ
                }
                env.update(
                    PATH=str(SOFTNET.parent)
                    + ":/opt/homebrew/bin:/usr/bin:/bin:/usr/sbin:/sbin",
                    TART_NO_AUTO_PRUNE="1",
                )
                process = subprocess.Popen(
                    [
                        "tart",
                        "run",
                        *([] if graphics else ["--no-graphics"]),
                        "--no-audio",
                        "--no-clipboard",
                        "--net-softnet",
                        "--net-softnet-block=0.0.0.0/0",
                        "--dir=qa:" + str(run / "share") + ":ro",
                        state["vm"],
                    ],
                    env=env,
                    stdout=log,
                    stderr=subprocess.STDOUT,
                )
                state["phase"] = "booting"
                state["graphicsWindow"] = graphics
                save(run / "state.json", state)
                deadline = time.monotonic() + 150
                while time.monotonic() < deadline:
                    if process.poll() is not None:
                        raise RuntimeError("Guest stopped during boot; inspect vm.log")
                    try:
                        identity = command(
                            ["tart", "exec", state["vm"], "/usr/bin/sw_vers"]
                        )
                        break
                    except (subprocess.CalledProcessError, subprocess.TimeoutExpired):
                        time.sleep(2)
                else:
                    raise TimeoutError(
                        "Guest agent unavailable; no guest changes attempted"
                    )
                (run / "guest-os.txt").write_bytes(identity)
                inventory = command(
                    [
                        "tart",
                        "exec",
                        state["vm"],
                        "/bin/sh",
                        "-c",
                        "uname -m; xcode-select -p 2>&1; for tool in node npm dotnet cargo python3 brew; "
                        'do command -v "$tool" || true; done; ls -d /Applications/Xcode.app '
                        "/Library/Developer/CommandLineTools 2>/dev/null || true",
                    ]
                )
                (run / "guest-tools.txt").write_bytes(inventory)
                state["guestOs"] = identity.decode().strip()
                state["guestToolInventory"] = inventory.decode().strip()
                state["phase"] = "guest-ready"
                save(run / "state.json", state)
                print(
                    json.dumps(
                        {
                            "run": str(run),
                            "vm": state["vm"],
                            "phase": state["phase"],
                            "holdSeconds": hold_seconds,
                        }
                    ),
                    flush=True,
                )
                deadline = time.monotonic() + hold_seconds
                while time.monotonic() < deadline:
                    if process.poll() is not None:
                        raise RuntimeError("Guest stopped during observation")
                    if (run / "finish-session").exists():
                        break
                    time.sleep(1)
        except BaseException as error:
            state["events"].append(
                {
                    "phase": state["phase"],
                    "error": type(error).__name__ + ": " + str(error),
                }
            )
            state["phase"] = "failed"
            save(run / "state.json", state)
            raise
        finally:
            # Re-read observer attempts recorded while the guest was running.
            latest = json.loads((run / "state.json").read_text())
            state["attempts"] = latest["attempts"]
            if state["created"]:
                try:
                    cleanup(run, state)
                finally:
                    if process:
                        process.wait(timeout=30)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    subs = parser.add_subparsers(dest="action", required=True)
    prep = subs.add_parser("prepare")
    prep.add_argument("--archive", type=Path, required=True)
    prep.add_argument("--runtime-archive", type=Path, required=True)
    guest = subs.add_parser("guest")
    guest.add_argument("--run", type=Path, required=True)
    guest.add_argument(
        "--hold-seconds",
        type=int,
        default=600,
        choices=range(0, 1801),
        metavar="0..1800",
    )
    guest.add_argument(
        "--graphics",
        action="store_true",
        help="Show the owned Tart window for observed GUI acceptance",
    )
    rec = subs.add_parser("record")
    rec.add_argument("--run", type=Path, required=True)
    rec.add_argument("--case", choices=CASES, required=True)
    rec.add_argument(
        "--status", choices=("PASS", "FAIL", "BLOCKED", "NOT RUN"), required=True
    )
    rec.add_argument("--observation", required=True)
    rec.add_argument("--evidence", action="append", default=[])
    rec.add_argument("--observer", default="")
    rec.add_argument(
        "--mode",
        choices=("artifact-verification", "observed-gui", "blocked"),
        required=True,
    )
    for name in ("summary", "cleanup"):
        sub = subs.add_parser(name)
        sub.add_argument("--run", type=Path, required=True)
    args = parser.parse_args()

    def interrupted(signum, _frame):
        raise InterruptedError("Qualification interrupted: " + str(signum))

    signal.signal(signal.SIGTERM, interrupted)
    if args.action == "prepare":
        prepare(args.archive, args.runtime_archive)
    elif args.action == "guest":
        try:
            guest_session(args.run, args.hold_seconds, args.graphics)
        except Exception as error:
            run, state = load(args.run)
            state["events"].append(
                {
                    "phase": "guest-command",
                    "error": type(error).__name__ + ": " + str(error),
                }
            )
            save(run / "state.json", state)
            raise
    elif args.action == "record":
        record(
            args.run,
            args.case,
            args.status,
            args.observation,
            args.evidence,
            args.observer,
            args.mode,
        )
    elif args.action == "cleanup":
        run, state = load(args.run)
        with (ROOT / ".artifacts/tart-runner/controller.lock").open("a") as lock:
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
            cleanup(run, state)
    else:
        _, state = load(args.run)
        print(json.dumps(summary(state), indent=2))


if __name__ == "__main__":
    main()
