#!/usr/bin/env python3
"""Guest-only CF-1 console: authenticated loopback RFB and a bounded QA queue.

Use serve to prepare/boot/capture an owned guest in one action. Request files
contain only virtual guest input. No host command, clipboard or arbitrary
network endpoint is accepted. Screenshots and connection data remain private.
The host's existing cryptography package is used only for VNC authentication.
"""

import argparse
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import re
import signal
import socket
import struct
import subprocess
import sys
import time
from urllib.parse import unquote, urlsplit
import zlib

SPEC = importlib.util.spec_from_file_location(
    "baseline_qa", Path(__file__).with_name("qualify-client-baseline.py")
)
qa = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(qa)
MAX_PIXELS = 4096 * 4096


def connection(run):
    run, state = qa.load(run)
    if (
        not state["created"]
        or state["phase"] != "guest-ready"
        or state.get("consoleMode") != "native-vnc"
        or qa.vm_config(state["vm"]) != state["ownedConfig"]
    ):
        raise ValueError("An active owned native-VNC guest is required")
    match = re.search(r"vnc://[^\s\x1b]+", (run / "vm.log").read_text())
    if not match:
        raise ValueError("Private VNC connection is not ready")
    endpoint = urlsplit(match.group())
    if endpoint.hostname != "127.0.0.1" or not endpoint.password or not endpoint.port:
        raise ValueError(
            "Only the owned guest's authenticated loopback endpoint is allowed"
        )
    return endpoint


def bounds(width, height):
    if not 0 < width <= 4096 or not 0 < height <= 4096 or width * height > MAX_PIXELS:
        raise ValueError("Framebuffer exceeds the QA budget")


