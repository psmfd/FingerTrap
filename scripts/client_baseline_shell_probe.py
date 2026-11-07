#!/usr/bin/env python3
"""CF-1C-AUTOMATION-3: bounded UI-only shell input with independent proof."""

import argparse
import base64
import json
from pathlib import Path
import re
import signal
import subprocess
import time

import client_baseline_client_observe as client

pilot = client.pilot
PROOF = "/tmp/ft-cf1-shell-proof"


def command_for(run):
    _, state = pilot.qa.load(run)
    marker = ("FT_CF1_SHELL_OK_" + state["runId"] + "\n").encode()
    encoded = base64.b64encode(marker).decode()
    return f"printf %s {encoded} | /usr/bin/base64 -D | /usr/bin/tee {PROOF}\n", marker


def button(value):
    client.require_ui(value)
    data = pilot.payload(value)
    buttons = [
        e
        for e in data["ui_elements"]
        if e.get("label") == "New shell tab" and e.get("ax_role") == "AXButton"
    ]
    if (
        len(buttons) != 1
        or not buttons[0].get("id")
        or buttons[0].get("is_actionable") is not True
        or not re.fullmatch(r"ps1_[0-9a-f]{32}", data.get("snapshot_id", ""))
    ):
        raise ValueError("Expected unique fresh New shell tab AX target")
    return buttons[0]["id"], data["snapshot_id"]


def terminal(value, window_id):
    if client.require_ui(value) != window_id:
        raise ValueError("Terminal receiver moved to another window")
    data = pilot.payload(value)
    close_shell = [
        e
        for e in data["ui_elements"]
        if e.get("ax_role") == "AXButton"
        and e.get("label", "").startswith("Close shell ")
    ]
    focused = data.get("focused_element", {})
    if (
        len(close_shell) != 1
        or focused.get("role") != "AXTextArea"
        or focused.get("windowID") != window_id
    ):
        raise ValueError(
            "Expected one observed shell tab and focused terminal text area"
        )


def require_proof(actual, expected):
    if actual != expected:
        raise ValueError(
            "Independent guest file did not contain the run-bound UI proof"
        )


def cli(run, root, name, deadline, *args, allow_indeterminate=False):
    try:
        return pilot.cli(run, root, name, deadline, *args)
    except subprocess.CalledProcessError:
        result = json.loads((root / (name + ".stdout")).read_text())
        outcome = result.get("outcome", {})
        if (
            not allow_indeterminate
            or outcome.get("state") != "indeterminate"
            or outcome.get("mutation_dispatched") is not True
        ):
            raise
        return result
    finally:
        client.checkpoint(run, root, name, deadline)


def guest(run, root, name, deadline, *args):
    try:
        return pilot.execute(run, root, name, deadline, *args)
    finally:
        client.checkpoint(run, root, name, deadline)


def shell_probe(run, root, deadline):
    root.mkdir(mode=0o700)  # No partial replay or second tab/command delivery.
    pilot.require_bridge(
        cli(run, root, "admission", deadline, "permissions", "status"), True
    )
    guest(
        run,
        root,
        "proof-absent",
        deadline,
        "/bin/sh",
        "-c",
        f"test ! -e {PROOF} && test ! -L {PROOF}",
    )
    value = cli(
        run, root, "before-tab", deadline, "see", "--app", client.BUNDLE, "--fresh"
    )
    window_id = client.require_ui(value)
    element, snapshot = button(value)
    cli(
        run,
        root,
        "new-shell",
        deadline,
        "click",
        "--on",
        element,
        "--snapshot",
        snapshot,
        "--app",
        client.BUNDLE,
        allow_indeterminate=True,
    )
    value = cli(
        run, root, "shell-receiver", deadline, "see", "--app", client.BUNDLE, "--fresh"
    )
    terminal(value, window_id)
    command, marker = command_for(run)
    pilot.qa.save(
        root / "expected.json",
        {"command": command, "marker": marker.decode(), "proof": PROOF},
    )
    try:
        cli(
            run,
            root,
            "type-once",
            deadline,
            "type",
            command,
            "--app",
            client.BUNDLE,
            "--window-id",
            str(window_id),
            "--foreground",
            "--delay",
            "50",
            "--accept-dispatched",
        )
    except subprocess.CalledProcessError:
        result = json.loads((root / "type-once.stdout").read_text())
        if (
            result.get("outcome", {}).get("state") != "indeterminate"
            or result.get("outcome", {}).get("mutation_dispatched") is not True
        ):
            raise
    # No more input after this point, including when delivery is indeterminate.
    verified = False
    for index in range(3):
        try:
            actual = guest(
                run,
                root,
                f"proof-read-{index:02d}",
                deadline,
                "/bin/sh",
                "-c",
                f"test -f {PROOF} && test ! -L {PROOF} && /usr/bin/head -c 256 {PROOF}",
            )
            if actual == marker or not marker.startswith(actual):
                require_proof(actual, marker)
                verified = True
                break
        except subprocess.CalledProcessError:
            pass
        time.sleep(min(2, pilot.atomic.remaining(deadline)))
    if not verified:
        raise ValueError("Single UI command did not produce independent shell proof")
    cli(run, root, "after-output", deadline, "see", "--app", client.BUNDLE, "--fresh")
    guest(run, root, "processes", deadline, "/bin/ps", "-axo", "pid,ppid,comm")
    pilot.qa.save(
        root / "result.json",
        {
            "slice": "CF-1C-AUTOMATION-3",
            "independentProofExact": True,
            "tabCreations": 1,
            "commandDeliveries": 1,
            "returnCount": 1,
            "decodedOutputNeedsIndependentImageReview": True,
            "baselineRowPromoted": False,
        },
    )


def request_step(request, index):
    if request == {"action": "shell-probe"}:
        return {"Name": f"step-{index:02d}", "ShellProbe": True}
    return pilot.request_step(request, index)


def dispatch(run, step, root, deadline):
    if step.get("ShellProbe"):
        client.client_observe(run, root / "client", deadline)
        result = json.loads((root / "client/result.json").read_text())
        if result.get("stableObservedWindow") is not True:
            raise ValueError("Corrected live main-window admission did not pass")
        shell_probe(run, root / "shell", deadline)
    else:
        pilot.dispatch(run, step, root, deadline)


def observe(run, image):
    pilot.atomic.vnc.connection(run)
    if image != run / "console-first-frame.png" or not image.is_file():
        raise ValueError("Shell probe requires the fresh owned first frame")
    root = run / "shell-observation"
    root.mkdir(mode=0o700)
    deadline = time.monotonic() + 1200
    pilot.install(run, root, deadline)
    pilot.readback(run, root, deadline)
    pilot.atomic.permission_session(
        run, root, deadline, request_step=request_step, dispatch=dispatch
    )


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--archive", type=Path, required=True)
    parser.add_argument("--runtime-archive", type=Path, required=True)
    args = parser.parse_args()

    def interrupted(_signum, _frame):
        raise InterruptedError("Shell probe interrupted")

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
