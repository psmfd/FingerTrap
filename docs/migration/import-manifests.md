# S2: reviewed import and test manifests

Implemented locally on 2026-10-20 for #200. This extends the inactive package's
import harness; it imports no additional extension and changes no meter code.

## Records and enforcement

- [import-slices.json](../../packages/agent-runtime/import-slices.json):
  each reviewed slice's repository/commit, exact source paths/blobs,
  destinations and inherited test files; authored tests are registered
  separately. The existing M1a selection still contains exactly 20 files.
- [provenance.json](../../packages/agent-runtime/provenance.json) schema v2:
  per-file slice/source identity, destination digest, transformation notes,
  optional independently reviewed patch sources, and authored-file inventory.
  The original source selection remains in `initial_import`.
- [Shared verifier](../../packages/agent-runtime/scripts/lib/import-manifest.mjs):
  checks agreement between both records and the actual package, including
  every source file outside installed root `node_modules`.
- [Test runner](../../packages/agent-runtime/scripts/test.mjs):
  verifies the package first, then runs the exact registered suites in the
  existing isolated child environment and temporary storage.

Missing/unaccounted files, case aliases, duplicate paths, path escapes,
symlinks, inconsistent source/patch identities and omitted tests fail.
Changed imports require updated digests and transformation notes; claiming
unchanged content additionally requires its Git blob hash to match.
Unknown `.test.*` suffixes require an explicit runner change.

The package currently accounts for 35 files: 20 imported, 14 authored and
the provenance record itself. Its eight registered suites comprise the six
inherited meter suites, profile contract tests and import-verifier tests.
The manifests are reviewed source records, not proof of provenance from a
remote authority. Actual source export/license/content review remains required
before adding a slice.

## Verification

- Provenance: 20/20 imported files; one reviewed slice; all eight suites.
- Strict TypeScript lint/typecheck and JavaScript syntax checks passed.
- 84 tests passed, zero failed/skipped, including 15 verifier regression
  cases and all previous 69 meter/profile cases.
- Negative fixtures cover altered bytes/digests, changed source identity,
  missing and unaccounted files, omitted inherited/authored suites, additional
  source revisions, unsupported test names, duplicate/case-alias paths,
  traversal/symlinks, and unreviewed patch provenance.
- Mutation probe: removing only the suite-accounting assertion caused both
  omitted-suite tests to fail; the authored-suite case exposed a missing
  expected rejection. Restored the verifier byte-for-byte and reran green.
- A fresh package-only export restored offline with `--frozen-lockfile
  --ignore-scripts` from the previously downloaded pnpm store. Provenance,
  lint, typecheck and all 84 tests passed there without pi_config, repository
  docs, an existing node_modules tree or network dependency resolution.
- The first offline attempt used the wrong default cache and reported a
  missing tarball. Pointing to the already populated
  `/private/tmp/ft-m1a-pnpm-store` resolved that precondition without downloads.

Local evidence is macOS ARM64 / Node 24.17.0 / pnpm 10.34.5. The existing
tsx deprecation remains visible. The symlink fixture reports an explicit
skip if a Windows runner lacks symlink privilege. Hosted S2 results are
recorded separately in [STATUS.md](STATUS.md) when observed.

## Next

S3 establishes the actual managed launch context and environment replacement
through RPC, PTY and the Unix native shim. S4 adapts storage/resume/meters.
S5 adds reviewed component closures using these manifests. The package remains
inactive and the executable/runtime pin is unchanged.