class Console:
    def __init__(self, run):
        endpoint = connection(run)
        self.socket = socket.create_connection(("127.0.0.1", endpoint.port), timeout=10)
        self.socket.settimeout(15)
        try:
            self.protocol = self.read(12)
            if not self.protocol.startswith(b"RFB 003."):
                raise ValueError("Unexpected RFB protocol")
            self.send(b"RFB 003.008\n")
            types = self.read(self.read(1)[0])
            if 2 not in types:
                raise ValueError("Password authentication is required")
            self.send(b"\x02")
            challenge = self.read(16)
            # Imported lazily: pure ownership/frame/queue guards need no crypto dependency.
            from cryptography.hazmat.primitives.ciphers import Cipher, modes
            from cryptography.hazmat.decrepit.ciphers.algorithms import TripleDES

            password = unquote(endpoint.password).encode()[:8].ljust(8, b"\0")
            key = bytes(int(f"{byte:08b}"[::-1], 2) for byte in password)
            encrypt = Cipher(TripleDES(key * 3), modes.ECB()).encryptor()
            self.send(encrypt.update(challenge) + encrypt.finalize())
            if struct.unpack(">I", self.read(4))[0]:
                raise ValueError("VNC authentication failed")
            self.send(b"\x01")
            self.width, self.height = struct.unpack(">HH", self.read(4))
            bounds(self.width, self.height)
            self.read(16)
            length = struct.unpack(">I", self.read(4))[0]
            if length > 4096:
                raise ValueError("VNC name exceeds the QA budget")
            self.read(length)
            self.send(
                b"\x00\x00\x00\x00"
                + struct.pack(">BBBBHHHBBBxxx", 32, 24, 0, 1, 255, 255, 255, 16, 8, 0)
            )
            self.send(struct.pack(">BBHii", 2, 0, 2, 0, -223))
        except BaseException:
            self.socket.close()
            raise

    def read(self, size):
        if not 0 <= size <= MAX_PIXELS * 4:
            raise ValueError("VNC message exceeds the QA budget")
        output = bytearray()
        while len(output) < size:
            chunk = self.socket.recv(size - len(output))
            if not chunk:
                raise EOFError("Guest console closed")
            output.extend(chunk)
        return bytes(output)

    def send(self, data):
        self.socket.sendall(data)

    def key(self, key, down):
        self.send(struct.pack(">BBHI", 4, int(down), 0, key))

    def action(self, request):
        action = request["action"]
        if action == "text":
            for char in request["text"]:
                self.key(ord(char), True)
                self.key(ord(char), False)
        elif action == "chord":
            for key in request["keys"]:
                self.key(key, True)
            for key in reversed(request["keys"]):
                self.key(key, False)
        elif action in ("click", "double-click"):
            x, y = request["x"], request["y"]
            if not 0 <= x < self.width or not 0 <= y < self.height:
                raise ValueError("Pointer outside guest framebuffer")
            for _ in range(2 if action == "double-click" else 1):
                self.send(struct.pack(">BBHH", 5, 1, x, y))
                self.send(struct.pack(">BBHH", 5, 0, x, y))
                time.sleep(0.08)
        elif action != "capture":
            raise ValueError("Unknown guest console action")
        time.sleep(request.get("delay", 0.5))

    def capture(self, path):
        self.send(struct.pack(">BBHHHH", 3, 0, 0, 0, self.width, self.height))
        frame = bytearray(self.width * self.height * 4)
        for _ in range(100):
            kind = self.read(1)[0]
            if kind == 0:
                got_pixels = False
                _, count = struct.unpack(">BH", self.read(3))
                if count > 10000:
                    raise ValueError("Too many framebuffer rectangles")
                for _ in range(count):
                    x, y, width, height, encoding = struct.unpack(
                        ">HHHHi", self.read(12)
                    )
                    if encoding == -223:
                        bounds(width, height)
                        self.width, self.height = width, height
                        frame = bytearray(width * height * 4)
                        got_pixels = False
                        continue
                    if (
                        encoding != 0
                        or x + width > self.width
                        or y + height > self.height
                    ):
                        raise ValueError("Unexpected framebuffer rectangle")
                    data = self.read(width * height * 4)
                    got_pixels = got_pixels or bool(data)
                    for row in range(height):
                        start = ((y + row) * self.width + x) * 4
                        frame[start : start + width * 4] = data[
                            row * width * 4 : (row + 1) * width * 4
                        ]
                if got_pixels:
                    break
                self.send(struct.pack(">BBHHHH", 3, 0, 0, 0, self.width, self.height))
                continue
            if kind == 2:
                continue
            if kind == 3:
                self.read(3)
                length = struct.unpack(">I", self.read(4))[0]
                if length > 65536:
                    raise ValueError("Guest clipboard message exceeds budget")
                self.read(length)  # Discard; never read or write the host clipboard.
            else:
                raise ValueError("Unexpected VNC server message")
        else:
            raise ValueError("No framebuffer update within budget")
        raw = bytearray()
        for y in range(self.height):
            raw.append(0)
            row = frame[y * self.width * 4 : (y + 1) * self.width * 4]
            rgb = bytearray(self.width * 3)
            rgb[0::3], rgb[1::3], rgb[2::3] = row[2::4], row[1::4], row[0::4]
            raw.extend(rgb)

        def chunk(kind, data):
            return (
                struct.pack(">I", len(data))
                + kind
                + data
                + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)
            )

        path.write_bytes(
            b"\x89PNG\r\n\x1a\n"
            + chunk(
                b"IHDR", struct.pack(">IIBBBBB", self.width, self.height, 8, 2, 0, 0, 0)
            )
            + chunk(b"IDAT", zlib.compress(raw, 6))
            + chunk(b"IEND", b"")
        )


def validate(request):
    action = request.get("action")
    allowed = {
        "capture": set(),
        "finish": set(),
        "text": {"text"},
        "chord": {"keys"},
        "click": {"x", "y"},
        "double-click": {"x", "y"},
    }
    if action not in allowed or set(request) - {"action", "delay"} != allowed[action]:
        raise ValueError("Only bounded guest input actions are accepted")
    if (
        not isinstance(request.get("delay", 0.5), (float, int))
        or not 0 <= request.get("delay", 0.5) <= 15
    ):
        raise ValueError("Action delay exceeds budget")
    if action == "text" and (
        not isinstance(request["text"], str)
        or len(request["text"]) > 8192
        or any(not 32 <= ord(c) <= 126 for c in request["text"])
    ):
        raise ValueError("Text must be bounded printable ASCII")
    if action == "chord" and (
        not isinstance(request["keys"], list)
        or not 1 <= len(request["keys"]) <= 8
        or any(type(k) is not int or not 0 <= k <= 0x10FFFF for k in request["keys"])
    ):
        raise ValueError("Invalid key chord")
    if action in ("click", "double-click") and any(
        type(request[k]) is not int for k in ("x", "y")
    ):
        raise ValueError("Pointer coordinates must be integers")


