#!/usr/bin/env python3
"""Digest-selected, keyless Pi startup qualification. Zero means isolation passed.

The known-bad --baseline-archive comparison deliberately omits the unsupported
policy selector; its failure must be actual isolation behavior, not flag parsing.
"""
import argparse
import concurrent.futures
import fcntl
import json
import os
from pathlib import Path
import queue
import signal
import struct
import subprocess
import tarfile
import tempfile
import termios
import threading

from pi_startup_fixture import SENTINEL, Fixture, NetworkCanary, digest, reap, records, snapshot, wait_until

BASELINE_ARCHIVE = "e0e0331d478af7724fa24d780a8d4abc0033c6f5ee7c0d45dddae6f922e5c4cb"
BASELINE_BINARY = "811d65137d4272b30a6b2bd9a4093218b8f94bb8f55a9db9ccf8d443c4d4b2a5"


def rpc_case(binary, fixture, baseline):
    messages, errors, checks = queue.Queue(), [], {}
    child = subprocess.Popen(fixture.arguments(binary, baseline), cwd=fixture.project, env=fixture.env,
        stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, start_new_session=True)
    def read_stdout():
        for line in child.stdout:
            try:
                messages.put(json.loads(line))
            except ValueError:
                messages.put({"nonJson": line[:200]})
        messages.put({"eof": True})
    def read_stderr():
        for line in child.stderr:
            if len(errors) < 30:
                errors.append(line[:300].strip())
    readers = [threading.Thread(target=read_stdout, daemon=True), threading.Thread(target=read_stderr, daemon=True)]
    for reader in readers:
        reader.start()
    def wait_for(predicate):
        import time
        deadline = time.monotonic() + 20
        while time.monotonic() < deadline:
            value = messages.get(timeout=max(0.001, deadline - time.monotonic()))
            if predicate(value):
                return value
            if value.get("eof"):
                raise RuntimeError("child exited before expected response")
        raise TimeoutError("response deadline")
    try:
        wait_for(lambda value: value.get("type") == "hello")
        checks["hello"] = True
        commands = [(True, {"type": "get_state"})]
        if not baseline:
            commands += [(False, {"type": "switch_session", "sessionPath": "/synthetic/must-not-read.jsonl"}),
                (False, {"type": "new_session", "parentSession": "/synthetic/parent.jsonl"}),
                (False, {"type": "fork", "entryId": "missing"})]
        commands += [(True, {"type": "new_session"}),
            (True, {"type": "prompt", "message": "/ft-probe-reload"}),
            (True, {"type": "abort"}), (True, {"type": "get_state"})]
        for number, (expected, command) in enumerate(commands):
            command["id"] = f"probe-{number}"
            child.stdin.write(json.dumps(command) + "\n")
            child.stdin.flush()
            response = wait_for(lambda value: value.get("id") == command["id"])
            checks[f"rpc-{number}-{command['type']}"] = response.get("success") is expected
    except (RuntimeError, TimeoutError, queue.Empty, BrokenPipeError) as error:
        checks["lifecycleCompleted"] = False
        errors.append(type(error).__name__ + ": " + str(error))
    finally:
        child.stdin.close()
        reap(child)
        for reader in readers:
            reader.join(timeout=2)
        child.stdout.close()
        child.stderr.close()
    checks.update(fixture.checks(), cleanExit=child.returncode == 0)
    return {"case": fixture.root.name, "transport": "direct-rpc", "checks": checks,
        "passed": all(checks.values()), "exitCode": child.returncode, "diagnostics": errors}


def pty_case(binary, fixture):
    checks, errors, output = {}, [], bytearray()
    master, slave = os.openpty()
    fcntl.ioctl(slave, termios.TIOCSWINSZ, struct.pack("HHHH", 40, 120, 0, 0))
    child = subprocess.Popen(fixture.arguments(binary, rpc=False), cwd=fixture.project, env=fixture.env,
        stdin=slave, stdout=slave, stderr=slave, start_new_session=True)
    os.close(slave)
    def drain():
        try:
            while chunk := os.read(master, 65536):
                if len(output) < 262144:
                    output.extend(chunk)
        except OSError:
            pass
    reader = threading.Thread(target=drain, daemon=True)
    reader.start()
    def count(kind):
        return sum(item["kind"] == kind for item in records(fixture.observed))
    try:
        wait_until(lambda: count("start") >= 1, child)
        checks["interactiveStartup"] = True
        # Each command is confirmed by an external observation before the next.
        os.write(master, b"/ft-probe-new\r")
        wait_until(lambda: count("load") == 2, child)
        wait_until(lambda: any(item.get("reason") == "new" for item in records(fixture.observed)), child)
        os.write(master, b"/ft-probe-reload\r")
        wait_until(lambda: count("load") == 3, child)
        wait_until(lambda: any(item.get("reason") == "reload" for item in records(fixture.observed)), child)
        # Built-in unsupported commands are nonfatal; then ask the selected
        # extension to confirm command processing remains usable.
        for command in (b"/login\r", b"/trust\r", b"/resume\r", b"/import /synthetic/missing.jsonl\r", b"/bug\r"):
            os.write(master, command)
            # TUI submissions are processed asynchronously.
            previous = count("ready")
            os.write(master, b"/ft-probe-ready\r")
            wait_until(lambda: count("ready") > previous, child)
        checks["unsupportedCommandsNonfatal"] = True
        os.write(master, b"/quit\r")
        reap(child)
    except (RuntimeError, TimeoutError, OSError) as error:
        checks["lifecycleCompleted"] = False
        errors.append(type(error).__name__ + ": " + str(error))
    finally:
        if child.poll() is None:
            os.killpg(child.pid, signal.SIGTERM)
            reap(child)
        reader.join(timeout=2)
        os.close(master)
    checks.update(fixture.checks(), cleanExit=child.returncode == 0)
    checks["bugReportRejected"] = b"managed-v1 does not support bug reports" in output
    checks["noAmbientCrashNotice"] = SENTINEL.encode() not in output
    if not all(checks.values()):
        errors.append(output[-6000:].decode(errors="replace"))
    return {"case": fixture.root.name, "transport": "direct-pty", "checks": checks,
        "passed": all(checks.values()), "exitCode": child.returncode, "diagnostics": errors}


