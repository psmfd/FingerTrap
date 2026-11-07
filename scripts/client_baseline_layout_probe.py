#!/usr/bin/env python3
"""CF-1C-AUTOMATION-4: observed shell resize, split and sibling-only close."""

import argparse
import base64
import json
from pathlib import Path
import re
import signal
import time

import client_baseline_shell_probe as shell

client = shell.client
pilot = shell.pilot
PHASES = ("before", "resized", "sibling", "survivor")


def receiver(value, window_id, count):
    if client.require_ui(value) != window_id:
        raise ValueError("Layout target moved to another window")
    data = pilot.payload(value)
    areas = [
        e
        for e in data["ui_elements"]
        if e.get("ax_role") == "AXTextArea" and e.get("label") == "Terminal input"
    ]
    focus = data.get("focused_element", {})
    if (
        len(areas) != count
        or focus.get("role") != "AXTextArea"
        or focus.get("windowID") != window_id
    ):
        raise ValueError("Expected observed terminal count and focused receiver")


def command_for(run, phase):
    if phase not in PHASES:
        raise ValueError("Unknown fixed proof phase")
    _, state = pilot.qa.load(run)
    nonce = state["runId"].rsplit("-", 1)[-1]
    tag = {"before": "b", "resized": "r", "sibling": "s", "survivor": "a"}[phase]
    marker = f"FT4_{nonce}_{tag}"
    encoded = base64.b64encode((marker + "\n").encode()).decode()
    path = "/tmp/ft4-" + tag
    prefix = f"FT4={nonce};" if phase == "before" else ""
    command = (
        prefix
        + f"{{ echo {encoded}|/usr/bin/base64 -D;echo PID=$$;/bin/stty size;echo SENTINEL=$FT4;}}|/usr/bin/tee {path}\n"
    )
    if len(command) > 150:
        raise ValueError("Proof command exceeds observed Bridge typing budget")
    return command, marker, path


def parse_proof(raw, marker):
    lines = raw.decode("ascii").splitlines()
    if (
        len(lines) != 4
        or lines[0] != marker
        or not re.fullmatch(r"PID=[1-9][0-9]*", lines[1])
        or not lines[3].startswith("SENTINEL=")
    ):
        raise ValueError("Independent layout proof does not match expected marker/PID")
    dimensions = re.fullmatch(r"([1-9][0-9]*)\s+([1-9][0-9]*)", lines[2].strip())
    if dimensions is None:
        raise ValueError("Interactive stty did not report positive rows/columns")
    return {
        "pid": int(lines[1][4:]),
        "rows": int(dimensions[1]),
        "columns": int(dimensions[2]),
        "sentinel": lines[3][9:],
    }


def verify_resize(before, resized, run_id):
    run_id = run_id.rsplit("-", 1)[-1]
    if (
        before["pid"] != resized["pid"]
        or before["sentinel"] != run_id
        or resized["sentinel"] != run_id
    ):
        raise ValueError("Original shell did not survive resize")
    if not (
        resized["rows"] < before["rows"] and resized["columns"] < before["columns"]
    ):
        raise ValueError("PTY geometry did not shrink on window resize")


def verify_split(proofs, run_id):
    before, resized, sibling = [proofs[p] for p in PHASES[:3]]
    verify_resize(before, resized, run_id)
    if (
        sibling["pid"] == before["pid"]
        or sibling["sentinel"]
        or sibling["columns"] >= resized["columns"]
    ):
        raise ValueError("Cannot close an unproven independent narrower sibling")


def verify_progress(proofs, run_id):
    verify_split(proofs, run_id)
    run_id = run_id.rsplit("-", 1)[-1]
    before, resized, sibling, survivor = [proofs[p] for p in PHASES]
    if (
        any(
            p["pid"] != before["pid"] or p["sentinel"] != run_id
            for p in (before, resized, survivor)
        )
        or sibling["pid"] == before["pid"]
        or sibling["sentinel"]
    ):
        raise ValueError(
            "Original shell identity/sentinel or sibling independence failed"
        )
    if not (
        resized["rows"] < before["rows"]
        and resized["columns"] < before["columns"]
        and sibling["columns"] < resized["columns"]
    ):
        raise ValueError("PTY geometry did not shrink on window resize/split")
    if (survivor["rows"], survivor["columns"]) != (resized["rows"], resized["columns"]):
        raise ValueError("Survivor PTY dimensions did not recover after sibling close")


