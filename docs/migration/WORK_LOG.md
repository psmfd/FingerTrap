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

## 2026-10-20 16:03 UTC — S3 focused approval plan

- **Request:** the maintainer asked for an S3 implementation plan for approval.
  The overall plan remains approved; this turn prepares the requested focused
  review, with no S3 implementation.
- **Review:** inspected RPC additive environment handling, both PTY inheritance
  layers, native ABI, Windows path fallback, profile/activation contracts and
  the exact pinned Pi startup/settings/resource/auth sources.
- **Finding:** discovery flags alone are insufficient evidence. Package
  resolution occurs before resource selection; startup settings and credential
  resolution also require explicit controls and real-process probes.
- **Planned artifact:** a standalone HTML plan covering staged changes,
  file ownership, native compatibility, acceptance tests, activation boundaries
  and durable checkpoints. Validate document links, navigation and rendering.
- **Next:** finish the document and present it for the requested S3 approval.

## 2026-10-20 16:09 UTC — S3 approval document ready

- **Artifact:** [S3 launch-isolation plan](s3-launch-isolation-plan.html),
  with S3-A through S3-F, proposed host/transport/native contracts, exact-pin
  discovery gate, change map, acceptance matrix and checkpoint discipline.
- **Document validation:** 42 links and 12 unique IDs checked; no external
  assets; JavaScript syntax passed. Headless browser verified 390-pixel
  mobile width without horizontal overflow, readable desktop/mobile sections,
  and current-section navigation. Markdown, whitespace and structural checks
  passed with no errors.
- **Scope:** documentation only. No S3 runtime code, managed activation,
  provider login, dependency change or issue closure.
- **Checkpoint intent:** commit and back up these four documentation files on
  the existing migration branch. Preserve all unrelated/private local files.
- **Resume:** await the requested S3 decision; after approval record it and
  begin S3-A's launch contract and pinned-runtime discovery audit. The broader
  six-step approval remains in effect.

## 2026-10-20 — S3 approved; begin S3-A

- **Authorization:** maintainer approved S3-A through S3-F. Approval persists;
  managed production activation, pin/backend changes and later slices retain
  the plan's separate gates.
- **Previous checkpoint:** `dac31ce` contains the plan and was pushed to the
  existing migration branch/draft PR #207.
- **Active work:** launch ADR/contracts and exact-pin startup/discovery audit,
  followed by verified resolution, RPC and native PTY replacement environments.
- **Validation:** behavioral process probes, artifact/path rejection,
  concurrency, inherited-mode compatibility, gate mutations and hosted checks.

## 2026-10-20 — S3-A finding; launch implementation underway

- **Audit finding:** pinned startup invokes project migrations before trust;
  `.pi/commands` can be renamed despite resource-disable/deny-trust flags.
  ADR-0036 and the discovery matrix record the activation blocker under
  #201/#202. Internal host fixtures reject project `.pi` and profile startup
  config. No pin/backend change.
- **S3-B–E changes:** trusted descriptor/digest inventory, contained profile
  paths, immutable launch contexts; explicit RPC replacement; PTY option
  snapshots and distinct Unix execve ABI with bounded exec-status reporting;
  Windows explicit block/path behavior; common pane-service composition.
- **State:** source edits only, not yet built or verified. Next: first build,
  behavioral tests and isolated exact-runtime migration reproduction.

## 2026-10-20 — S3 first verification and exact-runtime blocker

- **Build:** Release sidecar/native build passed, zero warnings/errors.
- **Exact artifact:** retained probe verified the release archive digest,
  extracted privately and reproduced `.pi/commands` → `.pi/prompts` before
  trust despite all disable flags, deny trust and offline mode. RPC hello
  observed; exit 0; synthetic operator state unchanged. Details in the audit.
- **First tests:** 382 passed, 11 failed, 12 skipped. Five new process probes
  exposed the FakePi apphost's dependency on ambient DOTNET_ROOT; six rejection
  assertions incorrectly treated InvalidDataException as IOException.
