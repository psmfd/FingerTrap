#!/usr/bin/env python3
"""CF-1C-AUTOMATION-2: one guest-only packaged-client launch and observation."""

import argparse
from datetime import datetime
import json
from pathlib import Path
import signal
import subprocess
import time

import client_baseline_peekaboo as pilot

APP = "/Applications/FingerTrap.app"
BUNDLE = "dev.fingertrap.app"
CONTROLS = {"Toggle session browser", "Toggle status panel"}


def verify_installed(value, state):
    expected = {item["name"]: item["sha256"] for item in state["binaries"]}
    if value.get("bundleId") != BUNDLE or value.get("binaries") != expected:
        raise ValueError("Installed candidate identity differs from verified inputs")


def require_window(value):
    data = pilot.payload(value)
    owner = data.get("target_application_info", {})
    windows = [
        w
        for w in data.get("windows", [])
        if w.get("is_on_screen") is True and w.get("window_title") == "FingerTrap"
    ]
    if owner.get("bundle_id") != BUNDLE or len(windows) != 1:
        raise ValueError("Expected one observed visible FingerTrap main window")
    window = windows[0]
    bounds = window.get("bounds", {})
    if (
        window.get("observation_capability") != "combined_eligible"
        or any(
            not isinstance(bounds.get(k), (int, float)) or bounds[k] <= 0
            for k in ("width", "height")
        )
        or not isinstance(window.get("window_id"), int)
    ):
        raise ValueError("Visible main window is not eligible for bound AX observation")
    return window["window_id"]


def require_ui(value):
    data = pilot.payload(value)
    context = data.get("coordinate_context", {})
    bounds = context.get("logical_bounds", [])
    window = context.get("window", {})
    labels = {
        e.get("label")
        for e in data.get("ui_elements", [])
        if e.get("ax_role") == "AXButton"
    }
    if (
        data.get("application_name") != "FingerTrap"
        or data.get("window_title") != "FingerTrap"
        or data.get("used_cache") is not False
        or data.get("observation", {}).get("warnings")
        or len(bounds) != 2
        or len(bounds[1]) != 2
        or any(not isinstance(n, (int, float)) or n <= 0 for n in bounds[1])
        or not CONTROLS.issubset(labels)
        or not isinstance(window.get("window_id"), int)
    ):
        raise ValueError("Fresh complete FingerTrap window/control observation missing")
    return window["window_id"]


def checkpoint(run, root, name, deadline):
    pilot.atomic.probe_step(
        run, {"Name": name}, root, deadline, readback=pilot.readback
    )
    output = root / name
    if (
        not (output / "govnc-before.png").is_file()
        or not (output / "readback.json").is_file()
    ):
        raise ValueError(
            "Client slice requires independent native/Bridge screen and clock"
        )


def client_observe(run, root, deadline):
    root.mkdir(mode=0o700)  # One launch; no partial replay in the same guest.
    pilot.require_bridge(
        pilot.cli(run, root, "admission", deadline, "permissions", "status"), True
    )
    command = r"""
set -eu
test ! -e /Applications/FingerTrap.app
/usr/bin/ditto -x -k "/Volumes/My Shared Files/qa/FingerTrap-apple-silicon.app.zip" /Applications
/usr/bin/python3 -c 'import hashlib,json,pathlib,plistlib; p=pathlib.Path("/Applications/FingerTrap.app/Contents"); info=plistlib.loads((p/"Info.plist").read_bytes()); print(json.dumps({"bundleId":info["CFBundleIdentifier"],"binaries":{n:hashlib.sha256((p/"MacOS"/n).read_bytes()).hexdigest() for n in ["fingertrap","fingertrap-sidecar","libporta_pty.dylib"]}}))'
"""
    try:
        installed = pilot.execute(
            run, root, "install", deadline, "/bin/sh", "-c", command, cap=60
        )
        _, state = pilot.qa.load(run)
        verify_installed(json.loads(installed), state)
    finally:
        checkpoint(run, root, "installed", deadline)
    try:
        pilot.execute(run, root, "launch", deadline, "/usr/bin/open", APP)
    finally:
        checkpoint(run, root, "launched", deadline)
    observed = []
    for index in range(3):
        name = f"observation-{index:02d}"
        try:
            try:
                windows = pilot.cli(
                    run,
                    root,
                    name + "-windows",
                    deadline,
                    "window",
                    "list",
                    "--app",
                    BUNDLE,
                )
            finally:
                checkpoint(run, root, name + "-windows", deadline)
            try:
                value = pilot.cli(
                    run, root, name + "-ax", deadline, "see", "--app", BUNDLE, "--fresh"
                )
            finally:
                checkpoint(run, root, name + "-ax", deadline)
            window_id = require_window(windows)
            identifier = require_ui(value)
            if window_id != identifier:
                raise ValueError("Window inventory and fresh AX target differ")
            observed.append(identifier)
        except (
            ValueError,
            subprocess.CalledProcessError,
            subprocess.TimeoutExpired,
        ) as error:
            pilot.qa.save(
                root / (name + "-blocked.json"),
                {"error": type(error).__name__, "detail": str(error)},
            )
            observed = []
        if len(observed) == 2 and observed[0] == observed[1]:
            break
        time.sleep(min(5, pilot.atomic.remaining(deadline)))
    try:
        pilot.execute(
            run, root, "processes", deadline, "/bin/ps", "-axo", "pid,ppid,comm"
        )
    finally:
        checkpoint(run, root, "process-readback", deadline)
    pilot.qa.save(
        root / "result.json",
        {
            "slice": "CF-1C-AUTOMATION-2",
            "stableObservedWindow": len(observed) == 2 and observed[0] == observed[1],
            "windowIds": observed,
            "requiresIndependentImageAndProcessReview": True,
            "baselineRowPromoted": False,
            "launchMode": "LaunchServices in tool-equipped disposable guest",
            "providerRuntimeAcceptance": False,
        },
    )


