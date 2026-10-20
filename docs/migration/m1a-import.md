# M1a: token and cache meter source import

Implemented locally on 2026-10-20 under
[#200](https://github.com/psmfd/FingerTrap/issues/200) and
[ADR-0034](../../adrs/0034-own-agent-runtime-and-retire-pi-config.md).
The first source slice is complete; the wider M1 migration remains open.
The package is not wired into app startup, Pi discovery, or installation.

## Imported scope and review

All 20 files in [first-import.json](first-import.json) were exported as exact
Git blobs from pi_config commit
`9862f35a50f5f0d0770d90665cac1a4b8d3f0947`. The source working tree, Git history,
live configuration, sessions, and ignored artifacts were not copied.

The package contains the two extensions, all six inherited test files, their
TypeScript configurations, the tier map, and the original MIT license.
Every selected file was reviewed for public content and provenance. Source
READMEs were rewritten to remove obsolete installer/helper instructions and
private relative documentation links. Fixture provider/model names are test
data; the provider tier map is retained configuration, not operator credentials.

[Package provenance](../../packages/agent-runtime/provenance.json) records each
source path/blob, destination SHA-256, and local transformation:

- Ten files remain byte-identical to their source blobs.
- Five implementation files have comment-only changes: clarify the source ADRs,
  omitted helpers, legacy paths, and limits of log/append guarantees.
- Two README files now describe this inactive package and its remaining gates.
- Two test files have portability changes: compare paths with `path.join`, and
  isolate Windows `USERPROFILE` alongside Unix `HOME`.
- One test file loses a trailing blank line to pass new-file whitespace checks;
  its assertions are unchanged.

All eight implementation TypeScript files have identical non-comment token
streams to the source. Record schemas, aggregation, settings precedence,
enablement, tool registration, environment propagation, and storage defaults
are unchanged.

## Package and maintenance

`packages/agent-runtime/` has independent package metadata, a pnpm lockfile,
strict type checking, the retained type-aware/security lint policy, an isolated
test runner, and a provenance verifier. It is private and declares no Pi package
registration, application entry point, or installer integration.

Direct tool/API versions retain the source baseline. The npm
`@earendil-works/pi-coding-agent@0.85.1` dependency is for development types;
the extension imports are type-only. TypeBox is the sole declared production
dependency. Installing SDK development types does not select an SDK backend
or qualify equivalence to the patched executable.

Dependency lifecycle scripts are disabled in the package-local `.npmrc`.
The old home-cache install/symlink helper is absent. The authored package is
explicitly exempted from the repository's NuGet `packages/` ignore rule.
CI now includes package validation on Linux, Windows, and macOS using Node 22.
Dependabot tracks the independent package and keeps Pi compatibility changes
separate from tooling groups.

## Verification

Local environment: macOS ARM64, Node `24.17.0`, pnpm `10.34.5`.

| Check | Result |
| --- | --- |
| Source blob identities | All 20 match the recorded selection |
| Source-to-destination implementation comparison | Eight TypeScript implementations retain identical non-comment tokens |
| Provenance verification | All 20 imported file hashes pass; a modified-file probe correctly fails |
| Focused tests | 41 passed, 0 failed, 0 skipped across all six inherited files |
| Strict typecheck and lint | Passed; zero lint findings |
| Independent restore | Fresh package-only copy restores with `--frozen-lockfile --offline --ignore-scripts` from the downloaded store; no private checkout, source node_modules, or network resolution required |
| Independent package validation | Provenance, typecheck, lint, and all 41 tests pass in the fresh copy |
| Environment isolation probe | Synthetic operator settings/logs remain unchanged despite parent enablement variables; `NODE_OPTIONS` preload does not reach test children; test scratch is cleaned |
| Secret scans | Gitleaks `8.30.1` default rules find no leaks in the exact source export or final package staging tree; no custom suppressions |
| Workflow validation | `actionlint` passes for the modified CI workflow |

The retained tsx version emits a `module.register()` deprecation warning under
local Node 24. Tests still pass; no warning suppression was added. Dependency
installation also reports the transitive `node-domexception@1.0.0` deprecation.
Hosted Node 22/Linux/Windows/macOS jobs are configured but have not run for
this uncommitted change. This report does not claim full application tests,
live provider measurements, packaged-runtime qualification, or release readiness.

## Next work

Continue [#200](https://github.com/psmfd/FingerTrap/issues/200) with reviewed
shared/vendor/test closures for the next component groups, including the
preserved subagent correctness fix before migrated dispatch is enabled.
Establish the common profile contract in
[#201](https://github.com/psmfd/FingerTrap/issues/201) before activating either
meter: their entry points still use legacy home paths, and their log readers
do not yet provide the bounded native-dashboard contract.

Native dashboards remain [#157](https://github.com/psmfd/FingerTrap/issues/157).
Executable/RPC and the Pi runtime pin remain unchanged. The separate SDK
evaluation stays in [#202](https://github.com/psmfd/FingerTrap/issues/202).
