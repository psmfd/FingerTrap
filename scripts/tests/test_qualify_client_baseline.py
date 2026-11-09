"""Regressions for CF-1 artifact, observation, history and VM ownership guards."""

import importlib.util
import json
from pathlib import Path
import stat
import subprocess
import tempfile
import unittest
from unittest.mock import patch
import zipfile

SPEC = importlib.util.spec_from_file_location(
    "client_baseline",
    Path(__file__).resolve().parents[1] / "qualify-client-baseline.py",
)
qa = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(qa)


class BaselineGuards(unittest.TestCase):
    def test_console_modes_keep_vnc_headless_and_reject_conflicting_modes(self):
        self.assertEqual(
            qa.console_arguments(vnc=True), ["--no-graphics", "--vnc-experimental"]
        )
        self.assertEqual(qa.console_arguments(graphics=True), [])
        self.assertEqual(qa.console_arguments(), ["--no-graphics"])
        with self.assertRaises(ValueError):
            qa.console_arguments(graphics=True, vnc=True)

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.run_id = "20261022T120000Z-abcdef123456"
        self.run = self.root / self.run_id
        self.run.mkdir()
        self.state = {
            "kind": "fingertrap-client-baseline",
            "runId": self.run_id,
            "vm": "ft-client-baseline-" + self.run_id.lower(),
            "base": qa.BASE,
            "runtimeTag": qa.RUNTIME_TAG,
            "created": False,
            "cleanupVerified": False,
            "attempts": [],
            "events": [],
        }
        qa.save(self.run / "state.json", self.state)
        (self.run / "observation.txt").write_text(
            "Observed packaged-client window and recorded child identities.\n"
        )

    def record(self, status="PASS", **changes):
        args = dict(
            run=self.run,
            case="CF1-02",
            status=status,
            observation="Finder opened a usable packaged app pane.",
            evidence=["observation.txt"],
            observer="QA observer",
            mode="observed-gui",
        )
        args.update(changes)
        qa.record(**args)

    def test_wrong_artifact_bytes_are_rejected(self):
        with self.assertRaises(ValueError):
            qa.verify_digest(self.run / "observation.txt", "0" * 64)

    def test_safe_archive_extracts_actual_bytes_and_executable_mode(self):
        archive = self.root / "app.zip"
        with zipfile.ZipFile(archive, "w") as z:
            info = zipfile.ZipInfo("App/Contents/MacOS/app")
            info.external_attr = (stat.S_IFREG | 0o755) << 16
            z.writestr(info, b"actual bytes")
        output = self.root / "output"
        qa.safe_unzip(archive, output)
        self.assertEqual((output / info.filename).read_bytes(), b"actual bytes")
        self.assertTrue((output / info.filename).stat().st_mode & 0o111)

    def test_unsafe_archive_paths_never_write_outside_destination(self):
        for name in (
            "../escaped",
            "/absolute",
            "App/../../escaped",
            "App\\escaped",
            "C:/escaped",
        ):
            with self.subTest(name=name):
                archive = self.root / "bad.zip"
                with zipfile.ZipFile(archive, "w") as z:
                    z.writestr(name, b"unsafe")
                with self.assertRaises(ValueError):
                    qa.safe_unzip(archive, self.root / "output")
                self.assertFalse((self.root / "escaped").exists())
                self.assertFalse((self.root / "output").exists())

    def test_archive_symlink_rejected_before_any_extraction(self):
        archive = self.root / "link.zip"
        with zipfile.ZipFile(archive, "w") as z:
            z.writestr("good", b"first")
            link = zipfile.ZipInfo("link")
            link.external_attr = (stat.S_IFLNK | 0o777) << 16
            z.writestr(link, "../../operator-data")
        with self.assertRaises(ValueError):
            qa.safe_unzip(archive, self.root / "output")
        self.assertFalse((self.root / "output").exists())

    def test_extract_into_existing_directory_refused(self):
        archive = self.root / "app.zip"
        with zipfile.ZipFile(archive, "w") as z:
            z.writestr("observation.txt", "overwrite")
        with self.assertRaises(ValueError):
            qa.safe_unzip(archive, self.run)
        self.assertNotEqual((self.run / "observation.txt").read_text(), "overwrite")

    def test_skipped_status_cannot_be_recorded_as_pass(self):
        with self.assertRaises(ValueError):
            self.record(status="SKIP")

    def test_headless_result_cannot_pass_gui_case(self):
        with self.assertRaises(ValueError):
            self.record(mode="artifact-verification")

    def test_unobserved_or_unsupported_pass_rejected(self):
        for changes in ({"observer": ""}, {"evidence": []}, {"observation": ""}):
            with self.subTest(changes=changes), self.assertRaises(ValueError):
                self.record(**changes)

    def test_outside_and_symlink_evidence_rejected(self):
        outside = self.root / "outside.txt"
        outside.write_text("private data")
        (self.run / "link.txt").symlink_to(outside)
        for path in ("../outside.txt", "link.txt"):
            with self.subTest(path=path), self.assertRaises(ValueError):
                self.record(evidence=[path])

    def test_failure_remains_after_successful_retest(self):
        self.record("FAIL", observation="First Finder launch failed.")
        self.record("PASS")
        state = json.loads((self.run / "state.json").read_text())
        result = qa.summary(state)
        self.assertEqual([a["status"] for a in result["attempts"]], ["FAIL", "PASS"])
        self.assertEqual(result["cases"][1]["status"], "PASS")
        self.assertFalse(result["baselineAccepted"])

    def test_blocked_and_unrun_cases_prevent_acceptance(self):
        self.record(
            "BLOCKED", mode="blocked", observation="Observable GUI unavailable."
        )
        result = qa.summary(json.loads((self.run / "state.json").read_text()))
        self.assertFalse(result["baselineCaptured"])
        self.assertFalse(result["readyForMaintainerAcceptance"])
        self.assertFalse(result["baselineAccepted"])

    def test_all_passes_require_cleanup_and_never_claim_maintainer_acceptance(self):
        for case in qa.CASES:
            self.record(case=case)
        state = json.loads((self.run / "state.json").read_text())
        self.assertFalse(qa.summary(state)["readyForMaintainerAcceptance"])
        state["cleanupVerified"] = True
        self.assertTrue(qa.summary(state)["readyForMaintainerAcceptance"])
        self.assertFalse(qa.summary(state)["baselineAccepted"])

    def test_unowned_vm_cleanup_invokes_no_commands(self):
        with patch.object(qa, "command") as command:
            with self.assertRaises(ValueError):
                qa.cleanup(self.run, self.state)
            command.assert_not_called()

    def test_changed_clone_identity_is_not_stopped_or_deleted(self):
        self.state.update(
            created=True, ownedConfig={"MAC": "owned"}, originalVms=[qa.BASE]
        )
        listing = json.dumps([{"Name": self.state["vm"]}, {"Name": qa.BASE}]).encode()
        with (
            patch.object(qa, "command", return_value=listing) as command,
            patch.object(qa, "vm_config", return_value={"MAC": "different"}),
        ):
            with self.assertRaises(ValueError):
                qa.cleanup(self.run, self.state)
            self.assertEqual(command.call_count, 1)
            self.assertEqual(
                command.call_args.args[0], ["tart", "list", "--format", "json"]
            )

    def test_owned_cleanup_verifies_external_absence_and_original_preservation(self):
        self.state.update(
            created=True,
            ownedConfig={"MAC": "owned"},
            originalVms=[qa.BASE, "operator-vm"],
        )
        before = json.dumps(
            [
                {"Name": n, "Running": n == self.state["vm"]}
                for n in [self.state["vm"], qa.BASE, "operator-vm"]
            ]
        ).encode()
        after = json.dumps([{"Name": n} for n in [qa.BASE, "operator-vm"]]).encode()
        with (
            patch.object(
                qa, "command", side_effect=[before, b"", b"", after]
            ) as command,
            patch.object(qa, "vm_config", return_value={"MAC": "owned"}),
        ):
            qa.cleanup(self.run, self.state)
        self.assertTrue(self.state["cleanupVerified"])
        self.assertIn(
            ["tart", "delete", self.state["vm"]],
            [c.args[0] for c in command.call_args_list],
        )

    def test_stopped_owned_clone_is_deleted_when_tart_stop_would_fail(self):
        self.state.update(
            created=True, ownedConfig={"MAC": "owned"}, originalVms=[qa.BASE]
        )
        existing = {self.state["vm"], qa.BASE}

        def command(argv):
            if argv == ["tart", "list", "--format", "json"]:
                return json.dumps(
                    [{"Name": name, "Running": False} for name in existing]
                ).encode()
            if argv == ["tart", "stop", self.state["vm"]]:
                raise subprocess.CalledProcessError(
                    2, argv, stderr=b"VM is not running"
                )
            if argv == ["tart", "delete", self.state["vm"]]:
                existing.remove(self.state["vm"])
                return b""
            raise AssertionError(argv)

        with (
            patch.object(qa, "command", side_effect=command),
            patch.object(qa, "vm_config", return_value={"MAC": "owned"}),
        ):
            qa.cleanup(self.run, self.state)
        self.assertEqual(existing, {qa.BASE})
        self.assertTrue(self.state["cleanupVerified"])


if __name__ == "__main__":
    unittest.main()
