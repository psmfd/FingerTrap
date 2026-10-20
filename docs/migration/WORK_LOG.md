# Runtime migration activity log

Append meaningful activity in chronological order. Each entry identifies
the slice, change, observed verification, blockers and next action.
Keep private logs, credentials and operator configuration out of this file.
The compact [status board](STATUS.md) is the starting point for a new session.

## 2026-10-20 14:13 UTC — approval and durable tracking

- **Scope:** maintainer approved the HTML plan and requested continuity across
  failures/sessions. Working issue pair: #201 and #200, as disclosed in the
  approved plan; #210 lookup was unsuccessful.
- **Existing work:** accepted ADR-0034, M0 inventory/successors and inactive
  M1a package remain uncommitted on `docs/pi-runtime-migration`, based on
  `8ffeae809f7b4579d6b4f006c3f6e8414df60620`.
- **Action:** add `STATUS.md`, this activity log and root `AGENTS.md` resume
  instructions; record plan approval; prepare scoped source checkpoints.
- **Evidence:** inspected current worktree and package/sidecar settings.
  Earlier M1a verification is preserved in `m1a-import.md`; no new runtime
  results are claimed by this entry.
- **Important implementation findings:** RPC environment is additive;
  Porta.Pty merges inherited variables in managed code and its Unix shim
  applies overrides without replacement. Profile isolation must cover both.
  Existing settings v1 tolerates unknown fields: managed selection must not
  be silently downgraded by an older executable.
- **Next:** checkpoint reviewed existing work, then implement the versioned
  profile contract and compatibility tests (S1). Hosted CI and remote backup
  remain pending until actual results are recorded.

## 2026-10-20 14:19 UTC — baseline checkpoint verification

- **Slice:** S1, preserve the already completed migration baseline.
- **Checks:** package provenance (20 files), lint, typecheck and all 41 meter
  tests passed again. The existing Node 24/tsx deprecation warning remains.
  Repository checks passed with zero errors/warnings. Gitleaks default rules
  found no leaks in the exact 46-file staged export; no suppressions.
- **Remote identity:** authenticated GitHub read confirms the canonical public
  repository is `psmfd/FingerTrap`; #200 and #201 are still open. No existing
  migration PR appeared in the open-PR read.
- **Checkpoint:** staged reviewed baseline plus durable records; excluded all
  three private/local artifacts. Commit and remote backup follow this entry.
- **Next:** profile contract/fixtures and explicit old-build rejection.

## 2026-10-20 — baseline backed up; S1 contract implementation