def rejection_cases(binary, root):
    fixture = Fixture.create(root)
    args = fixture.arguments(binary)
    before = snapshot(root)
    changes = [["install", "npm:synthetic"], ["config"], ["auth", "login"],
        ["mcp", "list"], ["mcp", "login", "synthetic"], ["--extension", "builtin:mcp"],
        ["--resume", "synthetic"], ["--session", "/synthetic/missing.jsonl"],
        ["--startup-policy", "managed-v1"], ["--extension", "npm:synthetic"], ["--provider", "synthetic"]]
    results = []
    for extra in changes:
        child = subprocess.run(args + extra, cwd=fixture.project, env=fixture.env,
            capture_output=True, timeout=15)
        checks = {"boundedFailure": child.returncode != 0,
            "noStartupWrites": snapshot(root) == before}
        results.append({"case": "reject-" + extra[0], "arguments": extra, "transport": "direct-cli",
            "checks": checks, "passed": all(checks.values())})
    unknown = args.copy()
    unknown[2] = "managed-v999"
    result = subprocess.run(unknown, cwd=fixture.project, env=fixture.env, capture_output=True, timeout=15)
    checks = {"boundedFailure": result.returncode != 0, "noStartupWrites": snapshot(root) == before}
    results.append({"case": "unknown-policy", "transport": "direct-cli", "checks": checks, "passed": all(checks.values())})
    return results


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    source = parser.add_mutually_exclusive_group(required=True)
    source.add_argument("--binary", type=Path)
    source.add_argument("--baseline-archive", type=Path)
    parser.add_argument("--sha256", help="independently selected executable digest")
    parser.add_argument("--source-revision", help="recorded candidate source identity")
    parser.add_argument("--report", type=Path)
    parser.add_argument("--rpc-only", action="store_true", help="focused development probe; not full qualification")
    args = parser.parse_args()
    with tempfile.TemporaryDirectory(prefix="ft-pi-qualify-") as scratch, NetworkCanary() as network:
        root = Path(scratch).resolve()
        baseline = args.baseline_archive is not None
        if baseline:
            if digest(args.baseline_archive) != BASELINE_ARCHIVE:
                parser.error("baseline archive digest mismatch")
            with tarfile.open(args.baseline_archive) as archive:
                archive.extractall(root / "runtime", filter="data")
            binary, expected = root / "runtime/pi/pi", BASELINE_BINARY
        else:
            if not args.sha256 or not args.source_revision:
                parser.error("candidate requires --sha256 and --source-revision")
            binary, expected = args.binary.resolve(), args.sha256
        if digest(binary) != expected:
            parser.error("executable digest mismatch")
        fixtures = [Fixture.create(root / name, poison=poison, proxy=network.url)
            for name, poison in (("migration", False), ("poisoned", True))]
        if not baseline:
            fixtures.append(Fixture.create(root / "linked-project", poison=True, linked=True, proxy=network.url))
            fixtures.append(Fixture.create(root / "current-layout-resources", legacy=False, proxy=network.url))
        with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
            cases = list(pool.map(lambda fixture: rpc_case(binary, fixture, baseline), fixtures))
        if not baseline and not args.rpc_only:
            cases.append(pty_case(binary, Fixture.create(root / "interactive", poison=True, proxy=network.url)))
            cases += rejection_cases(binary, root / "rejections")
        cases.append({"case": "controlled-network-endpoint", "checks": {"noAttempts": not network.attempts},
            "passed": not network.attempts, "attempts": network.attempts})
        report = {"kind": "known-bad-baseline" if baseline else "candidate", "executableSha256": expected,
            "sourceRevision": "e7d09c9dc5fca409f46a5476669e3c0404054d69" if baseline else args.source_revision,
            "scope": "rpc-only" if args.rpc_only else "direct-rpc-pty-cli", "cases": cases,
            "passed": all(case["passed"] for case in cases)}
        output = json.dumps(report, indent=2) + "\n"
        if args.report:
            args.report.parent.mkdir(parents=True, exist_ok=True)
            args.report.write_text(output)
        print(output, end="")
        raise SystemExit(0 if report["passed"] else 1)


if __name__ == "__main__":
    main()
