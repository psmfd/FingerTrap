"""Bounds and replay checks for the guest-only native console QA substrate."""

import importlib.util
import json
from pathlib import Path
import struct
import tempfile
import unittest
from unittest.mock import MagicMock, patch

SPEC = importlib.util.spec_from_file_location(
    "baseline_vnc", Path(__file__).resolve().parents[1] / "client_baseline_vnc.py"
)
vnc = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(vnc)


class ReplaySocket:
    def __init__(self, data):
        self.data = data
        self.sent = []

    def recv(self, size):
        result, self.data = self.data[:size], self.data[size:]
        return result

    def sendall(self, data):
        self.sent.append(data)


class ConsoleGuards(unittest.TestCase):
    def test_queue_accepts_virtual_input_and_rejects_host_commands(self):
        for request in (
            {"action": "capture"},
            {"action": "text", "text": "printf QA"},
            {"action": "chord", "keys": [0xFFE7, 111]},
            {"action": "click", "x": 3, "y": 4},
        ):
            vnc.validate(request)
        for request in (
            {"action": "exec", "command": "anything"},
            {"action": "capture", "host": "remote"},
            {"action": "text", "text": "bad\ninput"},
            {"action": "text", "text": "x" * 8193},
            {"action": "chord", "keys": [True]},
            {"action": "capture", "delay": 16},
        ):
            with self.subTest(request=str(request)[:70]), self.assertRaises(ValueError):
                vnc.validate(request)

    def test_cleaned_and_unowned_guests_cannot_be_connected(self):
        for state in (
            {"created": True, "phase": "cleaned"},
            {"created": False, "phase": "guest-ready"},
        ):
            with (
                patch.object(vnc.qa, "load", return_value=(Path("unused"), state)),
                self.assertRaises(ValueError),
            ):
                vnc.connection("unused")

    def test_changed_guest_identity_and_remote_endpoint_are_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            run = Path(temporary)
            state = {
                "created": True,
                "phase": "guest-ready",
                "consoleMode": "native-vnc",
                "vm": "owned",
                "ownedConfig": {"MAC": "ours"},
            }
            (run / "vm.log").write_text(
                "VNC server is running at vnc://:synthetic-password@192.0.2.10:5900\n"
            )
            with patch.object(vnc.qa, "load", return_value=(run, state)):
                with (
                    patch.object(
                        vnc.qa, "vm_config", return_value={"MAC": "different"}
                    ),
                    self.assertRaises(ValueError),
                ):
                    vnc.connection(run)
                with (
                    patch.object(
                        vnc.qa, "vm_config", return_value=state["ownedConfig"]
                    ),
                    self.assertRaises(ValueError),
                ):
                    vnc.connection(run)

    def console(self, wire):
        console = vnc.Console.__new__(vnc.Console)
        console.width, console.height = 1, 1
        console.socket = ReplaySocket(wire)
        return console

    def test_resize_only_update_is_followed_by_a_real_pixel_frame(self):
        resize = (
            b"\0\0" + struct.pack(">H", 1) + struct.pack(">HHHHi", 0, 0, 2, 1, -223)
        )
        pixels = (
            b"\0\0"
            + struct.pack(">H", 1)
            + struct.pack(">HHHHi", 0, 0, 2, 1, 0)
            + bytes([0, 0, 255, 0, 0, 255, 0, 0])
        )
        console = self.console(resize + pixels)
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary) / "frame.png"
            console.capture(output)
            data = output.read_bytes()
            self.assertEqual(data[:8], b"\x89PNG\r\n\x1a\n")
            self.assertEqual(struct.unpack(">II", data[16:24]), (2, 1))
            self.assertEqual(len(console.socket.sent), 2)

    def test_out_of_bounds_frame_and_oversized_resize_never_write_an_image(self):
        for rectangle in (
            struct.pack(">HHHHi", 1, 0, 1, 1, 0),
            struct.pack(">HHHHi", 0, 0, 65535, 65535, -223),
        ):
            console = self.console(b"\0\0" + struct.pack(">H", 1) + rectangle)
            with tempfile.TemporaryDirectory() as temporary:
                output = Path(temporary) / "frame.png"
                with self.assertRaises(ValueError):
                    console.capture(output)
                self.assertFalse(output.exists())

    def test_failed_capture_discards_connection_without_replaying_input(self):
        with tempfile.TemporaryDirectory() as temporary:
            run = Path(temporary)
            (run / "state.json").write_text(json.dumps({"phase": "guest-ready"}))
            for index, request in enumerate(
                (
                    {"action": "text", "text": "probe"},
                    {"action": "capture"},
                    {"action": "finish"},
                )
            ):
                (run / f"request-{index:012x}.json").write_text(json.dumps(request))
            old, fresh = MagicMock(), MagicMock()
            old.capture.side_effect = [None, TimeoutError("partial frame")]
            consoles = iter((old, fresh))

            def connect(_run):
                console = next(consoles)
                if console is fresh:
                    old.socket.close.assert_called_once()
                return console

            def save(path, value):
                # The test never reads or replaces the local active-console pointer.
                if path.parent == run:
                    path.write_text(json.dumps(value))

            with (
                patch.object(vnc.qa, "prepare", return_value=run),
                patch.object(vnc.qa, "save", side_effect=save),
                patch.object(vnc.subprocess, "Popen") as process,
                patch.object(vnc, "Console", side_effect=connect) as factory,
                patch.object(vnc, "connection") as ownership,
            ):
                process.return_value.poll.return_value = None
                vnc.serve(Path("archive"), Path("runtime"), 60)
            self.assertEqual(factory.call_count, 2)
            self.assertEqual(ownership.call_count, 2)
            old.action.assert_called_once_with({"action": "text", "text": "probe"})
            fresh.action.assert_called_once_with({"action": "capture"})
            old.socket.close.assert_called_once()
            fresh.socket.close.assert_called_once()
            self.assertFalse(
                json.loads((run / "response-000000000000.json").read_text())["ok"]
            )
            self.assertTrue(
                json.loads((run / "response-000000000001.json").read_text())["ok"]
            )

    def test_pointer_guard_emits_no_input_outside_frame(self):
        console = self.console(b"")
        with self.assertRaises(ValueError):
            console.action({"action": "click", "x": 100, "y": 100})
        self.assertEqual(console.socket.sent, [])


if __name__ == "__main__":
    unittest.main()
