#!/usr/bin/env python3
"""Atomic CF-1C diagnostic: fresh owned boot, RFB, guest screen/clock, cleanup.

Requires the preserved, hash-pinned private go-vnc probe. Raw results remain
private. Neither process success nor screenshot existence is input acceptance.
"""

import argparse
import base64
from datetime import datetime, timezone
import json
from pathlib import Path
import signal
import subprocess
import sys
import time

import client_baseline_vnc as vnc

qa = vnc.qa
PROBE = qa.ROOT / ".artifacts/client-baseline/rfb-compare"
STEPS = (
    {"Name": "applemenu", "Click": True, "X": 20, "Y": 12},
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


def guest_screen(run, output, deadline):
    def execute(*args):
        vnc.connection(run)
        _, state = qa.load(run)
        return qa.command(
            ["tart", "exec", state["vm"], *args], timeout=remaining(deadline)
        )

    # Fixed guest-only paths/commands; no host clipboard or operator data.
    command = (
        "/usr/sbin/screencapture -x /tmp/ft-rfb.png && "
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


def observe(run, image):
    # Explicit driver-owned run; active-console.json is never read or written.
    vnc.connection(run)
    if image != run / "console-first-frame.png" or not image.is_file():
        raise ValueError("Fresh driver first frame is required")
    deadline = time.monotonic() + 240
    root = run / "atomic-comparison"
    root.mkdir(mode=0o700)
    guest_screen(run, root, deadline)
    for step in STEPS:
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
            guest_screen(run, output, deadline)


def compare(archive, runtime):
    verify_probe()
    owned = []

    def observer(run, image):
        owned.append(run)
        observe(run, image)

    try:
        return vnc.serve(archive, runtime, 480, observer=observer)
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
    args = parser.parse_args()

    def interrupted(_signum, _frame):
        raise InterruptedError("Atomic comparison interrupted")

    signal.signal(signal.SIGTERM, interrupted)
    compare(args.archive, args.runtime_archive)
    print("Atomic comparison finished; inspect private images and logs.")


if __name__ == "__main__":
    sys.exit(main())
