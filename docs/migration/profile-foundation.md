# S1: managed-profile contract foundation

Implemented locally on 2026-10-20 under the approved #201/#200 plan.
[ADR-0035](../../adrs/0035-version-managed-runtime-profiles.md) records the
contract. This is a foundation, not an isolated managed launcher.

## Implemented behavior

- Settings v1 remains compatible. Settings v2 requires an explicit Pi mode.
  Managed selection requires bounded profile/runtime identifiers, rejects
  `pi.path`, and cannot be expressed in v1. Existing v1-only builds reject a
  managed v2 document instead of ignoring its selectors.
- The shared executable resolver rejects unactivated managed selection before
  request, settings, environment or PATH fallback. Both Pi PTY and RPC callers
  use this resolver. Explicit user-managed resolution retains its precedence.
- C# and TypeScript validate the same profile definition: version, profile ID,
  runtime ID and non-secret credential references. Unknown fields, invalid
  types, path-like identifiers, reserved device names and duplicate providers
  fail. Definitions cannot override storage roots.
- Both languages derive identical relative layouts from an explicit absolute
  application-data directory. Resolution creates no files, changes no process
  environment and supplies no credentials.
- A common 25-case JSON fixture set specifies validation and path layout.
  Tests also cover independent roots, absent/relative roots, reference count
  limits and fully qualified Windows paths.

The TypeScript helper is authored here, not imported from pi_config. Imported
meter files and their provenance hashes remain unchanged. The helper is not
yet connected to their entry points; that belongs to S4.

## Verification

Local host: macOS ARM64; .NET SDK 10.0.301, Node 24.17.0, pnpm 10.34.5.

| Check | Result |
| --- | --- |
| Runtime package | Provenance 20/20, lint and strict typecheck passed |
| Runtime tests | 69 passed, zero failed/skipped; includes all 41 inherited meter cases |
| Sidecar build | Release, no restore, single worker/shared compilation disabled: zero warnings/errors |
| Sidecar suite | 359 passed, 12 platform/opt-in skips, zero failures |
| Dependency reproducibility | `dotnet restore src-sidecar --locked-mode` passed with vulnerability auditing enabled |
| Repository checks | Zero errors/warnings; whitespace check passed |
| Managed fallback regression | Disabled only the new resolver condition: both requested-path and ambient-path tests failed with “No exception was thrown”; original source restored byte-for-byte and full suite passed |

The first test run exposed a test-only comparison of Node's special
`process.env` object against a plain object. Comparing two plain snapshots
corrected the assertion. The existing tsx/Node 24 deprecation remains visible.

Baseline hosted CI at `6a999c9` passed the new runtime-package matrix,
UI matrix, structural checks and CodeQL. Sidecar/Tauri restores failed on
the pre-existing MessagePack 2.5.302 advisory. The bounded dependency repair
pins 2.5.303, the upstream patched 2.x version, and updates only its affected
locks. Hosted results for the foundation/repair must be checked separately;
baseline results do not certify this newer source.

## Remaining gates

- S2: generalized import manifests, complete file accounting and explicit
  suite registration. The runner currently adds the profile test explicitly
  alongside the original meter suites.
- S3: verified runtime artifact selection, complete environment replacement
  through PTY native code/RPC, actual discovery control and child propagation.
- S4: filesystem/canonical-path checks, session/resume/store integration,
  credential boundary and meter entry-point adaptation.
- S5/S6: all retained source closures, pending subagent fixes and exact-runtime
  integration evidence. No production login, installation, SDK selection,
  native approval or managed activation is claimed.

See [STATUS.md](STATUS.md) for the latest checkpoint, CI and next action.