def process_tree(raw, app_pid, shell_pids, gone=None):
    rows = {}
    for line in raw.decode().splitlines():
        parts = line.split(maxsplit=2)
        if len(parts) == 3 and parts[0].isdigit() and parts[1].isdigit():
            rows[int(parts[0])] = (int(parts[1]), parts[2])
    if rows.get(app_pid, (None, None))[1] != client.APP + "/Contents/MacOS/fingertrap":
        raise ValueError("Recorded app process disappeared or changed")
    sidecars = [
        pid
        for pid, (ppid, cmd) in rows.items()
        if ppid == app_pid and cmd == client.APP + "/Contents/MacOS/fingertrap-sidecar"
    ]
    if (
        len(set(shell_pids)) != len(shell_pids)
        or len(sidecars) != 1
        or any(rows.get(pid) != (sidecars[0], "/bin/zsh") for pid in shell_pids)
        or gone in rows
    ):
        raise ValueError("Expected shell ancestry or sibling termination not observed")
    return sidecars[0]


def snapshot(run, root, name, deadline, window_id, count):
    value = shell.cli(
        run, root, name, deadline, "see", "--app", client.BUNDLE, "--fresh"
    )
    receiver(value, window_id, count)
    return value


def proof(run, root, phase, deadline, window_id, count):
    command, marker, path = command_for(run, phase)
    shell.guest(
        run,
        root,
        phase + "-absent",
        deadline,
        "/bin/sh",
        "-c",
        f"test ! -e {path} && test ! -L {path}",
    )
    snapshot(run, root, phase + "-receiver", deadline, window_id, count)
    pilot.qa.save(
        root / (phase + "-expected.json"),
        {"command": command, "marker": marker, "path": path},
    )
    shell.cli(
        run,
        root,
        phase + "-type",
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
        allow_indeterminate=True,
    )
    raw = shell.guest(
        run,
        root,
        phase + "-read",
        deadline,
        "/bin/sh",
        "-c",
        f"test -f {path} && test ! -L {path} && /usr/bin/head -c 512 {path}",
    )
    return parse_proof(raw, marker)


def configure(run, root, deadline):
    code = """import json,pathlib
p=pathlib.Path("/Users/admin/Library/Application Support/fingertrap/settings.json")
if p.exists() or p.is_symlink(): raise ValueError("Refuse existing app settings")
p.parent.mkdir(parents=True,exist_ok=True)
with p.open("x") as f: f.write('{"version":1,"pane":{"defaultKind":"shell"}}\\n')
p.chmod(0o600)
print(p.read_text())"""
    raw = shell.guest(
        run, root, "shell-default-fixture", deadline, "/usr/bin/python3", "-c", code
    )
    if json.loads(raw) != {"version": 1, "pane": {"defaultKind": "shell"}}:
        raise ValueError("Supported guest shell-default fixture did not verify")


def close_sibling(run, root, deadline, window_id, proofs, state, app_pid):
    verify_split(proofs, state["runId"])
    raw = shell.guest(
        run, root, "both-processes", deadline, "/bin/ps", "-axo", "pid,ppid,comm"
    )
    sidecar = process_tree(
        raw, app_pid, [proofs["before"]["pid"], proofs["sibling"]["pid"]]
    )
    shell.cli(
        run,
        root,
        "close-sibling-once",
        deadline,
        "press",
        "cmd+shift+w",
        "--app",
        client.BUNDLE,
        "--window-id",
        str(window_id),
        "--foreground",
        allow_indeterminate=True,
    )
    return sidecar


