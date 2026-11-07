"""UI shell proof must come from the actual terminal, never command self-report."""

import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import client_baseline_shell_probe as shell


class ShellGuards(unittest.TestCase):
    def ui(self):
        return {
            "application_name": "FingerTrap",
            "window_title": "FingerTrap",
            "used_cache": False,
            "snapshot_id": "ps1_" + "a" * 32,
            "coordinate_context": {
                "logical_bounds": [[0, 25], [1024, 686]],
                "window": {"window_id": 80},
            },
            "focused_element": {"role": "AXTextArea", "windowID": 80},
            "ui_elements": [
                {
                    "ax_role": "AXButton",
                    "label": name,
                    "id": "elem_" + str(i),
                    "is_actionable": True,
                }
                for i, name in enumerate(
                    [*shell.client.CONTROLS, "New shell tab", "Close shell 2"]
                )
            ],
        }

    def test_stale_duplicate_and_wrong_receiver_cannot_admit_input(self):
        good = self.ui()
        shell.button(good)
        shell.terminal(good, 80)
        for change in (
            {"used_cache": True},
            {"snapshot_id": "legacy"},
            {"ui_elements": good["ui_elements"] * 2},
        ):
            with self.assertRaises(ValueError):
                shell.button(dict(good, **change))
        for focused in (
            {"role": "AXButton", "windowID": 80},
            {"role": "AXTextArea", "windowID": 99},
        ):
            with self.assertRaises(ValueError):
                shell.terminal(dict(good, focused_element=focused), 80)

    def test_encoded_echo_is_not_output_and_only_one_return_is_delivered(self):
        with patch.object(
            shell.pilot.qa,
            "load",
            return_value=(Path("unused"), {"runId": "owned-run"}),
        ):
            command, marker = shell.command_for(Path("unused"))
        self.assertNotIn(marker.decode().strip(), command)
        self.assertEqual(command.count("\n"), 1)
        self.assertTrue(command.endswith("\n"))
        shell.require_proof(marker, marker)
        for forged in (
            b"",
            command.encode(),
            b"FT_CF1_SHELL_OK_other-run\n",
            marker + b"extra",
        ):
            with self.assertRaises(ValueError):
                shell.require_proof(forged, marker)

    def test_preexisting_proof_stops_before_tab_or_type(self):
        with tempfile.TemporaryDirectory() as directory:
            permissions = {
                "source": "bridge",
                "permissions": [
                    {"name": name, "isGranted": True}
                    for name in (
                        "Screen Recording",
                        "Accessibility",
                        "Event Synthesizing",
                    )
                ],
            }
            with (
                patch.object(shell, "cli", return_value=permissions) as cli,
                patch.object(
                    shell,
                    "guest",
                    side_effect=subprocess.CalledProcessError(1, "proof-exists"),
                ),
                self.assertRaises(subprocess.CalledProcessError),
            ):
                shell.shell_probe(Path(directory), Path(directory) / "shell", 100)
            self.assertEqual([c.args[2] for c in cli.call_args_list], ["admission"])

    def test_indeterminate_delivery_gets_readback_without_replay(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "mutation.stdout").write_text(
                json.dumps(
                    {"outcome": {"state": "indeterminate", "mutation_dispatched": True}}
                )
            )
            with (
                patch.object(
                    shell.pilot,
                    "cli",
                    side_effect=subprocess.CalledProcessError(1, "peekaboo"),
                ) as call,
                patch.object(shell.client, "checkpoint") as capture,
            ):
                result = shell.cli(
                    root, root, "mutation", 100, "click", allow_indeterminate=True
                )
            self.assertEqual(result["outcome"]["state"], "indeterminate")
            call.assert_called_once()
            capture.assert_called_once()

    def test_fixed_action_cannot_supply_command_or_target(self):
        self.assertTrue(shell.request_step({"action": "shell-probe"}, 0)["ShellProbe"])
        for request in (
            {"action": "shell-probe", "command": "anything"},
            {"action": "shell-probe", "target": "other"},
        ):
            with self.assertRaises(ValueError):
                shell.request_step(request, 0)
