"""Repeat-launch guards: stable session identities and no launch replay."""

import copy
from pathlib import Path
import subprocess
import sys
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import client_baseline_repeat_probe as repeat


class RepeatGuards(unittest.TestCase):
    def admitted(self):
        return {
            "owned": {
                "10": {"parent": 1, "started": "app-start", "command": "app"},
                "11": {"parent": 10, "started": "sidecar-start", "command": "sidecar"},
                "12": {"parent": 11, "started": "shell-start", "command": "/bin/zsh"},
            },
            "controlPid": 20,
            "control": {
                "parent": 1,
                "started": "control-start",
                "command": "/bin/sleep",
            },
            "sidecarPid": 11,
        }

    def proof(self):
        return {"pid": 12, "rows": 39, "columns": 126, "sentinel": "owned-nonce"}

    def test_replaced_extra_missing_or_reused_process_identity_is_rejected(self):
        good = self.admitted()
        repeat.require_repeat(
            good, copy.deepcopy(good), self.proof(), self.proof(), 80, 80, "owned-nonce"
        )
        changes = []
        for pid in ("10", "11", "12"):
            after = copy.deepcopy(good)
            after["owned"][pid]["started"] = "new start"
            changes.append(after)
            after = copy.deepcopy(good)
            del after["owned"][pid]
            changes.append(after)
        after = copy.deepcopy(good)
        after["owned"]["90"] = dict(after["owned"]["11"])
        changes.append(after)
        after = copy.deepcopy(good)
        after["control"]["started"] = "replacement"
        changes.append(after)
        for after in changes:
            with self.subTest(after=after), self.assertRaises(ValueError):
                repeat.require_repeat(
                    good, after, self.proof(), self.proof(), 80, 80, "owned-nonce"
                )

    def test_window_shell_state_geometry_and_nonce_must_survive(self):
        for field, value in (
            ("pid", 99),
            ("rows", 38),
            ("columns", 125),
            ("sentinel", "foreign"),
        ):
            second = dict(self.proof(), **{field: value})
            with self.subTest(field=field), self.assertRaises(ValueError):
                repeat.require_repeat(
                    self.admitted(),
                    self.admitted(),
                    self.proof(),
                    second,
                    80,
                    80,
                    "owned-nonce",
                )
        for window, nonce in ((81, "owned-nonce"), (80, "foreign")):
            with self.assertRaises(ValueError):
                repeat.require_repeat(
                    self.admitted(),
                    self.admitted(),
                    self.proof(),
                    self.proof(),
                    80,
                    window,
                    nonce,
                )

    def test_normal_launch_delivered_once_and_failures_never_replay(self):
        for fail_at in (None, 0, 1):
            events = []

            def guest(*args):
                events.append(args[2])
                if len(events) - 1 == fail_at:
                    raise subprocess.CalledProcessError(1, "open")

            with (
                self.subTest(fail_at=fail_at),
                patch.object(repeat.shell, "guest", side_effect=guest) as command,
            ):
                if fail_at is None:
                    repeat.reopen_once(Path("run"), Path("output"), 100)
                else:
                    with self.assertRaises(subprocess.CalledProcessError):
                        repeat.reopen_once(Path("run"), Path("output"), 100)
                self.assertEqual(
                    events,
                    ["finder-foreground"]
                    if fail_at == 0
                    else ["finder-foreground", "reopen-once"],
                )
                self.assertEqual(
                    command.call_args_list[0].args[4:],
                    ("/usr/bin/open", "-b", "com.apple.finder"),
                )
                if fail_at != 0:
                    self.assertEqual(
                        command.call_args_list[1].args[4:],
                        ("/usr/bin/open", repeat.client.APP),
                    )

    def test_fixed_queue_and_reserved_budget_reject_late_or_arbitrary_requests(self):
        self.assertTrue(repeat.request_step({"action": "repeat-probe"}, 1)["Repeat"])
        for request in (
            {"action": "repeat-probe", "command": "anything"},
            {"action": "exec", "command": "anything"},
        ):
            with self.assertRaises(ValueError):
                repeat.request_step(request, 1)
        with patch.object(repeat.time, "monotonic", return_value=100):
            self.assertEqual(repeat.scenario_deadline(580), 580)
            with self.assertRaises(TimeoutError):
                repeat.scenario_deadline(579)
