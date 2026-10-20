"""Policy regression checks; the real VM/network/Actions pilot is also required."""
import copy
import importlib.util
from pathlib import Path
import unittest

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


if __name__ == "__main__":
    unittest.main()
