"""Layout qualification must observe PTY state and protect the original shell."""

import ast
import copy
import json
from pathlib import Path
import sys
import subprocess
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import client_baseline_layout_probe as layout


class LayoutGuards(unittest.TestCase):
    def proofs(self):
        return {
            "before": {"pid": 30, "rows": 40, "columns": 125, "sentinel": "owned"},
            "resized": {"pid": 30, "rows": 34, "columns": 109, "sentinel": "owned"},
            "sibling": {"pid": 31, "rows": 34, "columns": 53, "sentinel": ""},
            "survivor": {"pid": 30, "rows": 34, "columns": 109, "sentinel": "owned"},
        }

    def processes(self):
        return f"10 1 {layout.client.APP}/Contents/MacOS/fingertrap\n20 10 {layout.client.APP}/Contents/MacOS/fingertrap-sidecar\n30 20 /bin/zsh\n31 20 /bin/zsh\n".encode()

    def test_proof_requires_world_marker_pid_and_real_tty_dimensions(self):
        self.assertEqual(
            layout.parse_proof(b"marker\nPID=30\n34 109\nSENTINEL=owned\n", "marker")[
                "pid"
            ],
            30,
        )
        for raw in (
            b"",
            b"command echo",
            b"wrong\nPID=30\n34 109\nSENTINEL=owned\n",
            b"marker\nPID=0\n34 109\nSENTINEL=owned\n",
            b"marker\nPID=30\n0 109\nSENTINEL=owned\n",
        ):
            with self.assertRaises(ValueError):
                layout.parse_proof(raw, "marker")

    def test_replacement_shell_wrong_geometry_and_missing_sentinel_fail(self):
        good = self.proofs()
        layout.verify_progress(good, "owned")
        for phase, change in [
            ("resized", {"pid": 32}),
            ("resized", {"rows": 40}),
            ("sibling", {"pid": 30}),
            ("sibling", {"sentinel": "owned"}),
            ("survivor", {"pid": 32}),
            ("survivor", {"sentinel": ""}),
            ("survivor", {"columns": 53}),
        ]:
            proofs = copy.deepcopy(good)
            proofs[phase].update(change)
            with (
                self.subTest(phase=phase, change=change),
                self.assertRaises(ValueError),
            ):
                layout.verify_progress(proofs, "owned")

    def test_unproven_sibling_stops_before_close_mutation(self):
        proofs = self.proofs()
        proofs["sibling"]["sentinel"] = "owned"
        with (
            patch.object(layout.shell, "guest", return_value=self.processes()),
            patch.object(layout.shell, "cli") as mutate,
            self.assertRaises(ValueError),
        ):
            layout.close_sibling(
                Path("unused"), Path("unused"), 100, 80, proofs, {"runId": "owned"}, 10
            )
        mutate.assert_not_called()

    def test_process_identity_and_sibling_termination_rechecked(self):
        raw = self.processes()
        self.assertEqual(layout.process_tree(raw, 10, [30, 31]), 20)
        for changed, pids, gone in [
            (raw, [30], 31),
            (raw.replace(b"30 20", b"30 99"), [30], None),
            (raw, [30, 30], None),
        ]:
            with self.assertRaises(ValueError):
                layout.process_tree(changed, 10, pids, gone)
        self.assertEqual(
            layout.process_tree(raw.replace(b"31 20 /bin/zsh\n", b""), 10, [30], 31), 20
        )

    def test_fixed_commands_are_run_bound_and_within_typing_budget(self):
        with patch.object(
            layout.pilot.qa,
            "load",
            return_value=(Path("unused"), {"runId": "20261107T182716Z-cc826ea8ca2a"}),
        ):
            for phase in layout.PHASES:
                command, marker, path = layout.command_for("unused", phase)
                self.assertNotIn(marker, command)
                self.assertEqual(command.count("\n"), 1)
                self.assertLessEqual(len(command), 150)
                self.assertTrue(path.startswith("/tmp/ft4-"))
            with self.assertRaises(ValueError):
                layout.command_for("unused", "arbitrary")

    def test_supported_guest_fixture_syntax_and_json(self):
        code = next(
            v
            for v in layout.configure.__code__.co_consts
            if isinstance(v, str) and v.startswith("import json")
        )
        tree = ast.parse(code)
        writes = [
            ast.literal_eval(n.args[0])
            for n in ast.walk(tree)
            if isinstance(n, ast.Call)
            and isinstance(n.func, ast.Attribute)
            and n.func.attr == "write"
        ]
        self.assertEqual(
            [json.loads(v) for v in writes],
            [{"version": 1, "pane": {"defaultKind": "shell"}}],
        )

    def test_readback_retries_only_missing_or_valid_prefixes(self):
        good = b"marker\nPID=30\n34 109\nSENTINEL=owned\n"
        missing = subprocess.CalledProcessError(44, "read")
        for reads, succeeds, attempts in (
            ([missing, good[:18], good], True, 3),
            ([b"", b"", b""], False, 3),
            ([b"wrong"], False, 1),
            ([b"marker\nPID=0"], False, 1),
            ([b"marker\nPID=30\n0"], False, 1),
            ([b"\xff"], False, 1),
            ([subprocess.CalledProcessError(1, "read")], False, 1),
        ):
            with (
                self.subTest(reads=reads),
                patch.object(
                    layout,
                    "command_for",
                    return_value=("command", "marker", "/tmp/proof"),
                ),
                patch.object(layout, "snapshot"),
                patch.object(layout.pilot.qa, "save"),
                patch.object(layout.shell, "cli") as mutate,
                patch.object(layout.shell, "guest", side_effect=[b"", *reads]) as guest,
                patch.object(layout.time, "sleep") as sleep,
                patch.object(layout.pilot.atomic, "remaining", return_value=0.5),
            ):
                if succeeds:
                    self.assertEqual(
                        layout.proof(
                            Path("unused"), Path("unused"), "before", 100, 80, 1
                        )["pid"],
                        30,
                    )
                else:
                    with self.assertRaises((ValueError, subprocess.CalledProcessError)):
                        layout.proof(
                            Path("unused"), Path("unused"), "before", 100, 80, 1
                        )
                self.assertEqual(guest.call_count, attempts + 1)
                mutate.assert_called_once()
                for call in sleep.call_args_list:
                    self.assertEqual(call.args, (0.5,))

    def test_every_strict_prefix_can_complete_but_invalid_content_cannot(self):
        good = b"marker\nPID=30\n 34 109\nSENTINEL=owned\n"
        for end in range(len(good)):
            self.assertTrue(layout.incomplete_proof(good[:end], "marker"), end)
        self.assertFalse(layout.incomplete_proof(good, "marker"))
        self.assertFalse(layout.incomplete_proof(good + b"extra", "marker"))
