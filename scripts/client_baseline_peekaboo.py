#!/usr/bin/env python3
"""CF-1C-AUTOMATION-1: pinned guest-only Peekaboo GUI Bridge pilot.

Raw results are private; command success never passes a packaged-client case.
"""

import argparse
import json
from pathlib import Path
import posixpath
import shutil
import signal
import stat
import subprocess
import sys
import time
import zipfile

import client_baseline_compare as atomic

qa = atomic.qa
SOURCE = "70dbc4183f390a5a72d5dc01d8a77eb672217070"
TEAM = "FWJYW4S8P8"
CLI = "/Users/admin/ft-peekaboo/peekaboo"
SOCKET = "/Users/admin/Library/Application Support/Peekaboo/bridge.sock"
APP = "/Applications/Peekaboo.app"
INPUTS = qa.EVIDENCE / "peekaboo-pilot-inputs.json"


def verify_app_archive(path):
    with zipfile.ZipFile(path) as archive:
        entries = archive.infolist()
        if len(entries) > 1000 or sum(e.file_size for e in entries) > 512 * 1024 * 1024:
            raise ValueError("Peekaboo app archive exceeds budget")
        names = set()
        for entry in entries:
            parts = Path(entry.filename).parts
            mode = entry.external_attr >> 16
            if (
                not parts
                or parts[0] != "Peekaboo.app"
                or ".." in parts
                or Path(entry.filename).is_absolute()
                or entry.filename in names
                or stat.S_IFMT(mode)
                not in (0, stat.S_IFREG, stat.S_IFDIR, stat.S_IFLNK)
            ):
                raise ValueError("Unsafe Peekaboo app archive path")
            names.add(entry.filename)
            if stat.S_ISLNK(mode):
                target = archive.read(entry).decode()
                resolved = posixpath.normpath(
                    posixpath.join(posixpath.dirname(entry.filename), target)
                )
                if posixpath.isabs(target) or not resolved.startswith("Peekaboo.app/"):
                    raise ValueError("Escaped Peekaboo framework symlink")


def stage(run):
    inputs = json.loads(INPUTS.read_text())
    if inputs["sourceCommit"] != SOURCE or inputs["teamId"] != TEAM:
        raise ValueError("Unexpected pilot source or signing identity")
    source = qa.ROOT / inputs["privateDirectory"]
    for name, digest in inputs["files"].items():
        path = source / name
        if path.is_symlink() or not path.is_file():
            raise ValueError("Pilot input must be a preserved regular file")
        qa.verify_digest(path, digest)
    verify_app_archive(source / "Peekaboo-4.9.8.app.zip")
    destination = run / "share/peekaboo"
    destination.mkdir(mode=0o700)
    for name in (
        "Peekaboo-4.9.8.app.zip",
        "peekaboo",
        "libswiftCompatibilitySpan.dylib",
    ):
        shutil.copyfile(source / name, destination / name)
        qa.verify_digest(destination / name, inputs["files"][name])
    qa.save(run / "peekaboo-inputs.json", inputs)


def payload(value):
    if not isinstance(value, dict) or value.get("success") is False:
        raise ValueError("Peekaboo did not return successful structured evidence")
    data = value.get("data", value)
    if not isinstance(data, dict):
        raise ValueError("Peekaboo structured data must be an object")
    return data


def require_bridge(value, require_permissions=False):
    data = payload(value)
    if data.get("source") != "bridge":
        raise ValueError("The signed GUI Bridge must own capture/input")
    if require_permissions:
        permissions = data.get("permissions", [])
        if (
            len(permissions) != 3
            or any(p.get("isGranted") is not True for p in permissions)
            or {p.get("name") for p in permissions}
            != {"Screen Recording", "Accessibility", "Event Synthesizing"}
        ):
            raise ValueError(
                "Pilot needs Bridge capture, Accessibility and keyboard grants"
            )
    return data


def require_source(value):
    def commits(node):
        if isinstance(node, dict):
            for key, val in node.items():
                if key == "sourceCommit":
                    yield val
                else:
                    yield from commits(val)
        elif isinstance(node, list):
            for val in node:
                yield from commits(val)

    if set(commits(value)) != {SOURCE}:
        raise ValueError("CLI embedded source does not match the pinned app release")


def execute(run, output, name, deadline, *args, cap=30):
    try:
        data = atomic.guest_command(run, deadline, *args, cap=cap)
    except (subprocess.CalledProcessError, subprocess.TimeoutExpired) as error:
        (output / (name + ".stdout")).write_bytes(error.stdout or b"")
        (output / (name + ".stderr")).write_bytes(error.stderr or b"")
        qa.save(
            output / (name + ".status.json"),
            {
                "exitCode": getattr(error, "returncode", None),
                "timedOut": isinstance(error, subprocess.TimeoutExpired),
            },
        )
        raise
    (output / (name + ".stdout")).write_bytes(data)
    return data


