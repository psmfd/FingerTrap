# S3 launch-isolation foundation

Implementation of the approved S3-A–F plan, 2026-10-20. The independently
verifiable host/transport foundation is implemented and verified locally.
Hosted checks are pending. **S3 discovery qualification is blocked** by the
exact pinned CLI's pre-trust project migrations. Production managed activation
remains unavailable; #200/#201 remain open.

## Implemented behavior

- ADR-0036 defines a shared, immutable host-selected Pi launch context.
  User-managed mode preserves executable precedence and inherited environment;
  shell panes remain login shells. Production supplies no managed selector.
- Descriptor v1 verifies a separately trusted descriptor digest, revision,
  platform, architecture, executable, explicit extensions and complete file
  inventory. Bounds: 1 MiB descriptor, 8192 files, 64 extensions, 512 MiB per
  file and 4 GiB aggregate declared file size. Hashing checks cancellation.
- Profile preparation rejects links and other-user-writable Unix directories;
  artifact paths reject links, escapes, aliases, absent/unlisted files and
  changed digests. Host roots are canonicalized, including macOS path aliases.
- Managed fixtures reject raw executable/environment overrides, resume,
  unresolved credential references, project `.pi` state and profile startup
  configuration. This restricted fixture is not arbitrary-project support.
- Replacement environments contain only explicit profile/platform values.
  No host keys, proxies, loader hooks or agent sockets are copied. Empty
  values remain literal values; no tool selection omits PATH rather than
  setting the unsafe empty Unix search path.
- RPC clears its inherited environment in replacement mode. Porta.Pty copies
  caller options, bypasses inherited/platform merges, and uses new Unix
  `pty_spawn_replace` / absolute `execve`. The original native ABI remains.
  Missing replacement support fails closed with a matching-companion error.
- Native replacement reports exec/chdir failures over a close-on-exec pipe,
  uses a monotonic bounded wait, and preserves Darwin coordination and master
  FD_CLOEXEC. Windows uses an explicit Unicode block and absolute application
  path, including correct empty-block termination.
- Both pane services consume the same resolver. Existing supervision,
  bounds, backpressure and shutdown behavior remain covered by the suite.

## Local validation

- Audited locked restore passed. Release build: zero warnings/errors.
- Sidecar suite: **401 passed, 12 existing skips, zero failures**.
  New coverage includes real RPC and native PTY observations, literal-empty
  and Unicode values, inherited compatibility, concurrent profiles in the same
  cwd, controlled grandchildren, sibling survival, canceled spawn, descriptor
  bounds/identity/digest failures, request rejection and production gates.
- A separate test process loaded a compiled old-ABI native stub and verified
  refusal to fall back to inherited spawning.
- C shim passed `clang -Wall -Wextra -Werror -fsyntax-only`.
- Structural checks, Markdown lint and whitespace checks passed.
- Self-contained macOS ARM64 publish passed. The existing PTY smoke passed
  against that published sidecar, with the digest-verified Pi release in a
  temporary keyless environment. RID lock changes were restored; subsequent
  audited locked restore passed and tracked locks are unchanged.
- Direct published-companion probe passed exact empty and two-entry
  replacement, including a literal empty value. Companion SHA-256:
  `4644ce3598bf59a4f1999e4e42b1312586ce9a00a7fa33059217308707f1b4a0`.

The FakePi process fixtures assemble an app-local runtime from the test
runner's installed .NET runtime and inventory/hash that closure. This avoids
an ambient DOTNET_ROOT dependency without weakening the production policy or
changing package dependencies.

## Regression evidence

Each deliberate regression was built and tested, then the exact original
source bytes were restored. The final clean suite passed afterward.

| Deliberate regression | Observed result |
| --- | --- |
| Remove RPC environment clearing | 2 RPC environment cases failed; 2 PTY cases passed |
| Replace native replacement execve with inherited execvp | 2 PTY environment cases failed; 2 RPC cases passed |
| Omit runtime file-digest verification | Tampered-artifact case failed; 6 other closure cases passed |
| Disable managed resolver gate | Both explicit/ambient fallback guard cases failed |

## Exact Pi evidence and remaining gates

The [discovery audit](s3-discovery-audit.md) records the actual pinned
release archive/executable digests and filesystem reproduction: project
`.pi/commands` is renamed despite deny-trust, offline and resource-disable
flags. This prevents claiming complete S3 arbitrary-project qualification.
The retained probe exits successfully when it reproduces this blocker.

```sh
python3 scripts/probe-pi-launch-isolation.py \
  --archive /path/to/pi-darwin-arm64-v0.85.1-psmfd.1.tar.gz

python3 scripts/probe-pi-launch-isolation.py \
  --archive /path/to/pi-darwin-arm64-v0.85.1-psmfd.1.tar.gz \
  --sidecar /path/to/published/fingertrap-sidecar

python3 scripts/probe-native-environment.py \
  --library /path/to/published/libporta_pty.dylib
```

Hosted Windows/Linux/macOS checks and Apple Silicon package results must be
recorded against the committed source. Windows-specific behavior is not
certified by local macOS results. Windows ACL/native-known-folder behavior,
arbitrary-project discovery/reload, production artifacts/credentials, S4
stores/resume, S5 dispatchers/tools and S6 assembled-runtime acceptance retain
their gates. Filesystem checks are not an OS sandbox or protection from
malicious same-user filesystem races.
