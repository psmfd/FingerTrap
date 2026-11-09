#!/usr/bin/env python3
"""CF-1C-AUTOMATION-6: one repeated LaunchServices launch preserves the session."""

import argparse
import json
from pathlib import Path
import signal
import time

import client_baseline_quit_probe as quit_probe

layout = quit_probe.layout
shell = quit_probe.shell
client = quit_probe.client
pilot = quit_probe.pilot
SCENARIO_SECONDS = 480


def require_repeat(before, after, first, second, window, observed_window, nonce):
    """Require unchanged process identities, shell state, geometry and window."""
    if before != after:
        raise ValueError("Repeated launch replaced or duplicated admitted processes")
    if window != observed_window:
        raise ValueError("Repeated launch changed the visible main window")
    if first != second or first["sentinel"] != nonce:
        raise ValueError(
            "Repeated launch lost the proven shell identity/state/geometry"
        )


def reopen_once(run, root, deadline):
    # Normal user launch semantics: activate Finder, then reopen the existing app.
    shell.guest(
        run,
        root,
        "finder-foreground",
        deadline,
        "/usr/bin/open",
        "-b",
        "com.apple.finder",
    )
    shell.guest(run, root, "reopen-once", deadline, "/usr/bin/open", client.APP)


def repeat_probe(run, root, deadline):
    root.mkdir(mode=0o700)  # No replay of partial launch or proof delivery.
    pilot.require_bridge(
        shell.cli(run, root, "admission", deadline, "permissions", "status"), True
    )
    layout.configure(run, root, deadline)
    control_pid = quit_probe.create_control(run, root, deadline)
    client.client_observe(run, root / "client", deadline)
    result = json.loads((root / "client/result.json").read_text())
    if result.get("stableObservedWindow") is not True:
        raise ValueError("Live app admission failed")
    window = result["windowIds"][-1]
    initial = layout.snapshot(run, root, "initial-ui", deadline, window, 1)
    app_pid = pilot.payload(initial)["focused_element"]["processIdentifier"]
    first = layout.proof(run, root, "before", deadline, window, 1)
    before = quit_probe.admit_tree(
        quit_probe.inventory(run, root, "before-repeat", deadline),
        app_pid,
        first["pid"],
        control_pid,
    )
    pilot.qa.save(root / "before-admitted.json", before)
    reopen_once(run, root, deadline)
    windows = shell.cli(
        run, root, "after-windows", deadline, "window", "list", "--app", client.BUNDLE
    )
    observed_window = client.require_window(windows)
    value = layout.snapshot(run, root, "after-ui", deadline, observed_window, 1)
    if pilot.payload(value)["focused_element"]["processIdentifier"] != app_pid:
        raise ValueError("Repeated launch focused a different app process")
    second = layout.proof(run, root, "survivor", deadline, observed_window, 1)
    after = quit_probe.admit_tree(
        quit_probe.inventory(run, root, "after-repeat", deadline),
        app_pid,
        second["pid"],
        control_pid,
    )
    _, state = pilot.qa.load(run)
    require_repeat(
        before,
        after,
        first,
        second,
        window,
        observed_window,
        state["runId"].rsplit("-", 1)[-1],
    )
    pilot.qa.save(
        root / "result.json",
        {
            "slice": "CF-1C-AUTOMATION-6",
            "windowId": window,
            "beforeProof": first,
            "afterProof": second,
            "beforeAdmitted": before,
            "afterAdmitted": after,
            "repeatLaunchDeliveries": 1,
            "finderActivations": 1,
            "proofDeliveries": 2,
            "sameObservedSession": True,
            "independentImageReviewRequired": True,
            "baselineRowPromoted": False,
        },
    )


def request_step(request, index):
    if request == {"action": "repeat-probe"}:
        return {"Name": f"step-{index:02d}", "Repeat": True}
    return pilot.request_step(request, index)


def scenario_deadline(deadline):
    now = time.monotonic()
    if deadline - now < SCENARIO_SECONDS:
        raise TimeoutError("Insufficient reserved repeat budget; scenario not started")
    return now + SCENARIO_SECONDS


def observe(run, image):
    pilot.atomic.vnc.connection(run)
    if image != run / "console-first-frame.png" or not image.is_file():
        raise ValueError("Exact fresh owned first frame required")
    root = run / "repeat-observation"
    root.mkdir(mode=0o700)
    deadline = time.monotonic() + quit_probe.OBSERVER_SECONDS
    pilot.install(run, root, deadline)
    pilot.readback(run, root, deadline)

    def dispatch(run, step, root, deadline):
        if step.get("Repeat"):
            repeat_probe(run, root / "repeat", scenario_deadline(deadline))
        else:
            pilot.atomic.remaining(deadline - SCENARIO_SECONDS)
            pilot.dispatch(run, step, root, deadline - SCENARIO_SECONDS)

    pilot.atomic.permission_session(
        run,
        root,
        deadline,
        request_step=request_step,
        dispatch=dispatch,
        complete=lambda step: step.get("Repeat") is True,
    )


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--archive", type=Path, required=True)
    parser.add_argument("--runtime-archive", type=Path, required=True)
    args = parser.parse_args()

    def interrupted(_signum, _frame):
        raise InterruptedError("Repeated launch probe interrupted")

    signal.signal(signal.SIGTERM, interrupted)
    pilot.atomic.compare(
        args.archive,
        args.runtime_archive,
        observer=observe,
        prepare_hook=pilot.stage,
        hold=1800,
    )


if __name__ == "__main__":
    main()