def cli(run, output, name, deadline, *args):
    data = execute(
        run, output, name, deadline, CLI, *args, "--bridge-socket", SOCKET, "--json"
    )
    value = json.loads(data)
    payload(value)
    return value


def install(run, root, deadline):
    command = r"""
set -eu
test ! -e /Applications/Peekaboo.app
test ! -e /Users/admin/ft-peekaboo
mkdir /Users/admin/ft-peekaboo
cp "/Volumes/My Shared Files/qa/peekaboo/peekaboo" /Users/admin/ft-peekaboo/
cp "/Volumes/My Shared Files/qa/peekaboo/libswiftCompatibilitySpan.dylib" /Users/admin/ft-peekaboo/
chmod 755 /Users/admin/ft-peekaboo/peekaboo
/usr/bin/ditto -x -k "/Volumes/My Shared Files/qa/peekaboo/Peekaboo-4.9.8.app.zip" /Applications
/usr/bin/codesign --verify --deep --strict /Applications/Peekaboo.app
/usr/bin/codesign --verify --strict /Users/admin/ft-peekaboo/peekaboo
/usr/bin/codesign --verify --strict /Users/admin/ft-peekaboo/libswiftCompatibilitySpan.dylib
/usr/bin/codesign -dv --verbose=4 /Applications/Peekaboo.app 2>&1
/usr/bin/codesign -dv --verbose=4 /Users/admin/ft-peekaboo/peekaboo 2>&1
/usr/libexec/PlistBuddy -c 'Print PeekabooSourceCommit' /Applications/Peekaboo.app/Contents/Info.plist
"""
    signature = execute(
        run, root, "install-verify", deadline, "/bin/sh", "-c", command, cap=60
    ).decode()
    if signature.count("TeamIdentifier=" + TEAM) != 2 or SOURCE not in signature:
        raise ValueError("Guest app/CLI signature/source verification failed")
    version = execute(run, root, "cli-version", deadline, CLI, "--version", "--json")
    require_source(json.loads(version))
    execute(
        run,
        root,
        "launch",
        deadline,
        "/usr/bin/open",
        "-a",
        APP,
        "--args",
        "--background-bridge-host",
    )
    # The canonical app socket is explicit: no daemon/client-local fallback.
    for _ in range(30):
        atomic.vnc.connection(run)
        try:
            cli(run, root, "bridge-startup", deadline, "permissions", "status")
            return
        except subprocess.CalledProcessError:
            time.sleep(min(1, atomic.remaining(deadline)))
    raise TimeoutError("Signed GUI Bridge did not become reachable")


def readback(run, output, deadline):
    def capture(run, output, deadline):
        require_bridge(
            cli(run, output, "permissions", deadline, "permissions", "status")
        )
        cli(
            run,
            output,
            "capture",
            deadline,
            "see",
            "--no-elements",
            "--mode",
            "screen",
            "--screen-index",
            "0",
            "--path",
            "/tmp/ft-rfb.png",
        )

    try:
        atomic.guest_screen(run, output, deadline, capture=capture)
    except (
        ValueError,
        subprocess.CalledProcessError,
        subprocess.TimeoutExpired,
    ) as error:
        qa.save(output / "capture-blocked.json", {"error": type(error).__name__})
        # A failed bootstrap capture still gets immediate independent guest time.
        (output / "guest-clock.txt").write_bytes(
            atomic.guest_command(run, deadline, "/bin/date", "-u")
        )


def request_step(request, index):
    if request == {"action": "pilot-scenario"}:
        return {"Name": f"step-{index:02d}", "Scenario": True}
    if request == {"action": "pilot-status"}:
        return {"Name": f"step-{index:02d}", "Status": True}
    if request == {"action": "pilot-permissions-ui"}:
        return {"Name": f"step-{index:02d}", "PermissionsUI": True}
    if request.get("action") not in ("capture", "click", "chord", "text", "finish"):
        raise ValueError("Only fixed pilot commands and bounded guest input accepted")
    return atomic.permission_step(request, index)


def require_finder_dialog(value, text=None):
    data = payload(value)
    if (
        data.get("used_cache") is not False
        or data.get("application_name") != "Finder"
        or data.get("is_dialog") is not True
    ):
        raise ValueError("Expected fresh Finder dialog observation")
    fields = [
        e
        for e in data.get("ui_elements", [])
        if e.get("ax_role") == "AXTextField" and e.get("identifier") == "PathTextField"
    ]
    if len(fields) != 1 or (text is not None and fields[0].get("value") != text):
        raise ValueError("Fresh Finder path field did not match the expected state")