- **Correction:** assemble a test-only app-local .NET runtime closure from the
  running runtime, inventory/hash it, and retain truly empty replacement
  environments. Correct the rejection exception assertions. No production
  environment allowance added to accommodate the test toolchain.
- **Next:** rebuild and rerun; then native companion, mutation and packaging
  evidence. Existing activation blocker remains.

## 2026-10-20 — S3 real-process tests passed

- **Results:** Release build passed with zero warnings/errors; 395 sidecar
  tests passed, 12 existing skips, zero failures. Real RPC/PTY replacement
  checks cover empty/literal-empty/Unicode environments, two concurrent profiles
  and their grandchildren, failure rejection and production activation guards.
- **Native compatibility:** an isolated test process loaded a compiled
  old-ABI stub and verified an actionable missing-replacement-export error.
  A first test build referenced the coordinator instead of its process-launch
  adapter; corrected to ChildProcessLauncher and the test passed.
- **Review correction:** omit PATH when no tools are selected; an empty Unix
  PATH would search cwd. Copy host tool selection before resolving contexts.
- **Active:** four controlled mutation checks, restoring source bytes after
  each, followed by clean rebuild, published-companion smoke and remote CI.

## 2026-10-20 17:12 UTC — S3 local verification complete; checkpoint next

- **Final local checks:** audited locked restore; Release build zero warnings/
  errors; 401 tests passed, 12 existing skips, zero failures. Structural,
  Markdown, whitespace and strict native-C compiler checks passed.
- **Mutations:** removing RPC clearing failed two RPC cases; replacing native
  execve with execvp failed two PTY cases; omitting file-digest validation
  failed the tamper case; disabling the managed resolver gate failed both
  legacy-fallback guards. Source restored before the final clean pass.
- **Publish:** self-contained ARM64 sidecar plus native library built; existing
  PTY smoke passed using temporary keyless state and a verified Pi archive.
  Original RID-agnostic locks restored, audited locked restore passed.
- **Published ABI:** direct native probe verified exactly empty and explicit
  two-entry environments, preserving an empty value. Results and digest are
  in the new S3 implementation report.
- **Remaining:** source checkpoint/remote backup, hosted matrices and package
  results. Full S3 discovery qualification remains blocked by the reproduced
  pinned CLI migration behavior. No activation or issue closure.

## 2026-10-20 — S3 checkpoint backed up; hosted platform tests passed

- **Committed/pushed:** `95424b3` contains the S3 foundation, tests, probes,
  ADR-0036, discovery evidence and tracking. Default-rule scan of the 34-file
  review export found no secrets. Unrelated private local artifacts remain
  untouched and untracked.
- **PR #207:** title/body updated for managed profiles and launch isolation,
  with local/mutation/publish evidence and the explicit qualification blocker.
  It remains a draft against `dev`.
- **Hosted observation:** sidecar, runtime-package, UI and Tauri checks passed
  on Windows, macOS and Linux, as did repository/workflow checks and
  actions/C#/JavaScript/Python analysis. Rust analysis and Apple Silicon
  package/smoke were still pending at this observation.
- **Next:** collect those final checks, record exact run/source identities,
  and leave the missing pinned-runtime control as the next decision.

## 2026-10-20 17:52 UTC — Hosted DMG bundling failure; unchanged-source retry

- **Failure:** Apple Silicon run `39469041145`, first attempt, failed in
  `bundle_dmg.sh` after Rust/UI build, native staging and `.app` creation
  succeeded. The available log gives no more specific DMG diagnostic.
  This is not a passing package/smoke result.
- **Action:** requested one rerun of the failed job at unchanged source
  `95424b3`. Do not alter implementation or weaken checks on this evidence.
  If repeated, collect additional bundler diagnostics before choosing a fix.
- **Other results:** all three platform sidecar/runtime/UI/Tauri jobs passed;
  Rust analysis remained pending at the last read. Production stays gated.

