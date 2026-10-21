#!/usr/bin/env python3
"""Build temporary startup regressions in an isolated Pi checkout and prove rejection.

Requires the reviewed Node/Bun toolchain on PATH, hydrated model data and built
workspace dependencies. Restores source and rebuilds the clean coding-agent
package in finally. Never writes the immutable qualified candidate directory.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tempfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--checkout", required=True, type=Path)
parser.add_argument("--output", required=True, type=Path)
args = parser.parse_args()
root = args.checkout.resolve()
out = args.output.resolve()
out.mkdir(parents=True, exist_ok=True)
host = Path(__file__).resolve().parents[4]
mutations = [
    ("migrations", "packages/coding-agent/src/main.ts",
     'const { migratedAuthProviders: migratedProviders, deprecationWarnings } = startupPolicy\n\t\t? { migratedAuthProviders: [], deprecationWarnings: [] }\n\t\t: runMigrations(cwd);',
     'const { migratedAuthProviders: migratedProviders, deprecationWarnings } = runMigrations(cwd);'),
    ("discovery", "packages/coding-agent/src/core/agent-session-services.ts",
     'const resourceLoader: ResourceLoader = policy\n',
     'const resourceLoader: ResourceLoader = policy && process.env.FT_MUTATION_KEEP_MANAGED === "1"\n'),
    ("crash-discovery", "packages/coding-agent/src/modes/interactive/interactive-mode.ts",
     'this.isManagedStartup ? undefined : takeUnnotifiedCrash()', 'takeUnnotifiedCrash()'),
    ("bug-report", "packages/coding-agent/src/modes/interactive/interactive-mode.ts",
     'if (this.rejectManagedOperation("bug reports")) return;',
     'if (false && this.rejectManagedOperation("bug reports")) return;'),
]
saved = {path: (root / path).read_bytes() for _, path, _, _ in mutations}
assert subprocess.check_output(["git", "-C", str(root), "status", "--porcelain"], text=True) == ""
revision = subprocess.check_output(["git", "-C", str(root), "rev-parse", "HEAD"], text=True).strip()
records = []
def run(command, log, cwd=root, timeout=120):
    with log.open("w") as stream:
        return subprocess.run(command, cwd=cwd, stdout=stream, stderr=subprocess.STDOUT, timeout=timeout)
try:
    with tempfile.TemporaryDirectory(prefix="ft-next-mutations-") as scratch:
        for name, path, before, after in mutations:
            original = saved[path].decode()
            assert original.count(before) == 1, (name, "source match changed")
            (root / path).write_text(original.replace(before, after))
            assert run(["npm", "run", "build", "--workspace", "packages/coding-agent"], out / (name + "-build.txt")).returncode == 0
            binary_dir = Path(scratch) / name
            command = ["bash", "scripts/build-binaries.sh", "--skip-install", "--skip-build", "--platform", "darwin-arm64", "--out", str(binary_dir)]
            assert run(command, out / (name + "-binary.txt")).returncode == 0
            binary = binary_dir / "darwin-arm64/pi"
            digest = hashlib.sha256(binary.read_bytes()).hexdigest()
            result = run(["python3", str(host / "scripts/qualify-pi-startup.py"), "--binary", str(binary),
                          "--sha256", digest, "--source-revision", revision + "+mutation:" + name,
                          "--report", str(out / (name + ".json"))], out / (name + "-qualification.txt"), cwd=host, timeout=180)
            report = json.loads((out / (name + ".json")).read_text())
            failed = {case.get("name", case.get("case", str(index))): [key for key, value in case.get("checks", {}).items() if value is False]
                      for index, case in enumerate(report["cases"]) if not case.get("passed", True)}
            assert result.returncode != 0 and not report["passed"], (name, "regression unexpectedly accepted")
            records.append({"mutation": name, "caught": True, "sha256": digest, "failedCases": failed})
            (root / path).write_bytes(saved[path])
finally:
    for path, content in saved.items():
        (root / path).write_bytes(content)
    restored = run(["npm", "run", "build", "--workspace", "packages/coding-agent"], out / "restored-build.txt")
    assert restored.returncode == 0
    assert subprocess.check_output(["git", "-C", str(root), "status", "--porcelain"], text=True) == ""
(out / "runtime-mutations.json").write_text(json.dumps(records, indent=2) + "\n")
print(json.dumps(records, indent=2))
