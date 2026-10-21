"""Policy regression checks; the real VM/network/Actions pilot is also required."""
import copy
import importlib.util
from pathlib import Path
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location(
    "tart_runner", Path(__file__).with_name("tart-runner.py"))
runner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(runner)


class GroupPolicyTests(unittest.TestCase):
    def setUp(self):
        self.group = {
            "name": runner.GROUP, "inherited": False, "visibility": "selected",
            "allows_public_repositories": True, "restricted_to_workflows": True,
            "selected_workflows": [runner.WORKFLOW],
        }
        self.repos = [{"id": runner.REPO_ID}]

    def test_exact_boundary_is_allowed(self):
        runner.validate_group(self.group, self.repos)

    def test_expanded_or_missing_boundary_is_rejected(self):
        changes = [
            ("name", "Default"), ("inherited", True), ("visibility", "all"),
            ("allows_public_repositories", False),
            ("restricted_to_workflows", False), ("selected_workflows", []),
            ("selected_workflows", [runner.WORKFLOW, "attacker/repo/ci.yml@main"]),
            ("selected_workflows", [runner.WORKFLOW.replace(
                "refs/heads/ci/tart-runner-pilot", "refs/pull/207/merge")]),
        ]
        for key, value in changes:
            with self.subTest(key=key, value=value):
                group = copy.deepcopy(self.group)
                group[key] = value
                with self.assertRaises(RuntimeError):
                    runner.validate_group(group, self.repos)

    def test_other_repositories_are_rejected(self):
        for repos in ([], [{"id": 1}], self.repos + [{"id": 1}]):
            with self.subTest(repos=repos), self.assertRaises(RuntimeError):
                runner.validate_group(self.group, repos)

    def test_cutover_allows_only_the_exact_reviewed_workflows(self):
        expected = [runner.WORKFLOW, "psmfd/FingerTrap/.github/workflows/ci.yml@refs/heads/dev"]
        self.group["selected_workflows"] = expected
        runner.validate_group(self.group, self.repos, expected)
        self.group["selected_workflows"] = expected + [
            "psmfd/FingerTrap/.github/workflows/attacker.yml@refs/heads/dev"]
        with self.assertRaises(RuntimeError):
            runner.validate_group(self.group, self.repos, expected)

    def test_fork_pr_cannot_be_enrolled(self):
        pr = {"state": "open", "head": {"repo": {"id": 1}, "ref": "branch"},
              "base": {"repo": {"id": runner.REPO_ID}, "ref": "dev"}}
        with patch.object(runner, "api", return_value=pr), self.assertRaises(RuntimeError):
            runner.routing_policy({}, {"trustedPullRequests": [207], "workerRevision": "a" * 40})

    def test_trusted_pr_has_only_known_workflows_and_refs(self):
        pr = {"state": "open",
              "head": {"repo": {"id": runner.REPO_ID}, "ref": "docs/pi-runtime-migration"},
              "base": {"repo": {"id": runner.REPO_ID}, "ref": "dev"}}
        with patch.object(runner, "api", return_value=pr):
            workflows, prs = runner.routing_policy(
                {}, {"trustedPullRequests": [207], "workerRevision": "a" * 40})
        self.assertEqual(prs, [207])
        self.assertIn("psmfd/FingerTrap/.github/workflows/macos-worker.yml@" + "a" * 40,
                      workflows)
        self.assertEqual(len(workflows), 2)
        self.assertTrue(all(w.startswith("psmfd/FingerTrap/.github/workflows/")
                            for w in workflows))

    def test_mutable_worker_revision_is_rejected(self):
        with self.assertRaises(RuntimeError):
            runner.routing_policy({}, {"trustedPullRequests": [], "workerRevision": "main"})


if __name__ == "__main__":
    unittest.main()
