"""Normal GUI Quit attribution, control survival and bounded completion guards."""

import copy
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import client_baseline_quit_probe as quit_probe


class QuitGuards(unittest.TestCase):
    def rows(self):
        base = quit_probe.client.APP + "/Contents/MacOS/"
        return {
            10: {
                "parent": 1,
                "started": "Sat Nov 7 22:00:00 2026",
                "command": base + "fingertrap",
            },
            11: {
                "parent": 10,
                "started": "Sat Nov 7 22:00:01 2026",
                "command": base + "fingertrap-sidecar",
            },
            12: {
                "parent": 11,
                "started": "Sat Nov 7 22:00:02 2026",
                "command": "/bin/zsh",
            },
            13: {
                "parent": 12,
                "started": "Sat Nov 7 22:00:03 2026",
                "command": "/bin/cat",
            },
            20: {
                "parent": 1,
                "started": "Sat Nov 7 21:59:00 2026",
                "command": "/bin/sleep",
            },
        }

    def test_admission_requires_real_proven_ancestry_and_unrelated_control(self):
        good = self.rows()
        admitted = quit_probe.admit_tree(good, 10, 12, 20)
        self.assertEqual(set(admitted["owned"]), {"10", "11", "12", "13"})
        for pid, key, value in (
            (10, "command", "/other"),
            (12, "parent", 1),
            (20, "parent", 11),
            (20, "command", "/bin/zsh"),
        ):
            rows = copy.deepcopy(good)
            rows[pid][key] = value
            with self.assertRaises(ValueError):
                quit_probe.admit_tree(rows, 10, 12, 20)

    def test_shutdown_rejects_reparented_children_and_replaced_or_missing_control(self):
        rows = self.rows()
        admitted = quit_probe.admit_tree(rows, 10, 12, 20)
        survivor = {20: rows[20]}
        quit_probe.require_shutdown(survivor, admitted)
        for bad in (
            {},
            {20: dict(rows[20], started="reused PID")},
            {20: rows[20], 13: dict(rows[13], parent=1)},
            {20: rows[20], 40: dict(rows[10], parent=1)},
        ):
            with self.assertRaises(ValueError):
                quit_probe.require_shutdown(bad, admitted)

    def test_quit_delivered_once_while_reads_poll_and_control_loss_is_fatal(self):
        before = self.rows()
        admitted = quit_probe.admit_tree(before, 10, 12, 20)
        after = {20: before[20]}
        for reads, succeeds, count in (
            ([before, after], True, 2),
            ([before] * 3, False, 3),
            ([{}], False, 1),
        ):
            with (
                self.subTest(reads=reads),
                patch.object(quit_probe.shell, "cli") as mutation,
                patch.object(quit_probe, "inventory", side_effect=reads) as inventory,
                patch.object(quit_probe.time, "sleep"),
                patch.object(quit_probe.pilot.atomic, "remaining", return_value=0.5),
            ):
                if succeeds:
                    quit_probe.quit_once(
                        Path("unused"), Path("unused"), 100, 80, admitted
                    )
                else:
                    with self.assertRaises(ValueError):
                        quit_probe.quit_once(
                            Path("unused"), Path("unused"), 100, 80, admitted
                        )
                mutation.assert_called_once()
                self.assertIn("cmd+q", mutation.call_args.args)
                self.assertEqual(inventory.call_count, count)

    def test_reserved_scenario_budget_rejects_late_start_and_queue_is_fixed(self):
        with patch.object(quit_probe.time, "monotonic", return_value=100):
            self.assertEqual(quit_probe.scenario_deadline(460), 460)
            with self.assertRaises(TimeoutError):
                quit_probe.scenario_deadline(459)
        self.assertTrue(quit_probe.request_step({"action": "quit-probe"}, 2)["Quit"])
        with self.assertRaises(ValueError):
            quit_probe.request_step({"action": "quit-probe", "command": "anything"}, 2)

    def test_parser_preserves_start_time_and_rejects_duplicate_or_empty_inventory(self):
        value = quit_probe.processes(
            b" PID PPID STARTED COMM\n20 1 Sat Nov  7 21:59:00 2026 /bin/sleep\n"
        )
        self.assertEqual(value[20]["started"], "Sat Nov 7 21:59:00 2026")
        for raw in (b"", b"20 1 Sat Nov 7 21:59:00 2026 /bin/sleep\n" * 2):
            with self.assertRaises(ValueError):
                quit_probe.processes(raw)

    def test_queue_finishes_only_after_successful_fixed_dispatch(self):
        atomic = quit_probe.pilot.atomic
        for fails in (False, True):
            with tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                (root / "request-00.json").write_text(
                    json.dumps({"action": "quit-probe"})
                )
                with (
                    patch.object(atomic.vnc, "connection"),
                    patch.object(atomic, "remaining", return_value=1),
                    patch.object(atomic, "time") as clock,
                ):
                    clock.sleep.side_effect = AssertionError(
                        "Must not wait for another operator request"
                    )

                    def dispatch(*args):
                        self.assertEqual(
                            json.loads((root / "session.json").read_text())["phase"],
                            "waiting",
                        )
                        if fails:
                            raise ValueError("shutdown unverified")

                    if fails:
                        with self.assertRaises(ValueError):
                            atomic.permission_session(
                                root,
                                root,
                                100,
                                request_step=quit_probe.request_step,
                                dispatch=dispatch,
                                complete=lambda step: step.get("Quit") is True,
                            )
                        self.assertEqual(
                            json.loads((root / "session.json").read_text())["phase"],
                            "waiting",
                        )
                    else:
                        atomic.permission_session(
                            root,
                            root,
                            100,
                            request_step=quit_probe.request_step,
                            dispatch=dispatch,
                            complete=lambda step: step.get("Quit") is True,
                        )
                        self.assertEqual(
                            json.loads((root / "session.json").read_text()),
                            {"phase": "finished", "actions": 1},
                        )
