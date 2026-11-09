#!/usr/bin/env python3
"""CF-1C-AUTOMATION-5: one normal GUI Quit, owned descendants gone, control alive."""

import argparse
import json
from pathlib import Path
import signal
import time

import client_baseline_layout_probe as layout

shell = layout.shell
client = layout.client
pilot = layout.pilot
SCENARIO_SECONDS = 360
OBSERVER_SECONDS = 1500


def processes(raw):
    """Parse PID/parent/start-time/executable without treating PID alone as identity."""
    rows = {}
    for line in raw.decode("ascii").splitlines():
        parts = line.split(maxsplit=7)
        if len(parts) == 8 and parts[0].isdigit() and parts[1].isdigit():
            pid = int(parts[0])
            if pid in rows:
                raise ValueError("Duplicate process identity")
            rows[pid] = {
                "parent": int(parts[1]),
                "started": " ".join(parts[2:7]),
                "command": parts[7],
            }
    if not rows:
        raise ValueError("Empty independent process inventory")
    return rows


def admit_tree(rows, app_pid, shell_pid, control_pid):
    app = client.APP + "/Contents/MacOS/fingertrap"
    sidecar = client.APP + "/Contents/MacOS/fingertrap-sidecar"
    if rows.get(app_pid, {}).get("command") != app:
        raise ValueError("Admitted app identity changed")
    sidecars = [
        pid
        for pid, row in rows.items()
        if row["parent"] == app_pid and row["command"] == sidecar
    ]
    if (
        len(sidecars) != 1
        or rows.get(shell_pid, {}).get("parent") != sidecars[0]
        or rows.get(shell_pid, {}).get("command") != "/bin/zsh"
    ):
        raise ValueError("Expected exact app -> sidecar -> proven shell")
    owned = {app_pid}
    while True:
        more = {pid for pid, row in rows.items() if row["parent"] in owned}
        if more <= owned:
            break
        owned |= more
    control = rows.get(control_pid)
    if (
        control_pid in owned
        or not control
        or control["parent"] != 1
        or control["command"] != "/bin/sleep"
    ):
        raise ValueError("Control must be an independent adopted guest sleep")
    if any(
        row["command"] in (app, sidecar) and pid not in owned
        for pid, row in rows.items()
    ):
        raise ValueError("Ambiguous extra app or sidecar")
    return {
        "owned": {str(pid): rows[pid] for pid in sorted(owned)},
        "controlPid": control_pid,
        "control": control,
        "sidecarPid": sidecars[0],
    }


def require_shutdown(rows, admitted):
    """Reparenting is not termination; the unrelated control must retain identity."""
    if rows.get(admitted["controlPid"]) != admitted["control"]:
        raise ValueError("Unrelated control disappeared or changed identity")
    if any(int(pid) in rows for pid in admitted["owned"]):
        raise ValueError("Recorded app descendant remains alive")
    commands = {
        client.APP + "/Contents/MacOS/" + name
        for name in ("fingertrap", "fingertrap-sidecar")
    }
    if any(row["command"] in commands for row in rows.values()):
        raise ValueError("App or sidecar executable still present")


def inventory(run, root, name, deadline):
    return processes(
        shell.guest(
            run, root, name, deadline, "/bin/ps", "-axo", "pid,ppid,lstart,comm"
        )
    )


def create_control(run, root, deadline):
    code = """import json,pathlib,subprocess
p=pathlib.Path('/tmp/ft-quit-control.json')
if p.exists() or p.is_symlink(): raise ValueError('Existing control fixture')
with p.open('x') as f:
 child=subprocess.Popen(['/bin/sleep','1200'],stdin=subprocess.DEVNULL,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,start_new_session=True)
 json.dump({'pid':child.pid},f)
p.chmod(0o600)
print(p.read_text())"""
    value = json.loads(
        shell.guest(
            run, root, "control-create", deadline, "/usr/bin/python3", "-c", code
        )
    )
    if type(value.get("pid")) is not int or value["pid"] <= 1:
        raise ValueError("Invalid unrelated control PID")
    return value["pid"]


