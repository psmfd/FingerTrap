#!/usr/bin/env python3
"""Atomic CF-1C diagnostic: fresh owned boot, RFB, guest screen/clock, cleanup.

Requires the preserved, hash-pinned private go-vnc probe. Raw results remain
private. Neither process success nor screenshot existence is input acceptance.
"""

import argparse
import base64
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re
import signal
import subprocess
import sys
import time

import client_baseline_vnc as vnc

qa = vnc.qa
PROBE = qa.ROOT / ".artifacts/client-baseline/rfb-compare"
STEPS = (
    # CF-1C-GO-1: observed in the preserved 2048x1536 native first frame.
    {"Name": "gomenu", "Click": True, "X": 520, "Y": 24},
    {"Name": "escape", "Keys": [65307]},
    {"Name": "lowerg", "Keys": [65511, 65505, 103]},
)


def remaining(deadline, cap=30):
    seconds = deadline - time.monotonic()
    if seconds <= 0:
        raise TimeoutError("Atomic observation deadline expired")
    return min(seconds, cap)


def verify_probe():
    reference = json.loads(
        (qa.EVIDENCE / "gui-continuation/rfb-comparison.json").read_text()
    )["module"]
    for name, digest in reference["files"].items():
        qa.verify_digest(PROBE / name, digest)
    if (PROBE / "go.sum").read_text() != reference["goSum"]:
        raise ValueError("Independent probe module sums changed")


def guest_command(run, deadline, *args, cap=30):
    vnc.connection(run)
    _, state = qa.load(run)
    return qa.command(
        ["tart", "exec", state["vm"], *args], timeout=remaining(deadline, cap)
    )


def agent_inventory(run, output, deadline):
    evidence = {"commands": [], "services": []}

    def execute(*args):
        try:
            data = guest_command(run, deadline, *args)
        except (subprocess.CalledProcessError, subprocess.TimeoutExpired) as error:
            evidence["commands"].append(
                {
                    "arguments": args,
                    "exitCode": getattr(error, "returncode", None),
                    "timedOut": isinstance(error, subprocess.TimeoutExpired),
                    "stdout": (error.stdout or b"").decode(errors="replace"),
                    "stderr": (error.stderr or b"").decode(errors="replace"),
                }
            )
            qa.save(output, evidence)
            raise
        evidence["commands"].append(
            {
                "arguments": args,
                "exitCode": 0,
                "stdout": data.decode(errors="replace"),
            }
        )
        qa.save(output, evidence)
        return data

    evidence["uid"] = int(execute("/usr/bin/id", "-u"))
    processes = {}
    for line in execute("/bin/ps", "-axo", "pid=,uid=,comm=").decode().splitlines():
        fields = line.split(None, 2)
        if len(fields) == 3 and Path(fields[2]).name == "tart-guest-agent":
            processes[int(fields[0])] = {"uid": int(fields[1]), "executable": fields[2]}
    evidence["processes"] = processes
    # Optional directories are handled in the guest, never dropped on ls failure.
    paths = (
        execute(
            "/bin/sh",
            "-c",
            "for p in /Library/LaunchAgents/*tart*.plist "
            "/Library/LaunchDaemons/*tart*.plist "
            "/Users/admin/Library/LaunchAgents/*tart*.plist; "
            'do if test -f "$p"; then printf "%s\\n" "$p"; fi; done',
        )
        .decode()
        .splitlines()
    )
    if len(paths) > 16:
        raise ValueError("Guest-agent plist inventory exceeds budget")
    for path in paths:
        if not re.fullmatch(
            r"/(?:Library/Launch(?:Agents|Daemons)|Users/admin/Library/LaunchAgents)"
            r"/[A-Za-z0-9._-]*tart[A-Za-z0-9._-]*\.plist",
            path,
        ):
            raise ValueError("Unexpected guest-agent plist path")
        raw = execute("/usr/bin/plutil", "-convert", "json", "-o", "-", path)
        document = json.loads(raw)
        label = document.get("Label", "")
        if not re.fullmatch(r"[A-Za-z0-9._-]{1,128}", label):
            raise ValueError("Unexpected guest launch service label")
        target = (
            "system/"
            if path.startswith("/Library/LaunchDaemons/")
            else f"gui/{evidence['uid']}/"
        ) + label
        service = {
            "plist": path,
            "plistSha256": hashlib.sha256(raw).hexdigest(),
            "target": target,
            "document": document,
        }
        evidence["services"].append(service)
        try:
            listing = execute("/bin/launchctl", "print", target).decode()
        except subprocess.CalledProcessError:
            continue  # Preserve inactive/unavailable candidates and their errors.
        match = re.search(r"^\s*pid = ([0-9]+)\s*$", listing, re.MULTILINE)
        if not match or int(match[1]) not in processes:
            continue
        service.update(pid=int(match[1]), **processes[int(match[1])])
        arguments = document.get("ProgramArguments", [])
        program = document.get("Program") or (arguments[0] if arguments else "")
        if not Path(program).is_absolute() or Path(program).name != "tart-guest-agent":
            continue
        service["program"] = program
        digest = execute("/usr/bin/shasum", "-a", "256", program).decode().split()[0]
        if not re.fullmatch(r"[0-9a-f]{64}", digest):
            raise ValueError("Invalid guest-agent executable digest")
        service["programSha256"] = digest
        executable = service["executable"]
        if not Path(executable).is_absolute():
            continue
        actual = execute("/usr/bin/shasum", "-a", "256", executable).decode().split()[0]
        service["executableMatches"] = actual == digest
    qa.save(output, evidence)
    return evidence


