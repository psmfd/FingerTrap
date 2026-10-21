#!/usr/bin/env python3
"""Record all real-Pi RPC scenarios twice, retaining drift without changing goldens.

Build the Release test assembly first. The recorder uses temporary homes and a
local canned provider; no operator credential environment reaches the tests.
Compare baseline and candidate under the same host/toolchain for path drift.
"""
import argparse
import difflib
import json
from pathlib import Path
import shutil
import subprocess
import tempfile

from pi_startup_fixture import digest


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--binary", required=True, type=Path)
    parser.add_argument("--sha256", required=True)
    parser.add_argument("--source-revision", required=True)
    parser.add_argument("--report-directory", required=True, type=Path)
    args = parser.parse_args()
    binary = args.binary.resolve()
    if binary.name != "pi" or digest(binary) != args.sha256:
        parser.error("Expected digest-selected executable named pi")
    dotnet = shutil.which("dotnet")
    if not dotnet:
        parser.error("dotnet is required")
    root = Path(__file__).resolve().parent.parent
    data = root / "src-sidecar/tests/FingerTrap.Sidecar.Tests/Goldens/data"
    before = {path: path.read_bytes() for path in sorted(data.glob("*.golden.jsonl"))}
    if len(before) != 12:
        parser.error("Expected the reviewed twelve-scenario inventory")
    out = args.report_directory.resolve()
    out.mkdir(parents=True, exist_ok=True)
    (out / "recordings").mkdir(exist_ok=True)
    (out / "raw").mkdir(exist_ok=True)
    report = {"sourceRevision": args.source_revision, "binarySha256": args.sha256,
              "twicePerScenario": True, "passed": False, "scenarios": []}
    try:
        with tempfile.TemporaryDirectory(prefix="ft-s3-record-") as scratch:
            home = Path(scratch) / "home"
            home.mkdir()
            env = {"PATH": str(binary.parent) + ":/usr/bin:/bin", "HOME": str(home),
                   "USERPROFILE": str(home), "XDG_CONFIG_HOME": str(home / ".config"),
                   "FT_RECORD_GOLDENS": "1", "PI_OFFLINE": "1", "PI_SKIP_VERSION_CHECK": "1",
                   "FT_RECORD_RAW_DIR": str(out / "raw"),
                   "PI_NO_LOCAL_LLM": "1", "DOTNET_ROOT": str(Path(dotnet).resolve().parent),
                   "DOTNET_GENERATE_ASPNET_CERTIFICATE": "false", "DOTNET_NOLOGO": "1",
                   "DOTNET_CLI_TELEMETRY_OPTOUT": "1", "DOTNET_SKIP_FIRST_TIME_EXPERIENCE": "1",
                   "LANG": "en_US.UTF-8"}
            with (out / "record.log").open("w") as log:
                run = subprocess.run([dotnet, "test", "src-sidecar", "-c", "Release", "--no-build",
                    "--no-restore", "--disable-build-servers", "-m:1", "--", "--filter-method", "*Record_Scenario*"],
                    cwd=root, env=env, stdout=log, stderr=subprocess.STDOUT, timeout=600)
            report["exitCode"] = run.returncode
            patches = []
            for path, original in before.items():
                after = path.read_bytes()
                (out / "recordings" / path.name).write_bytes(after)
                report["scenarios"].append({"file": path.name, "changed": after != original,
                                           "recordingSha256": digest(out / "recordings" / path.name)})
                if after != original:
                    patches.extend(difflib.unified_diff(original.decode().splitlines(keepends=True),
                        after.decode().splitlines(keepends=True), fromfile="committed/" + path.name,
                        tofile="recorded/" + path.name))
            (out / "drift.patch").write_text("".join(patches))
            # Exit zero alone is insufficient if the opt-in suite skipped.
            log_text = (out / "record.log").read_text().lower()
            report["passed"] = run.returncode == 0 and "passed: 12" in log_text and "skipped: 0" in log_text
    finally:
        for path, original in before.items():
            if path.read_bytes() != original:
                path.write_bytes(original)
        report["originalGoldensRestored"] = all(path.read_bytes() == original for path, original in before.items())
        (out / "report.json").write_text(json.dumps(report, indent=2) + "\n")
    print(json.dumps(report, indent=2))
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
