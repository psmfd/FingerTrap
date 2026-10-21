#!/usr/bin/env python3
"""Reproduce the pinned CLI's pre-trust project migration using synthetic state.

Uses only a digest-verified local release archive, a temporary extraction and
an explicit keyless environment. No provider request, install or operator-state
access is needed. Exit 0 means the known blocker was reproduced, not qualified.
"""

import argparse
import hashlib
import json
import os
from pathlib import Path
import platform
import subprocess
import sys
import tarfile
import tempfile


ARCHIVE_SHA256 = "e0e0331d478af7724fa24d780a8d4abc0033c6f5ee7c0d45dddae6f922e5c4cb"


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--archive", required=True, type=Path)
    parser.add_argument("--sidecar", type=Path, help="also run the existing PTY smoke against this published sidecar")
    args = parser.parse_args()
    if platform.system() != "Darwin" or platform.machine() != "arm64":
        raise SystemExit("This retained exact-artifact probe requires macOS ARM64.")
    if digest(args.archive) != ARCHIVE_SHA256:
        raise SystemExit("Refusing archive: digest differs from the pinned release asset.")

    with tempfile.TemporaryDirectory(prefix="ft-s3-pi-") as scratch:
        root = Path(scratch).resolve()
        unpacked = root / "runtime"
        unpacked.mkdir()
        with tarfile.open(args.archive) as archive:
            archive.extractall(unpacked, filter="data")
        binary = unpacked / "pi" / "pi"
        home, agent, project, temp = [root / name for name in ("home", "agent", "project", "tmp")]
        for directory in (home, agent, project, temp):
            directory.mkdir(mode=0o700)
        commands = project / ".pi" / "commands"
        commands.mkdir(parents=True)
        original = commands / "synthetic.md"
        original.write_text("Synthetic migration sentinel; no operator data.\n")
        before = digest(original)
        operator_file = home / ".pi" / "agent" / "settings.json"
        operator_file.parent.mkdir(parents=True)
        operator_file.write_text('{"syntheticOperatorSentinel": true}\n')
        operator_before = digest(operator_file)
        environment = {
            "HOME": str(home), "USERPROFILE": str(home), "PATH": "/usr/bin:/bin",
            "XDG_CONFIG_HOME": str(home / "config"), "XDG_DATA_HOME": str(home / "data"),
            "XDG_CACHE_HOME": str(home / "cache"), "XDG_STATE_HOME": str(home / "state"),
            "TMPDIR": str(temp), "TMP": str(temp), "TEMP": str(temp), "LANG": "en_US.UTF-8",
            "PI_CODING_AGENT_DIR": str(agent),
            "PI_CODING_AGENT_SESSION_DIR": str(agent / "sessions"),
            "PI_OFFLINE": "1",
        }
        version = subprocess.run(
            [str(binary), "--version"], cwd=project, env=environment,
            capture_output=True, text=True, timeout=15, check=True,
        ).stdout.strip()
        if version != "0.85.1":
            raise SystemExit(f"Unexpected version in verified release: {version!r}")
        arguments = [
            "--mode", "rpc", "--no-extensions", "--no-skills", "--no-prompt-templates",
            "--no-themes", "--no-context-files", "--no-approve", "--offline",
            "--session-dir", str(agent / "sessions"), "--system-prompt", "Synthetic launch probe.",
        ]
        child = subprocess.Popen(
            [str(binary), *arguments], cwd=project, env=environment,
            stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
            text=True, start_new_session=True,
        )
        try:
            stdout, stderr = child.communicate(input="", timeout=20)
        except subprocess.TimeoutExpired:
            os.killpg(child.pid, 9)
            child.communicate()
            raise SystemExit("Pinned runtime did not stop within the probe deadline.")
        migrated = project / ".pi" / "prompts" / "synthetic.md"
        reproduced = not original.exists() and migrated.exists() and digest(migrated) == before
        operator_unchanged = digest(operator_file) == operator_before
        report = {
            "release": "v0.85.1-psmfd.1", "sourceRevision": "e7d09c9dc5fca409f46a5476669e3c0404054d69",
            "archiveSha256": ARCHIVE_SHA256, "executableSha256": digest(binary),
            "reportedVersion": version, "exitCode": child.returncode,
            "projectCommandsRenamedDespiteDenyTrust": reproduced,
            "syntheticOperatorFileUnchanged": operator_unchanged,
            "helloObserved": any('"type":"hello"' in line.replace(" ", "") for line in stdout.splitlines()),
            "stderrBytes": len(stderr.encode()),
            "qualification": "blocked" if reproduced else "unexpected-result-review-required",
        }
        if args.sidecar:
            smoke_environment = dict(environment, FINGERTRAP_PI=str(binary), SHELL="/bin/sh")
            smoke = subprocess.run(
                [sys.executable, str(Path(__file__).with_name("smoke-pty.py")),
                 "--sidecar", str(args.sidecar.resolve()), "--require-pi"],
                cwd=root, env=smoke_environment, capture_output=True, text=True, timeout=90,
            )
            report["publishedSidecarSmokeExitCode"] = smoke.returncode
            if smoke.returncode:
                print(smoke.stdout, file=sys.stderr)
                print(smoke.stderr, file=sys.stderr)
        print(json.dumps(report, indent=2))
        if not reproduced or not operator_unchanged:
            raise SystemExit("Probe did not reproduce the expected bounded fixture behavior.")
        if args.sidecar and smoke.returncode:
            raise SystemExit("Published sidecar smoke failed.")


if __name__ == "__main__":
    main()
