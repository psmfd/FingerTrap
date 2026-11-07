"""Packaged observation must verify installed bytes and fresh real UI evidence."""

import json
from pathlib import Path
import sys
import unittest
import tempfile
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import client_baseline_client_observe as client


class ClientObservationGuards(unittest.TestCase):
    def ui(self):
        return {
            "application_name": "FingerTrap",
            "window_title": "FingerTrap",
            "used_cache": False,
            "coordinate_context": {
                "logical_bounds": [[0, 0], [1024, 720]],
                "window": {"window_id": 42},
            },
            "ui_elements": [
                {"ax_role": "AXButton", "label": name} for name in client.CONTROLS
            ],
        }

    def test_wrong_bundle_and_changed_installed_binary_are_rejected(self):
        state = {"binaries": [{"name": "fingertrap", "sha256": "expected"}]}
        good = {"bundleId": client.BUNDLE, "binaries": {"fingertrap": "expected"}}
        client.verify_installed(good, state)
        for change in ({"bundleId": "other"}, {"binaries": {"fingertrap": "changed"}}):
            with self.assertRaises(ValueError):
                client.verify_installed(dict(good, **change), state)

    def test_process_or_window_title_alone_cannot_establish_ui_readiness(self):
        self.assertEqual(client.require_ui(self.ui()), 42)
        for change in (
            {"used_cache": True},
            {"ui_elements": []},
            {"application_name": "Other"},
            {"observation": {"warnings": ["truncated"]}},
            {
                "coordinate_context": {
                    "logical_bounds": [[0, 0], [0, 720]],
                    "window": {"window_id": 42},
                }
            },
        ):
            with self.subTest(change=change), self.assertRaises(ValueError):
                client.require_ui(dict(self.ui(), **change))

    def test_ambiguous_and_absent_windows_block_observation(self):
        fixture = json.loads(
            (
                Path(__file__).parent / "fixtures/client-visible-window-inventory.json"
            ).read_text()
        )
        self.assertEqual(client.require_window(fixture), 80)
        visible = [w for w in fixture["data"]["windows"] if w["is_on_screen"]]
        for windows in (
            [],
            visible * 2,
            [dict(visible[0], is_on_screen=False)],
            [dict(visible[0], bounds={"width": 0, "height": 720})],
        ):
            with self.assertRaises(ValueError):
                client.require_window({"data": dict(fixture["data"], windows=windows)})
        with self.assertRaises(ValueError):
            client.require_window(
                {
                    "data": dict(
                        fixture["data"], target_application_info={"bundle_id": "other"}
                    )
                }
            )

    def test_offline_review_cannot_use_live_or_uncleaned_guest(self):
        with (
            patch.object(
                client.pilot.qa,
                "load",
                return_value=(
                    Path("unused"),
                    {"phase": "guest-ready", "cleanupVerified": False},
                ),
            ),
            self.assertRaises(ValueError),
        ):
            client.review(Path("unused"))

    def test_client_action_has_no_caller_command_or_path(self):
        self.assertTrue(
            client.request_step({"action": "client-observe"}, 0)["ClientObserve"]
        )
        for request in (
            {"action": "client-observe", "path": "/other"},
            {"action": "client-observe", "command": "other"},
        ):
            with self.assertRaises(ValueError):
                client.request_step(request, 0)

    def test_latest_two_consecutive_observations_determine_live_status(self):
        for ids, stable, count in (
            ([1, 2, 2], True, 3),
            ([1, 2, 1], False, 3),
            ([1, 1], True, 2),
            ([1, ValueError("blocked"), 1], False, 3),
        ):
            with (
                tempfile.TemporaryDirectory() as directory,
                patch.object(client.pilot, "require_bridge"),
                patch.object(client.pilot, "cli"),
                patch.object(client.pilot, "execute", return_value="{}"),
                patch.object(client.pilot.qa, "load", return_value=(None, {})),
                patch.object(client, "verify_installed"),
                patch.object(client, "checkpoint"),
                patch.object(client, "require_window", side_effect=ids),
                patch.object(
                    client,
                    "require_ui",
                    side_effect=[v for v in ids if isinstance(v, int)],
                ),
                patch.object(client.time, "sleep"),
                patch.object(client.pilot.atomic, "remaining", return_value=5),
                patch.object(client.pilot.qa, "save") as save,
            ):
                client.client_observe(Path(directory), Path(directory) / "client", 100)
                result = save.call_args.args[1]
                self.assertEqual(result["stableObservedWindow"], stable)
                self.assertEqual(client.require_window.call_count, count)
