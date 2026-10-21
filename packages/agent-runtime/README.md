# FingerTrap agent runtime

Private TypeScript package for source migrating from pi_config under
[ADR-0034](../../adrs/0034-own-agent-runtime-and-retire-pi-config.md).
M1a contains token-meter and cache-meter. The app does not load this package,
and it has no Pi package registration, entry point, or installer integration.
Managed activation waits for [#201](https://github.com/psmfd/FingerTrap/issues/201)
and runtime qualification.
The authored [profile contract helper](runtime/profile.ts) validates profile
definitions and projects paths under ADR-0035. It does not yet redirect the
meter entry points or enable a managed launcher.

## Develop and verify

Use Node.js 22.19+ and pnpm 10.34.5. From this directory:

```sh
pnpm install --frozen-lockfile
pnpm check:provenance
pnpm lint
pnpm typecheck
pnpm test
```

The package-local `.npmrc` disables dependency lifecycle scripts. Dependencies
and integrity hashes are recorded in `pnpm-lock.yaml`; no global install,
source-repo node_modules link, or home-cache helper is used.
`@earendil-works/pi-coding-agent@0.85.1` supplies **development types only**.
The runtime imports TypeBox and Node builtins; it does not create an SDK
session. These npm types do not establish equivalence with the patched Pi
executable or change FingerTrap's `v0.85.1-psmfd.1` runtime selection.

`pnpm test` runs the suites explicitly registered in
[import-slices.json](import-slices.json): all six inherited `node:test` files,
the shared profile contract tests and the manifest-verifier regression tests.
It validates file/suite completeness before starting child processes with
an isolated home and temporary storage. Provider credentials, Pi configuration,
meter environment variables, and `NODE_OPTIONS` are not inherited. Temporary
files are removed after the run, including scratch files from inherited tests.
This is test isolation, not a security sandbox for arbitrary extensions.

## Components and contracts

| Component | Retained behavior | Remaining activation gate |
| --- | --- | --- |
| [token-meter](extensions/token-meter/README.md) | Per-session JSONL usage, model/provider/tier/policy aggregation, enabled-only child environment propagation, optional UI status and `token_usage` | Replace legacy settings/session paths through the common profile contract |
| [cache-meter](extensions/cache-meter/README.md) | Config-gated observational JSONL recorder, provider attribution, optional status | Replace legacy log path and qualify child environment propagation |
| [profile contract](runtime/profile.ts) | Validated definition, explicit root, common C#/TypeScript fixtures | Launcher verification and all-consumer integration; no filesystem authority from path projection alone |

The recorder logic, schemas and defaults retain the source behavior. Both
extensions still construct legacy `~/.pi/agent` paths when no storage root is
supplied; installing the package does not make them managed-profile-safe.
Native dashboards, the legacy shell analysis helpers, prefill-meter, subagent
hosting, and SDK-worker adoption are outside this import.

## Provenance and changes

[import-slices.json](import-slices.json) is the reviewed per-slice allowlist:
source identities, exact files/blobs, inherited suites and authored suites.
[provenance.json](provenance.json) schema v2 records each selected file's
slice/source identity, local transformations, destination SHA-256 and any
separately reviewed patch-source identities. It also inventories authored files.
There are currently 20 imported files in the original M1a slice.
The [candidate manifest](../../docs/migration/first-import.json) remains the
original selection. `pnpm check:provenance` verifies imported file integrity
without requiring private repository access; it is not a source attestation.
When editing an imported file, review and update its transformation description
and digest in the same change.

The verifier checks every package file outside root `node_modules` against
the imported/authored inventory. It rejects missing/unaccounted files,
duplicate or escaped paths, symlinks, inconsistent source/patch identities
and omitted test suites. Tests currently use `.test.ts` or `.test.mjs`; another
`.test.*` suffix requires an explicit runner change. Installed dependencies
are verified by the frozen pnpm lock, not by this source-file inventory.
Adding a slice requires its actual dependency/test closure and activation
disposition to be reviewed in the migration records before changing these
manifests. Source import does not register extensions with Pi.

The original MIT notice is retained in [LICENSE](LICENSE). Extension READMEs
were rewritten for this package; implementation comments now identify legacy
contracts. Two tests received platform-portability changes without changing
their behavioral assertions. Package tooling and CI integration are authored
in FingerTrap. Unqualified historical issue/ADR numbers in imported comments
refer to pi_config, not FingerTrap.

Content review and secret scanning are recorded in the
[M1a import report](../../docs/migration/m1a-import.md). Passing package checks
does not qualify a managed runtime, provider login, native approval, or a
published distribution. The remaining migration work stays in
[#200](https://github.com/psmfd/FingerTrap/issues/200).