def layout(run, root, deadline):
    root.mkdir(mode=0o700)
    pilot.require_bridge(
        shell.cli(run, root, "admission", deadline, "permissions", "status"), True
    )
    configure(run, root, deadline)
    client.client_observe(run, root / "client", deadline)
    if (
        json.loads((root / "client/result.json").read_text()).get(
            "stableObservedWindow"
        )
        is not True
    ):
        raise ValueError("Live main window admission failed")
    value = shell.cli(
        run, root, "initial-ui", deadline, "see", "--app", client.BUNDLE, "--fresh"
    )
    window_id = client.require_ui(value)
    app_pid = pilot.payload(value)["focused_element"]["processIdentifier"]
    proofs = {"before": proof(run, root, "before", deadline, window_id, 1)}
    shell.cli(
        run,
        root,
        "resize-once",
        deadline,
        "window",
        "resize",
        "--app",
        client.BUNDLE,
        "--window-id",
        str(window_id),
        "-w",
        "900",
        "--height",
        "600",
        allow_indeterminate=True,
    )
    value = snapshot(run, root, "resized-ui", deadline, window_id, 1)
    bounds = pilot.payload(value)["coordinate_context"]["logical_bounds"][1]
    if any(abs(a - b) > 2 for a, b in zip(bounds, (900, 600))):
        raise ValueError("Fresh AX geometry does not match requested 900x600")
    proofs["resized"] = proof(run, root, "resized", deadline, window_id, 1)
    _, state = pilot.qa.load(run)
    verify_resize(proofs["before"], proofs["resized"], state["runId"])
    shell.cli(
        run,
        root,
        "split-once",
        deadline,
        "press",
        "cmd+shift+d",
        "--app",
        client.BUNDLE,
        "--window-id",
        str(window_id),
        "--foreground",
        allow_indeterminate=True,
    )
    snapshot(run, root, "split-ui", deadline, window_id, 2)
    proofs["sibling"] = proof(run, root, "sibling", deadline, window_id, 2)
    sidecar = close_sibling(run, root, deadline, window_id, proofs, state, app_pid)
    snapshot(run, root, "survivor-ui", deadline, window_id, 1)
    proofs["survivor"] = proof(run, root, "survivor", deadline, window_id, 1)
    _, state = pilot.qa.load(run)
    verify_progress(proofs, state["runId"])
    for index in range(3):
        raw = shell.guest(
            run,
            root,
            f"survivor-processes-{index}",
            deadline,
            "/bin/ps",
            "-axo",
            "pid,ppid,comm",
        )
        try:
            if (
                process_tree(
                    raw,
                    app_pid,
                    [proofs["before"]["pid"]],
                    gone=proofs["sibling"]["pid"],
                )
                != sidecar
            ):
                raise ValueError("Sidecar identity changed")
            break
        except ValueError:
            if index == 2:
                raise
            time.sleep(min(2, pilot.atomic.remaining(deadline)))
    pilot.qa.save(
        root / "result.json",
        {
            "slice": "CF-1C-AUTOMATION-4",
            "proofs": proofs,
            "appPid": app_pid,
            "sidecarPid": sidecar,
            "windowId": window_id,
            "fixture": {"pane.defaultKind": "shell"},
            "siblingClosedOriginalSurvived": True,
            "independentImageReviewRequired": True,
            "baselineRowPromoted": False,
        },
    )


def request_step(request, index):
    if request == {"action": "layout-probe"}:
        return {"Name": f"step-{index:02d}", "Layout": True}
    return pilot.request_step(request, index)


def dispatch(run, step, root, deadline):
    if step.get("Layout"):
        layout(run, root / "layout", deadline)
    else:
        pilot.dispatch(run, step, root, deadline)


def observe(run, image):
    pilot.atomic.vnc.connection(run)
    if image != run / "console-first-frame.png" or not image.is_file():
        raise ValueError("Fresh owned first frame required")
    root = run / "layout-observation"
    root.mkdir(mode=0o700)
    deadline = time.monotonic() + 1500
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
        raise InterruptedError("Layout probe interrupted")

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
