"""Atomic diagnostic regressions: fresh selection, deadlines and cleanup."""

import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import Mock, patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
SPEC = importlib.util.spec_from_file_location(
    "baseline_compare",
    Path(__file__).resolve().parents[1] / "client_baseline_compare.py",
)
compare = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(compare)
vnc = compare.vnc


class AtomicGuards(unittest.TestCase):
    def test_expired_deadline_starts_no_command_and_timeout_is_enforced(self):
        with self.assertRaises(TimeoutError):
            compare.remaining(0)
        with self.assertRaises(subprocess.TimeoutExpired):
            compare.qa.command(
                [sys.executable, "-c", "import time; time.sleep(10)"], timeout=0.05
            )

    def test_unowned_run_is_refused_before_any_observation(self):
        with tempfile.TemporaryDirectory() as directory:
            run = Path(directory)
            with (
                patch.object(vnc, "connection", side_effect=ValueError("unowned")),
                self.assertRaises(ValueError),
            ):
                compare.observe(run, run / "console-first-frame.png")
            self.assertFalse((run / "atomic-comparison").exists())

    def test_probe_timeout_gets_immediate_screen_before_next_input(self):
        with tempfile.TemporaryDirectory() as directory:
            run = Path(directory)
            image = run / "console-first-frame.png"
            image.write_bytes(b"fresh frame")
            # A cleaned pointer is present but cannot select the run or be changed.
            pointer = run / "active-console.json"
            pointer.write_text('{"run":"cleaned-old-run"}')
            events = []

            def probe(*args, **kwargs):
                events.append("probe")
                raise subprocess.TimeoutExpired(args[0], kwargs["timeout"])

            def screen(run_arg, output, deadline):
                self.assertEqual(run_arg, run)
                events.append(output.name)

            with (
                patch.object(
                    vnc,
                    "connection",
                    return_value=SimpleNamespace(port=5900, password="synthetic"),
                ),
                patch.object(compare, "guest_screen", side_effect=screen),
                patch.object(compare.subprocess, "run", side_effect=probe),
            ):
                compare.observe(run, image)
            self.assertEqual(
                events,
                [
                    "atomic-comparison",
                    "probe",
                    "applemenu",
                    "probe",
                    "escape",
                    "probe",
                    "lowerg",
                ],
            )
            self.assertEqual(pointer.read_text(), '{"run":"cleaned-old-run"}')
            self.assertTrue(
                json.loads(
                    (run / "atomic-comparison/applemenu/process.json").read_text()
                )["timedOut"]
            )

    def test_driver_observer_failure_finishes_exact_fresh_run_without_pointer(self):
        with tempfile.TemporaryDirectory() as directory:
            run = Path(directory)
            (run / "state.json").write_text('{"phase":"guest-ready"}')
            child = Mock()
            child.poll.return_value = None
            console = Mock()
            seen = []

            def observer(actual, image):
                seen.append((actual, image))
                raise TimeoutError("observation expired")

            with (
                patch.object(vnc.qa, "prepare", return_value=run),
                patch.object(vnc.subprocess, "Popen", return_value=child),
                patch.object(vnc, "Console", return_value=console),
                patch.object(vnc.qa, "load", return_value=(run, {"events": []})),
                self.assertRaises(TimeoutError),
            ):
                vnc.serve(Path("archive"), Path("runtime"), 480, observer=observer)
            self.assertEqual(seen, [(run, run / "console-first-frame.png")])
            self.assertTrue((run / "finish-session").exists())
            child.wait.assert_called_once_with(timeout=60)
            console.socket.close.assert_called_once()

    def test_independent_cleanup_readback_runs_after_observer_failure(self):
        with tempfile.TemporaryDirectory() as directory:
            run = Path(directory)
            state = {
                "phase": "cleaned",
                "cleanupVerified": True,
                "vm": "owned",
                "originalVms": ["original"],
            }

            def serve(*args, observer):
                observer(run, run / "console-first-frame.png")

            for verified in (True, False):
                state["cleanupVerified"] = verified
                with (
                    patch.object(compare, "verify_probe"),
                    patch.object(vnc, "serve", side_effect=serve),
                    patch.object(
                        compare, "observe", side_effect=TimeoutError("expired")
                    ),
                    patch.object(compare.qa, "load", return_value=(run, state)),
                    patch.object(
                        compare.qa,
                        "command",
                        return_value=json.dumps(
                            [{"Name": "original", "Running": False}]
                        ).encode(),
                    ),
                    self.assertRaises(TimeoutError if verified else RuntimeError),
                ):
                    compare.compare(Path("archive"), Path("runtime"))
            self.assertTrue((run / "atomic-cleanup.json").is_file())


if __name__ == "__main__":
    unittest.main()