def perform(run, request, output):
    validate(request)
    console = Console(run)
    try:
        console.action(request)
        console.capture(output)
    finally:
        console.socket.close()


def serve(archive, runtime, hold):
    run = qa.prepare(archive, runtime)
    child = None
    failure = None
    console = None
    try:
        with (run / "controller.log").open("wb") as log:
            child = subprocess.Popen(
                [
                    sys.executable,
                    str(Path(__file__).with_name("qualify-client-baseline.py")),
                    "guest",
                    "--run",
                    str(run),
                    "--vnc",
                    "--hold-seconds",
                    str(hold),
                ],
                stdin=subprocess.DEVNULL,
                stdout=log,
                stderr=subprocess.STDOUT,
            )
            deadline = time.monotonic() + 200
            while time.monotonic() < deadline:
                if child.poll() is not None:
                    raise RuntimeError(
                        "Guest controller exited; inspect private controller.log"
                    )
                if (
                    json.loads((run / "state.json").read_text())["phase"]
                    == "guest-ready"
                ):
                    break
                time.sleep(1)
            else:
                raise TimeoutError("Guest boot exceeded budget")
            image = run / "console-first-frame.png"
            console = Console(run)
            console.capture(image)
            qa.save(
                qa.ROOT / ".artifacts/client-baseline/active-console.json",
                {"run": str(run), "image": str(image)},
            )
            print(json.dumps({"run": str(run), "firstFrame": str(image)}), flush=True)
            while child.poll() is None:
                for path in sorted(run.glob("request-*.json")):
                    identifier = path.stem.removeprefix("request-")
                    if (
                        not re.fullmatch(r"[0-9a-f]{12}", identifier)
                        or path.is_symlink()
                        or path.stat().st_size > 16384
                    ):
                        raise ValueError("Invalid console queue path or size")
                    response_path = run / ("response-" + identifier + ".json")
                    if response_path.exists():
                        continue
                    try:
                        request = json.loads(path.read_text())
                        validate(request)
                        if request["action"] == "finish":
                            (run / "finish-session").touch()
                            qa.save(response_path, {"ok": True, "finished": True})
                            return run
                        image = run / ("console-" + identifier + ".png")
                        connection(run)  # Revalidate ownership before every input.
                        console.action(request)
                        console.capture(image)
                        qa.save(
                            response_path,
                            {
                                "ok": True,
                                "image": str(image),
                                "utc": datetime.now(timezone.utc).isoformat(),
                            },
                        )
                    except Exception as error:
                        qa.save(
                            response_path,
                            {
                                "ok": False,
                                "error": type(error).__name__ + ": " + str(error),
                            },
                        )
                time.sleep(0.2)
    except BaseException as error:
        failure = type(error).__name__ + ": " + str(error)
        raise
    finally:
        if console:
            console.socket.close()
        (run / "finish-session").touch()
        if child:
            try:
                child.wait(timeout=60)
            except subprocess.TimeoutExpired:
                child.terminate()
                child.wait(timeout=60)
        if failure:
            _, state = qa.load(run)
            state["events"].append({"phase": "native-vnc-console", "error": failure})
            qa.save(run / "state.json", state)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--archive", type=Path, required=True)
    parser.add_argument("--runtime-archive", type=Path, required=True)
    parser.add_argument(
        "--hold-seconds",
        type=int,
        default=1800,
        choices=range(1, 1801),
        metavar="1..1800",
    )
    args = parser.parse_args()

    def interrupted(_signum, _frame):
        raise InterruptedError("Console session interrupted")

    signal.signal(signal.SIGTERM, interrupted)
    serve(args.archive, args.runtime_archive, args.hold_seconds)


if __name__ == "__main__":
    main()