## 2026-10-20 — S3 source checks complete; local runner requested

- **Final source result:** Rust analysis passed. Apple Silicon retry job
  `118790720905` in run `39469041145` passed at unchanged `95424b3`, including
  DMG creation, packaged-sidecar smoke and candidate upload. The initial DMG
  failure remains recorded; a retry pass does not establish its root cause.
  Full S3 discovery qualification remains blocked; no managed activation.
- **New direction:** maintainer requested reducing GitHub-hosted builds with
  a local runner, possibly Tart. No additional hosted builds will be started
  during investigation. Existing completed checks are retained.
- **Initial evidence:** Tart 2.32.1 and Softnet 0.19.0 installed; cached macOS
  Sequoia base; existing acceptance VMs stopped and preserved. Host has 18 CPUs,
  128 GiB RAM and approximately 122 GiB free. Repository is public; authorized
  identity has repository administration; no repository runners registered.
- **Next:** determine organization runner-group restrictions and prepare a
  disposable, isolated macOS job VM. Optional question asks whether to move
  macOS CI/package lanes first or begin with manual validation. ADR-0033's
  acceptance-only runner boundary needs an explicit successor.
- **Durability:** these status updates are local and not yet committed or
  pushed. A push to this PR would trigger hosted CI under current workflows.

## 2026-10-20 — Local Tart pilot prepared; activation verification next

- **Implementation:** ADR-0037, runner runbook, dedicated workflow, combined
  macOS validation/package script, exact runner/base preparation, isolated
  single-job controller and server-side group-policy checks prepared locally.
- **Host/guest:** separate base `ft-actions-base-20261020`, 8 CPUs/16 GiB/80 GB
  sparse disk. Guest macOS 15.7.7 / 24G720. Official runner 2.338.0 archive
  checksum verified; installed unregistered in `~/fingertrap-actions-runner`;
  clean base shut down. Existing acceptance VMs preserved.
- **Privilege step:** initial Softnet boot failed because no privileged helper
  was available. With macOS administrator authorization, installed the reviewed
  binary under `/Library/PrivilegedHelperTools/fingertrap-softnet`, root-owned
  mode 4755. Installer and launcher pin its SHA-256. No sudoers/Homebrew changes.
- **Checks:** three policy tests passed (11 invalid-boundary cases); bypassing
  the validator in memory made all 11 cases fail. Shell syntax/ShellCheck,
  Python compilation, structural and Markdown checks passed. Initial actionlint
  required declaring the custom runner label; config added and lint passed.
- **GitHub setup:** authorized identity can administer organization runner
  groups. Creation rejected HTTP 400 because the exact trusted pilot branch
  does not yet exist. No broader group was substituted.
- **Next:** commit and publish the scoped pilot to `ci/tart-runner-pilot`
  without a PR; existing hosted CI push triggers do not match that branch.
  Read back actual run routing, configure restricted group, prove network
  isolation, then run the complete local macOS job. Do not call the runner
  operational or switch routine CI until the pilot and cleanup pass.
- **Branch isolation:** switched the staged pilot to `ci/tart-runner-pilot`
  from `95424b3`; the migration PR branch itself remains unchanged.

## 2026-10-20 19:03 UTC — Pilot backed up and running locally

- **Checkpoint:** `a6bdb88` committed/pushed to the dedicated pilot branch.
  Default-rule scan of the review export found no leaks. Actual Actions run
  listing showed only `local-macos` for that push, no hosted build workflow.
- **Bootstrap correction:** first run `39478496007` failed before execution
  because the required group did not yet exist. After branch publication,
  created/read back restricted group 5, then retried unchanged source.
- **Isolation:** cloned job VM
  `ft-actions-job-20261020t185329z-6f068950`; Softnet boot succeeded. A real
  listening host-gateway canary timed out from the guest; external GitHub HTTPS
  succeeded. Public DNS configured only inside the disposable guest.