def select_agent(inventory):
    candidates = [
        s
        for s in inventory["services"]
        if (
            s.get("pid")
            and s.get("executableMatches")
            and s.get("programSha256")
            and s.get("uid") == inventory["uid"]
            and set(s["document"].get("ProgramArguments", []))
            & {"--run-agent", "--run-rpc"}
        )
    ]
    if len(candidates) != 1:
        raise ValueError("A unique active guest RPC service is required")
    return candidates[0]


def same_service(before, after):
    return all(
        before.get(k) == after.get(k)
        for k in (
            "plist",
            "plistSha256",
            "target",
            "uid",
            "program",
            "programSha256",
            "executable",
        )
    )


def restart_agent(run, root, step, deadline):
    output = root / step["Name"]
    output.mkdir(mode=0o700)
    restart_deadline = min(deadline, time.monotonic() + 90)
    baseline = select_agent(json.loads((root / "agent-inventory.json").read_text()))
    before = select_agent(
        agent_inventory(run, output / "before.json", restart_deadline)
    )
    if not same_service(baseline, before) or baseline["pid"] != before["pid"]:
        raise ValueError("Guest RPC service identity changed before restart")
    receipt = {"target": before["target"], "beforePid": before["pid"]}
    qa.save(output / "restart.json", receipt)
    try:
        # A normal service restart can disconnect its own RPC transport.
        guest_command(
            run,
            restart_deadline,
            "/bin/launchctl",
            "kickstart",
            "-k",
            before["target"],
            cap=10,
        )
        receipt["requestCompleted"] = True
    except (subprocess.CalledProcessError, subprocess.TimeoutExpired) as error:
        receipt["requestError"] = type(error).__name__
    qa.save(output / "restart.json", receipt)
    attempt = 0
    while True:
        remaining(restart_deadline)
        try:
            current = agent_inventory(
                run, output / f"reconnect-{attempt:02d}.json", restart_deadline
            )
            after = select_agent(current)
            if not same_service(before, after):
                raise RuntimeError("Guest RPC service identity changed after restart")
            if before["pid"] != after["pid"]:
                receipt.update(afterPid=after["pid"], reconnected=True)
                qa.save(output / "restart.json", receipt)
                guest_screen(run, output, restart_deadline)
                return
        except (subprocess.CalledProcessError, subprocess.TimeoutExpired):
            pass
        attempt += 1
        time.sleep(min(1, remaining(restart_deadline)))