def scenario(run, root, deadline):
    root.mkdir(mode=0o700)  # A partial scenario cannot be silently replayed.
    require_bridge(cli(run, root, "admission", deadline, "permissions", "status"), True)
    execute(
        run, root, "finder-launch", deadline, "/usr/bin/open", "-b", "com.apple.finder"
    )
    commands = [
        ("menus", ("menu", "list", "--app", "com.apple.finder")),
        (
            "menu-goto",
            (
                "menu",
                "click",
                "--app",
                "com.apple.finder",
                "--path",
                "Go > Go to Folder…",
            ),
        ),
        ("dialog", ("see", "--app", "com.apple.finder", "--fresh")),
        (
            "type",
            (
                "type",
                "/tmp/ft-input-probe",
                "--app",
                "com.apple.finder",
                "--foreground",
                "--clear",
            ),
        ),
        ("typed-read", ("see", "--app", "com.apple.finder", "--fresh")),
        ("escape", ("press", "Escape", "--app", "com.apple.finder", "--foreground")),
        (
            "keyboard-goto",
            ("press", "cmd+shift+g", "--app", "com.apple.finder", "--foreground"),
        ),
        ("keyboard-read", ("see", "--app", "com.apple.finder", "--fresh")),
    ]
    for name, args in commands:
        try:
            value = cli(run, root, name, deadline, *args)
        except subprocess.CalledProcessError:
            # A possibly dispatched type must be observed, never replayed.
            if name != "type":
                raise
            result = json.loads((root / (name + ".stdout")).read_bytes())
            if not (
                name == "type"
                and result.get("error", {}).get("code") == "INTERACTION_FAILED"
                and result.get("outcome", {}).get("state") == "indeterminate"
                and result.get("outcome", {}).get("requires_fresh_observation") is True
                and result.get("outcome", {}).get("retry_safe") is False
            ):
                raise
        finally:
            # Independent RFB pixels then GUI-Bridge pixels/clock after every command.
            atomic.probe_step(run, {"Name": name}, root, deadline, readback=readback)
        if not (root / name / "readback.json").is_file():
            raise ValueError("Scenario requires independent Bridge screen/clock")
        if name in ("dialog", "keyboard-read"):
            require_finder_dialog(value)
        elif name == "typed-read":
            require_finder_dialog(value, "/tmp/ft-input-probe")
    qa.save(
        root / "scenario-completed.json",
        {
            "commandsCompleted": True,
            "syntheticTextObserved": True,
            "requiresIndependentImageReview": True,
            "appAcceptance": False,
        },
    )


def dispatch(run, step, root, deadline):
    if step.get("Scenario"):
        scenario(run, root / "scenario", deadline)
    elif step.get("Status"):
        output = root / step["Name"]
        output.mkdir(mode=0o700)
        cli(
            run,
            output,
            "all-permissions",
            deadline,
            "permissions",
            "status",
            "--all-sources",
        )
        readback(run, output, deadline)
    elif step.get("PermissionsUI"):
        output = root / step["Name"]
        output.mkdir(mode=0o700)
        execute(
            run,
            output,
            "interactive",
            deadline,
            "/usr/bin/open",
            "-a",
            APP,
            "--args",
            "--interactive",
        )
        atomic.probe_step(run, {"Name": "observe"}, output, deadline, readback=readback)
    else:
        atomic.probe_step(run, step, root, deadline, readback=readback)


def observe(run, image):
    atomic.vnc.connection(run)
    if image != run / "console-first-frame.png" or not image.is_file():
        raise ValueError("Pilot requires the exact fresh owned first frame")
    root = run / "peekaboo-pilot"
    root.mkdir(mode=0o700)
    deadline = time.monotonic() + 1200
    install(run, root, deadline)
    readback(run, root, deadline)
    atomic.permission_session(
        run, root, deadline, request_step=request_step, dispatch=dispatch
    )


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--archive", type=Path, required=True)
    parser.add_argument("--runtime-archive", type=Path, required=True)
    args = parser.parse_args()

    def interrupted(_signum, _frame):
        raise InterruptedError("Peekaboo pilot interrupted")

    signal.signal(signal.SIGTERM, interrupted)
    atomic.compare(
        args.archive,
        args.runtime_archive,
        observer=observe,
        prepare_hook=stage,
        hold=1500,
    )


if __name__ == "__main__":
    sys.exit(main())
