#!/usr/bin/env python3
"""Verify candidate patch admission, caps, workflow registry and residual paths."""
import argparse
import ast
import json
from pathlib import Path
import re
import subprocess
import yaml

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--checkout", required=True, type=Path)
parser.add_argument("--report", required=True, type=Path)
args = parser.parse_args()
root = args.checkout.resolve()
def git(*argv):
    return subprocess.check_output(["git", "-C", str(root), *argv], text=True).strip()
assert git("status", "--porcelain") == ""
base = "71023aec1546d6d677a65669c5665c0927ce0dd0"
active = [p for p in yaml.safe_load((root / ".psmfd/patches/manifest.yml").read_text())["patches"] if p["status"] != "retired"]
paths = {kind: {path for patch in active if patch["class"] == kind for path in patch["patched_paths"]}
         for kind in ["security", "capability"]}
for workflow in ["psmfd-zero-divergence.yml", "psmfd-divergence-detect.yml"]:
    text = (root / ".github/workflows" / workflow).read_text()
    for kind, variable in [("security", "SECURITY_PATCH_PATHS"), ("capability", "C_CLASS_PATCH_PATHS")]:
        assert ast.literal_eval(re.search(variable + r" = (\{.*?\})", text, re.S)[1]) == paths[kind]
allowlist = {line for line in (root / ".psmfd/overlay-allowlist.txt").read_text().splitlines() if line and not line.startswith("#")}
assert paths["security"] | paths["capability"] <= allowlist
capabilities = [p for p in active if p["class"] == "capability"]
counts = {}
for patch in capabilities:
    stats = git("diff", "--numstat", base, "HEAD", "--", *patch["patched_paths"])
    counts[patch["id"]] = sum(int(a) + int(b) for a, b, _ in (line.split("\t") for line in stats.splitlines()))
assert len(capabilities) <= 7 and len(paths["capability"]) <= 25 and sum(counts.values()) <= 2200
assert sum(value for key, value in counts.items() if key != "psmfd-patch-020") <= 2000
owned = {"PROVENANCE.md", "README.md", "README.psmfd.md", "SECURITY.md", "NOTICE.psmfd.md", ".gitleaks.toml", ".github/CODEOWNERS", ".github/dependabot.yml"}
residual = [path for path in git("diff", "--name-only", base, "HEAD").splitlines()
            if path not in owned | paths["security"] | paths["capability"]
            and not path.startswith((".psmfd/", ".github/workflows/", ".github/workflows-upstream-reference/"))]
assert not residual, residual
approved = {p["path"] for p in yaml.safe_load((root / ".psmfd/workflow-allowlist.yml").read_text())["workflows"]}
assert approved == {str(p.relative_to(root)) for p in (root / ".github/workflows").iterdir() if p.is_file()}
lock = json.loads((root / "packages/coding-agent/install-lock/package-lock.json").read_text())["packages"]
excluded = ["vite", "node-forge", "@earendil-works/gondolin", "shell-quote"]
assert not [path for path in lock if any(path.endswith("node_modules/" + name) for name in excluded)]
report = {"source": git("rev-parse", "HEAD"), "tree": git("rev-parse", "HEAD^{tree}"), "upstream": base,
          "capabilityPatches": len(capabilities), "distinctCapabilityPaths": len(paths["capability"]),
          "changedLines": sum(counts.values()), "perPatch": counts, "unchangedCeiling": [7, 25, 2200],
          "guardDataParity": True, "unexplainedResidualPaths": residual, "workflowRegistryParity": True,
          "runtimeInstallLockExcluded": excluded, "integrationStatus": "draft; pending patches/retirements require merge review"}
args.report.write_text(json.dumps(report, indent=2) + "\n")
print(json.dumps(report, indent=2))