def diagnose_agent(run, root, step, deadline):
    output = root / step["Name"]
    output.mkdir(mode=0o700)
    selected = select_agent(json.loads((root / "agent-inventory.json").read_text()))
    current = select_agent(agent_inventory(run, output / "identity.json", deadline))
    if not same_service(selected, current):
        raise ValueError("Guest RPC identity changed before privacy diagnostic")
    signing_status = {"exitCode": 0}
    try:
        signing = guest_command(
            run,
            deadline,
            "/bin/sh",
            "-c",
            'exec /usr/bin/codesign -d -r- --verbose=4 "$1" 2>&1',
            "sh",
            current["program"],
        )
    except (subprocess.CalledProcessError, subprocess.TimeoutExpired) as error:
        signing = (error.stdout or b"") + (error.stderr or b"")
        signing_status = {
            "exitCode": getattr(error, "returncode", None),
            "timedOut": isinstance(error, subprocess.TimeoutExpired),
        }
    (output / "code-signing.txt").write_bytes(signing)
    qa.save(output / "code-signing-status.json", signing_status)
    # Read only this disposable guest's live TCC decisions. log's own timeout
    # bounds the guest process even if its RPC client disconnects.
    command = """
/usr/bin/sudo -n /usr/bin/log stream --timeout 12 --level debug --style compact \
--predicate 'subsystem == "com.apple.TCC" AND (eventMessage CONTAINS[c] \
"tart-guest-agent" OR eventMessage CONTAINS "ScreenCapture" OR eventMessage \
BEGINSWITH "AttributionChain")' > /tmp/ft-tcc-decisions.log 2>&1 &
tcc_pid=$!
trap 'kill "$tcc_pid" 2>/dev/null || true' EXIT
/bin/sleep 1
/usr/sbin/screencapture -x /tmp/ft-tcc-probe.png
/bin/date -u > /tmp/ft-tcc-probe-clock.txt
wait "$tcc_pid"
tcc_exit=$?
trap - EXIT
/usr/bin/head -c 262144 /tmp/ft-tcc-decisions.log
exit "$tcc_exit"
"""
    try:
        data = guest_command(run, deadline, "/bin/sh", "-c", command)
    except subprocess.CalledProcessError as error:
        (output / "tcc-decisions.log").write_bytes(error.stdout or b"")
        (output / "tcc-error.txt").write_bytes(error.stderr or b"")
        raise
    (output / "tcc-decisions.log").write_bytes(data)
    guest_screen(run, output, deadline)


def guest_screen(run, output, deadline, capture=None):
    def execute(*args):
        return guest_command(run, deadline, *args)

    # Fixed guest-only paths/commands; no host clipboard or operator data.
    if capture is not None:
        capture(run, output, deadline)
    command = (
        "/usr/sbin/screencapture -x /tmp/ft-rfb.png && " if capture is None else ""
    ) + (
        "/usr/bin/sips -s format jpeg -s formatOptions 60 -Z 1024 "
        "/tmp/ft-rfb.png --out /tmp/ft-rfb.jpg >/dev/null && "
        "/usr/bin/base64 -i /tmp/ft-rfb.jpg > /tmp/ft-rfb.b64 && "
        "/usr/bin/stat -f%z /tmp/ft-rfb.b64"
    )
    size = int(execute("/bin/sh", "-c", command))
    if not 0 < size < 1024 * 1024:
        raise ValueError("Guest screenshot exceeds transfer budget")
    data = bytearray()
    for offset in range(0, size, 65536):
        data.extend(
            execute(
                "/bin/sh",
                "-c",
                f"/usr/bin/tail -c +{offset + 1} /tmp/ft-rfb.b64 | /usr/bin/head -c 65536",
            )
        )
    if len(data) != size:
        raise ValueError("Incomplete guest screenshot transfer")
    (output / "guest-screen.jpg").write_bytes(base64.b64decode(data))
    (output / "guest-clock.txt").write_bytes(execute("/bin/date", "-u"))
    qa.save(
        output / "readback.json",
        {
            "hostUtc": datetime.now(timezone.utc).isoformat(),
            "screenSha256": qa.digest(output / "guest-screen.jpg"),
        },
    )


def probe_step(run, step, root, deadline, readback=None):
    output = root / step["Name"]
    output.mkdir(mode=0o700)
    endpoint = vnc.connection(run)
    config = {
        "Address": f"127.0.0.1:{endpoint.port}",
        "Password": vnc.unquote(endpoint.password),
        "Run": str(output),
        "Steps": [step],
    }
    qa.save(output / "step.json", step)
    try:
        with (output / "probe.log").open("wb") as log:
            result = subprocess.run(
                [str(PROBE / "rfb-probe")],
                input=json.dumps(config).encode(),
                stdout=log,
                stderr=subprocess.STDOUT,
                timeout=remaining(deadline, 45),
                check=False,
            )
        qa.save(output / "process.json", {"exitCode": result.returncode})
    except subprocess.TimeoutExpired:
        qa.save(output / "process.json", {"timedOut": True})
    finally:
        # Even a failed/timed-out RFB step gets immediate independent readback.
        (readback or guest_screen)(run, output, deadline)