- **Registration:** JIT runner 23 registered only after those checks. Retry
  job `118825790114` is running on that exact VM/group. Checkout, identity,
  pnpm, Node and .NET setup passed; Rust installation/checks remain in progress.
- **Resume:** inspect run `39478496007`, controller session/logs and
  `.artifacts/tart-runner/ft-actions-job-20261020t185329z-6f068950/state.json`.
  Controller is bounded and should remove the clone/registration after the
  job. A running job or zero runner exit is not passing validation.

## 2026-10-20 — Local pilot exposed Bash prerequisite

- **Result:** job `118825790114` ran on local runner 23. Tool setup passed,
  then `scripts/check.sh` failed because macOS system Bash 3.2 lacks `mapfile`.
  Application checks and packaging did not run. This is a template/workflow
  prerequisite, not an S3 regression.
- **Lifecycle:** controller completed cleanup; runner exit 0 was correctly
  distinguished from the failed GitHub job. Job clone and registration are
  removed; private VM/runner diagnostics retained under `.artifacts/`.
- **Correction:** add Homebrew Bash to base preparation; select its absolute
  path in the workflow and use `$BASH` for nested repository checks.
  Rebuild the unregistered template's tooling and rerun locally.
- **Template correction verified:** Bash 5.3.15 installed; version assertion
  passed; base remains unregistered and is stopped. The preparation helper's
  first boot-readiness poll timed out; retry handling corrected and install
  completed. Independent Tart listing and GitHub runner listing confirmed the
  previous job clone was absent and repository-visible runner count was zero.

## 2026-10-21 — Persisted template and explicit macOS cutover scope

- **Approval:** maintainer answered the scope question: move macOS jobs locally
  first, leave Windows/Linux hosted. This authorizes the CI routing change.
- **Second pilot:** `a403061` was committed/pushed. Its first local run
  `39521793168` failed because `/opt/homebrew/bin/bash` was absent in the clone,
  despite verification before stopping the template. The base itself also
  lacked Bash on next boot. Recent guest writes had not persisted.
- **Repair:** reinstall Bash, sync, request shutdown from inside macOS, and
  wait for guest exit. The shutdown transport disconnected as expected.
  Fresh clone `ft-actions-job-20261021t003855z-222e26e7` independently verified
  modern Bash before JIT registration. Retry job `118970292546` now executes
  the real validation/package script. Do not confuse prior setup failures with
  app test results.
- **Cutover preparation:** preserve exact check names required by both branch
  protection and rulesets. Explicitly enroll same-repository PR #207, CI on
  dev/main, and dev candidate packaging through server workflow-ref restrictions.
  Add a bounded five-job drain command. Other PRs retain hosted coverage until
  enrolled; Windows/Linux stay hosted. No protection changes or app activation.

## 2026-10-21 — Pilot passed; GitHub restriction changes routing design

- **Pilot:** job `118970292546` at `a403061` passed the complete validation and
  packaging script, uploaded the candidate, and controller cleanup finished.
  Collect final GitHub status after cache post-steps before the final claim.
- **Tracker:** created #208 for approved macOS cutover and operational evidence.
- **API evidence:** GitHub rejected `refs/pull/207/merge` as an ambiguous
  runner-group ref. It also rejected the PR's test-merge SHA as an unresolved
  workflow commit. Both rejected PATCH requests left the pilot policy intact.
- **Revised implementation:** a full-SHA-pinned reusable worker is the server
  boundary. Its immutable routing logic sends enrolled same-repository PR #207
  and protected dev/main work to Tart, retaining hosted compatibility for other
  PRs. Separate lane inputs preserve test/package coverage without duplicating
  the complete build five times.
- **Protection consequence:** reusable jobs add `/ validate` to check names.
  Preserve enforcement by mapping the three existing required macOS checks to
  their equivalent names after observing successful execution. Windows/Linux
  names and all other protection settings remain as read.
