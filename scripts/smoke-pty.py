#!/usr/bin/env python3
"""
Drive fingertrap-sidecar over stdio with JSON-RPC, run a shell, send a
known-good command, and report whether the shell's output came back as
pty/output notifications. Then spawn a REAL pi pane the way the app does
(kind="pi", resolver chain and all) and prove it produces output and dies
cleanly on pty/kill — the kept real-entry-path smoke (ADR-0027 P10, #142).
The pi case self-skips when no pi executable is resolvable, so keyless
machines and CI stay green.

Used to isolate "no keystroke echo" symptoms: the Tauri transport is bypassed
entirely, so a passing run proves the sidecar PTY works and points the bug at
Tauri-side wiring; a failing run keeps the bug inside the sidecar.

Side effect of the pi case: real pi creates a session file under
~/.pi/agent/sessions for the throwaway temp cwd. Cheap, bounded, and exactly
what "test the real entry path" costs.
"""

from __future__ import annotations

import base64
import json
import os
import selectors
import shutil
import subprocess
import sys
import tempfile
import time
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
SIDECAR = REPO_ROOT / "src-tauri" / "binaries" / "fingertrap-sidecar-aarch64-apple-darwin"


def encode(msg: dict) -> bytes:
    body = json.dumps(msg).encode("utf-8")
    return f"Content-Length: {len(body)}\r\n\r\n".encode("ascii") + body


def read_message(buf: bytearray) -> tuple[dict | None, bytearray]:
    sep = buf.find(b"\r\n\r\n")
    if sep == -1:
        return None, buf
    header = buf[:sep].decode("ascii", errors="replace")
    length = None
    for line in header.split("\r\n"):
        if line.lower().startswith("content-length:"):
            length = int(line.split(":", 1)[1].strip())
            break
    if length is None:
        raise ValueError(f"no Content-Length in header: {header!r}")
    total = sep + 4 + length
    if len(buf) < total:
        return None, buf
    body = bytes(buf[sep + 4 : total]).decode("utf-8")
    return json.loads(body), buf[total:]


def smoke_pi(proc, pump, outputs_by_session, exited_sessions, responses) -> int:
    """The real-entry-path pi case (ADR-0027 P10, #142): spawn kind="pi"
    through the sidecar's real resolver chain, require output, kill, and
    require the exit notification. Self-skips when pi is not resolvable."""
    if shutil.which("pi") is None and not os.environ.get("FINGERTRAP_PI"):
        print("SKIP  [pi-spawn] no pi executable resolvable (PATH/FINGERTRAP_PI); pi case skipped")
        return 0

    # A fresh non-git temp cwd: real pi in a git repo engages pi_config's
    # worktree extension (a worktree left behind per run is not smoke litter
    # we want).
    pi_cwd = tempfile.mkdtemp(prefix="ft-smoke-pi-")
    try:
        spawn = {
            "jsonrpc": "2.0",
            "id": 3,
            "method": "pty/spawn",
            "params": [
                {
                    "sessionId": "smoke-pi",
                    "shell": None,
                    "cwd": pi_cwd,
                    "cols": 80,
                    "rows": 24,
                    "env": None,
                    "kind": "pi",
                }
            ],
        }
        proc.stdin.write(encode(spawn))
        proc.stdin.flush()
        deadline = time.monotonic() + 10.0
        while time.monotonic() < deadline and 3 not in responses:
            pump(time.monotonic() + 0.5)
        resp = responses.get(3)
        if resp is None:
            print("ERROR [pi-spawn] no response within 10s", file=sys.stderr)
            return 1
        if "error" in resp:
            print(f"ERROR [pi-spawn] spawn failed: {resp['error']}", file=sys.stderr)
            return 1
        print(f"OK    [pi-spawn] pid={resp['result'].get('pid')}")

        # Real pi draws a TUI; ANY pty/output for this session proves the
        # child is alive and wired to the slave. Verified from the wire, not
        # from pi's own logging.
        deadline = time.monotonic() + 15.0
        while time.monotonic() < deadline and not outputs_by_session.get("smoke-pi"):
            pump(time.monotonic() + 0.5)
        if not outputs_by_session.get("smoke-pi"):
            print("ERROR [pi-output] no pty/output from the pi pane within 15s", file=sys.stderr)
            return 1
        print("OK    [pi-output] pi pane produced output")

        kill = {
            "jsonrpc": "2.0",
            "id": 4,
            "method": "pty/kill",
            "params": [{"sessionId": "smoke-pi"}],
        }
        proc.stdin.write(encode(kill))
        proc.stdin.flush()
        deadline = time.monotonic() + 8.0
        while time.monotonic() < deadline and "smoke-pi" not in exited_sessions:
            pump(time.monotonic() + 0.5)
        if "smoke-pi" not in exited_sessions:
            print("ERROR [pi-kill] no pty/exit for the pi pane within 8s", file=sys.stderr)
            return 1
        print("OK    [pi-kill] pi pane killed; pty/exit received")
        return 0
    finally:
        shutil.rmtree(pi_cwd, ignore_errors=True)