def quit_once(run, root, deadline, window_id, admitted):
    shell.cli(
        run,
        root,
        "quit-once",
        deadline,
        "press",
        "cmd+q",
        "--app",
        client.BUNDLE,
        "--window-id",
        str(window_id),
        "--foreground",
        allow_indeterminate=True,
    )
    for index in range(3):
        rows = inventory(run, root, f"after-quit-{index:02d}", deadline)
        # Loss of the unrelated process is never a transient successful shutdown.
        if rows.get(admitted["controlPid"]) != admitted["control"]:
            raise ValueError("Unrelated control lost after Quit")
        try:
            require_shutdown(rows, admitted)
            return
        except ValueError:
            if index == 2:
                raise
            time.sleep(min(2, pilot.atomic.remaining(deadline)))


def quit_probe(run, root, deadline):
    root.mkdir(mode=0o700)  # A partial Quit scenario must never replay.
    pilot.require_bridge(
        shell.cli(run, root, "admission", deadline, "permissions", "status"), True
    )
    layout.configure(run, root, deadline)
    control_pid = create_control(run, root, deadline)
    client.client_observe(run, root / "client", deadline)
    if (
        json.loads((root / "client/result.json").read_text()).get(
            "stableObservedWindow"
        )
        is not True
    ):
        raise ValueError("Live app admission did not pass")
    value = layout.snapshot(
        run,
        root,
        "initial-ui",
        deadline,
        json.loads((root / "client/result.json").read_text())["windowIds"][-1],
        1,
    )
    window_id = client.require_ui(value)
    app_pid = pilot.payload(value)["focused_element"]["processIdentifier"]
    proof = layout.proof(run, root, "before", deadline, window_id, 1)
    # UI proof must precede the final independent process inventory and Quit.
    admitted = admit_tree(
        inventory(run, root, "before-quit", deadline),
        app_pid,
        proof["pid"],
        control_pid,
    )
    pilot.qa.save(root / "admitted.json", admitted)
    quit_once(run, root, deadline, window_id, admitted)
    pilot.qa.save(
        root / "result.json",
        {
            "slice": "CF-1C-AUTOMATION-5",
            "windowId": window_id,
            "shellProof": proof,
            "admitted": admitted,
            "quitDeliveries": 1,
            "ownedDescendantsGone": True,
            "unrelatedControlSurvived": True,
            "independentImageReviewRequired": True,
            "baselineRowPromoted": False,
        },
    )


def request_step(request, index):
    if request == {"action": "quit-probe"}:
        return {"Name": f"step-{index:02d}", "Quit": True}
    return pilot.request_step(request, index)


def scenario_deadline(deadline):
    now = time.monotonic()
    if deadline - now < SCENARIO_SECONDS:
        raise TimeoutError("Insufficient reserved budget; no client scenario started")
    return now + SCENARIO_SECONDS


def observe(run, image):
    pilot.atomic.vnc.connection(run)
    if image != run / "console-first-frame.png" or not image.is_file():
        raise ValueError("Exact fresh owned first frame required")
    root = run / "quit-observation"
    root.mkdir(mode=0o700)
    deadline = time.monotonic() + OBSERVER_SECONDS
    pilot.install(run, root, deadline)
    pilot.readback(run, root, deadline)

    def dispatch(run, step, root, deadline):
        if step.get("Quit"):
            quit_probe(run, root / "quit", scenario_deadline(deadline))
        else:
            # Enrollment cannot consume the reserved scenario budget.
            pilot.atomic.remaining(deadline - SCENARIO_SECONDS)
            pilot.dispatch(run, step, root, deadline - SCENARIO_SECONDS)

    pilot.atomic.permission_session(
        run,
        root,
        deadline,
        request_step=request_step,
        dispatch=dispatch,
        complete=lambda step: step.get("Quit") is True,
    )


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--archive", type=Path, required=True)
    parser.add_argument("--runtime-archive", type=Path, required=True)
    args = parser.parse_args()

    def interrupted(_signum, _frame):
        raise InterruptedError("Quit probe interrupted")

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