def request_step(request, index):
    if request == {"action": "client-observe"}:
        return {"Name": f"step-{index:02d}", "ClientObserve": True}
    return pilot.request_step(request, index)


def dispatch(run, step, root, deadline):
    if step.get("ClientObserve"):
        client_observe(run, root / "client", deadline)
    else:
        pilot.dispatch(run, step, root, deadline)


def observe(run, image):
    pilot.atomic.vnc.connection(run)
    if image != run / "console-first-frame.png" or not image.is_file():
        raise ValueError("Client observation requires fresh owned first frame")
    root = run / "client-observation"
    root.mkdir(mode=0o700)
    deadline = time.monotonic() + 1200
    pilot.install(run, root, deadline)
    pilot.readback(run, root, deadline)
    pilot.atomic.permission_session(
        run, root, deadline, request_step=request_step, dispatch=dispatch
    )


def review(run):
    """Reclassify retained world evidence; never replace the original live result."""
    run, state = pilot.qa.load(run)
    if state["phase"] != "cleaned" or not state.get("cleanupVerified"):
        raise ValueError("Offline review requires a cleaned owned run")
    root = run / "client-observation/client"
    verify_installed(json.loads((root / "install.stdout").read_text()), state)
    observed, clocks = [], []
    for index in range(3):
        name = f"observation-{index:02d}"
        window = require_window(
            json.loads((root / (name + "-windows.stdout")).read_text())
        )
        ax = require_ui(json.loads((root / (name + "-ax.stdout")).read_text()))
        if window != ax:
            raise ValueError("Retained window and AX identity differ")
        output = root / (name + "-ax")
        receipt = json.loads((output / "readback.json").read_text())
        pilot.qa.verify_digest(output / "guest-screen.jpg", receipt["screenSha256"])
        for item in ("govnc-before.png", "guest-clock.txt"):
            if not (output / item).is_file() or not (output / item).stat().st_size:
                raise ValueError("Retained independent image/clock missing")
        observed.append(ax)
        clocks.append(datetime.fromisoformat(receipt["hostUtc"]))
    if len(set(observed)) != 1 or any(
        (b - a).total_seconds() < 5 for a, b in zip(clocks, clocks[1:])
    ):
        raise ValueError("Retained observations do not establish stable window")
    return {
        "runId": state["runId"],
        "classificationMode": "corrected offline evidence replay",
        "originalLiveResult": json.loads((root / "result.json").read_text()),
        "stableObservedVisibleMainWindow": True,
        "windowId": observed[0],
        "uncachedObservations": 3,
        "completeGlobalWindowInventory": False,
        "requiresIndependentImageAndProcessReview": True,
        "baselineRowPromoted": False,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--archive", type=Path)
    parser.add_argument("--runtime-archive", type=Path)
    parser.add_argument("--review-run", type=Path)
    args = parser.parse_args()
    if args.review_run:
        if args.archive or args.runtime_archive:
            parser.error("Offline review cannot start a guest")
        print(json.dumps(review(args.review_run), indent=2))
        return
    if not args.archive or not args.runtime_archive:
        parser.error("Live observation requires both pinned archives")

    def interrupted(_signum, _frame):
        raise InterruptedError("Packaged-client observation interrupted")

    signal.signal(signal.SIGTERM, interrupted)
    pilot.atomic.compare(
        args.archive,
        args.runtime_archive,
        observer=observe,
        prepare_hook=pilot.stage,
        hold=1500,
    )


if __name__ == "__main__":
    main()
