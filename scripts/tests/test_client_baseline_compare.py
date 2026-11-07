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
    def agent(self, pid=42):
        return {
            "plist": "/Library/LaunchAgents/org.cirruslabs.tart-guest-agent.plist",
            "plistSha256": "a" * 64,
            "target": "gui/501/org.cirruslabs.tart-guest-agent",
            "uid": 501,
            "pid": pid,
            "program": "/opt/homebrew/bin/tart-guest-agent",
            "executable": "/opt/homebrew/bin/tart-guest-agent",
            "programSha256": "b" * 64,
            "executableMatches": True,
            "document": {
                "ProgramArguments": [
                    "/opt/homebrew/bin/tart-guest-agent",
                    "--run-agent",
                ]
            },
        }

    def test_restart_selector_rejects_ambiguous_non_rpc_wrong_uid_or_binary(self):
        good = self.agent()
        for services in (
            [],
            [good, self.agent(43)],
            [dict(good, uid=0)],
            [dict(good, executableMatches=False)],
            [
                dict(
                    good,
                    document={"ProgramArguments": [good["program"], "--run-daemon"]},
                )
            ],
        ):
            with self.subTest(services=services), self.assertRaises(ValueError):
                compare.select_agent({"uid": 501, "services": services})
        self.assertEqual(compare.select_agent({"uid": 501, "services": [good]}), good)
        with self.assertRaises(ValueError):
            compare.permission_step(
                {"action": "restart-agent", "target": "system/other"}, 0
            )

    def test_restart_revalidates_pid_and_binary_before_service_command(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            baseline = {"uid": 501, "services": [self.agent()]}
            (root / "agent-inventory.json").write_text(json.dumps(baseline))
            for index, changed in enumerate(
                (self.agent(43), dict(self.agent(), programSha256="c" * 64))
            ):
                with (
                    patch.object(
                        compare,
                        "agent_inventory",
                        return_value={"uid": 501, "services": [changed]},
                    ),
                    patch.object(
                        compare,
                        "guest_command",
                        side_effect=AssertionError("Restart must not run"),
                    ) as command,
                    self.assertRaises(ValueError),
                ):
                    compare.restart_agent(
                        root,
                        root,
                        {"Name": f"step-{index:02d}"},
                        compare.time.monotonic() + 90,
                    )
                command.assert_not_called()

    def test_restart_transport_loss_requires_changed_pid_before_screen_readback(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            baseline = {"uid": 501, "services": [self.agent()]}
            after = {"uid": 501, "services": [self.agent(43)]}
            (root / "agent-inventory.json").write_text(json.dumps(baseline))
            events = []

            def inventory(*args):
                events.append("inventory")
                return after if len(events) >= 4 else baseline

            def restart(*args, **kwargs):
                events.append("restart")
                raise subprocess.CalledProcessError(
                    1, "tart", stderr=b"RPC disconnected"
                )

            with (
                patch.object(compare, "agent_inventory", side_effect=inventory),
                patch.object(compare, "guest_command", side_effect=restart),
                patch.object(compare.time, "sleep"),
                patch.object(
                    compare,
                    "guest_screen",
                    side_effect=lambda *args: events.append("screen"),
                ),
            ):
                compare.restart_agent(
                    root, root, {"Name": "step-00"}, compare.time.monotonic() + 90
                )
            self.assertEqual(
                events, ["inventory", "restart", "inventory", "inventory", "screen"]
            )
            receipt = json.loads((root / "step-00/restart.json").read_text())
            self.assertEqual((receipt["beforePid"], receipt["afterPid"]), (42, 43))
            self.assertTrue(receipt["reconnected"])

    def test_inventory_keeps_failed_service_output_and_precedes_first_capture(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            service = self.agent()
            document = dict(
                service["document"], Label="org.cirruslabs.tart-guest-agent"
            )
            with patch.object(
                compare,
                "guest_command",
                side_effect=[
                    b"501\n",
                    b"42 501 /opt/homebrew/bin/tart-guest-agent\n",
                    (service["plist"] + "\n").encode(),
                    json.dumps(document).encode(),
                    subprocess.CalledProcessError(
                        1, "launchctl", output=b"partial inventory", stderr=b"inactive"
                    ),
                ],
            ):
                compare.agent_inventory(
                    root, root / "inventory.json", compare.time.monotonic() + 10
                )
            evidence = json.loads((root / "inventory.json").read_text())
            self.assertEqual(evidence["commands"][-1]["stdout"], "partial inventory")
            self.assertEqual(evidence["commands"][-1]["stderr"], "inactive")
            image = root / "console-first-frame.png"
            image.write_bytes(b"fresh frame")
            events = []
            with (
                patch.object(vnc, "connection"),
                patch.object(
                    compare,
                    "agent_inventory",
                    side_effect=lambda *args: events.append("inventory"),
                ),
                patch.object(
                    compare,
                    "guest_screen",
                    side_effect=lambda *args: events.append("screen"),
                ),
                patch.object(
                    compare,
                    "permission_session",
                    side_effect=lambda *args: events.append("queue"),
                ),
            ):
                compare.observe(root, image, permission=True)
            self.assertEqual(events, ["inventory", "screen", "queue"])

    def test_unowned_guest_cannot_execute_inventory_or_restart_command(self):
        with (
            patch.object(vnc, "connection", side_effect=ValueError("unowned")),
            patch.object(compare.qa, "command") as command,
            self.assertRaises(ValueError),
        ):
            compare.guest_command(
                Path("unowned"), compare.time.monotonic() + 10, "/usr/bin/id", "-u"
            )
        command.assert_not_called()

    def test_privacy_diagnostic_rejects_changed_identity_and_keeps_log_failure(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            baseline = {"uid": 501, "services": [self.agent()]}
            (root / "agent-inventory.json").write_text(json.dumps(baseline))
            changed = {
                "uid": 501,
                "services": [dict(self.agent(), programSha256="c" * 64)],
            }
            with (
                patch.object(compare, "agent_inventory", return_value=changed),
                patch.object(compare, "guest_command") as command,
                self.assertRaises(ValueError),
            ):
                compare.diagnose_agent(
                    root, root, {"Name": "step-00"}, compare.time.monotonic() + 30
                )
            command.assert_not_called()
            with (
                patch.object(compare, "agent_inventory", return_value=baseline),
                patch.object(
                    compare,
                    "guest_command",
                    side_effect=[
                        b"fixture signature",
                        subprocess.CalledProcessError(
                            1, "log", output=b"partial decisions", stderr=b"denied"
                        ),
                    ],
                ),
                self.assertRaises(subprocess.CalledProcessError),
            ):
                compare.diagnose_agent(
                    root, root, {"Name": "step-01"}, compare.time.monotonic() + 30
                )
            self.assertEqual(
                (root / "step-01/tcc-decisions.log").read_bytes(), b"partial decisions"
            )
            self.assertEqual((root / "step-01/tcc-error.txt").read_bytes(), b"denied")
            with self.assertRaises(ValueError):
                compare.permission_step(
                    {"action": "diagnose-agent", "command": "id"}, 0
                )

    def test_nonzero_signing_result_is_preserved_and_does_not_skip_privacy_log(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            baseline = {"uid": 501, "services": [self.agent()]}
            (root / "agent-inventory.json").write_text(json.dumps(baseline))
            with (
                patch.object(compare, "agent_inventory", return_value=baseline),
                patch.object(
                    compare,
                    "guest_command",
                    side_effect=[
                        subprocess.CalledProcessError(
                            1, "codesign", output=b"fixture unsigned binary"
                        ),
                        b"fixture TCC decision",
                    ],
                ),
                patch.object(compare, "guest_screen") as screen,
            ):
                compare.diagnose_agent(
                    root, root, {"Name": "step-00"}, compare.time.monotonic() + 30
                )
            self.assertEqual(
                (root / "step-00/code-signing.txt").read_bytes(),
                b"fixture unsigned binary",
            )
            self.assertEqual(
                json.loads((root / "step-00/code-signing-status.json").read_text())[
                    "exitCode"
                ],
                1,
            )
            self.assertEqual(
                (root / "step-00/tcc-decisions.log").read_bytes(),
                b"fixture TCC decision",
            )
            screen.assert_called_once()

    def test_permission_requests_reject_unknown_actions_and_arbitrary_text(self):
        for request in (
            {"action": "shell", "command": "id"},
            {"action": "text", "text": "private credential"},
            {"action": "click", "x": 1, "y": 2, "delay": 15},
        ):
            with self.subTest(request=request), self.assertRaises(ValueError):
                compare.permission_step(request, 0)
        self.assertEqual(
            compare.permission_step(
                {"action": "text", "text": "/tmp/ft-input-probe"}, 1
            ),
            {"Name": "step-01", "Text": "/tmp/ft-input-probe"},
        )

    def test_permission_queue_ownership_symlink_size_and_deadline_block_input(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            request = root / "request-00.json"
            target = root / "private.json"
            target.write_text('{"action":"click","x":520,"y":24}')
            request.symlink_to(target)
            with (
                patch.object(vnc, "connection"),
                patch.object(compare, "probe_step") as probe,
                self.assertRaises(ValueError),
            ):
                compare.permission_session(root, root, compare.time.monotonic() + 2)
            probe.assert_not_called()
            request.unlink()
            request.write_text(" " * 2049)
            with (
                patch.object(vnc, "connection"),
                patch.object(compare, "probe_step") as probe,
                self.assertRaises(ValueError),
            ):
                compare.permission_session(root, root, compare.time.monotonic() + 2)
            probe.assert_not_called()
            request.write_text(target.read_text())
            with (
                patch.object(vnc, "connection", side_effect=ValueError("unowned")),
                patch.object(compare, "probe_step") as probe,
                self.assertRaises(ValueError),
            ):
                compare.permission_session(root, root, compare.time.monotonic() + 2)
            probe.assert_not_called()
            with (
                patch.object(vnc, "connection"),
                patch.object(compare, "probe_step") as probe,
                self.assertRaises(TimeoutError),
            ):
                compare.permission_session(root, root, 0)
            probe.assert_not_called()

    def test_permission_queue_limits_actions_and_finishes_without_acceptance(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for index in range(40):
                (root / f"request-{index:02d}.json").write_text('{"action":"capture"}')
            with (
                patch.object(vnc, "connection"),
                patch.object(compare, "probe_step") as probe,
                self.assertRaises(TimeoutError),
            ):
                compare.permission_session(root, root, compare.time.monotonic() + 2)
            self.assertEqual(probe.call_count, 40)
            (root / "request-01.json").write_text('{"action":"finish"}')
            with (
                patch.object(vnc, "connection"),
                patch.object(compare, "probe_step") as probe,
            ):
                compare.permission_session(root, root, compare.time.monotonic() + 2)
            self.assertEqual(probe.call_count, 1)
            self.assertEqual(
                json.loads((root / "session.json").read_text()),
                {"phase": "finished", "actions": 1},
            )

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
                    "gomenu",
                    "probe",
                    "escape",
                    "probe",
                    "lowerg",
                ],
            )
            self.assertEqual(pointer.read_text(), '{"run":"cleaned-old-run"}')
            self.assertTrue(
                json.loads((run / "atomic-comparison/gomenu/process.json").read_text())[
                    "timedOut"
                ]
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
