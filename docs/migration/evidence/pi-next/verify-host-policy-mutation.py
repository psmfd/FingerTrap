#!/usr/bin/env python3
"""Prove the host descriptor policy check rejects a regression; restore and test.

Run from the FingerTrap root after building dependencies. Uses local test
fixtures only. Restores the exact source bytes even if the negative check fails.
"""
import argparse
from pathlib import Path
import subprocess

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--output", required=True, type=Path)
args = parser.parse_args()
root = Path(__file__).resolve().parents[4]
out = args.output.resolve()
out.mkdir(parents=True, exist_ok=True)
path = root / "src-sidecar/src/FingerTrap.Sidecar/Runtime/RuntimeArtifact.cs"
before = path.read_bytes()
removed = '            || definition.GetProperty("startupPolicy").GetString() != StartupPolicy\n'
assert before.decode().count(removed) == 1
command = ["dotnet", "test", "src-sidecar", "-c", "Release", "--no-restore", "--disable-build-servers",
           "-m:1", "-p:UseSharedCompilation=false"]
try:
    path.write_text(before.decode().replace(removed, ""))
    with (out / "host-policy-red.txt").open("w") as log:
        result = subprocess.run(command + ["--", "--filter-method", "*Resolve_InvalidDescriptor*"],
                                cwd=root, stdout=log, stderr=subprocess.STDOUT, timeout=120)
    detail = root / "src-sidecar/tests/FingerTrap.Sidecar.Tests/bin/Release/net10.0/TestResults/FingerTrap.Sidecar.Tests_net10.0_arm64.log"
    raw = detail.read_bytes()
    text = raw.decode("utf-16" if raw.startswith((b"\xff\xfe", b"\xfe\xff")) else "utf-8")
    (out / "host-policy-red-detail.txt").write_text(text)
    assert result.returncode != 0 and 'startupPolicy' in text and 'managed-v2' in text
finally:
    path.write_bytes(before)
with (out / "host-policy-restored-tests.txt").open("w") as log:
    result = subprocess.run(command, cwd=root, stdout=log, stderr=subprocess.STDOUT, timeout=180)
assert result.returncode == 0
assert path.read_bytes() == before
print("host policy regression rejected; exact source restored; full sidecar suite passes")
