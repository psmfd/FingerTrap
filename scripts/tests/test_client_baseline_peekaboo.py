"""Pilot boundaries: no Bridge fallback, unsafe archive or missing observation."""

import json
from pathlib import Path
import stat
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import client_baseline_peekaboo as pilot


class PilotGuards(unittest.TestCase):
    def test_changed_artifact_is_rejected_before_share_staging(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / "inputs"
            source.mkdir()
            (source / "Peekaboo-4.9.8.app.zip").write_bytes(b"changed")
            manifest = root / "inputs.json"
            manifest.write_text(
                json.dumps(
                    {
                        "sourceCommit": pilot.SOURCE,
                        "teamId": pilot.TEAM,
                        "privateDirectory": "inputs",
                        "files": {"Peekaboo-4.9.8.app.zip": "0" * 64},
                    }
                )
            )
            with (
                patch.object(pilot, "INPUTS", manifest),
                patch.object(pilot.qa, "ROOT", root),
                self.assertRaises(ValueError),
            ):
                pilot.stage(root / "run")
            self.assertFalse((root / "run/share/peekaboo").exists())

    def test_cached_wrong_app_and_wrong_field_cannot_verify_input(self):
        good = {
            "used_cache": False,
            "application_name": "Finder",
            "is_dialog": True,
            "ui_elements": [
                {
                    "ax_role": "AXTextField",
                    "identifier": "PathTextField",
                    "value": "/tmp/ft-input-probe",
                }
            ],
        }
        pilot.require_finder_dialog(good, "/tmp/ft-input-probe")
        for change in (
            {"used_cache": True},
            {"application_name": "Other"},
            {"is_dialog": False},
            {"ui_elements": []},
        ):
            with self.assertRaises(ValueError):
                pilot.require_finder_dialog(dict(good, **change), "/tmp/ft-input-probe")

    def test_indeterminate_type_is_observed_once_without_replay(self):
        for observed in ("/tmp/ft-input-probe", "wrong"):
            with (
                self.subTest(observed=observed),
                tempfile.TemporaryDirectory() as directory,
            ):
                events = []
                root = Path(directory) / "scenario"

                def cli(run, output, name, deadline, *args):
                    events.append(name)
                    if name == "admission":
                        return {
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
                    if name == "type":
                        (output / "type.stdout").write_text(
                            json.dumps(
                                {
                                    "error": {"code": "INTERACTION_FAILED"},
                                    "outcome": {
                                        "state": "indeterminate",
                                        "requires_fresh_observation": True,
                                        "retry_safe": False,
                                    },
                                }
                            )
                        )
                        raise subprocess.CalledProcessError(1, "guest-cli")
                    return {
                        "used_cache": False,
                        "application_name": "Finder",
                        "is_dialog": True,
                        "ui_elements": [
                            {
                                "ax_role": "AXTextField",
                                "identifier": "PathTextField",
                                "value": observed,
                            }
                        ],
                    }

                def probe(run, step, root, deadline, **kwargs):
                    output = root / step["Name"]
                    output.mkdir()
                    (output / "readback.json").write_text("{}")

                with (
                    patch.object(pilot, "cli", side_effect=cli),
                    patch.object(pilot, "execute"),
                    patch.object(pilot.atomic, "probe_step", side_effect=probe),
                ):
                    if observed == "wrong":
                        with self.assertRaises(ValueError):
                            pilot.scenario(Path(directory), root, 100)
                        self.assertNotIn("escape", events)
                    else:
                        pilot.scenario(Path(directory), root, 100)
                        self.assertLess(
                            events.index("typed-read"), events.index("escape")
                        )
                self.assertEqual(events.count("type"), 1)

    def test_source_and_bridge_must_match(self):
        pilot.require_source({"sourceCommit": pilot.SOURCE})
        for value in ({}, {"sourceCommit": "wrong"}):
            with self.assertRaises(ValueError):
                pilot.require_source(value)
        for value in ({"source": "local"}, {"success": False}, {"data": []}):
            with self.assertRaises(ValueError):
                pilot.require_bridge(value)
        with self.assertRaises(ValueError):
            pilot.require_bridge(
                {"source": "bridge", "permissions": [{"isGranted": True}]}, True
            )

    def test_archive_rejects_escape_and_framework_link_escape(self):
        with tempfile.TemporaryDirectory() as directory:
            for index, name in enumerate(
                ("../escape", "/absolute", "Peekaboo.app/../escape")
            ):
                path = Path(directory) / str(index)
                with zipfile.ZipFile(path, "w") as archive:
                    archive.writestr(name, "x")
                with self.assertRaises(ValueError):
                    pilot.verify_app_archive(path)
            path = Path(directory) / "link"
            with zipfile.ZipFile(path, "w") as archive:
                entry = zipfile.ZipInfo("Peekaboo.app/link")
                entry.external_attr = (stat.S_IFLNK | 0o777) << 16
                archive.writestr(entry, "../../escape")
            with self.assertRaises(ValueError):
                pilot.verify_app_archive(path)

    def test_failed_scenario_command_still_gets_independent_probe(self):
        with tempfile.TemporaryDirectory() as directory:
            events = []

            def cli(*args):
                if args[2] == "admission":
                    return {
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
                events.append("failed-command")
                raise subprocess.CalledProcessError(1, "guest-cli")

            with (
                patch.object(pilot, "cli", side_effect=cli),
                patch.object(pilot, "execute"),
                patch.object(
                    pilot.atomic,
                    "probe_step",
                    side_effect=lambda *a, **kw: events.append("independent-probe"),
                ),
                self.assertRaises(subprocess.CalledProcessError),
            ):
                pilot.scenario(Path(directory), Path(directory) / "scenario", 100)
            self.assertEqual(events, ["failed-command", "independent-probe"])

    def test_staging_failure_prevents_guest_spawn(self):
        with tempfile.TemporaryDirectory() as directory:
            run = Path(directory)
            with (
                patch.object(pilot.atomic.vnc.qa, "prepare", return_value=run),
                patch.object(
                    pilot.atomic.vnc.qa, "load", return_value=(run, {"events": []})
                ),
                patch.object(pilot.atomic.vnc.subprocess, "Popen") as spawn,
                self.assertRaises(ValueError),
            ):
                pilot.atomic.vnc.serve(
                    None,
                    None,
                    1500,
                    prepare_hook=lambda run: (_ for _ in ()).throw(
                        ValueError("bad input")
                    ),
                )
            spawn.assert_not_called()
            self.assertTrue((run / "finish-session").exists())

    def test_pilot_queue_cannot_execute_arbitrary_commands(self):
        for value in (
            {"action": "exec", "command": "anything"},
            {"action": "pilot-scenario", "extra": True},
            {"action": "restart-agent"},
        ):
            with self.assertRaises(ValueError):
                pilot.request_step(value, 0)