def main() -> int:
    if not SIDECAR.exists():
        print(f"ERROR sidecar not found at {SIDECAR}", file=sys.stderr)
        return 2

    print(f"INFO  spawning {SIDECAR}")
    proc = subprocess.Popen(
        [str(SIDECAR)],
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        bufsize=0,
    )

    # Async-friendly select on stdout + stderr.
    sel = selectors.DefaultSelector()
    sel.register(proc.stdout, selectors.EVENT_READ, "stdout")
    sel.register(proc.stderr, selectors.EVENT_READ, "stderr")
    os.set_blocking(proc.stdout.fileno(), False)
    os.set_blocking(proc.stderr.fileno(), False)

    buf_out = bytearray()
    output_chunks: list[bytes] = []
    outputs_by_session: dict[str, list[bytes]] = {}
    exited_sessions: set[str] = set()
    responses: dict[int, dict] = {}
    stderr_lines: list[str] = []
    seen_spawn_response = False
    pid_reported: int | None = None

    def pump(deadline: float):
        nonlocal seen_spawn_response, pid_reported
        while time.monotonic() < deadline:
            timeout = max(0.0, deadline - time.monotonic())
            for key, _ in sel.select(timeout=timeout):
                if key.data == "stdout":
                    chunk = proc.stdout.read(65536)
                    if chunk:
                        buf_out.extend(chunk)
                elif key.data == "stderr":
                    chunk = proc.stderr.read(65536)
                    if chunk:
                        text = chunk.decode("utf-8", errors="replace")
                        for line in text.splitlines():
                            stderr_lines.append(line)
                            print(f"      [sidecar stderr] {line}")
            # Drain framed messages
            while True:
                msg, rest = read_message(buf_out)
                if msg is None:
                    break
                buf_out.clear()
                buf_out.extend(rest)
                if msg.get("method") == "pty/output":
                    raw = msg.get("params")
                    p = raw[0] if isinstance(raw, list) and raw else (raw or {})
                    data = base64.b64decode(p.get("dataBase64", ""))
                    output_chunks.append(data)
                    outputs_by_session.setdefault(p.get("sessionId", "?"), []).append(data)
                    print(f"      [pty/output] {data[:120]!r}")
                elif msg.get("method") == "pty/exit":
                    raw = msg.get("params")
                    p = raw[0] if isinstance(raw, list) and raw else (raw or {})
                    exited_sessions.add(p.get("sessionId", "?"))
                    print(f"      [pty/exit] {msg.get('params')}")
                elif isinstance(msg.get("id"), int):
                    responses[msg["id"]] = msg
                    if msg.get("id") == 1 and "result" in msg:
                        seen_spawn_response = True
                        pid_reported = msg["result"].get("pid")
                        print(f"OK    [spawn] pid={pid_reported}")
                    elif msg.get("id") == 2:
                        if "error" in msg:
                            print(f"ERROR [write] {msg['error']}", file=sys.stderr)
                        else:
                            print(f"OK    [write] response={msg.get('result')}")
                    else:
                        print(f"      [rpc] {msg}")
                else:
                    print(f"      [rpc] {msg}")

    try:
        # 1) pty/spawn
        spawn = {
            "jsonrpc": "2.0",
            "id": 1,
            "method": "pty/spawn",
            "params": [
                {
                    "sessionId": "smoke-1",
                    "shell": None,
                    "cwd": None,
                    "cols": 80,
                    "rows": 24,
                    "env": None,
                    # Pinned: this case tests shell echo; the host default
                    # (settings/env-dependent) must not decide what spawns.
                    "kind": "shell",
                }
            ],
        }
        proc.stdin.write(encode(spawn))
        proc.stdin.flush()
        pump(time.monotonic() + 8.0)
        if not seen_spawn_response:
            print("ERROR [spawn] no response within 8s", file=sys.stderr)
            return 1

        # Give the shell a moment to print its prompt before we write.
        pump(time.monotonic() + 1.0)

        # 2) pty/write — send "echo finger-trap-marker\n"
        marker = "finger-trap-marker"
        payload = f"echo {marker}\n".encode()
        write_req = {
            "jsonrpc": "2.0",
            "id": 2,
            "method": "pty/write",
            "params": [
                {
                    "sessionId": "smoke-1",
                    "dataBase64": base64.b64encode(payload).decode("ascii"),
                }
            ],
        }
        proc.stdin.write(encode(write_req))
        proc.stdin.flush()
        pump(time.monotonic() + 3.0)

        # Final drain
        pump(time.monotonic() + 1.0)

        all_output = b"".join(output_chunks)
        text = all_output.decode("utf-8", errors="replace")
        if marker in text and text.count(marker) >= 2:
            # echo prints the marker itself, plus the shell echoes the
            # command line — we expect to see it at least twice.
            print("OK    [echo] shell output contains marker")
            return smoke_pi(proc, pump, outputs_by_session, exited_sessions, responses)
        elif marker in text:
            print("WARN  [echo] marker present but shell echo of command line missing")
            print("      (this can happen if ECHO is disabled on the slave; investigate stty)")
            return smoke_pi(proc, pump, outputs_by_session, exited_sessions, responses)
        elif not all_output:
            print(
                "ERROR [echo] no pty/output notifications received at all",
                file=sys.stderr,
            )
            print(
                "      The shell process either didn't start or isn't connected to the slave.",
                file=sys.stderr,
            )
            return 1
        else:
            print(f"ERROR [echo] marker not found in output: {text!r}", file=sys.stderr)
            return 1
    finally:
        try:
            proc.terminate()
            proc.wait(timeout=2)
        except subprocess.TimeoutExpired:
            proc.kill()


if __name__ == "__main__":
    sys.exit(main())
