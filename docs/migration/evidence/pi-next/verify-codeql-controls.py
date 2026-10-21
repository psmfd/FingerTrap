#!/usr/bin/env python3
"""Run retained Pi security regressions on before/after source, restoring files.

Use an isolated candidate checkout with Node 22 on PATH and installed workspace
dependencies. Do not run alongside any other command using that checkout.
"""
import argparse
import json
from pathlib import Path
import subprocess

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--checkout", required=True, type=Path)
parser.add_argument("--output", required=True, type=Path)
args = parser.parse_args()
root = args.checkout.resolve()
out = args.output.resolve()
out.mkdir(parents=True, exist_ok=True)
paths = ["scripts/model-catalog-protocol.ts", "scripts/issues/main.ts",
         "packages/durable/src/harness/json.ts", "packages/coding-agent/src/core/resolve-config-value.ts"]
saved = {path: (root / path).read_bytes() for path in paths}
commands = [("security", root, ["node", "--test", ".psmfd/test/security-1.3.test.mjs"]),
            ("credential", root / "packages/coding-agent", ["node", str(root / "node_modules/vitest/dist/cli.js"),
                                                            "--run", "test/resolve-config-value.test.ts"])]
result = {}
try:
    for path in paths:
        (root / path).write_bytes(subprocess.check_output(["git", "-C", str(root), "show", "c7d2a827d7956fcdd8e2a300b5096f6b263dc579:" + path]))
    for name, cwd, command in commands:
        run = subprocess.run(command, cwd=cwd, capture_output=True, text=True, timeout=30)
        (out / (name + "-red.txt")).write_text(run.stdout + run.stderr)
        assert run.returncode != 0, (name, "negative control unexpectedly passed")
        result[name + "Red"] = run.returncode
finally:
    for path, content in saved.items():
        (root / path).write_bytes(content)
for name, cwd, command in commands:
    run = subprocess.run(command, cwd=cwd, capture_output=True, text=True, timeout=30)
    (out / (name + "-green.txt")).write_text(run.stdout + run.stderr)
    assert run.returncode == 0, (name, run.stdout, run.stderr)
    result[name + "Green"] = run.returncode
result["sourceRestored"] = all((root / path).read_bytes() == content for path, content in saved.items())
(out / "codeql-controls.json").write_text(json.dumps(result, indent=2) + "\n")
print(json.dumps(result))