def permission_step(request, index):
    if request == {"action": "restart-agent"}:
        return {"Name": f"step-{index:02d}", "RestartAgent": True}
    if request == {"action": "diagnose-agent"}:
        return {"Name": f"step-{index:02d}", "DiagnoseAgent": True}
    vnc.validate(request)
    action = request["action"]
    if (
        action not in ("capture", "finish", "click", "chord", "text")
        or "delay" in request
    ):
        raise ValueError("Unsupported permission-session action")
    if action == "text" and request["text"] != "/tmp/ft-input-probe":
        raise ValueError("Only the synthetic input-verification path is allowed")
    step = {"Name": f"step-{index:02d}"}
    if action == "click":
        step.update(Click=True, X=request["x"], Y=request["y"])
    elif action == "chord":
        step["Keys"] = request["keys"]
    elif action == "text":
        step["Text"] = request["text"]
    return None if action == "finish" else step


def permission_session(
    run, root, deadline, request_step=permission_step, dispatch=None, complete=None
):
    # Files are private to this new explicit run, never selected via a pointer.
    for index in range(40):
        request = root / f"request-{index:02d}.json"
        qa.save(root / "session.json", {"phase": "waiting", "nextIndex": index})
        while True:
            remaining(deadline)
            vnc.connection(run)
            if request.is_symlink():
                raise ValueError("Symlinked permission request")
            if request.exists():
                if not request.is_file() or request.stat().st_size > 2048:
                    raise ValueError("Permission request exceeds budget")
                step = request_step(json.loads(request.read_text()), index)
                break
            time.sleep(min(0.5, remaining(deadline)))
        if step is None:
            qa.save(root / "session.json", {"phase": "finished", "actions": index})
            return
        if dispatch is not None:
            dispatch(run, step, root, deadline)
        elif step.get("RestartAgent"):
            restart_agent(run, root, step, deadline)
        elif step.get("DiagnoseAgent"):
            diagnose_agent(run, root, step, deadline)
        else:
            probe_step(run, step, root, deadline)
        if complete is not None and complete(step):
            qa.save(root / "session.json", {"phase": "finished", "actions": index + 1})
            return
    raise TimeoutError("Permission session action budget exhausted")


def observe(run, image, permission=False):
    # Explicit driver-owned run; active-console.json is never read or written.
    vnc.connection(run)
    if image != run / "console-first-frame.png" or not image.is_file():
        raise ValueError("Fresh driver first frame is required")
    deadline = time.monotonic() + (720 if permission else 240)
    root = run / "atomic-comparison"
    root.mkdir(mode=0o700)
    if permission:
        agent_inventory(run, root / "agent-inventory.json", deadline)
    guest_screen(run, root, deadline)
    if permission:
        permission_session(run, root, deadline)
        return
    for step in STEPS:
        probe_step(run, step, root, deadline)


def compare(
    archive, runtime, permission=False, *, observer=None, prepare_hook=None, hold=None
):
    verify_probe()
    owned = []

    def observe_owned(run, image):
        owned.append(run)
        if observer is None:
            observe(run, image, permission=permission)
        else:
            observer(run, image)

    try:
        options = {"observer": observe_owned}
        if prepare_hook is not None:
            options["prepare_hook"] = prepare_hook
        return vnc.serve(
            archive,
            runtime,
            hold if hold is not None else (960 if permission else 480),
            **options,
        )
    finally:
        # serve finishes the owned controller before this independent readback.
        for run in owned:
            _, state = qa.load(run)
            listing = json.loads(qa.command(["tart", "list", "--format", "json"]))
            names = {vm["Name"] for vm in listing}
            if (
                not state.get("cleanupVerified")
                or state["phase"] != "cleaned"
                or state["vm"] in names
                or not set(state["originalVms"]).issubset(names)
                or any(vm["Running"] for vm in listing)
            ):
                raise RuntimeError("Atomic comparison cleanup verification failed")
            qa.save(
                run / "atomic-cleanup.json",
                {
                    "cleanupVerified": True,
                    "originalVmsStoppedAndPreserved": True,
                },
            )


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--archive", type=Path, required=True)
    parser.add_argument("--runtime-archive", type=Path, required=True)
    parser.add_argument(
        "--permission-session",
        action="store_true",
        help="Bounded fresh-run guest permission/input UI session (720s, 40 actions)",
    )
    args = parser.parse_args()

    def interrupted(_signum, _frame):
        raise InterruptedError("Atomic comparison interrupted")

    signal.signal(signal.SIGTERM, interrupted)
    compare(args.archive, args.runtime_archive, permission=args.permission_session)
    print("Atomic comparison finished; inspect private images and logs.")


if __name__ == "__main__":
    sys.exit(main())