- **Recovery checkpoint:** `6a999c9` is pushed to the canonical public
  `psmfd/FingerTrap` repository on `docs/pi-runtime-migration`.
  [Draft PR #207](https://github.com/psmfd/FingerTrap/pull/207) targets `dev`.
- **Contract:** ADR-0035 requires settings v2 for managed selectors so an old
  v1-only executable fails closed. Existing v1/user-managed behavior remains.
  Profile definitions and path layout receive common C#/TypeScript fixtures.
- **Pinned-source audit:** exact fork `e7d09c9…` includes agent/session
  directory overrides and `--no-context-files` alongside other discovery
  flags. This is source evidence, not yet real-process qualification.
- **In progress:** implement and test S1. Managed selection must fail clearly
  before any executable fallback while activation remains unavailable.
- **First validation:** TypeScript lint/typecheck passed. A new environment
  non-mutation assertion compared Node's special `process.env` object with a
  plain snapshot, causing a prototype mismatch; corrected it to compare two
  plain snapshots. This was a test assertion issue, not a path/runtime failure.
  C# build and hosted baseline CI results are still being collected.

## 2026-10-20 — baseline CI evidence and dependency repair

- **Baseline CI:** the Node 22 runtime-package and UI jobs passed on all
  three platforms; repository/workflow checks and CodeQL passed.
  [Run 39439548689](https://github.com/psmfd/FingerTrap/actions/runs/39439548689)
  failed all sidecar/Tauri jobs during restore due to existing
  `MessagePack 2.5.302` (NU1902, treated as error).
- **Cause and bounded repair:** StreamJsonRpc transitively selects that
  version. The upstream [advisory](https://github.com/advisories/GHSA-qhrr-8q5h-9q3h)
  identifies 2.5.303 as the patched 2.x release. Add an explicit 2.5.303
  dependency floor and regenerate only affected RID-agnostic locks; retain
  audit enforcement. Review/test this separately from the profile contract.
- **Local checks:** TypeScript suite now passes all 68 tests. Sandboxed .NET
  build stalled; stopped that process and reran single-worker without shared
  compilation outside the sandbox. Production code compiled; test build
  requires the previously absent FakeMcm assets, now restored from its lock.
- **Next:** restore the patched dependency, complete C# tests and prove the
  managed fallback regression fails its guard test before checkpointing S1.

## 2026-10-20 14:48 UTC — S1 verified locally

- **Implementation:** ADR-0035, v1/v2 settings boundary, managed resolver
  refusal, C#/TypeScript profile validation/path projection and 25 shared
  contract cases. Meter source remains unchanged/inactive.
- **Final local checks:** 69 runtime tests passed; provenance 20/20, lint and
  typecheck passed. Audited locked NuGet restore passed. Sidecar Release
  build had zero warnings/errors; 359 tests passed, 12 skipped, zero failures.
  Repository checks and whitespace checks passed.
- **Regression evidence:** temporarily disabling only the resolver's managed
  guard caused both explicit-path and ambient-path cases to fail because no
  exception was thrown. Restored the source byte-for-byte, rebuilt and reran
  the full green suite. No temporary mutation remains.
- **Durable evidence:** [profile-foundation.md](profile-foundation.md).
- **Next:** separate dependency/S1 checkpoints, remote backup and hosted CI;
  then S2 reviewed import/test manifest generalization.

## 2026-10-20 — S1 checkpoint backed up; begin S2

- **Committed/pushed:** dependency repair `44455f5`; S1 foundation `52fc6bc`.
  PR #207 contains both. Staged S1 secret scan found no leaks; Markdown lint
  passed (one pre-existing bare issue reference was made an explicit link).
- **S2 intent:** introduce reviewed per-slice source/file/test selections and
  authored-test registration. Account for every package file outside installed
  dependencies, reject symlink/path escapes, retain hashes/transformations,
  support multiple source revisions and reject omitted regression suites.
- **Validation planned:** keep all 69 existing tests; add negative verifier
  fixtures for tampering, missing/unaccounted paths, changed source selection
  and omitted tests. Restore/validate a fresh package-only copy.
- **Next:** implement the manifest verifier and update the test runner to
  consume the reviewed registry. No new extension import or activation.
- **First S2 check:** provenance/lint/typecheck passed. One new fixture compared
  `/var` with the verifier's canonical `/private/var` on macOS; canonicalized
  the fixture root so the assertion tests the promised resolved-path contract.
- **Hosted S1 update:** sidecar, runtime-package and UI jobs passed on Linux,
  Windows and macOS after the dependency repair. Tauri/Apple Silicon packaging
  and Rust analysis were still pending when read; no full-green claim yet.

## 2026-10-20 15:01 UTC — S2 verified locally

- **Implementation:** explicit import/test selections; provenance schema v2
  with per-file source identity and patch-source support; full authored/imported
  package accounting; test runner consumes the verified suite registry.
- **Results:** all 84 tests passed (15 new verifier cases); provenance
  20/20, lint/typecheck and JavaScript syntax passed. Eight suites and 35
  package files accounted for. No imported meter implementation changed.
- **Regression proof:** removed only suite-accounting validation; both
  omission tests failed, including the authored-suite missing-exception case.
  Restored original bytes and verified the complete package successfully.
- **Independent restore:** fresh package-only export, offline/frozen restore
  with install scripts disabled, then provenance/lint/typecheck/84 tests all
  passed. The initial default-cache attempt lacked a tarball; using the
  existing populated temporary store succeeded without a network download.
- **Hosted S1 update:** Apple Silicon package/smoke and Tauri macOS/Linux
  passed; Windows Tauri and Rust analysis still pending at last read.
- **Next:** checkpoint S2 and update PR evidence, then S3's explicit
  replacement environment and actual managed launch context.

## 2026-10-20 — S2 remote checkpoint and next-session boundary

- **Committed/pushed:** `92137c4` contains S2 implementation and evidence.
  Default-rule staged secret scan, Markdown lint, structural and whitespace
  checks passed before committing. All task changes have scoped source
  checkpoints; the three pre-existing local/private artifacts remain untouched.
- **S1 final hosted result:** all checks passed, including all platform
  matrices, CodeQL and Apple Silicon package/smoke. This certifies that
  checkpoint's checks, not managed activation or future S2 changes.
- **PR review record:** #207 was updated to describe the final S1/S2
  foundations, separate dependency repair, mutation evidence and remaining
  work. The PR remains a draft against `dev`; #200/#201 remain open.
- **Resume:** read S2's new CI results, then start S3 at the RPC and Porta.Pty
  environment-inheritance boundary. Use `STATUS.md` for commands and gates.
  Approval persists; no additional plan confirmation is needed.

## 2026-10-20 15:20 UTC — S2 hosted checks passed

- **Observed:** every check passed for S2 `92137c4`, including Node runtime,
  sidecar, UI and Tauri on all three platforms, CodeQL, structural checks,
  and Apple Silicon package/smoke.
  [CI run](https://github.com/psmfd/FingerTrap/actions/runs/39445278440);
  [package/smoke](https://github.com/psmfd/FingerTrap/actions/runs/39445278068).
- **Checkpoint boundary:** S1 and S2 are verified and remotely recoverable.
  This final documentation-only update records their actual source hashes
  and hosted results; a new CI run for the bookkeeping commit is distinct.
- **Next action:** S3 environment replacement through RPC, Porta.Pty and its
  Unix shim, followed by verified runtime/resource launch selection. No
  managed activation, issue closure, PR merge or source-repo retirement.
