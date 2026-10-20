#!/usr/bin/env python3
"""Verify replacement semantics in a published Unix PTY companion."""

import argparse
import ctypes
import errno
import hashlib
import json
import os
from pathlib import Path
import select
import signal
import tempfile
import time


class SpawnResult(ctypes.Structure):
    _fields_ = [("master_fd", ctypes.c_int), ("pid", ctypes.c_int), ("error", ctypes.c_int)]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--library", required=True, type=Path)
    args = parser.parse_args()
    library_path = args.library.resolve(strict=True)
    library = ctypes.CDLL(str(library_path))
    spawn = library.pty_spawn_replace
    pointer = ctypes.POINTER(ctypes.c_char_p)
    spawn.argtypes = [ctypes.c_char_p, pointer, pointer, ctypes.c_char_p, ctypes.c_void_p, ctypes.c_void_p]
    spawn.restype = SpawnResult
    executable = b"/usr/bin/env"
    argv = (ctypes.c_char_p * 2)(executable, None)
    results = []
    with tempfile.TemporaryDirectory(prefix="ft-native-probe-") as scratch:
        for supplied in ([], [b"FT_PUBLISHED_PROBE=isolated", b"FT_LITERAL_EMPTY="]):
            env = (ctypes.c_char_p * (len(supplied) + 1))(*supplied, None)
            result = spawn(executable, argv, env, os.fsencode(scratch), None, None)
            if result.pid < 0:
                raise SystemExit(f"Native replacement spawn failed with errno {result.error}.")
            try:
                output = b""
                deadline = time.monotonic() + 5
                while time.monotonic() < deadline:
                    ready, _, _ = select.select([result.master_fd], [], [], 0.1)
                    if not ready:
                        continue
                    try:
                        chunk = os.read(result.master_fd, 8192)
                    except OSError as error:
                        if error.errno == errno.EIO:
                            break
                        raise
                    if not chunk:
                        break
                    output += chunk
                    if len(output) > 65536:
                        raise SystemExit("Native environment output exceeded the probe bound.")
                else:
                    raise SystemExit("Native replacement probe timed out.")
                # Never print unexpected environment output: it might contain inherited secrets.
                if set(output.splitlines()) != set(supplied):
                    raise SystemExit("Published native companion failed exact environment replacement.")
                results.append({"suppliedEntries": len(supplied), "exactEnvironmentObserved": True})
            finally:
                os.close(result.master_fd)
                try:
                    os.kill(result.pid, signal.SIGKILL)
                except ProcessLookupError:
                    pass
                os.waitpid(result.pid, 0)
    with library_path.open("rb") as stream:
        digest = hashlib.file_digest(stream, "sha256").hexdigest()
    print(json.dumps({"companionSha256": digest, "cases": results}, indent=2))


if __name__ == "__main__":
    main()
