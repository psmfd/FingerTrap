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

## 2026-10-21 — Pilot final success; pinned worker policy verified

- **Final pilot:** GitHub reports job `118970292546` completed successfully.
  Artifact `12312192004` contains the `a403061` candidate (71,490,490 bytes),
  archive digest `b54803491980300cd24168e52a29acaf0533ce00048128e66d0440e632feba17`.
  Controller deleted its VM and registration; local diagnostics were retained.
- **Worker checkpoint:** `cf1cd50ca296ad2b35711b028acfe2f66f7c8aa4` committed/
  pushed with CI skipped, so callers can pin an existing immutable worker.
- **Group read-back finding:** GitHub's successful group PATCH cleared its
  selected repository membership. Strict validation caught the empty set.
  Added an explicit PUT of FingerTrap alone after PATCH; read-back now verifies
  repository ID `1221327956`, restricted workflows, pilot plus exact worker SHA,
  and both matching administration-controlled routing variables. No runner was
  registered while policy validation failed.
- **Next:** commit caller routing, fast-forward the migration branch, push PR
  #207, run five disposable macOS workers, and inspect the actual check names
  before mapping their equivalent protection entries. #208 tracks completion.

## 2026-10-21 — Five local macOS lanes passed; final handoff

- **Source:** `e0427460285c83805587ee5db599ace50032b400` committed and pushed
  after fast-forwarding the migration branch. The reusable worker stays pinned
  to `cf1cd50ca296ad2b35711b028acfe2f66f7c8aa4`.
- **Actual CI:** run `39526283019` passed; packaging run `39526283095` passed.
  All 23 reported checks passed, including CodeQL and hosted Windows/Linux.
  Local jobs: Tauri `118981561180`, UI `118981561226`, sidecar `118981561244`,
  runtime package `118981561331`, candidate `118981561420`.
- **Local lifecycle:** each job used a distinct `ft-actions-job-*` VM and JIT
  runner, with host-canary denial and GitHub HTTPS verified before registration.
  All five cleanup records completed. Independent `tart list` showed only the
  preserved stopped base/acceptance VMs; GitHub runner count was zero.
- **Artifact proof:** downloaded artifact `12313103374`, verified outer SHA-256
  `112d5440e3af8f92d567969a06abfadd8676438511e03db4b15a1d88c7e22b3d`
  and both app/DMG checksums. Build identity matches test-merge
  `41cb6c6b59dcc10eed2f78a99b6b2d20e99f0bee` and the recorded Tart runner.
  Evidence/files retained under ignored `.artifacts/tart-final-candidate/`.
- **Controller verification:** eight policy/lifecycle tests pass; bypassing
  group checks detected 11 invalid boundaries; omitting the repository-membership
  PUT failed its regression test. Mutations were in memory only. Workflow,
  shell, Python, Markdown, structural and whitespace checks passed.
- **Protection timing:** actual reusable check names have `/ validate` suffixes.
  Global requirements are still unchanged because other branches use the old
  workflow. The runbook records the exact three equivalent mappings for the
  coordinated merge. PR #207 stays draft; #208 stays open for that rollout.
- **Handoff:** checkpoint documentation with CI skipped to preserve the source
  evidence without another hosted matrix. Next source session starts the local
  controller after jobs queue. S3's exact-Pi discovery blocker remains the next
  migration decision; no managed activation, SDK change, pin bump or retirement.

## 2026-10-21 — Approved coordinated merge preparation

- **Authorization:** maintainer approved proceeding after the final handoff.
  Final prior docs checkpoint `d701bdf` is pushed; source `e042746` is the prior
  23-check pass. Preserve exact-candidate validation before merging.
- **Inspection:** PR #207 is draft, conflict-free, based on unchanged `dev`
  `8ffeae8`. No human change request; one CodeQL review flags the canary's
  all-interface socket. Narrow it to the VM gateway and verify live isolation.
- **Plan:** validate the resulting head; map only the three equivalent macOS
  contexts in `dev` ruleset/classic protection immediately before squash merge.
  Preserve all other requirements and use no bypass. `main` still uses the old
  workflow, so defer its mapping to the later release promotion. Then verify
  protected `dev` CI and a manual Apple Silicon candidate on local Tart.
- **Canary correction:** query the clean guest gateway, validate a specific IPv4
  address, and bind/probe only that host interface. The canary still requires
  guest timeout plus GitHub HTTPS before JIT registration. Nine controller tests
  pass, including a real-socket binding check; restoring the wildcard bind in
  memory fails that check. Keep the suite in `repo-check`. Sandbox socket denial
  required an authorized unsandboxed run; macOS loopback-alias/SO_ACCEPTCONN
  assumptions were replaced with the portable kernel `getsockname` assertion.
- **Next:** push this candidate without CI skipping, run local macOS jobs,
  verify CodeQL finding #8 is fixed, and only then coordinate `dev` protection.

## 2026-10-21 — PR #207 merged; protected dev verification active

- **Candidate:** `8196b4fbd85a3cf96b1511cc3d4da726ec9ce8ef` pushed and passed
  all 23 distinct checks (25 reports including title/gitflow repeats).
  CI `39530284288`; packaging `39530284279`; CodeQL #8 fixed, no open PR alerts.
- **Local candidate jobs:** UI `118994354345`, sidecar `118994354408`, Tauri
  `118994354645`, runtime `118994354668`, candidate `118994353815` all passed.
  Five gateway-only live canaries passed; all clones/registrations were removed,
  confirmed independently via Tart and GitHub listings.
- **Merge:** PR marked ready; exact-head squash merge succeeded without bypass
  at `9bfc0926cf387644a1cd2512e3fcb63f22287165`. Git tree equals tested candidate.
- **Protection:** only three macOS names mapped in dev ruleset 20894193 and
  classic protection. Full independent readback preserves all other settings;
  main ruleset/classic protection unchanged. Worker branch remains at `cf1cd50`.
  Before/desired/observed snapshots retained in `.artifacts/tart-rollout-207/`.
- **Next:** dev CI `39531687800` plus a manual dev candidate are queued/running;
  drain five local jobs, verify outputs/cleanup, then save final docs on
  `docs/runtime-migration-handoff` with CI skipped. This branch starts at the
  merged dev commit; preserve it for the next implementation PR.

## 2026-10-21 — Protected dev rollout verified; final handoff

- **Merged source:** `9bfc0926cf387644a1cd2512e3fcb63f22287165`. CI
  `39531687800` and manual candidate `39531859652` completed successfully.
  All 22 post-merge check reports passed, including hosted Windows/Linux and
  all five CodeQL language analyses; no open dev CodeQL findings remain.
- **Local jobs:** sidecar `118998808299`, UI `118998808334`, Tauri `118998808481`,
  runtime `118998808483`, candidate `118999372393`. Each had its own fresh VM,
  verified canary/HTTPS and JIT registration. All five cleaned up; independent
  Tart listing shows preserved stopped VMs only and GitHub runner count is zero.
- **Artifact:** downloaded `12316018036`; archive SHA-256
  `ee2ec15073e50ac55173bb075fca4052be904e2ea4ef6382436840b55b7ea1d1`.
  Verified both app/DMG digests and embedded commit/runner against the successful
  dev job. Files/evidence: `.artifacts/tart-dev-candidate/`; job and protection
  evidence: `.artifacts/tart-rollout-207/`.
- **Durable handoff:** final runbook, ADR status, migration status and this log
  are saved on `docs/runtime-migration-handoff`, based on merged dev, with CI
  skipped for documentation only. PR #207 and #208 record the exact checkpoint.
  Carry this branch's documentation into the next implementation PR.
- **Next:** review S3's missing startup/discovery control under #201/#202.
  Runtime activation remains disabled. #208 retains later main-promotion check
  mapping and any unattended-controller decision. Enroll future trusted PRs
  before source builds as documented; keep the immutable worker branch.

## 2026-10-21 — S3-G discovery-resolution planning started

- **Request:** present an implementation plan for S3's pinned-Pi discovery
  blocker. The earlier HTML review preference and durable-tracking requirement
  remain applicable. This request authorizes planning; the new fork change is
  proposed, not implemented or approved.
- **Checkpoint:** `44b37cad0f8d4cbb776a219975483a39bc4b7fb8` is committed and
  pushed on `docs/runtime-migration-handoff`; merged source remains `9bfc092`.
- **Evidence:** refreshed #201/#202 and exact-source startup files for baseline
  `e7d09c9` and release `v0.87.1-psmfd.1` / `42429b9`. Both source revisions
  invoke migrations before final project trust; bootstrap settings also precede
  normal argument parsing. The newer release has not been executable-qualified.
  The retained baseline probe already demonstrates the project rename.
- **Intended change:** a standalone HTML proposal for a versioned, opt-in
  managed startup policy; explicit artifact capability; retained executable/RPC;
  real PTY/RPC regression evidence; separate candidate and promotion gates.
- **Validation planned:** source/link/anchor checks, JavaScript syntax, desktop
  and mobile rendering, Markdown/structural checks, and scoped documentation
  commit/push with CI skipped. No runtime source or Pi checkout changes.
- **Next:** finish and validate the proposal, then present it for approval.

## 2026-10-21 — S3-G proposal ready for review

- **Deliverable:** [HTML plan](s3-discovery-resolution-plan.html), five proposed
  checkpoints covering a managed Pi startup policy, descriptor v2 capability,
  shared host integration, actual artifact qualification and a separate pin
  decision. The proposed CLI flag does not exist yet. Executable/RPC remains
  the recommended backend for this slice.
- **Approval boundary:** approval would cover the focused fork/host changes,
  companion commits/draft PRs and unpublished qualification artifacts, plus
  preparation of the promotion report. Exact production pin promotion and
  managed activation remain separate gates under ADR-0034/0036.
- **Validation:** 41 links, 13 unique IDs and local targets passed; no remote
  assets. JavaScript syntax, 390-pixel mobile width, desktop rendering and
  section navigation passed. Markdown lint and repository structural checks
  passed with zero errors/warnings; whitespace check passed. No runtime code
  changed and no implementation acceptance result is claimed.
- **Checkpoint:** save the three documentation files on the existing handoff
  branch with CI skipped; verify remote identity before reporting backup. The
  exact resulting commit is available in git history and the session handoff.
- **Next:** maintainer review. After approval, record authorization and start
  S3-G1: linked Pi regression tracker, policy ADR, and baseline-failing candidate
  qualification fixtures. Preserve unrelated local files and existing runner
  enrollment requirements.

## 2026-10-21 — S3-G approval recorded; G1 started

- **Authorization:** maintainer approved the presented plan. Proceed with G1–G4
  and preparation of the G5 report, including focused companion commits/draft
  PRs and unpublished builds. Exact-pin promotion and activation stay gated.
- **Starting checkpoint:** `0dc2b926fe7df637e6f83186fc9d6282989fc91b`, committed
  and pushed. Preserve the three unrelated untracked local paths.
- **G1 intended work:** linked Pi regression tracker, ADR-0038, immutable policy
  contract and separate candidate qualification fixtures. Use an isolated Pi
  worktree at `e7d09c9`; do not switch or modify the operator's Pi checkout.
- **Validation:** original pinned executable must fail isolation assertions for
  its known behavior; keep the baseline reproducer's existing success semantics.
  Record each fixture's observed outcome before implementing the fork patch.
- **Next:** create the tracker/worktree and inspect the exact entry/services
  paths before writing the policy and retained tests.

## 2026-10-21 — G1 regression evidence and dependency worktree

- **Tracker:** [Pi #80](https://github.com/psmfd/pi/issues/80), linked to #201/#202.
- **Dependency worktree:** `/private/tmp/ft-pi-managed`, branch
  `fix/managed-startup-v1`, exact baseline `e7d09c9`. Original Pi checkout
  remains on its existing branch. Pinned `npm ci --ignore-scripts` succeeded
  after sandbox DNS failure and authorized network retry; no lockfile changes.
- **Contract:** ADR-0038 records the approved managed-v1 boundary and descriptor
  capability. Runtime implementation has not begun yet.
- **Red evidence:** the new candidate qualifier runs the baseline without the
  unsupported policy flag. Actual RPC hello, state, new-session and reload
  requests succeed, but project state changes, profile migration changes inputs,
  and a literal system prompt is interpreted as a filename. Both isolation
  cases fail as expected. Evidence: `.artifacts/s3-discovery/baseline.json`.
- **Harness correction:** count extension factory invocations separately from
  session-start events; the existing lifecycle emits additional start events.
  This avoids misclassifying an event as a duplicate extension load.
- **Next:** save the documentation checkpoint and establish the implementation
  draft PR for local runner enrollment. Then implement early parsing and an
  explicit managed resource/service path in Pi; retain the red comparator.

## 2026-10-21 — G2 runtime patch in progress; draft established

- **Checkpoint:** contract/approval `2d82399`, pushed on
  `feat/managed-pi-startup`; draft FingerTrap #209 targets dev. No source CI
  was requested for that documentation-only checkpoint.
- **G2 intended change:** early validated policy, explicit managed loader,
  in-memory configuration/auth/model inputs, persistent lifecycle policy and
  interactive startup controls. Focused tests and the real binary qualifier
  must pass before candidate qualification is claimed.
- **Working changes:** first Pi core implementation exists only in the isolated
  worktree. Candidate qualifier exists locally in FingerTrap. Neither is yet
  committed or CI verified. Runtime/profile activation is disabled.
- **Runner action:** enroll same-repository draft #209 with the existing worker
  SHA before source pushes. Keep Windows/Linux hosted and preserve #207 entry.
- **Next:** complete interactive controls and regression tests, build the
  unpublished exact-source candidate, and rerun the isolation matrix.

## 2026-10-21 — G2 policy implemented; first local build checks

- **Implemented locally:** pure early selector validation, immutable managed
  resource inputs, in-memory settings/auth/models, lifecycle rejection guards,
  interactive keybindings/themes/helper/background/trust/auth/session controls.
  Explicit extension failures now fail startup. No production composition change.
- **Checks:** `npm run check` passes after correcting theme API/test types and
  hydrating the exact `@earendil-works/pi-ai@0.85.1` model data (including its
  manifest). `npm run build:offline` passes; dependencies/lockfiles unchanged.
  First focused test attempt exposed unbuilt workspace exports; rerun after build.
- **Toolchain:** temporary Bun 1.3.10 from the official release, matching the
  fork workflow. Model package integrity:
  `sha512-+VgVIJDkDO2efYJKEEqvPTH4zmnIaXdAppGbO+vKFA9qy5PdhFiAenuFAkU+oiCSfOC4dMHDyrjdQeL4ZoC5CQ==`.
- **Not yet qualified:** source edits/tests remain local and uncommitted; the
  standalone build and focused tests are running. Next run the retained binary
  qualifier, then expand direct PTY/RPC and rejection/mutation coverage before G3.

## 2026-10-21 — G2 initial executable passes; G3 started

- **Local evidence:** all 110 focused Pi tests pass. The first standalone macOS
  ARM64 build passes both retained direct-RPC cases (migration and poisoned
  profile), including reload/new-session, literal prompt, cwd and factory count.
  Development executable SHA-256:
  `1f1a604cb44a733441d6bb660a9f2e16b4604853a6c5e17446c5fef998f2d596`.
  This is an uncommitted development build, not a promotable identity.
- **G3 intended change:** strict descriptor v2, independently host-selected
  source revision and descriptor digest, required managed-v1 policy, shared
  transport arguments, candidate-only project .pi allowance. Keep production
  selection absent and the remaining profile/override/credential/resume guards.
- **Validation:** host rejection tests and both transport process tests; then
  published fixture-host/native companion qualification against the real Pi
  candidate. Full Pi hermetic tests and expanded qualification remain in flight.

## 2026-10-21 — G2 broader checks pass; G3 host checks pass

- **Pi:** required static checks and the full hermetic `./test.sh` pass after
  allowing local sockets; the sandboxed run failed on EPERM listeners. Two old
  interactive test doubles were updated for the new guard method. Added service
  traps prove no implicit settings/auth/default-loader/package-resolution calls;
  all 30 instrumented policy/theme tests pass after that addition.
- **Direct artifact:** RPC migration, poisoned profile and linked-project cases
  pass concurrently. Real PTY startup, new-session, reload, unsupported command
  rejection and clean quit pass. Installer/helper canaries and the controlled
  network endpoint show no attempts. One harness assertion incorrectly required
  the word "managed" in the duplicate-selector error; actual bounded rejection
  and unchanged state passed. Removed that wording-specific assertion.
- **FingerTrap:** descriptor v2 and exact host-selected revision/policy checks
  implemented. Shared args include managed-v1. Build: zero warnings/errors.
  Sidecar tests: 406 passed, 12 skipped, 0 failures on macOS ARM64, including
  native process tests and goldens. Test runner needed local pipe permission.
- **G4 preparation:** separate qualification project compiles the same Program
  with a test-only selection loader. Production compilation excludes that loader
  and selection branch; no runtime setting/env switch enables managed mode.
  Publish/real-process validation and mutation evidence are next. Production
  activation and exact-pin promotion remain pending.

## 2026-10-21 — Mutation exposed and corrected a fixture blind spot

- Restoring the migration call initially escaped the expanded qualifier: adding
  an ambient `prompts` directory had suppressed the legacy commands-to-prompts
  rename. This was a test-fixture defect, not isolation evidence.
- Separate legacy-layout cases from current-layout resource cases. Migration
  fixtures now omit the destination prompts directory; current-layout fixtures
  independently exercise discovered prompts. Re-run baseline, both mutations
  and the restored candidate before trusting the expanded suite.

## 2026-10-21 — Pi checkpoint backed up; committed candidate direct pass

- **Pi source:** `39b729b625f50d4c82bd993151865d5ded8dd78e`, clean candidate
  branch pushed; draft [Pi #81](https://github.com/psmfd/pi/pull/81). CI skipped
  on this dependency checkpoint; no hosted Pi macOS jobs or release requested.
- **Exact artifact:** rebuilt from that commit with offline model data/Bun
  1.3.10; binary SHA remains
  `1f1a604cb44a733441d6bb660a9f2e16b4604853a6c5e17446c5fef998f2d596`.
  Corrected direct RPC/PTY/CLI/network matrix passes. Both migration and discovery
  mutations now fail the relevant assertions; original source/build restored.
- **Host fixture:** initial published test host did not pick up the synthetic
  settings via platform path lookup. Change its compile-time-only entry to
  explicitly select its settings and orphan registry and skip login-shell PATH
  discovery. The product path remains unchanged. Re-publish before retrying.
- **Next:** finish real host qualification, host capability mutation, exact
  source checkpoint/CI, clean Tart execution and protocol evidence. Do not mark
  candidate qualified or S3 complete yet. Tart preflight passes; #209 is enrolled.

## 2026-10-21 — Published host matrix and host mutation verified locally

- **Published qualification host:** two concurrent profiles, each real RPC and
  native PTY, pass startup/reload/new-session/cancellation/state, parent canary
  exclusion, independent roots, unchanged project/home/profile inputs, override
  and resume rejection, sibling survival, graceful host exit and complete reaping.
  Development host SHA `d22b8e476e4c5e4bf06128a1d28d976ca7320294f4318c79bd1f54058e5ec14e`;
  native SHA `4644ce3598bf59a4f1999e4e42b1312586ce9a00a7fa33059217308707f1b4a0`.
- **Host red proof:** removing only the startup-policy comparison produced the
  expected missing-exception failure for `startupPolicy=managed-v2` (1 failed,
  7 passed). The runner summary omits theory inputs; detailed UTF-16 test output
  confirms the precise case. Source guard restored and full tests rerunning.
- **Baseline:** corrected retained baseline comparison fails project migration,
  profile mutation and literal-prompt assertions while RPC itself succeeds.
- **Next:** source checkpoint, republish to bind host identity to that commit,
  twice-recorded RPC compatibility, clean Tart artifact run and required CI.

## 2026-10-21 — Exact host passes; clean Tart and protocol comparison started

- FingerTrap `bb5f02fd64b41310a7f35aec92a60b67bdd8bad8` is pushed in draft
  #209. Its published qualification executable SHA-256 is
  `80beb8542052fe1246528d2a9a0617e2b3760c81c39163f228b0c128d97af3da`;
  the native companion digest is unchanged. Exact-host two-profile RPC/PTY
  qualification passes. Restored full sidecar suite: 406 passed, 12 skipped.
- All 12 opt-in RPC recordings pass twice-per-scenario determinism. Seven
  fixtures differ only in apparent `/tmp` versus `/private/tmp` spelling.
  Compare the baseline in the same environment before classifying this drift.
  Original committed goldens are restored; no runtime pin changed.
- First recorder attempts failed before tests because its deliberately empty
  environment lacked the dotnet executable/root. Explicit executable and
  DOTNET_ROOT correct the harness; the successful run is keyless with synthetic
  local HTTP responses. Non-RID restore after publishing succeeds.
- Next: retain a clean Tart artifact runner, clone the stopped base with no
  host mounts or GitHub credentials and blocked outbound network, run both
  committed artifact probes, collect reports, and verify clone cleanup.

## 2026-10-21 — Clean Tart and RPC comparison pass; CI checkpoint prepared

- Clean clone `ft-s3-qualification-5e7313a37e6d`, macOS 15.7.7 / 24G720
  ARM64 / Python 3.9.6, passes exact direct and published-host matrices.
  No host shares or GitHub registration; Softnet configured to block all
  outbound IPv4. Clone removal and base preservation verified. Retained runner
  serializes with the GitHub controller and records cleanup failures as failures.
- Baseline and candidate each pass all twelve twice-recorded RPC scenarios.
  All twelve resulting transcript hashes match across the two executables.
  Seven committed-golden differences are identical existing macOS canonical
  path spelling; original golden files remain unchanged. Recorder is retained.
- Essential JSON evidence, full asset inventory, input hashes, path drift and
  HTML G5 report now live in the repository. Candidate archive SHA-256:
  `2759240a44ca9ebd0acd01258d048014767950c88b7de5f2c08f5bc7810116f9`.
- Fork review: current main `42429b9` has four later commits (#74–#79),
  including security/correctness work absent from the old-pin experiment.
  Its six capability slots are occupied. Recommend a reviewed forward-port
  and cap disposition before production promotion; do not silently drop fixes.
  pi_config #1170 remains useful subagent evidence for S5; #1172 is an
  overlapping pin proposal, not a requirement to resume pi_config development.
- Next: commit/push this checkpoint without skipping CI, drain five enrolled
  local macOS jobs, verify hosted lanes and package evidence. Required matrix
  is still pending; no runtime release, pin change, SDK switch or activation.

## 2026-10-21 — Qualification checkpoint pushed; fork bookkeeping completion

- `c6f088c7cf7b2ef8294a104a091c5d5d08271e03` is pushed. Required CI
  [39598135785](https://github.com/psmfd/FingerTrap/actions/runs/39598135785)
  and packaging [39598135709](https://github.com/psmfd/FingerTrap/actions/runs/39598135709)
  are queued/running. Five macOS jobs route to the enrolled Tart worker;
  the bounded local controller is running. Hosted checks are in progress.
- Complete experimental Pi patch bookkeeping as pending `psmfd-patch-020`,
  avoiding IDs 013–019 already used on maintained main. Add the exact 13
  touched paths to manifest/allowlist/both guards without altering guard logic.
  Keep the tested binary bound to source `39b729b`; this metadata checkpoint
  is distinct and does not replace the artifact identity.
- Old-base conservative capability accounting is 4 patches / 19 paths /
  1,772 changed lines including the candidate (1,108 existing per-path patch
  sums plus 664 candidate additions/deletions). Maintained main has 6 active
  patches already; a forward-port requires fresh accounting and a decision.

## 2026-10-21 — G4 and G5 report complete; separate promotion decision remains

- All 23 check reports pass for source `c6f088c`. Five distinct local Tart
  jobs cover packaging, UI, Tauri, sidecar and runtime package; Windows/Linux
  hosted checks and all five CodeQL analyses pass. Every controller record is
  cleaned; independent GitHub runner count is zero, Tart has no generated job
  or qualification clones, and the base is stopped. Unrelated VMs preserved.
- Downloaded product artifact `12345815425`: archive SHA-256
  `05d764a2a98d78cfaa35981bcda17f2186a801714fe8626b29a927cf815d9789`;
  app/DMG hashes and local runner match. Build `f87c1a9` is the PR test merge;
  tree `83ea0036e69d0153248e1a4afe326447a32ede91` equals head `c6f088c`.
  Packaged native-shell smoke passes; its Pi case skips (product has no Pi).
  The first evidence helper expected a generic PASS string; corrected it to
  the script's actual spawn/write/echo success markers. This was an evidence
  parser error, not a failed package job. Managed artifact evidence is separate.
- Pi metadata `b0a151df397d88db1cb3f52b7e8a526ee22deb1e` is pushed in #81.
  Pending patch 020 is synchronized in manifest/text/both guard sets; only
  allowlist data changed. Candidate source remains `39b729b`. Maintained-base
  projection is 7 patches / 25 paths / 2,081 changed lines; no cap waiver.
- Secret scans pass across both scoped histories. HTML preview exposed missing
  table cell openers; corrected and rechecked the 390px layout and desktop
  rendering. Structural/links/markup checks accompany the final doc checkpoint.
- Authorized S3-G1–G4 and G5 report preparation are complete for the macOS
  artifact. No production pin, release, managed activation, SDK adoption or
  pi_config development occurred. Next: separate maintained-base/cap decision,
  then exact-candidate requalification/pin review before S4. Keep #201/#202 open.
- Final follow-up changes only documentation/evidence and is saved with CI
  skipped. CI attribution remains explicitly `c6f088c`, distinct from the
  later documentation head. Both PRs remain drafts; no merge requested.

## 2026-10-21 — Maintained-base work approved; latest-release campaign queued

- Maintainer approved forward-porting onto maintained Pi, resolving capability
  limits and requalifying the result. Main readback remains
  `42429b94e4aca33fdbb8d094de29758b36411ba1`; current fork release remains
  `v0.87.1-psmfd.1`. Preserve the original experiment and its evidence.
- S3-G6 intended change: separate worktree/branch on that main revision,
  policy port preserving #74/#75/#76/#79, bounded patch-020 exception recorded
  by a new FingerTrap ADR and fork bookkeeping, plus actual cap accounting.
  Validate with release-major Node 22 and Bun 1.3.10, then repeat exact direct,
  host, clean Tart, mutation and twice-recorded protocol qualification.
- The user also requests upgrading Pi to the latest release and validating
  it after this campaign settles. Queue that as a separate durable tracker;
  latest is discovered at its start rather than frozen prematurely here.
  No pi_config implementation resumes; FingerTrap owns the migration work.
- Exact production pin/release and managed product activation are not inferred
  from permission to port and qualify a new candidate.

## 2026-10-21 — Maintained port: new startup paths reproduced and corrected

- Separate worktree `/private/tmp/ft-pi-maintained`, branch
  `fix/managed-startup-maintained`, starts at `42429b9`. Original source patch
  applied cleanly; existing fork fixes are preserved. Source review found
  0.87.1 crash-state discovery and default cache warming. Added guards and
  in-memory `cacheWarming: off` defaults; `/bug` rejects nonfatally.
- A real unfixed standalone binary changed the valid seeded `crashes.json`
  in PTY startup; RPC still passed. The new service assertion failed on
  `streaming` instead of `off`. Corrected direct RPC/PTY/CLI matrix passes.
- Node 22.23.0, Bun 1.3.10, npm ci with scripts disabled, and exact published
  pi-ai 0.87.1 model data produced the offline build. Sandbox DNS blocked
  the first package download; approved network retry succeeded. Initial
  test attempt before hydration had no runnable tests, not a policy failure.
- Static checks pass. Full hermetic suite's only failure was the new page
  being orphaned by 0.87.1 docs navigation. Content moved into existing
  `docs/cli-integration.md`; focused tests rerun before checkpoint. Both
  release-preflight tests pass. Linux-only release-build harness is not run
  on this macOS host; release workflow/build logic is unchanged.
- Fresh upstream comparison gives 7 C-class patches / 25 distinct paths /
  2,090 additions+deletions (020: 673). ADR-0039 and fork security baseline
  bound the exception to 020 with a 2,200-line aggregate ceiling; all three
  allowlist copies match, guard logic unchanged.
- npm reports 15 baseline dependency vulnerabilities (3 moderate, 11 high,
  1 critical); no dependency changes made. Review belongs to the follow-on
  campaign and must be considered before production promotion.
- Created [#210](https://github.com/psmfd/FingerTrap/issues/210) for latest-Pi
  upgrade/validation after this campaign. This number was newly allocated;
  it does not retroactively change the earlier #200/#201 plan authorization.
- Next: finish corrected build/tests, scoped checkpoints, exact host/Tart,
  regression mutations and twice-recorded RPC qualification, then remote backup.

## 2026-10-21 — Exact maintained artifacts pass isolation; recorder repair in progress

- Pi `1f37eea4bf71f95ed9a4e62674949de3a9feb8b0` is committed/pushed in
  draft [Pi #82](https://github.com/psmfd/pi/pull/82). Full hermetic suite
  passes after documentation relocation; 2,469 coding-agent tests pass with
  50 intentional skips. All other workspaces pass, including 312 Chord tests.
- Exact committed binary SHA-256 `9a9f53131cec748801b8c176190ef4dc77e7b6e833859ff734fcfa3d297c7614`;
  archive `fde413d00ceece7a4817c602560f30c1c1903bb9e95739da3512896f9b547f67`.
  Direct checks, two-profile published host matrix and clean Tart checks pass.
  Tart lifecycle reaches complete after cleanup; independent listing pending.
- Downloaded maintained v0.87.1-psmfd.1 baseline; release digest and GitHub
  attestation verify. Its twice-recorded RPC check fails 6/12 determinism
  comparisons on timestamp token numbers (`@TS:1@` versus `@TS:2@`). This is
  on the unmodified release, before candidate comparison. Original goldens
  restored. Do not claim protocol equality yet.
- Next authorized repair: normalize numeric timestamp fields independently
  of same-millisecond collisions, preserving event ordering/correlation and
  all nonvolatile payload; add a regression and retain raw keyless recordings.
  Then rerun all twelve scenarios twice on baseline and candidate and review
  actual protocol differences. Host production source remains unchanged.

## 2026-10-21 — Maintained candidate qualification passes; CI checkpoint next

- Four reversible runtime mutations caught: migrations, default resource
  loader, crash discovery and `/bug` fallback. Source restored and generated
  code rebuilt. The immutable candidate artifact remains unchanged.
- Recorder regression: both new tests fail before repair; full sidecar suite
  passes afterward (408 passed, 12 opt-in skips). Final build: zero warnings
  and errors. Numeric timestamps use one volatile token; installation paths
  use `@PI_HOME@`; no event or tool/schema payload is removed.
- Baseline and candidate each pass 12/12 twice-recorded scenarios. All twelve
  final normalized transcripts match exactly. Both raw recordings are retained
  for each scenario. Original production-pin goldens are restored unchanged.
- Pi 0.87.1 emits additional system-message events with tool schemas and
  installation documentation paths. This is present in the attested baseline
  too, not introduced by managed startup. The frontend's existing role checks
  ignore system messages; production UI source is unchanged.
- Independent Tart listing shows only the stopped base and pre-existing VMs;
  no qualification clone remains. Pi #82 has seven successful check reports,
  including allowlist, secret scan and CodeQL. Local scoped secret scan passes.
- Structural checks pass with zero errors/warnings. Next: checkpoint/push
  FingerTrap source and run five macOS jobs locally; retain exact CI/package,
  report and promotion/next-campaign handoff evidence afterward.

## 2026-10-21 — G6 complete; exact CI/package and future campaign recorded

- FingerTrap source checkpoint `faa4242f1add364191078d9bc12f4aaf154983e6`
  is pushed in #209. All 23 check reports pass: CI `39618649368` and package
  `39618649346`, including CodeQL and Windows/Linux. Five macOS jobs ran
  locally in distinct Tart clones, all cleaned. Independent GitHub runner
  count is zero; no job/qualification clones remain and base is stopped.
- Downloaded product artifact `12354825465`: archive SHA-256
  `333ccbdd3c79035e750b4708c9105eef2d6816ec788f731c196f2e44d256e46a`.
  App/DMG checksums and runner match. Build `7b8a83b` is the PR test merge;
  tree `3243d2eff6a982c963750669f2987e4116f0ea18` equals `faa4242`.
  Packaged sidecar/native-shell smoke passes; bundled Pi case skips because
  the product does not bundle Pi. Managed evidence remains separate.
- Host-policy mutation repeated: removing the comparison fails the
  `startupPolicy` rejection case; restored tests pass. Pi source and production
  host source are clean. All runtime mutations were restored.
- Full dependency audit retained: 15 findings (3 moderate, 11 high, 1 critical).
  brace-expansion 5.0.9 is in the core runtime shrinkwrap and has high-severity
  advisories. Critical shell-quote 1.10.0 is absent from that core shrinkwrap;
  example/development reachability still needs disposition. No dependencies
  changed and production promotion is not recommended before that review.
- [G6 HTML report](s3-managed-startup-maintained-qualification.html) and
  `evidence/s3-managed-startup-maintained/` preserve exact identities, source
  summaries, all raw/normalized RPC pairs, mutations, audit, attestation,
  asset inventory and CI/package/cleanup evidence. Mobile 390px and desktop
  layouts verified. Old report now points to this maintained candidate.
- Requested latest-release upgrade remains queued in #210 with dependency
  resolution and full validation criteria. No pi_config implementation, merge,
  release, installed pin, SDK switch or managed activation occurred.
- Authorized G6 implementation and qualification are complete. Next: settle
  companion draft integration/promotion, then execute #210. S3/#200/#201/#202
  remain open. Final documentation/evidence checkpoint skips CI; source
  verification belongs to `faa4242` and Pi `1f37eea`.

## 2026-10-21 — Final evidence review and backup preparation

- Validated 102 retained evidence files; all 95 JSON/JSONL files parse.
  Current report: 37 links and 10 unique IDs; historical report: 41 links
  and eight unique IDs. All local references and HTML anchors resolve.
- Final Markdown lint initially found two pre-existing compact table
  separators in the touched RPC contract document. Added table spacing;
  all six reviewed Markdown files now pass. Structural checks pass with
  zero errors/warnings, and the working diff has no whitespace errors.
- Pi source remains clean; production host source remains identical to
  `bb5f02f`. Only qualification evidence and documentation enter this
  checkpoint. Unrelated private files remain untouched.
- Companion draft and #210 descriptions are prepared with exact tested
  sources, dependency disposition and follow-on acceptance. Next: scoped
  commit, secret scan, remote backup and description readback; then record
  those observed results. The documentation checkpoint skips source CI.

- Evidence/report checkpoint `0cf4c98188204489a9e45c21e4895f9cc1f1090b`
  committed; scoped secret scan from `090cefa` through this checkpoint passes.
  Final tracking inspection found three command logs excluded by the global
  `*.log` rule. Renamed them to `.txt` for durable retention before backup;
  all 102 evidence files will be tracked. No test outcomes changed.

## 2026-10-21 — Qualification remotely backed up; recovery checkpoint

- Report/evidence `0cf4c98188204489a9e45c21e4895f9cc1f1090b` and command-log
  follow-up `7d081d01603761fc1f057c46d31a61aab1476b11` are pushed to
  `psmfd/FingerTrap:feat/managed-pi-startup`; fetched remote matches the head.
  Scoped scans cover both source/evidence and the log follow-up, with no leaks.
  Independent inventory confirms all 102 retained evidence files are tracked.
- Updated and read back FingerTrap #209, maintained Pi #82 and follow-on #210.
  Both implementation PRs remain open drafts. Pi #81 now explicitly points to
  the maintained replacement and remains historical; no merge or issue closure.
- Latest-Pi #210 carries concrete runtime brace-expansion and shell-quote
  disposition, protocol drift, cap accounting and revalidation requirements.
  Discover latest only when that campaign begins. Exact pin/release and
  managed activation remain separate from this completed qualification.
- Resume: review the qualified companion drafts, settle integration/promotion
  disposition, then execute #210. Source CI is green on FT `faa4242` and
  Pi `1f37eea`; documentation commits intentionally skip those build lanes.
  This status/log-only recovery note records the preceding verified backup.

## 2026-10-21 — Session stop for patching

- Maintainer requests ending the session and a durable restart handoff.
  Implementation is paused; no qualification/build command is pending.
  Previous recovery checkpoint `42aab08fffc0a854f5664f11630bba68721c0e36`
  is verified on local and remote `feat/managed-pi-startup`. Pi remains clean
  at pushed `1f37eea`; #209/#82 are open drafts, and #210 remains open.
- Refreshed GitHub runner listing shows zero registrations. Tart listing
  shows every VM stopped, with no job/qualification clones. This stop does
  not need a runner drain or additional source test run.
- Preserved exact artifacts outside `/private/tmp`, under ignored local
  `.artifacts/s3-discovery/patching-handoff-20261021/`: candidate runtime and
  archive, published host closure, released comparison and attestation,
  model-data archive and metadata. Verified 426 files/symlinks against their
  sources plus the qualified binary hashes. Inventory SHA-256:
  `8ae0a0357b0614696989a675f6b43ff761f80b6eb6bfe01412bd9fa90ff531a5`.
  Binary preservation is local only; source/evidence remote backup is separate.
- STATUS now contains restart wording, branch recovery and artifact locations.
  Ledger records the patching pause; qualification and activation states do
  not change. This handoff is documentation only and skips build CI.
- Next session: verify local/remote state and toolchain after patching, review
  qualified companion drafts and settle integration/promotion disposition;
  then execute latest-Pi #210. Dependency findings still require disposition
  before a production pin. Keep pi_config retired from new implementation.
- Handoff finalization: validate documents, commit/push this scoped update,
  and link its actual commit in the companion PR and #210 handoff notes.
  The session's final response reports the observed backup result.

## 2026-10-21 — Resume after patching; companion review correction S3-G7

- Read the approved plan, status/handoff, latest log, ADR-0034/0039 and
  provenance. Local/fetched FingerTrap branch is exactly `7f99454`; remote
  #209/#82 remain drafts at `7f99454` / `1f37eea`, with unchanged bases.
- Patching removed `/private/tmp/ft-pi-maintained`; recovered saved branch
  into independent `/private/tmp/ft-pi-resume`. Sibling Pi and private local
  FingerTrap files are preserved. Sandbox DNS blocked clone and credential
  lookup; explicit escalated retry succeeded without changing global account.
- Tart check passes (group 5); all VMs stopped. Host default Node 24.17.0,
  pnpm 10.34.5, .NET 10.0.301; Bun is absent from PATH. Restore reviewed
  release-major tools before source validation.
- Review discovered unresolved CodeQL alert 87 on Pi `1f37eea`, generated
  extension fixture path interpolation. Prior green check reports did not
  establish that no alerts remained. Intended correction: static module-relative
  fixture path; validate affected tests, full static checks and cap recount,
  then checkpoint/push and verify remote analysis. No production code change
  is intended and previous exact artifact qualification remains separately bound.
- Latest release API readback identifies upstream `v1.3.0` (2026-10-15) and
  fork `v0.87.1-psmfd.1`. After G7 review disposition, #210 begins with exact
  provenance, release/dependency review and full retained-patch reconciliation.
  Existing ceilings and exact-pin/release/activation gates persist.

## 2026-10-21 — G7 corrected and backed up; Pi-next N1 begins

- Test-only Pi correction `95e95f0eca5dd6638f4e8c308932134a6d071798` is
  committed/pushed in #82. Full `npm run check` and 23 policy tests pass on
  Node 22.23.0. Missing ignored model JSON initially blocked both checks;
  hydration from the preserved SHA-256-verified 0.87.1 archive fixed that
  environment prerequisite. First sandbox npm attempt failed DNS; network
  retry installed exact locked dependencies with scripts disabled.
- No runtime source changed relative to `1f37eea`; prior artifact evidence
  remains bound to its original source and digest. C accounting unchanged:
  7 patches / 25 paths / 2,090 changed lines. Remote allowlist/gitleaks pass;
  CodeQL is still running, so alert resolution is not yet claimed.
- Verified all 426 preserved artifact entries and inventory digest. Current
  maintained candidate is retained as reference; promotion remains deferred
  for dependency findings. No merge/pin/activation decision was inferred.
- N1 selects upstream latest stable `v1.3.0`, exact commit
  `71023aec1546d6d677a65669c5665c0927ce0dd0`; fork latest remains 0.87.1.
  Independent source worktree `/private/tmp/ft-pi-latest`, branch
  `feat/fingertrap-pi-1.3`, starts at that tag. Release metadata is retained.
- Intended N1 work: reconcile all fourteen live/pending patches, prepare
  source port preserving upstream behavior, retain conflict/retirement evidence,
  and recount before any build. Static preview uses explicit v0.87.1 content
  baseline, avoiding the recorded squash-ancestry trap. Upstream removed
  pico3/skills implementations and replaced Chord tracking; do not restore
  obsolete files to make old patches apply. Validate replacement behavior or
  removed reachability before proposing retirement. Ceilings stay binding.

## 2026-10-21 — G7 remote verification complete; N1 port and dependency review

- All seven Pi `95e95f0` check reports pass, including every CodeQL language
  analysis. Alert 87 independently reads back `fixed`; no dismissal. G7 is
  settled as a test-only correction to the retained qualified reference.
- FingerTrap recovery checkpoint `6f4b491` is committed, scanned and pushed
  in #209. Scoped staged paths exclude the operator's three private untracked
  entries. Initial raw diff evidence triggered whitespace checks on patch
  context; retained the exact input as a JSON string instead of altering it.
- N1 capability source port preserves new upstream provider help, theme schema
  validation, MCP dispatch, terminal-color behavior and RPC response fields.
  Three conflicts resolved; source static checks pass after hydrating 43 JSON
  files from upstream's digest-verified 1.3.0 source release. Initial check on
  the fresh checkout lacked model data and found an old theme-test API name;
  fixed that API reference, then all static checks and 91 focused tests pass
  (one binary test skips before build).
- Preliminary 6 / 23 / 2,019 C accounting assumes proposed retirement of 019.
  Node 22 probe reproduces the original upstream 100,000-item RangeError and
  verifies replacement exact contents, replay, preparation/adoption, 27 native
  splice comparisons and a 20,000-item middle insertion. No Chord source changed.
- Security ports 001/003/005/014/015 applied; test conflict preserves new
  `--legacy-peer-deps` while retaining safe git argument assertions. Static
  check passes. 016/017 target source removed by upstream; deletion/reachability
  evidence is required before proposed retirement, not an `--ours` restore.
- Refreshed root audit has three high findings: Vite plus gondolin/node-forge.
  Core brace-expansion is 5.0.12; root shell-quote is 1.12.0. Nested sandbox
  lock is separate and still had vulnerable 1.10.0 after old patch 003. Its
  independent copied lock refresh selects 1.12.0 with zero audit findings;
  also reconciles already-pinned sandbox dependency metadata. First audit from
  the workspace subdirectory selected the parent graph and is not proof for
  that nested lock. Maintainer shell-quote advisory confirms <1.11.0 affected.
- Full hermetic suite is running. Next: finish independent audit/reachability,
  retain failure/pass evidence, reconcile overlay/patch metadata and actual
  accounting, then source checkpoint and exact-artifact validation.

## 2026-10-21 — N1 source validation and release-workflow reconciliation

- Offline 1.3.0 workspace build passes; required Rust environment daemon
  builds with the locked Cargo graph. First full Pi suite failed in the tool
  sandbox on fixture sockets and pre-build exports. A fresh credential-isolated
  run outside the sandbox passes all workspaces except one pinned-git cache
  fixture (2,736 coding-agent cases pass, 50 skip). That fixture also fails
  with unmodified upstream package-manager code. Corrected its cache identity
  to include the ref; all 13 Git-update cases pass. Final static check passes.
- Vite security override now resolves 8.1.6 after explicit npm re-resolution;
  the first lock-only install left the old transitive version intact. Registry
  integrity/publication age and Vite release notes were reviewed. Candidate
  registers pending security patch 021; upstream main still has 8.0.16 and no
  open Vite fix PR was found. Root audit now retains only gondolin/node-forge;
  production install-lock reachability still needs recorded disposition.
- 003 refresh independently regenerates the sandbox lock at shell-quote
  1.12.0; isolated audit is clean. Proposed retirements 016/017 preserve upstream
  harness removal; 019 has real Node 22 replacement-behavior proof. Manifest,
  text allowlist and both guard data sets are reconciled; no guard logic changes.
- Copied approved PSMFD overlays and quarantined all 13 upstream workflows.
  Release workspace-order guard fails against the stale 0.87.1 build block.
  Updated inline sequence/TypeScript 7/model-data copy and Bun pin to upstream's
  1.3.14; order guard and both release-preflight tests pass. Linux/Bubblewrap
  integration has not run and remains a release-validation gate.
- Actual C accounting: 6 patches / 23 paths / 2,021 lines (020: 682). Ceiling
  stays 7 / 25 / 2,200; other patches remain within their original budget.
- GitHub app reads succeed but description mutations return 403 (integration
  lacks write access). Authorized CLI fallback updates and independently reads
  back #209/#82/#210; no global account switch. Evidence retained.
- N2 intended change: extend synthetic startup qualification for MCP config
  and helper discovery/rejection, then qualify digest-selected compiled Pi,
  published host, twice-recorded protocol, mutations and clean Tart. Before
  standalone build, finish source/overlay review, checkpoint and verify exact
  model-data/toolchain identities. Production release/pin/activation stay gated.

## 2026-10-21 — N1 green; N2 catches model-data input difference

- Complete unsandboxed, credential-isolated suite passes after fixture repair:
  coding-agent 2,737 passed / 50 skipped; AI 1,357 / 864; Chord 517 passed;
  both durable modes 1,220 / 4 each; env 85 / 2; other workspaces pass. No paid
  provider, user auth or product activation was used. This run used the exact
  source-release model data.
- Verified manifest/text/both guard parity, active workflow allowlist parity,
  no unexplained residual upstream paths, 6 / 23 / 2,021 cap accounting and
  runtime install-lock exclusion of Vite, gondolin/node-forge and shell-quote.
  Ordinary 1.3.0 CLI executes the valid seeded MCP helper; fixture external
  process/log guard detects it. Managed matrices remain to run on the binary.
- npm-integrity-verified pi-ai 1.3.0 tarball differs from the source release
  data in `.manifest.json` and `openrouter.json`. Do not silently treat these
  inputs as identical. Select the published package input required by the
  PSMFD release workflow, retain the differences/identities, rerun affected
  static/build/test checks and then checkpoint/compile. Previous full-suite
  pass remains bound to the source-archive data.

- N2-L intended change: narrow read-only PSMFD candidate-qualification workflow
  on same-repository `sync/upstream-v1.3.0`, using Node 22 and the selected model
  archive digest, to execute the retained Linux/Bubblewrap release-build gate.
  No Linux runtime is installed on this Mac. Register the workflow under the
  existing execution policy, validate YAML locally and observe exact-head
  hosted results. No release, secrets, protection or existing platform routing
  change is part of this check.

## 2026-10-21 — N2 exact startup green; N2-S security review

- Source c7d2a827d7956fcdd8e2a300b5096f6b263dc579 is clean, committed and
  remotely backed up in draft Pi #83. Complete static/build/hermetic tests were
  repeated successfully with the selected npm model data. ARM64 standalone
  SHA-256 is 64a5c7c79c821457f47be724a4cc6de6eaf601e7bde4a1d6067b5b672f97b649.
- Direct 18-case matrix, exact preserved published host with two profiles and
  offline disposable Tart qualification pass, including MCP process canaries.
  Tart state confirms clone cleanup; independent listing shows all VMs stopped.
  Hosted Linux release-build job 119388342468 passes, including Bubblewrap.
- CodeQL aggregate fails with eight newly imported alerts (88–95). Full data
  flows are retained locally for review; language-analysis jobs themselves pass.
  No alert has been dismissed. Review identifies regex backtracking, returned
  internal errors and possible command-reference disclosure; HTML escaping,
  fixture-only operations and Chord draft semantics require executable proof.
- N2-S intended change: check current upstream source/open fix PRs, retain
  false-positive evidence, register bounded S-class corrections where needed,
  run failing-then-passing tests and static checks, checkpoint and read back
  security analysis. Do not claim prior binary evidence for changed bytes.
- Current sidecar audited locked restore and Release build pass (zero warnings).
  First test launch failed on sandbox-denied named pipes; unsandboxed retry
  passes. Protocol recordings, mutations and final source CI are next.

- N2-S four real findings reproduce: ambiguous whitespace exceeds an external
  one-second parser limit; HTTP errors return a synthetic internal path;
  failed credential command text is exposed; plain and actual Chord containers
  mutate Object.prototype when streamed JSON contains `__proto__`. All controls
  pass after corrections. Retained before/after runner restores source bytes.
  Pending S-class 022/023/024 register only the reviewed source/test paths;
  manifest/text/both guards agree and C accounting remains 6 / 23 / 2,021.
- Latest upstream main retains affected code and the open-PR search found no
  applicable fix. Private upstream reporting is left for maintainer assessment;
  no message or report was sent. Actual OAuth callback/HTML escaping proof
  passes; fixture-only SCRIPT extraction and public URL hashing are reviewed
  separately as false positives. No broad scan suppression is introduced.
- Baseline RPC recording passes all 12 scenarios twice with the exact preserved
  0.87.1 comparison binary. All original goldens restore byte-for-byte. Full
  static check, catalog tests, actionlint, release block and repository checks
  pass; final complete Pi suite is running on the corrected source.

- Security follow-up 28741c97f9baf5649e8ad3ba1c996d7bbcb5abd2 is committed,
  secret-scanned and remotely backed up. Complete Pi suite passes (coding-agent
  2,738 / 50; both durable modes 1,220 / 4); new binary digest is
  901a10f67a06f04310f4d064e95f7e3e60ec523d253366731e077a239237407f.
  Direct and published-host matrices pass; offline Tart reports completion.
- Candidate RPC recorder fails determinism in four of twelve scenarios solely
  at the newly added durationMs clock value. Eight scenarios pass; raw pairs
  and failure details are preserved and production goldens restore. N2-R
  intended change: add narrow duration-field normalization with a red-first
  unit test, then rerun baseline/candidate pairs under the same normalizer and
  review protocol drift. Do not mask message content, costs or token counts.
- GitHub alert review initially skipped null top-level state for PR-only alerts;
  switched to instance state. First mutation attempt is rejected by the API's
  280-character comment limit. Short rationale retry is pending readback; full
  rationale remains in the review helper and will be retained as evidence.

- Four real alerts 88/92/93/95 now read fixed; four reviewed false positives
  89/90/91/94 read dismissed with specific rationale. All eight exact-source
  checks pass at 28741c97f, including CodeQL and hosted Linux/Bubblewrap.
- Narrow duration normalization fails its new test before the change and passes
  all three normalizer tests afterward. Baseline and candidate now each pass
  all twelve RPC scenarios twice under the same normalizer. Both raw recording
  sets remain available; production goldens restore unchanged. Protocol drift
  review and runtime/host mutation controls are in progress.

- FingerTrap fixture checkpoint 0143c3317db5343d004bbfa1b75fd566b87c1b6c
  is secret-scanned and remotely backed up in #209. Its unchanged hosted
  Windows/Linux lanes are passing; five macOS jobs are draining locally on
  disposable Tart runners under the existing runner-group policy.
- All four rebuilt runtime regressions are caught at their expected external
  checks (migrations, resource discovery, crash discovery and bug-report fallback).
  Source and clean package build restore afterward. Host policy-check removal
  fails exactly the bad-policy descriptor case; restored full sidecar suite
  passes 409 tests with 12 opt-in skips. First helper read failed because the
  test detail log is UTF-16; exact source still restored, then decoding was
  corrected and both red/green checks completed.
- Structural protocol review confirms identical outbound commands and process
  controls across all twelve scenarios. Inbound differences are Pi version,
  added started/queued disposition, elapsed timing, thinkingLevel, settled
  aborted field and system documentation for MCP/codemode. Full changed paths,
  raw recordings and drift patch are retained. No production golden was changed.

## 2026-10-21 — Authorized test scope and additional safety checks

- Maintainer clarified that reported additional safety checks concern testing
  this owned app and repositories. Read the linked OpenAI Help Center guidance:
  [additional safety checks](https://help.openai.com/en/articles/20001326-additional-safety-checks-for-biological-and-cybersecurity-requests-in-chatgpt-codex-and-the-api).
- Continue with narrowly scoped defensive validation: owned FingerTrap/Pi
  checkouts, synthetic data, loopback fixtures and disposable local VMs. Keep
  security controls limited to proof needed to verify prevention/remediation;
  omit unrelated exploit detail. Preserve production approval gates.
- No automated safety rejection text or request ID is available in this session;
  do not invent either. If a block recurs, retain its exact message, product/model,
  date/time/timezone and request ID when available for redacted support review.
  Do not disable, evade or claim to override platform safeguards.
- While making the Chord retirement control portable, a default-stack standalone
  probe did not reproduce the historical 100,000-item overflow; larger inputs
  can also exceed the replacement's public JavaScript call envelope. Existing
  source/artifact qualification remains green. Record this reproducibility limit
  and use native and replacement controls before claiming the original red proof.

- Portable Chord proof now pins an 850 KiB Node 22 stack. In the same process,
  native push and the unchanged replacement accept 100,000 inputs, while the
  exact original upstream module throws. Exact contents/replay/adoption, 27
  splice cases and 20,000-item middle insertion pass. Default stack did not
  reproduce; a smaller 820 KiB stack also exceeded the replacement's public
  call envelope and was rejected as proof. The explicit controlled budget is
  retained in the executable fixture and output rather than hiding this limit.

## 2026-10-21 — N2 qualification complete; production decision remains gated

- All eight Pi checks at 28741c97f and all 23 FingerTrap source checks at
  0143c33 pass. Five macOS jobs ran locally; all five VM/runner cleanup records
  are clean. Independent final GitHub readback shows zero registrations;
  independent Tart listing shows no disposable clones and all VMs stopped.
- Downloaded Apple artifact 12376529013 matches GitHub's SHA-256. App/DMG
  checksums, three ARM64 binaries, runner identity and build merge parents/tree
  verify. The merge tree equals tested source 0143c33. Packaged shell smoke
  passes; product Pi smoke explicitly skips. Separate runtime/host/Tart matrices
  establish candidate managed behavior; no GUI or activation claim is inferred.
- Latest stable rechecked at completion: upstream remains v1.3.0; fork remains
  v0.87.1-psmfd.1. No release/tag/pin changed. Final digest-bound qualification,
  dependency disposition and promotion/rollback preparation are retained beside
  the evidence README. #210's production decision/verification item stays open.
- Copied the exact runtime/host, selected npm model archive, source archive and
  Bun archive outside temporary storage. All 215 copied files match sources;
  local inventory SHA-256 is 67587ddb8e4d1d4d5755b4edc1aa0fe12dc76141fae1e2e83b6f1c6c52096ef6.
  Preserve .artifacts/pi-next/preserved-20261021 across patching. Source/evidence
  are backed up at 0143c33; the final documentation checkpoint follows after
  checks. Next action is maintainer review of Pi #83 and promotion/rollback
  record, including remaining Gondolin/node-forge example findings.

- Final qualification/decision checkpoint f8225ebc125aede6cc226fdf8bf74222ac550107
  is committed and remotely backed up. Scoped secret scan passes; its first
  launch could not write the sandboxed report cache, and the approved retry
  passes. Markdown, JSON parsing, local links, script syntax, structural and
  whitespace checks pass. Source CI remains attributed to 0143c33 / 28741c97f.
- Existing Pi #82/#83, FingerTrap #209 and #210 descriptions now reflect the
  final results and were independently read back. #210's first five items are
  complete; the last production decision/verification item remains unchecked.
  No issue was closed, PR merged, release dispatched, production pin changed,
  SDK switched or managed mode activated. The next action is recorded at the
  top of STATUS; there is no outstanding command or disposable runtime process.

## 2026-10-21 — S3-M1 open-PR review and integration authorized

- Maintainer explicitly requests inclusion of review/merge of open PRs so the
  repository is clean for continuation. Source merge authorization supersedes
  the earlier wait-for-merge decision; release/pin and activation gates remain.
- Intended change: inventory and review all eight FingerTrap PRs and the three
  Pi campaign drafts; assess dependencies/threads/rules, resolve redundant or
  stale proposals, merge reviewed heads in dependency order, verify protected
  branch results and leave the local checkout synchronized without losing the
  operator's three private untracked entries or sibling checkout state.
- Validation: exact-head diffs and existing checks first; rerun affected checks
  for changed inputs, verify merged tree/commit/CI and cleanup. FingerTrap #199
  targets main, contrary to feature/dependency branch convention, so assess its
  current dev equivalent before any retarget/merge. Pi #81/#82 are older
  alternatives; do not merge their old-base trees over the latest candidate.

- Pi #83's eight exact-head checks pass. Four remaining quality threads were
  reviewed against full context and upstream identity: two inferred-type
  regression checks, one deliberate pre-GC reference release, and one unused
  example assignment whose awaited initialization is preserved. Record rationale
  in evidence/pr-merge-20261021/README.md before resolving. No source edit needed.
- Dependency lock updates overlap and most proposals predate local CI routing.
  Plan one reviewed reconciliation PR against merged dev, with full combined
  validation, then close incorporated originals. #199 must not merge into main.
  #188's architecture proposal is superseded; its implementation issues stay open.
- Review checkpoint 297528a is committed. Initial formatting invocation from the
  repository root found no package; corrected invocation from src-ui identified
  the new review table's alignment. Prettier corrected that file before push.
  Tart readiness check passes (group 5); the GitHub connector cannot resolve
  review threads, so use the existing per-command TheSemicolon CLI identity.
- Review/format checkpoint d1891e7 is scanned, committed and remotely verified;
  fresh #209 protected CI is running, five macOS jobs on local Tart.
- Pi #83 merged at 5f1f1dfbacb508086ed9b68563c78c3aa1456d7c with parents
  42429b94 / 28741c97 and identical qualified tree 46fd5cc4. All four reviewed
  threads are resolved with individual rationale. #81/#82 closed as superseded.
  Readback found #82's branch absent after merge cleanup; its commit is retained
  in #83 ancestry and locally. Restore that exact remote reference, then verify.
- Dependency worktree created on chore/reconcile-open-dependencies from dev
  9bfc092. Cargo #198 patch applies cleanly; UI proposals combined in package.json.
  jsdom 30 requires a higher Node patch minimum; record engines and align setup
  guidance. Sandboxed registry DNS failed; stopped that install and retried with
  network approval. No install lifecycle scripts enabled during dependency review.
- #82's exact branch 95e95f0 is restored and remotely verified after GitHub's
  cleanup removed it. #188 is closed as superseded, with #186/#187 left open.
- #209 head d1891e7 passes all 23 checks and squash-merges to protected dev at
  cc3bf8855b0f748c04c44a92093a9a708381f264. Merged tree equals checked head;
  four dev macOS jobs are draining locally. Pi main's six post-merge checks pass.
- Combined dependency graph initially resolves newer releases under caret ranges;
  narrowed to reviewed proposal versions. Audit then identifies nine advisories
  (three high) in stale Vite 8.3.1 and brace-expansion 5.0.9. Select Vite 8.3.3
  and brace-expansion 5.0.12 fixes; post-fix audit exits zero. Retain concise
  advisory IDs/ranges and counts, without unnecessary reproduction payloads.
- All 100 UI tests pass on Node 22.23.0 with Vitest 5.0.0/jsdom 30.0.1; build
  and ESLint pass. Native cargo test first fails because the isolated checkout
  lacks required sidecar binaries. Staging existing local fixtures resolves the
  prerequisite; cargo test --locked passes. Fresh source staging/package CI will
  validate the real bundle. Structural check passes with zero warnings;
  actionlint, shellcheck and whitespace checks pass. All six dependency PRs have
  no unresolved review threads. Release action v8 uses Node 24 and requires
  semantic-release >=20; this repository has no incompatible version pin.

- Final local dependency checks: Clippy passes with warnings denied; UI audit
  reports zero findings. Corrected a Markdown line beginning with a PR number;
  all three tracking/review files now pass Markdown lint and formatting.
  Prepare a draft checkpoint with CI skipped solely until its PR number is
  enrolled in existing local Tart routing, then queue the full required suite.
- Dependency checkpoint 5ccfd5a is committed and secret-scanned. Restored the
  proposed vscode-jsonrpc caret range; offline lock regeneration lacks cached
  metadata and a sandboxed frozen install uses a different store. Align only
  its lock specifier and verify the frozen install in the normal store before
  backing up the draft. Resolved versions and previously tested code are unchanged.
- Corrected frozen lock install passes in the normal pnpm store. Checkpoint
  2a2c6a0 is secret-scanned, remotely backed up and opens draft #211. All 19
  post-merge dev checks pass at cc3bf88; its four disposable Tart VMs are cleaned.
  Before the full source CI push, add only owned PR #211 to the trusted policy,
  keeping the immutable worker and runner-group repository/workflow restrictions.
  Apply/read back routing, then push without skip-ci and drain five local jobs.
- Dependency #211 passes all 23 checks at 42bb1b2 and squash-merges at
  260aec5067d37247b240a8641829bc520ae74bf3 with the identical reviewed tree.
  Apple artifact 12382323858 verifies archive digest, app/DMG hashes, ARM64
  binaries and synthetic merge parents/tree. Packaged shell smoke passes;
  Pi spawn explicitly skips because the bundle does not install Pi.
- Final S3-M1 tracking slice: reconcile all PR dispositions; update source-merge
  versus release-gate language, README Node guidance and qualification metadata
  index. Retain source CI/merge/artifact evidence; run Markdown/JSON/link/structural
  checks and protected CI, merge the final documentation PR, then fast-forward
  the main checkout without removing private untracked files. Source post-merge
  macOS checks are draining; no controller or standalone Tart run overlaps.
- Source backlog disposition readback: FingerTrap and Pi each have zero open
  PRs. All six dependency originals are closed with incorporation links; #188
  is closed as superseded. GitHub marks #82 merged by the same #83 commit,
  because it is an ancestor; no separate old-tree merge ran. The saved #82
  reference remains restored. Exact states and check/artifact records are copied
  into evidence/pr-merge-20261021; publication and private upstream reports remain
  untouched. The final tracking branch starts at merged dev 260aec5.
- Final tracking checks: six Markdown files lint clean after correcting a line
  beginning with a PR number; formatting, JSON parsing and all 72 direct local
  links pass. The 261-entry qualification index is refreshed only for its two
  changed narrative metadata files; every other indexed byte identity verifies.
- Tracking checkpoint 993e709 is committed, scanned and remotely backed up in
  draft #212. All 19 dependency post-merge checks now pass at 260aec5; its four
  local VMs are cleaned. Record those exact checks and enroll only owned #212
  for the existing pinned worker. The controller is stopped before cutover.
- Main checkout is fast-forwarded to dev 260aec5 with a clean tracked tree and
  frozen UI dependencies synchronized. Our earlier tracking edits are verified
  in committed 5ccfd5a and preserved in .artifacts/pr-merge-20261021/root-tracking-backup.
  The three private untracked entries remain; no operator sibling checkout moved.
- #212 passes all 22 head checks and merges at 9d8b892b1569fa5b75d360a663c54c0063a8f6ed;
  main checkout fast-forwards. Its final dev run 39683025954 fails one Linux
  sidecar test (408 pass / 12 skip); compilation succeeds. Retrieve exact detail
  from digest-verified artifact 12385226610 before diagnosing or retrying. Other
  jobs remain active. Source backlog remains closed; final verification is not
  yet complete. Failure evidence is preserved locally under pr-merge-20261021.
- S3-M2 diagnosis: malformed-line golden expects parse response, then hello,
  then stdin EOF. The scenario waits only for the response; the test thread can
  record EOF before the stdout pump records hello. Production already exposes
  WaitForHelloAsync, but the default 500ms legacy grace is unsuitable for a
  deliberately delayed handshake regression. Add a test-harness HelloGrace
  override bounded by EventBudget, and await readiness in this scenario. First
  prove failure with a FakePi delayed hello via RecordScenarioHost, then verify
  all golden transcripts unchanged and run the full suite. No runtime change.
- The delayed-hello regression fails before the fix at its exact ordering
  assertion; all twelve existing replay cases pass. The child delays hello by
  one second, exceeding the default 500ms legacy grace. Implement the scenario's
  event-budget handshake override and explicit readiness await; next run focused
  and full sidecar checks, preserving the original failure and red test logs.
- S3-M2 green: all 13 focused cases pass; full sidecar suite passes 410 tests,
  with 12 opt-in recorder skips. The golden-data diff is empty. Preserve original
  Linux failure, local red assertion and green/full logs in evidence/golden-handshake.
  Change scope is three test-only C# files and tracking/evidence. Production
  runtime, normalizer and transcript bytes are unchanged; qualify the fix through
  a dedicated PR under the same restricted local Tart/hosted Linux/Windows policy.
- Test/evidence checkpoint 7e4e727 is committed, secret-scanned and remotely
  backed up in draft #213. Enroll only this owned PR in the existing trusted
  policy, preserve the immutable worker and runner-group restrictions, then
  queue full protected CI and Apple packaging. Previous dev runner drain is
  complete; no local Tart controller remains active before cutover.
- S3-M2 protected completion: #213 passes all 23 distinct head checks at
  0a287c4eb6769ee810330de6d625861f241f65ce and merges at
  b0292a9fb3c38f70abfec534d004c6ccf13dbac4 with the same source tree.
  All 19 dev checks pass. Apple artifact 12385811971 matches its GitHub digest,
  checked tree and merge parents; app/DMG and all three ARM64 binaries verify,
  and packaged shell smoke passes. Pi spawn explicitly skips without installed
  Pi. All five PR and four dev disposable Tart VMs/registrations are removed.
- S3-M3 starts after the maintainer reports CI concerns. A newly created #214
  proposes Vite 8.1.6 against older main. Inspect its exact dependency delta,
  Gitflow and platform logs; compare current dev's dependency fixes and checks.
  Record why current dev passes and main still fails, close an already
  incorporated proposal, and finish this tracking update through protected CI.
  No release promotion, security alert dismissal or protection bypass is in scope.
- S3-M3 diagnosis and disposition: #214 has no unresolved review threads and
  changes only the UI manifest/lock. Its Vite 8.1.6 intent is superseded by #211's
  audited 8.3.3 graph. All six platform failures are NU1902 for main's MessagePack
  2.5.302; dev has explicit 2.5.303 since #207. Gitflow correctly rejects its
  main base. Saved exact error excerpts, job URLs and all 19 successful dev
  checks in evidence/pr-merge-20261021/final-ci-readback.json. Closed #214 with
  a reviewed explanation and independent state readback; no alerts dismissed.
  Main remains older until normal release promotion. Next: validate these
  tracking files, checkpoint and complete protected CI, then leave #210's exact
  runtime release/pin and example dependency disposition as the concrete gate.
- S3-M3 checkpoint bb326a1 is committed, secret-scanned and remotely backed up
  in draft #215. Markdown, formatting, JSON, all 38 direct local links, whitespace
  and repository structural checks pass. Enroll only owned #215 in the existing
  Tart policy, keep the immutable worker and restricted runner group, mark ready
  before the full CI push, then run all required checks and merge normally.
  This documentation slice introduces no production or test behavior change.

## 2026-10-22 UTC / 2026-10-21 ET — CF-0 client foundation tracking

- Maintainer selected the foundation-first sequence: packaged-client acceptance,
  completed profile isolation and a supervised JavaScript/Pi SDK worker before
  further extension catalog import/review. Prepare durable trackers using the
  established status/log/plan/evidence pattern, then discuss item 1 before
  execution. This is authorization for tracking and plan preparation only.
- Before the slice, dev/origin/dev match 6fe2627d040b385fa96fdd91f7341c39214a112f;
  #215 is merged, all 22 head and 19 post-merge checks passed, and both source
  PR backlogs are empty. Private untracked evidence is preserved. The global
  Stop-hook output bug is tracked independently in agent-framework-codex #19;
  it does not replace or block explicit validation.
- CF-0 intended result: umbrella/baseline issues linked to #201/#202/#210,
  ordered foundation plan, proposed baseline execution/acceptance/evidence
  contract, current resume action and scoped backed-up documentation commit.
  Validate Markdown/HTML/JSON/local links, structural checks and secret scan;
  preserve separate production release/pin, SDK adoption and activation gates.
- CF-0 remote result: foundation umbrella #216 and item-1 baseline #217 are
  created and independently read back; #216 links #217 in its ordered checklist.
  No baseline case, client fix or worker implementation has started. Existing
  #201/#202 retain profile/SDK ownership; #200 catalog work is deferred by the
  maintainer's current sequencing, with only existing meter integration in CF-3.
- Prepared client-foundation-plan.html with all ten areas and the proposed
  CF-1A–F execution/file/acceptance plan. Added a twelve-case NOT RUN worksheet
  and explicit prior-artifact reference. It names #213's actual artifact source
  and synthetic build merge; it does not attribute those bytes to current dev.
  Baseline captured is distinguished from baseline accepted; GUI/provider skips
  never pass. Next validate the tracking slice and reconcile issue cross-links,
  then checkpoint/back it up before discussing CF-1 execution with the maintainer.
- CF-0 issue reconciliation: #200/#201/#202/#210 now link #216/#217 and preserve
  their open acceptance/activation gates. #202's superseded 0.87.1 candidate
  sentence now names the qualified Pi 1.3.0 CLI source while requiring a separate
  same-source SDK closure. All four updates were independently read back.
- Local tracking verification passes: six Markdown files, new HTML/evidence
  formatting, all 85 direct local links and eight unique HTML IDs, twelve NOT RUN
  worksheet rows, prior-reference metadata consistency, whitespace and structural
  checks. First Markdown lint caught two issue-number lines parsed as headings;
  corrected those and reran green. Preserved original formatting of nine
  unchanged tables. No browser-render, GUI, provider or CF-1 runtime pass is claimed.
  Next commit/scan/back up a draft, enroll only its owned PR in the existing Tart
  routing, and finish protected tracking verification before the plan discussion.
- Tracking checkpoint 7f75c700bc268bfad7e66dd7d7bda716f1ab4c4d is committed,
  secret-scanned and remotely backed up in draft #218. Current runner group 5
  and immutable worker policy verify. Enroll only this owned PR, keep the
  restricted repository/workflow boundary, mark it ready before the final source
  push, and run protected CI with four local macOS jobs. CF-1 remains unstarted.
  UTC crossed midnight during tracking; current timestamps explicitly distinguish
  2026-10-22 UTC from the maintainer's 2026-10-21 ET date.

## 2026-10-22 — CF-0S production-baseline-only decision

- Maintainer answered the item-1 scope question: establish the current production
  baseline only; handle Pi 1.3.0 comparison as a separate item. CF-1 runtime is
  v0.85.1-psmfd.1. This is a scope decision during plan discussion, not a claim
  that baseline execution or the complete implementation plan is approved.
- Before this documentation slice, dev/origin/dev match
  538ff32df0759c2237ea685997d519df3501dd8b. Tracking #218 passed all 22 head and
  19 post-merge checks; #216/#217 contain verified checkpoint links. Private
  untracked evidence remains preserved, with no CI controller or baseline run
  left active by that checkpoint.
- Intended result: a separate deferred comparison issue linked to #216/#217/#210,
  consistent production-only scope in STATUS/plan/worksheet and live issues,
  focused Markdown/HTML/link validation, scoped commit and remote backup.
  Continue item-1 plan discussion; do not run CF-1 or the comparison from this
  clarification alone.
- Separate comparison #219 is created, linked from #216/#217/#210 and
  independently read back. It is deferred pending the accepted production
  baseline and its own plan. Replaced optional-comparison wording in the local
  plan/worksheet and recorded production-only scope in the current resume action.
  No baseline harness, acceptance case or candidate comparison has started.
- Focused verification: corrected issue-number/Markdown table lint findings;
  Markdown, HTML/local links and reference/worksheet consistency checks pass.
  Checkpoint the scope refinement in a draft documentation PR while the rest of
  the plan is under discussion. Full protected CI/routing enrollment is pending
  that review and must complete before merge; no required check is waived.
- Scope checkpoint 674805a8dc4a26a36edc5adb6d74b266934b01b5 is committed,
  secret-scanned (zero findings) and remotely backed up in draft #220 against
  dev. No harness, acceptance case, comparison, CI controller or activation was
  started. Continue the remaining CF-1 harness/GUI/provider plan discussion;
  after that review, enroll only owned #220 in existing Tart routing and push
  without CI skipping so all protected checks complete before any merge.

## 2026-10-22 — CF-1A/B production-client baseline execution

- Maintainer says "Proceed" after the production-only CF-1 scope and harness/
  observed-GUI division are recorded. Execute the CF-1A–F plan using production
  Pi v0.85.1-psmfd.1; #219 comparison and all activation/SDK/release gates remain
  separate. Baseline acceptance still requires observed mandatory cases and
  recorded maintainer acceptance.
- Starting branch docs/client-baseline-scope at
  1e2a7507d04f799e31d5356bd1f656bfcec595e1 is backed up in draft #220 against
  dev. Preserve three private untracked entries, artifact evidence and sibling
  checkouts. Intended first slice: input/provenance verification, bounded owned
  Tart staging/results/cleanup harness and guard regressions. Record first
  failures, real GUI/provider blockers and exact next action durably; enroll
  only the owned PR for full protected CI before merge.
- Validation planned: wrong package/runtime digest and unsafe archive rejection,
  refusal of unowned VM cleanup, failure retention and rejection of incomplete
  acceptance, with a red-first guard mutation. Reverify existing candidate bytes
  and package-relevant source inputs before reuse. Inspect clean macOS base and
  running CI before any standalone guest launch.
- Implemented initial harness and 15 passing guard tests; add their kept lane
  to the existing repo-check job in .github/workflows/ci.yml. Exact production
  release asset 553290482 verifies SHA-256 e0e0331d478af7724fa24d780a8d4abc0033c6f5ee7c0d45dddae6f922e5c4cb;
  tagged source is e7d09c9dc5fca409f46a5476669e3c0404054d69 and the archive's Pi
  executable is 811d65137d4272b30a6b2bd9a4093218b8f94bb8f55a9db9ccf8d443c4d4b2a5.
  Remote Git commit data independently confirms the app's historical synthetic
  build tree and both parents; the absent local merge object is not a blocker.
- First prepare attempt 20261022T160004Z-2c7099cfa211 retained its failure:
  the conservative source guard classified the newly added repo-check Python
  lane as a package change. Narrow the check to application/runtime package
  inputs and the actual apple-silicon-candidate build workflow. No package
  source changed. Tart get does not support cached OCI names; inspect the fixed
  cache config/manifest and clone by pinned OCI digest instead. No VM started.
- Package preparation now passes, including all three ARM64 binaries and both
  nested package digests. Initial guest preflight found a local OCI cache path
  mismatch before cloning: Tart stores image/digest as separate directories.
  Correct that fixed path and retain preflight failures in the run's event log.
  Next retry the owned clean guest; no existing VM or host settings changed.
- Owned guest runs macOS 15.7.7/24G720 ARM64. The stock pinned image includes
  Homebrew, Node/npm and Command Line Tools. Moved only those guest tool trees
  aside before first launch and verified Node/npm/brew unavailable; no host
  tools moved. Finder AppleEvents timed out before installation. A separately
  recorded ditto/LaunchServices attempt starts app PID 826 and sidecar PID 834,
  but the headless screenshot does not show a usable app window. This is not
  GUI acceptance or an established product defect. Retain the failed substrate
  attempts and try a visible Tart console before declaring a GUI blocker.
- Correct cleanup ownership to use the persisted clone hardware/MAC config;
  Tart get includes mutable running state and allocated size. Preserve the
  current unique clone receipt during the transition, remove only that guest,
  then verify the corrected harness on a fresh visible-console guest. Screenshot
  retrieval also needs bounded chunks; oversized guest-agent output disconnects.
- Corrected-harness visible run 20261022T175149Z-c22f92748f50 stages the same
  verified app/production runtime. Finder again times out before installation;
  no usable app window is established. The last GUI-context follow-up reaches
  the already-cleaned bounded session and does not execute. Both guests are
  independently absent, no VM is running, and every original VM is preserved.
- Captured twelve mandatory results: CF1-01 PASS, CF1-02 through CF1-12 BLOCKED.
  GUI participation/test login and selected exact prior-app bytes remain missing
  inputs. This is captured with blockers, never baseline accepted. Supplementary
  packaged-sidecar smoke on the host passes shell echo, real Pi PTY output and
  Pi termination using the exact 0.85.1 executable, temporary HOME and explicit
  agent directory with no inherited provider environment. It bypasses Tauri.
  Preserve only sanitized summaries/excerpts publicly; raw screenshots/logs stay
  ignored. Runtime pin, operator home, SDK and managed activation remain intact.
- All 15 kept harness guards pass. Isolated removal of archive traversal and
  GUI-observation guards proves red failures before the unchanged source passes;
  retain guard-red-green.txt. Ruff checks pass; formatting was corrected after
  its first check. Next validate documentation/evidence/CI workflow, checkpoint
  and enroll only owned #220 in the existing Tart routing for protected CI.
- Local verification now passes: 15 harness regressions, both red-first guard
  probes, Ruff lint/format, actionlint, Markdown, HTML/JSON formatting, 90 local
  links, eight HTML IDs, every case evidence digest and structural/lock-shape/RPC
  checks (zero errors/warnings). Corrected one table-alignment and HTML-format
  finding. No product source changed and no blocker is waived. Checkpoint this
  reviewed slice, secret-scan/back it up in #220, then enroll that owned PR for
  the existing full CI matrix, preserving its pinned worker and runner group.
- Checkpoint 11f92741c0ee6f1779c8028e297301a7e62e81d8 is committed,
  secret-scanned (zero findings) and remotely backed up in #220. PR title/body
  now describe the implementation and blockers; #217 records approval/capture
  and is independently read back. Existing runner group 5 verifies. Add only
  owned #220 to the trusted list, apply existing restricted server routing,
  mark ready and push the final source without CI skipping. No standalone
  guest remains active before CI; runtime/package/GUI acceptance is unchanged.
- Protected head f86413f7e7bf31a69e874e9bf88367d7d7b8c56d passes all 22
  distinct checks, including repo-check's kept harness suite and every hosted
  Linux/Windows/local macOS lane. CodeQL's transient missing-configuration
  notice clears as all analyses complete; its final conclusion is success.
  All four local CI clones are cleaned. PR review has zero unresolved threads
  and normal clean merge state. Retain source-ci.json with exact check URLs.
- Final documentation checkpoint records the source result and the concrete
  resume action: arrange an observable guest session, supported test login and
  exact previous-app bytes, then repeat eleven blocked rows. It must pass its
  own protected checks before merging #220 normally. Final head/merge identities
  and post-merge readback belong in #220/#217 without fabricating a self hash.
  #217 remains open, baseline not accepted, CF-2 unstarted, #219 deferred.

## 2026-10-23 UTC — CF-1C observable GUI continuation

- Maintainer says "Proceed" after #220 merged at
  8e5fd0cb60748ba197c60928e3b0c4895fbb1ba8 with 22 head and 19 post-merge
  checks passed. Main checkout matches origin/dev with only the three preserved
  private entries untracked. All original VMs are stopped; no disposable
  baseline/CI VM remains. Recheck remote state and exact input digests before use.
- Intended slice: establish guest GUI input/frame observation through Tart's
  native console/VNC path, since guest AppleEvents timed out. Inspect installed
  CLI and primary source behavior first; use only a loopback connection to our
  uniquely owned guest, with no host-home/credential/clipboard sharing. Preserve
  first failures. Continue keyless actual-client cases and identify verified
  previous QA app bytes; supported real-provider login remains a separate input.
- Proposed harness change, if needed: add a bounded native-VNC console option
  to scripts/qualify-client-baseline.py, preserving ownership, shared CI lock,
  guest lifetime and cleanup guards. Validate mutually exclusive console modes,
  private connection metadata and actual owned-guest cleanup. Add only meaningful
  guard regressions for changed behavior. No client fix is assumed before a
  concrete reproduction; no Pi 1.3.0 comparison or SDK/backend activation.
- Installed Tart 2.32.1 primary source confirms native Virtualization VNC with a
  generated per-session password and 127.0.0.1 URL. Add mutually exclusive
  --vnc/--graphics modes; VNC runs with --no-graphics so no host viewer opens.
  Preserve connection data only in the private run's vm.log. A bounded QA VNC
  probe may be kept as scripts/client_baseline_vnc.py if it proves useful;
  restrict it to the owned active run/loopback endpoint and retain meaningful
  ownership/frame-size/input guards. No privacy database changes are authorized
  or needed for this virtual keyboard/frame path.
- Native-VNC run 20261023T004000Z-26339eea6cb5 reached guest-ready and wrote its
  private connection URL. Its thirty-minute deadline cleaned it before the
  separately launched framebuffer probe executed; that probe correctly refused
  the cleaned run. No GUI result is claimed. Consolidate boot/first capture and
  a bounded guest-only input queue in scripts/client_baseline_vnc.py, so the
  initial observation is atomic and subsequent virtual input stays inside the
  approved owned session. Preserve the same deadline, ownership, offline network,
  clipboard/share and cleanup restrictions; validate the queue/path/frame guards.
- Atomic run 20261023T132854Z-e69e67da8c60 authenticates to native VNC but the
  host framework aborts after its pseudo-encoding warning on the first pixel
  request. No app/UI case ran. Add DesktopSize capability negotiation and
  resize bounds, retaining real pixel updates as a capture requirement.
- The resulting stopped clone exposes a harness cleanup defect: tart stop on
  an already-stopped owned VM returns 2 and prevents deletion. A new regression
  fails before the fix, then all 17 baseline guards pass after checking Running
  before stop. The exact stopped clone is removed with receipt/config validation;
  prior failure history stays in its private state. Add six console bounds/
  ownership/replay guards to repo-check's scripts/tests discovery before retry.
- Run 20261023T133506Z-b727c20f4c58 captures a real 1824x1368 desktop frame
  after DesktopSize negotiation, but Tart's script wrapper exits before the
  first input and the controller cleans its guest. The wrapper had /dev/null
  input, sending EOF to its PTY. Keep a dedicated unwritten pipe open instead,
  close it after process exit, and retain one authenticated VNC connection for
  the observation session. Record process exit codes. No app case passes yet.

## 2026-10-28 — maintainer-requested reboot handoff

- Stop acceptance work now and preserve a scoped checkpoint on
  test/client-baseline-gui. Dev/origin/dev remain
  8e5fd0cb60748ba197c60928e3b0c4895fbb1ba8; #220 is merged with the already
  recorded 22 head/19 merged checks. Remote new Dependabot #221/#222/#223 are
  inventoried only; no dependency/runtime input changed for this handoff.
- Last aborted call left its edits in place but created no new run. The final
  input-pipe/persistent-connection change is implemented and Ruff-clean; fresh
  verification passes all 23 current local guard tests. It is not live-verified
  and has no new protected CI result. Baseline remains one mandatory input PASS
  and eleven BLOCKED cases; no app acceptance, production pin or activation.
- Independent shutdown check: all Tart VMs stopped, no disposable clone remains,
  and process inspection finds no QA driver, baseline controller or CI drain.
  All recorded created runs report cleanupVerified=true. The historical failed
  preparation created no VM. Original VM names and private files are preserved.
- Rehashed current app archive, production Pi archive, previous QA app archive
  and first native framebuffer; all expected digests match. Essential references
  and sanitized red/green cleanup evidence are saved with the handoff. Private
  binaries, raw logs/connection URLs, screenshots and run states remain ignored
  under .artifacts; retain that directory through reboot. No temporary helper
  under /private/tmp is required to resume the kept driver.
- Next concrete action: verify the checkpoint/remote and host tools, start the
  atomic VNC driver and confirm first frame plus one Go To Folder key chord on
  the live owned guest. Do not repeat the old separate probe against the stale
  active-console pointer. Full protected CI/enrollment/merge and all real client
  cases remain pending after this WIP checkpoint. Exact commands and prompt:
  HANDOFF-client-baseline-gui.md. No new guest or CI run starts before reboot.

- Reboot backup 977c128ed63e8bacf57dfce41af8022d745f1c27 is committed, secret-scanned and remotely
  backed up in draft #224. A subsequent status-only checkpoint records
  this identity; its own hash is read from git. No guest or CI was launched.

## 2026-10-28 — CF-1C post-reboot persistent-console retest

- Resumed at maintainer request. Saved branch/draft #224 match c8abce5; remote
  dev is unchanged at 8e5fd0c. #217/#219 remain open; six CodeQL checks pass,
  full protected CI pending. No runtime or dependency integration.
- Intended slice: live retest of dedicated input pipe/persistent native VNC,
  first frame then one guest Go To Folder chord; continue Finder/keyless cases
  only if the console survives. Preserve private evidence and production Pi
  v0.85.1-psmfd.1; Pi 1.3.0 comparison and activation stay deferred.
- Preflight passes: 23 guards, Ruff, all four reboot inventory hashes, Softnet
  digest/permissions, available controller lock, Tart 2.32.1/cryptography 49.0.0.
  All original VMs stopped, no disposable clone. GitHub readback required access
  outside the sandbox; per-command TheSemicolon identity succeeds unchanged.
- Next: launch kept atomic driver, inspect first frame and chord response, then
  record lifetime/input result before any app acceptance attempt.

- Live attempt 20261028T183917Z-4e765150c1e2 failed before creating a clone in
  the filesystem sandbox; retained failure. Approved outside-sandbox retry
  20261028T183947Z-e59ca2bacfab booted macOS 15.7.7/24G720, authenticated
  VNC and wrote a first framebuffer at 18:40:04 UTC plus active-console.json.
  It then recorded BrokenPipeError and immediately requested normal cleanup.
- Diagnosis: after capture and active-pointer save, the next operation is a
  flushed stdout print. No input requests exist, and the owned controller log
  has no boot/lifetime error. Strong evidence identifies a closed tool-output
  pipe at that status print, rather than an app crash; no traceback survives
  the vanished exec session, so the exact origin is inferred. No new relevant
  Tart/Python crash report was found. Persistent input is still unverified.
- Independently verified cleanup: cleanupVerified=true, clone absent, original
  VMs stopped/preserved, no QA controller/driver/Tart/Softnet process. Raw logs,
  frame and state remain private. Active pointer is stale again; do not reuse.
- Concrete resume: redirect atomic-driver stdout/stderr to a unique private
  .artifacts/client-baseline log and run with required Tart filesystem access;
  inspect fresh first pixels and send the handoff's single Go To Folder chord.
  Record live lifetime/input before app validation. No runtime/acceptance change.

- Maintainer says Proceed. Retest started with stdout/stderr redirected to private
  driver-20261028-retest.log, avoiding the vanished tool-output pipe. New owned
  run 20261028T184851Z-755e4f85b29e; planned observation remains fresh first frame
  plus one Go To Folder chord before any app acceptance.

- Redirected run 20261028T184851Z-755e4f85b29e remains guest-ready across
  repeated requests; first desktop is 2048x1536 (viewer renders it scaled).
  No output-pipe failure. Pointer input visibly opens Finder View/Go menus.
  Original Meta+Shift+G chord and Super variant return frame timeouts; a paced
  diagnostic Meta chord still does not show Go To Folder. Independent shared
  connection captures a real desktop; later original-connection captures can
  succeed. Thus lifetime is live-verified, keyboard/usable console still blocked.
  No app case attempted/passed. Preserve all queue responses/screenshots,
  including failed requests. Investigate bounded input/protocol correction
  before continuing installation; do not confuse viewer coordinates with PNG.

- Finished bounded diagnostic session normally. Run phase cleaned, events=[],
  cleanupVerified=true; external Tart listing and filtered process inspection
  verify original VMs stopped/preserved, owned clone absent and no QA processes.
  No app/source/runtime changes or acceptance claims. Canonical evidence:
  evidence/client-baseline/gui-continuation/live-retest.json (sanitized metadata,
  queue outcomes and private-image hashes). All screenshots remain ignored.
- Independent guest screenshot retrieval first hit the existing large-output
  transport limit; the smaller 320px copy retrieved successfully. The guest
  date/process readback works and the independent screen still shows desktop.
  Avoid attributing this to a GPU freeze or a product defect without evidence.
- Next bounded slice: compare a known RFB client's keyboard/pointer events and
  fresh frames against this minimal driver in a fresh owned offline guest.
  No mtell/vncdotool/vncviewer on PATH. Record exact isolated probe provenance
  before use, validate actual input/frame freshness, then continue Finder app
  installation. Do not churn through app rows until the console is usable.
  Keep stdout/stderr redirected privately; no Pi 1.3.0/#219 or activation change.

- Sanitized evidence checkpoint b9db2fe81e02c491b6775b70592374300ac5dc98
  committed, secret-scanned (zero findings), pushed to existing draft #224.
  Markdown/Prettier, all retained image digests/local links, diff whitespace and
  repository structure/lock/RPC checks pass. Only original three private entries
  remain untracked. This subsequent status-only checkpoint records that identity;
  its own hash must be read from git. Protected CI/review remain pending, no merge.

## 2026-10-28 — CF-1C independent RFB input comparison

- Maintainer says proceed. Start from backed-up draft #224 head 41c80e6 with
  original private entries preserved. Intended slice: compare an independent
  go-vnc client, pin v0.0.0-20150629162542-723ed9867aed (Cirrus Labs mtell's
  published go.mod uses this exact version), with the kept driver in one owned
  offline clean guest. Probe remains isolated under ignored artifacts; retain
  exact source/module hashes, loopback/ownership validation, bounded lifetime,
  read-only QA share, no host credentials/clipboard. Validate fresh frames and
  actual keyboard/pointer response before installation. Any harness correction
  requires named scope and meaningful regression/live verification.

## 2026-10-29 — maintainer-requested session wrap-up

- Independent run 20261028T194636Z-ceb986018619 / probe independent-8e000f6c323c
  authenticated and captured a 2048x1536 desktop using pinned go-vnc. Escape,
  paced Meta+Shift+g and paced Meta+Shift+G each had capture-timeout results.
  Probe process exited 0, which is not an input success claim. No post-input
  screenshot was captured before the 1800-second owned guest deadline.
- Next-day independent screenshot request safely refused the already-cleaned
  run. Keep the failed/expired attempt; no keyboard success, app case or root
  cause is established. The comparison reproduces frame timeout using a second
  implementation, so do not assume changing our wire encoder fixes it.
- Shutdown verified: state cleaned, cleanupVerified=true, events empty, original
  VMs stopped/preserved, owned clone absent, no QA controller/driver/comparison/
  Tart/Softnet process. Active-console pointer is stale. No new guest starts
  after the maintainer's wrap-up request. Preserve all ignored/private evidence.
- Durable sanitized rfb-comparison.json and exact probe source/module text
  fixtures retain provenance and reproduction. Private rfb-compare directory
  retains its module/build caches, original source/binary/provenance manifest;
  raw frame and probe logs stay in the run. No app/runtime dependency added.
- Concrete resume action: name/implement a bounded atomic boot/probe/immediate
  independent guest-screen+clock/cleanup wrapper, validating stale-run ownership
  and deadline cleanup. Run these within one approved execution rather than
  letting a separate approval consume the guest observation window. Existing
  helper's screenshot transfer uses bounded chunks. Do not start app cases
  until observed input and frame freshness work. #217 open, #219 deferred,
  production Pi v0.85.1-psmfd.1 and all existing gates unchanged.

- Wrap-up checks rerun: all 23 kept guards, Ruff, Markdown/Prettier, local links,
  exact probe/module/source/frame hashes and whitespace pass. No app/runtime
  source changed. Full protected CI/review remain pending. Downloading the pinned
  comparison dependency initially hit sandbox DNS; approved isolated download
  succeeded, then its build used GOPROXY=off. No host tool installation.

- Handoff evidence checkpoint 2cdf67406416f4d0cce3a5023af4bde7be66fd8b committed, secret-scanned
  (zero findings) and remotely backed up in draft #224. Repository structural/
  lock/RPC checks pass with zero errors/warnings. This subsequent status-only
  checkpoint records that identity; read git for its own hash. The PR body and
  #217 tracker receive the final source/branch identities and exact resume action.

## 2026-11-07 — CF-1C atomic comparison

- Maintainer resumes the handoff. Local branch and GitHub draft #224 match
  81341dd6df244fcf6d879e2ead0b6b50e2cde207, open/draft against dev.
- Intended slice: kept bounded boot/RFB/immediate independent guest screen and
  clock/owned cleanup wrapper. Pass the newly created run directly; never select
  the cleaned active-console pointer. Validate ownership, deadlines and failure
  cleanup before live use. Preserve existing private files and raw evidence.
- Production Pi v0.85.1-psmfd.1 unchanged; #219 comparison deferred. App validation
  remains gated on observed input and fresh frames. No new result/CI claimed.

- Implemented scripts/client_baseline_compare.py and an explicit fresh-run observer
  hook in the driver. Per-step probe (45s cap) is followed immediately by guest
  screen/clock, including timeouts; total observation budget 240s, guest hold
  480s. Existing guest cleanup runs in finally; wrapper checks external VM absence
  and original stopped VM preservation. Active pointer is neither read nor written.
- Local verification: 28 guards and Ruff pass. Deliberately dropping the observer
  call makes the new driver-failure guard fail; restored code passes. Deadline
  check also kills a real sleeping child through the command timeout.
- Preflight: four preserved input/frame digests, pinned private probe source/binary
  hashes and module sums, Softnet ownership/setuid/digest and free controller lock
  verified. Tart 2.32.1, cryptography 49.0.0; only original stopped VMs listed.
  Next: one complete bounded live execution with output redirected privately.

- Live atomic run 20261107T131951Z-0c27fad0d0fb completed normally. Initial and
  all three immediate post-input guest screen/clock readbacks succeeded (13:20:06,
  :10, :13, :32 UTC). Independent RFB captured after pointer and Escape; lowerg
  still timed out. Apple icon highlighting is visible after pointer, desktop after
  Escape, desktop without Go To Folder after the chord. Usable keyboard delivery
  remains unproven; no app case attempted/passed. Do not overclaim freshness from
  a clock readback alone. Sanitized metadata and private-file hashes are retained
  in evidence/client-baseline/gui-continuation/atomic-comparison.json.
- Controller events empty; normal owned cleanup and independent inventory verified
  five original VMs stopped/preserved and no disposable clone. Private evidence
  and stale active pointer preserved. Next: bounded fresh atomic comparison of
  pointer-selected Finder menu at native coordinates versus keyboard input, with
  independent screen after each; app cases remain gated. No production pin change.

- Implementation/evidence checkpoint fe7ec1471cae5751700bcc77a61e8b48fbe977d7 committed and pushed
  to existing draft #224. Tracked-file secret scan reports zero findings.
  Markdown/Prettier, 28 guards, Ruff, local links, all atomic evidence hashes,
  whitespace and repository structural/lock/RPC checks pass. This subsequent
  status-only checkpoint records the implementation identity; read git/draft head
  for its own SHA. Full protected CI/review remain pending, no merge/acceptance.

## 2026-11-07 — maintainer-requested session end

- End acceptance/diagnostic work at maintainer request; no further guest starts.
  Implementation fe7ec1471cae5751700bcc77a61e8b48fbe977d7 and status head
  3a71165aec179d376b91729d1a3c10ca7ed9af4b are committed and backed up in
  draft #224. Final wrap-up documentation follows; read git/draft for its SHA.
- Rechecked five original stopped Tart VMs, no disposable clone, no QA driver,
  controller, independent probe, Tart/Softnet or local CI drain process. Latest
  atomic receipt remains cleaned/cleanupVerified=true/events empty. Old active
  pointer untouched; private evidence and original untracked files preserved.
- Hosted readback at 3a71165 has successful repo-check/lint and several platform
  jobs; other checks remain in progress. No full protected CI/review, acceptance
  or merge claimed. Re-read current head checks next session.
- Reconcile #216/#217 and draft #224 with the completed atomic wrapper and
  persistent keyboard blocker; retain one input PASS/eleven BLOCKED, production
  Pi v0.85.1-psmfd.1 and deferred #219. Update worksheet index/ledger references,
  STATUS and handoff with a copyable next-session prompt.
- One concrete resume action: name/record the next bounded atomic comparison of
  pointer-selected Finder Go menu at native PNG coordinates versus keyboard,
  keeping immediate independent screen/clock and cleanup. Do not reimplement
  the wrapper or advance app validation before usable input/fresh frames work.

## 2026-11-07 — CF-1C-GO-1 native-coordinate Go menu comparison

Starting checkpoint and CI refreshed; named scope recorded before diagnostic changes:

Starting checkpoint `219daae53e3e28cd334156c0c776f52737252944` matches local
`test/client-baseline-gui` and open draft #224 against `dev` (`8e5fd0c`).
CI run [42377205471](https://github.com/psmfd/FingerTrap/actions/runs/42377205471)
completed successfully: all fourteen jobs pass. Exact-head checks: 24 successful,
CodeRabbit skipped, no pending/failing checks. Review and acceptance remain
separate; these results apply to the saved checkpoint, not upcoming changes.

**Named bounded slice, recorded before changing diagnostics:** CF-1C-GO-1.
Reuse the implemented atomic wrapper and pinned private probe; change only the
pointer step from Apple to Finder Go at native PNG `(520, 24)`, then Escape and
the existing paced Meta+Shift+g comparator. Coordinates were observed in the
2048x1536 first frame of run `20261107T131951Z-0c27fad0d0fb` (SHA-256
`6a44b071a7d70e697b089fb563150d5c30557596de3cfe261bbd5f8a8140bcac`).
Inspect the fresh first frame and each independent guest screen for matching
layout and actual menu/dialog changes before interpreting input.

Validation: kept Python guards, Ruff and repository checks; one fresh owned
boot/probes/immediate per-input guest screen+clock/cleanup execution with both
output streams redirected to a new private log. Retain the existing 240s
observation/480s guest bounds and independent cleanup check. Never read or write
the stale active-console pointer. Initial inventory: five original stopped VMs,
no disposable clone. Raw evidence and unrelated files remain private/preserved.
Next: update the named pointer step, validate, then run this single comparison.
No app validation; baseline remains one input PASS/eleven BLOCKED. Keep Pi
`v0.85.1-psmfd.1`, deferred #219 and all activation/backend/release gates.

CF-1C-GO-1 preflight: named pointer step and existing guard expectation updated; all 28 guards, Ruff and scripts/check.sh pass. Next: one fresh atomic live execution, both output streams redirected privately; no app validation.

Run `20261107T141221Z-dbb92f6fb20e` completed normally. Fresh native first frame
matches the coordinate reference. RFB shows the Go dropdown opening, then closing
after Escape: pointer response, limited Escape delivery and changed RFB frames
are observed. All three post-input RFB captures succeeded, including the chord;
Meta+Shift+g produced no Go To Folder dialog. The earlier timeout did not recur
in this run; that does not establish a fix or usable chord/text delivery.

RFB also reveals a **Screen Recording permission prompt for tart-guest-agent**,
behind the dropdown and persisting after Escape/chord. Immediate independent
guest screen/clock readbacks completed after every step, but those screenshots
omit both the dropdown and permission prompt. They show only the menu highlight
change, so they cannot corroborate window contents. Permission/focus interference
is a hypothesis; no root cause or app defect is established. No app case ran.
[Sanitized evidence and 29 private-file hashes](evidence/client-baseline/gui-continuation/go-menu-comparison.json)
retain exact observations; raw frames and logs remain private.

Cleanup verified: controller events empty, five original VMs stopped/preserved,
owned clone absent, no QA/probe/Tart/Softnet/CI-drain process. The stale active
pointer was never used. Wrapper source unchanged except the named pointer step
and existing test expectation; all 28 guards, Ruff and repository checks pass.
Result is implemented and locally/live verified; commit/backup/CI for this change
remain pending. Saved-head CI above does not transfer to this change.

**One concrete next action: CF-1C-CAPTURE-1**, a separately recorded bounded atomic
permission/focus observation in a fresh owned guest. Inspect the guest-only
Screen Recording prompt using native RFB pixels and supported guest privacy UI,
then compare pointer-selected Go To Folder with keyboard input. Verify independent
screens actually include visible windows before app validation. Keep immediate
per-input screen/clock, private redirected output, explicit ownership and cleanup.
No host permission changes or TCC bypass. Do not rerun a cleaned guest.

CF-1C-GO-1 implementation/evidence checkpoint `1ca324f6d85779a1081680e05eed5f5292d2f3a8` is committed,
secret-scanned (zero findings) and remotely backed up in open draft #224 against
`dev`. GitHub independently confirms the branch head. All 28 guards, Ruff,
repository structural/lock/RPC checks, Markdown/Prettier, local links, 29 evidence
digests and pinned-probe provenance pass. New CI run
[42381113002](https://github.com/psmfd/FingerTrap/actions/runs/42381113002) has
started; title/gitflow pass, full platform CI/review remain pending. No activation
or acceptance. This subsequent status-only checkpoint records that identity;
read git/draft head for its own SHA. Next remains CF-1C-CAPTURE-1 as described above.

- Implementation CI run 42381113002 now passes all fourteen jobs, including all
  hosted macOS lanes. Title/gitflow and four CodeQL language analyses pass;
  Rust remains in progress, aggregate CodeQL neutral, CodeRabbit skipped at
  readback. Full CodeQL/review remain pending. Documentation-only status
  checkpoint follows with CI skipped; source CI is specific to 1ca324f.
- Prescribed local drain started one unused runner because this draft's jobs
  ran on hosted macOS. Sent SIGTERM to owned drain PID 6993; its finally handler
  cleaned ft-actions-job-20261107t142044z-4dbc3bf5 with cleanup_errors empty.
  Drain exits 1 with InterruptedError(signal 15), an intentional cleanup, not
  a CI test failure. Independent readback: zero GitHub runner registrations,
  five original stopped VMs, no clone/QA/probe/Tart/Softnet/drain process.
- Final local checks: 28 guards, Ruff, repository checks, Markdown/Prettier,
  local links and all 29 private evidence hashes pass; tracked-file secret
  scan has zero findings. No app result or gate changed. Concrete resume action
  remains CF-1C-CAPTURE-1, supported guest permission/focus observation before
  input comparison; see STATUS/handoff for the one-execution reproduction.

## 2026-11-07 — CF-1C-CAPTURE-1 approved implementation

Maintainer approved the presented plan. Start at `6a1ae46f76dff2b0a84b814e6768fd0bb70f4179`
on `test/client-baseline-gui`, draft #224 against dev. Reuse the atomic driver,
pinned independent probe, immediate guest screen/clock and owned cleanup.
Affected implementation: `scripts/client_baseline_compare.py` and its kept guards.
Add an explicit permission-session option: a fresh-run-only bounded guest-input
queue permits inspecting native RFB images between UI actions while the same
boot/observer/cleanup execution stays alive. No active-console pointer access.
Each action still executes the pinned probe and immediate independent readback.

Budget reviewed before changing code: permission session gets 720s observation,
960s guest hold, at most 40 actions; retain 45s probe and 30s guest-command caps.
The default three-step comparison keeps 240s/480s. Unknown/oversized/symlinked
requests fail closed; timeout or interruption invokes owned cleanup. UI actions
only, no host commands/clipboard, TCC database writes, permission bypass or host
permission changes. Raw screenshots, request files and logs remain private.

Live sequence: trigger/read the guest-agent recording prompt, use native RFB
coordinates to open supported guest Privacy & Security settings, enable the
actual guest-agent Screen Recording entry and follow any restart requirement.
Then compare a visible window/menu between RFB and guest capture, pointer-selected
Go To Folder versus focused Finder keyboard chord, and harmless synthetic text.
Do not infer success from screenshot existence, clock progress or process exit.
Stop with a preserved blocker if the guest UI cannot be resolved within bounds.

Validation: existing guards plus meaningful queue ownership/limits/failure-readback
regressions and red-first proof, Ruff, repository/docs checks, then one private
redirected bounded live execution. Record exact observations and cleanup. No app
validation until usable input and reliable window capture are observed. Keep
one input PASS/eleven BLOCKED, Pi v0.85.1-psmfd.1, deferred #219 and all gates.
Next: implement the explicit bounded session mode and validate before guest boot.

CF-1C-CAPTURE-1 implemented: explicit 720s/40-action fresh-run queue, 960s guest
hold; default comparison unchanged. All 31 guards, Ruff and repository checks
pass. Red proof: isolated removal of symlink rejection makes the new guard fail;
production source unchanged and full restored suite passes. Pinned private probe
verified, five originals stopped, no QA/Tart/Softnet/drain process. Next: one fresh
permission-session launch, privately redirected, inspect UI and remediate within
that execution; retain independent screen/clock after every probe and cleanup.

CF-1C-CAPTURE-1 live checkpoint: run 20261107T143813Z-90064f4d9802. Supported
Settings UI shows duplicate guest-agent entries: executable disabled, another
entry enabled. Enabling the executable requests normal guest administrator
authentication. Use only the base image's documented public fixture account
through this guest UI (Tart quick-start credentials); no host/operator credential
is read or copied. Keep masked-field frames/logs private. No permission database
write or bypass. Next: authenticate the guest UI, inspect resulting permission
state and verify independent window capture before any app work.

Run `20261107T143813Z-90064f4d9802` completed sixteen probes with immediate
independent screen/clock readbacks. The supported guest privacy panel showed two
guest-agent entries: executable disabled, another entry enabled. Normal guest
administrator authentication using the documented public base-image fixture
account enabled the executable toggle. No host/operator credential was used.
Native RFB proves the toggle change and captures Settings/Finder windows; no
restart instruction was shown. The recording prompt later recurred, and all
independent guest screenshots continued to omit windows. Permission activation
or duplicate identity remains unresolved; enabling the toggle alone is not a fix.

Pointer Go To Folder attempts did not establish a visible dialog. Focused chord
comparison and synthetic path verification were not completed; no app case ran.
Capture-only probes 07/12 timed out after their initial RFB frame; every probe
still received its immediate independent readback. Observer hit its 720s deadline
while waiting for the next action, exited 1 and recorded TimeoutError. Owned
cleanup succeeded and independently verified five original stopped VMs, no clone
or QA/probe/Tart/Softnet/drain process. A supporting service inventory command
failed on an absent optional directory; stdout was not retained, so no active
service identity is claimed. Follow-up validation safely refused the cleaned run.

[Sanitized permission evidence](evidence/client-baseline/gui-continuation/permission-comparison.json)
records sixteen probes, 148 private-file hashes and exact timeout/cleanup outcome.
All 31 guards, Ruff and repository checks pass. Isolated removal of symlink
rejection makes the new guard fail; unchanged production source passes. Source
and evidence are locally verified; commit/remote/CI identities will follow.

**Next bounded action: CF-1C-CAPTURE-2.** Before triggering capture in a fresh
owned guest, inventory the active guest-agent executable and launch service.
After the supported UI grant, restart that identified guest service normally,
within the same bounded controller execution; verify reconnection and actual
window capture. Preserve duplicate-entry evidence. Keep immediate per-input
readbacks, private output and cleanup. No TCC database edits, host permission
changes or app validation. Pointer/chord/synthetic text comparison follows only
a reliable capture result. Pi v0.85.1-psmfd.1, deferred #219 and all gates remain.

- Final documentation check initially flagged a line starting with bare #219 as
  an ATX heading in issue-ledger.md; changed it to prose. Next: rerun that lint,
  secret scan and checkpoint only the reviewed slice/evidence files.

CF-1C-CAPTURE-1 implementation/evidence checkpoint
`ddb4ef1a043bf5615f86e143c95ad3e4926ca584` is committed, secret-scanned (zero
findings) and remotely backed up in open draft #224 against dev. Independent
GitHub readback confirms the head. CI run
[42385049680](https://github.com/psmfd/FingerTrap/actions/runs/42385049680) is in
progress; title/gitflow pass, full CI/CodeQL/review remain pending. All four macOS
jobs use hosted macos-15 runners; no local jobs queued, no local drain launched.
GitHub runner registrations are zero. This subsequent documentation-only
checkpoint records the implementation identity with CI skipped; read git/draft
head for its own SHA. No activation, app validation or baseline acceptance.
Next remains CF-1C-CAPTURE-2 as described above.

Final Markdown/Prettier and local links pass after the lint correction. All 148
private evidence digests match; all 31 kept guards, Ruff and repository checks
pass. Private evidence, stale active pointer and original unrelated files preserved.

Post-checkpoint CI refresh: all fourteen jobs in run 42385049680 pass on source
ddb4ef1a043bf5615f86e143c95ad3e4926ca584. Title/gitflow pass; full CodeQL/review
still pending. Documentation checkpoint 2a7009d0aecf2ff8d0c4f1870c8aca30165246b3
is remotely backed up with CI skipped; this final CI-readback update follows it.
No new guest starts, app validation or gate changes. Next: CF-1C-CAPTURE-2.

## 2026-11-07 — CF-1C-CAPTURE-2 approved implementation

Maintainer approved this next bounded action. Start at
`0cf35d21f239c7c65f5d1c82b359913cf86285ee`, branch `test/client-baseline-gui`,
open draft #224 against dev. Reuse the existing atomic permission session.
Before the first independent capture, inventory guest-agent process executable,
launch plist, domain/label and active PID. Preserve complete private inventory
and errors; optional missing directories must not discard useful output.
Only a unique active RPC-capable launch service whose PID/executable match the
inventory may be restarted. Revalidate identity immediately before restart.
Use normal guest launchctl service restart, then bounded reconnection and verify
a changed PID with the same service/executable. No guessed service, broad kill,
TCC edits, host settings change or permission bypass.

Affected files: scripts/client_baseline_compare.py and kept guard tests, plus
migration records/evidence. Add one fixed restart-agent queue action with no
caller-supplied command, service, path or endpoint. Supported guest UI grant and
public fixture authentication remain observable through RFB. Keep immediate
independent screen/clock attempts after every probe; restart records its own
bounded reconnect/readback outcome in the same owned execution. Default runner
unchanged. Retain 720s observation, 960s hold, 40 actions, 45s probe and 30s guest
command limits; restart gets at most 90s within remaining observation time.

Validate ownership, ambiguous/non-RPC/wrong-PID rejection and changed-PID
readback with meaningful regressions/red proof, then one fresh private redirected
live run. Compare actual Settings/Finder windows in both capture paths after UI
grant and restart. Only after reliable capture, qualify pointer Go To Folder,
focused keyboard chord and synthetic text. No app cases in this slice. Finish
or fail within bounds and independently verify owned cleanup. One input PASS /
eleven BLOCKED, Pi v0.85.1-psmfd.1, deferred #219 and all gates remain intact.
Next: implement and verify the inventory/restart operation before guest boot.

CF-1C-CAPTURE-2 preflight: inventory and identity-checked restart-agent operation
implemented in the existing permission session. All 36 guards and Ruff pass.
Removing pre-restart PID comparison in an isolated module fails the new guard;
production source unchanged. Pinned probe verified; local/draft head 0cf35d2
matches open draft #224 against dev. Five original stopped VMs, no guest driver,
probe/Tart/Softnet/drain process. Next: one fresh atomic private redirected run,
inspect pre-capture inventory, supported UI grant and normal service restart.

Run `20261107T153328Z-e16d52dd729f` finished explicitly after nine actions:
eight RFB probes plus the normal RPC-agent restart. Initial and all nine immediate
independent guest screen/clock readbacks completed. Inventory before capture
identified `gui/501/org.cirruslabs.tart-guest-agent`, PID 423, executable
`/opt/homebrew/bin/tart-guest-agent`; executable/plist program digests match.
The disk-resize daemon is separately inventoried and excluded from selection.
Supported guest privacy UI/public fixture authentication enabled the executable
recording toggle while another guest-agent entry was already enabled.

Identity was revalidated before `launchctl kickstart -k`. Its RPC request hit
the 10s command timeout, but reconnection and fresh process/service inventory
proved PID **423 → 843** with the same service, plist and executable identity.
Immediate post-restart screen/clock succeeded. RFB after restart still shows
the permission prompt over Settings; independent screenshots still omit windows.
The restart is verified and is not sufficient to fix capture in this run.
Only probe 00's follow-up frame timed out; the other seven follow-up RFB captures
succeeded. No input qualification or app case advanced. Driver exit 0 is not a
capture/input pass. Normal owned cleanup verified, controller events empty;
five original VMs stopped/preserved, no clone or QA/probe/Tart/Softnet/drain process.

[Sanitized agent restart evidence](evidence/client-baseline/gui-continuation/agent-restart-comparison.json)
retains exact service/PID/digests, nine actions, ten independent readbacks and
86 private-file hashes. Raw inventory/frames/logs remain private. All 36 kept
guards, Ruff and repository checks pass. Red proof: isolated removal of the
pre-restart PID comparison fails the kept guard; unchanged production source
passes. Result is implemented and locally/live verified; commit/backup/CI follow.

**Next bounded action: CF-1C-CAPTURE-3.** Collect the identified guest agent's
code-signing identity and narrowly scoped macOS privacy (TCC) decision logs around
one capture in a fresh owned atomic session. Correlate the actual requesting
client with the two visible permission entries before deciding any supported
exact-entry UI re-enrollment. Do not repeat grant/restart as an assumed fix.
No TCC database edits, permission bypass, host changes or app validation. Retain
immediate readbacks, private output and cleanup. Baseline stays one input PASS /
eleven BLOCKED, Pi v0.85.1-psmfd.1, deferred #219 and all existing gates.

CF-1C-CAPTURE-2 implementation/evidence checkpoint
`3db39be45bfe320089ca91491d529cd369767782` is committed, secret-scanned (zero
findings) and remotely backed up in open draft #224 against dev. Independent
GitHub readback confirms the head. CI run
[42387709718](https://github.com/psmfd/FingerTrap/actions/runs/42387709718) has
started; title/gitflow pass. Full platform CI/CodeQL/review remain pending at
this readback. This subsequent documentation-only checkpoint records the source
identity with CI skipped; read git/draft head for its own SHA. No activation or
acceptance. Next remains CF-1C-CAPTURE-3 as described above.

Final verification: 36 guards, Ruff, repository structural/lock/RPC checks,
Markdown/Prettier, local file links, all 86 private-file hashes and secret scan
pass. Five original VMs stopped/preserved; no clone or QA processes. Private
evidence/stale pointer and original unrelated files preserved.

Final implementation CI refresh: all fourteen jobs in 42387709718 pass on
3db39be45bfe320089ca91491d529cd369767782. Four CodeQL analyses/title/gitflow pass;
Rust remains in progress, aggregate neutral, CodeRabbit skipped. Full CodeQL/
review remain pending. All macOS lanes hosted; no local queue/drain; zero runner
registrations. Next remains CF-1C-CAPTURE-3 privacy-client attribution diagnostic.

## 2026-11-07 — CF-1C-CAPTURE-3 read-only continuation

Continue within persistent CF-1 diagnostic approval after verified restart
proved insufficient. Starting source 3db39be and documentation
`a61b2596275a4ed8dc556d19ba04122dedfab43d` are backed up in draft #224.
Before changing the slice: add a fixed diagnose-agent queue action with no
caller commands/paths/predicates. Read the selected guest executable's signing
identity/designated requirement and the last two minutes of narrowly filtered
macOS TCC decision logs for capture/agent attribution; preserve output privately.
No permission/config/database mutation, global logging change or host operation.

Affected files: existing atomic comparison script, focused guard test and
migration records/evidence. Keep guest ownership checks, 720s observation/960s
hold/40-action budgets and 30s command caps. A guest-local privileged read of
system privacy logs may use the clean image's existing noninteractive sudo;
never request or copy host credentials. Failures/redaction are evidence, not an
excuse to alter TCC or weaken gates. No app or input qualification in this slice.
One fresh atomic session: inventory, initial capture/clock, fixed diagnostic
readout, explicit finish and verified cleanup. Validate before boot; review the
actual requesting client and denial before proposing any exact-entry UI change.
Next: implement the read-only diagnostic action and kept guard.

CF-1C-CAPTURE-3 logging refinement before implementation: installed Apple log
help confirms bounded stream --timeout. Use a fixed 12-second debug stream
around one guest capture rather than relying only on persisted debug history.
Retain a 30s guest-command cap, 256 KiB returned-log cap and guest-only files.
This changes no logging configuration, TCC database or permission. No UI grant
or restart is repeated for this attribution-only run.

CF-1C-CAPTURE-3 implementation/preflight: fixed read-only diagnose-agent action
added. Initial quoted command failed Python parsing; replaced with a fixed
multiline shell script, now Python/Ruff and sh -n pass. All 37 guards pass;
isolated removal of diagnostic identity validation fails the kept guard.
No production source mutation for red proof. Next: one fresh private redirected
atomic attribution run, normal finish and owned cleanup; no UI permission change.

CF-1C-CAPTURE-3 first run 20261107T160333Z-036e67bd3539 failed at codesign
(exit 1) before log collection. Diagnostic did not retain codesign stdout, so no
unsigned/signature/root-cause claim is established. Cleanup verified; preserve
this failed run. Bounded correction: retain nonzero signing output/status and
continue the read-only log collection; add regression, then retry one fresh
atomic attribution run. No permission change or app validation.

CF-1C-CAPTURE-3 correction implemented: signing exit/stdout/stderr now retained;
negative signing status does not skip privacy log collection. All 38 guards and
Ruff pass, including the real-failure regression. Next: fresh bounded read-only
retry with private output; preserve the original failed run.

The read-only retry `20261107T160656Z-104c57df8692` completed diagnostic,
independent readback, an RFB capture and explicit normal finish/cleanup. Original
failed run `20261107T160333Z-036e67bd3539` is preserved: codesign exit 1 stopped
the first implementation before logs, without retaining signing output. The kept
correction now preserves nonzero signing status/output and continues diagnostics;
all 38 guards pass, including that real-failure regression.

**Observed privacy failure:** TCC attributes capture to resolved executable
`/opt/homebrew/Cellar/tart-guest-agent/0.10.0/bin/tart-guest-agent` (type 1),
then explicitly reports failure to match an existing Screen Recording code
requirement (check status -67050) and returns denied. The 12-second stream
returned 28,069 bytes, below the 256 KiB cap, with four code-requirement failures.
The binary digest matches the CAPTURE-2 agent. Signing display shows identifier
`a.out`, linker-signed ad hoc metadata, no bound Info.plist/team, and exit 1 for
the designated-requirement readout; do not flatten this into an entirely unsigned
binary claim. This establishes an actual permission/code-requirement failure
in the fresh unmodified guest. Why the prior UI grant failed and whether UI
re-enrollment fixes it remain unproven. No permission/signing/database/logging
configuration, binary or host setting changed in CAPTURE-3.

RFB still shows the recording prompt; independent guest screenshot omits it.
The RFB follow-up capture timed out after its initial frame; its independent
screen/clock completed. No input/app qualification advanced. Both owned guests
cleaned and independent inventory verified five original stopped VMs, no clone
or QA/probe/Tart/Softnet/controller process. Raw signing/TCC logs stay private.
[Sanitized attribution and 33 private-file hashes](evidence/client-baseline/gui-continuation/privacy-attribution.json)
preserve both attempts, selected identity, short denial excerpts and cleanup.
No app row changes: one input PASS/eleven BLOCKED. Pi v0.85.1-psmfd.1, deferred
comparison #219 and all activation/backend/release gates remain intact.

**Next bounded action: CF-1C-CAPTURE-4.** Record a supported exact-entry guest
permission re-enrollment experiment. Preserve both guest-agent entries and collect
TCC decisions after the normal UI grant in that same guest. If the requirement
mismatch persists, remove/re-add only the identified agent entry through normal
Privacy & Security UI using the exact resolved executable, authenticate with the
public fixture account, then verify TCC decisions and actual window captures.
No direct TCC database edits, host changes or binary re-signing. Re-enrollment is
proposed, not implemented/proven. Keep immediate readbacks, private output and
owned cleanup; input/app qualification stays gated until reliable capture.

Final Markdown check flagged a bare #219 at line start in the new shared result;
changed to prose. Next: rerun lint, secret scan and scoped checkpoint.

## 2026-11-07 — open-source UI automation evaluation requested

Maintainer approves the interrupted checkpoint command and asks whether an open-
source automation solution should script interface execution. Resumed checkpoint
verification shows all CAPTURE-3 changes preserved, not yet staged/committed; no
new guest start. Research recommendation (not adoption/installation): evaluate
Peekaboo released CLI plus signed GUI Bridge inside a fresh disposable guest.
Its documented semantic element/window/menu commands and JSON results can be
driven deterministically; permissions guidance specifically covers wallpaper-
only background-session captures and prefers the GUI Bridge for these contexts.
Primary sources: [Peekaboo](https://github.com/openclaw/Peekaboo), its
[permissions guide](https://github.com/openclaw/Peekaboo/blob/main/docs/permissions.md)
and [platform support](https://github.com/openclaw/Peekaboo/blob/main/docs/platform-support.md). Released app/CLI target macOS 15+. Main-branch docs
are candidate capabilities, not proof that any chosen release/our guest works.

Compare Hammerspoon (Lua/Accessibility/CLI scripting) and Appium Mac2 (native
XCTest/WebDriver, requires Xcode). Proposed CF-1C-AUTOMATION-1: pin/review one
matching Peekaboo app+CLI release and signatures/digests; guest-only installation
and supported permissions; deterministic Finder menu/dialog/text scenario with
observable window assertions, fresh screenshots and independent RFB comparison;
owned cleanup and exact artifact/permission provenance. Keep model-agent/MCP/cloud
features out of this offline deterministic pilot. Retain current Tart ownership/
budget/evidence wrapper. No automatic adoption, host/global installation, product
dependency change, TCC bypass or app-case acceptance. Existing direct UI re-
enrollment remains a separate proposed diagnostic; evaluate tooling before more
custom UI orchestration. Pi v0.85.1-psmfd.1, deferred #219 and all gates intact.

CF-1C-CAPTURE-3 implementation/evidence checkpoint
`8876c5839ed6536aec879ccdba11c36163278fd7` is committed, secret-scanned (zero
findings) and remotely backed up in open draft #224 against dev. All 38 guards,
Ruff, repository checks, fixed shell syntax, Markdown/Prettier, local file links
and 33 private-file digests pass. Implementation CI
[42392072357](https://github.com/psmfd/FingerTrap/actions/runs/42392072357) passes
all fourteen jobs; title/gitflow pass. Full CodeQL/review remain pending.
All macOS jobs hosted; no local drain needed, zero runner registrations. Final
inventory: five original stopped VMs, no disposable clone or QA processes.
This following documentation-only checkpoint records that source identity with
CI skipped; read git/draft head for its own SHA. Automation tooling is proposed,
not installed/adopted. Latest steering: evaluate pinned Peekaboo CLI + GUI Bridge
before additional custom UI orchestration; retain existing ownership/cleanup and
all acceptance/activation gates.

The automation research note initially had a bare-URL Markdown lint finding;
converted sources to proper Markdown links and lint passes. No new guest start
after attribution cleanup, and no automation tool installation performed.

## 2026-11-07 — approved CF-1C-AUTOMATION-1 Peekaboo pilot

Maintainer approved the pilot. Start at `a0aa2655a30c220a33481b4f632d1bd4e0841a09`,
branch test/client-baseline-gui, draft #224 against dev. Select published Peekaboo
v4.9.8 (release 435831803, 2026-11-05), matching app zip asset 715044161 and
ARM64 CLI tar asset 715044399. Pin GitHub asset digests, release signing/inventory
manifests and actual embedded source identities before any guest execution.
No host/global installation or package manager, no runtime/product dependency.

Scope: a dedicated QA pilot module using the existing Tart/VNC owner and cleanup
wrapper. Minimal reviewed hooks may stage verified tool inputs into the new
read-only QA share and select a per-probe independent readback. Do not rebuild
the atomic lifetime/RFB implementation. Guest-only installation of matching
prebuilt app/CLI, normal supported Screen Recording/Accessibility permissions to
the actual signed GUI Bridge, deterministic CLI Finder/menu/dialog/text scenario,
JSON/AX/window assertions, real screenshots plus independent native RFB evidence.
No model agent, MCP/cloud credentials, host clipboard, TCC edits/bypass or binary
re-signing. No fallback capture through a background CLI accepted as a Bridge pass.
Record test-tool footprint; do not claim a pristine tool-free consumer test.

Reviewed pilot bounds before implementation: 1200s observer, 1500s guest hold,
40 native bootstrap actions and a fixed finite CLI scenario. Each guest command
at most 30s (install/verification at most 60s bounded by remaining time), native
probe cap 45s. One boot/stage/permissions/script/observations/cleanup execution
with private redirected stdout/stderr. Before Bridge permission works, preserve
capture failure plus independent guest clock without inferring window contents.
After grants, require actual full-window capture and input proof. Run identifier
passed explicitly; never select the cleaned active-console pointer.

Validation: artifact/source/signature/archive provenance, meaningful staging/
callback/ownership/Bridge-source guards and red proof, existing kept suite/Ruff/
repo/docs checks, then bounded live pilot and independent cleanup. Preserve all
private evidence and unrelated files. Pilot go/no-go stays separate from app
validation: baseline one input PASS/eleven BLOCKED, Pi v0.85.1-psmfd.1, deferred
comparison #219 and all activation/backend/release gates remain unchanged. Next: acquire and
review exact artifacts and release command docs, implement minimal QA integration.

AUTOMATION-1 artifact review: v4.9.8 asset/inventory/signing digests match; source
70dbc4183f390a5a72d5dc01d8a77eb672217070, Developer ID team FWJYW4S8P8. App zip
paths and six framework symlinks remain contained. Passive host extraction only,
no tool launch/installation. App identifier boo.peekaboo.mac, minimum macOS 15.0,
source stamp matches, stapled ticket observed. App/deep library and ARM64 CLI
strict signature checks pass outside the sandbox. Initial sandboxed CLI signature
check reported invalid/authority unavailable; unrestricted read-only verification
passes, so no corrupt-artifact claim. CLI requires its shipped Swift compatibility
dylib. Next: stage these pinned inputs and implement Bridge-only QA readback.

AUTOMATION-1 preflight: matching app/CLI/dylib provenance reviewed and pinned in
peekaboo-pilot-inputs.json. Dedicated guest-only pilot and minimal prepare/readback
hooks implemented locally; 43 kept guards/Ruff/repository checks pass. Intentional
Bridge-local fallback regression fails the kept guard; restored code passes.
Branch/draft head a0aa265 verified; prior implementation 8876c58 CI remains green.
Five original VMs stopped, no disposable clone. Next: one bounded live boot with
normal GUI app permission enrollment, fixed Finder scenario, independent frames/
clock and owned cleanup. Source is not yet committed or CI-verified.

AUTOMATION-1 live run 20261107T171635Z-8d74599c3c32: guest signature/source
verification succeeds and canonical GUI Bridge responds with source=bridge.
All three grants initially denied, blocked capture and immediate guest clock
preserved. Native menu item pointer attempts dismiss menu without opening UI;
Down then Return visibly selects/opens the permission checklist. This is bounded
native keyboard evidence, not Peekaboo input qualification. Normal GUI Screen
Recording Grant produces correctly attributed Peekaboo prompt. Next: finish
normal guest app permission enrollment then run the fixed Finder scenario.

AUTOMATION-1 result: run 20261107T171635Z-8d74599c3c32 aborted safely on
Peekaboo type exit 1/INTERACTION_FAILED, outcome indeterminate/may_have_dispatched,
retry unsafe. Immediate independent RFB and Bridge pixels both visibly show exact
/tmp/ft-input-probe in Finder Go to Folder. The CLI error is preserved; do not
blindly replay. GUI app Screen Recording + Accessibility normal enrollment,
macOS Quit & Reopen, supported Allow prompt, then normal app quit/relaunch yield
all three Bridge grants. Bridge full-window capture works, unlike prior agent
capture. Semantic Go menu delivery resolves to fresh AX GoToWindow/PathTextField;
used_cache=false. Escape/chord not reached. Owned cleanup verified, five originals
stopped/preserved. No app validation and baseline unchanged.

Next bounded comparison CF-1C-AUTOMATION-1B: preserve indeterminate type failure,
then resolve once with mandatory fresh Finder AX value plus independent images;
never repeat typing. Continue Escape and keyboard shortcut only if the exact
field is observed. Strengthen dialog/freshness/readback checks and guards before
one fresh pilot with identical pinned artifacts, deadlines and owned cleanup.

## 2026-11-07 — CF-1C-AUTOMATION-1/1B results

Pinned Peekaboo v4.9.8, source `70dbc4183f390a5a72d5dc01d8a77eb672217070`,
Developer ID team `FWJYW4S8P8`, app `boo.peekaboo.mac`, matching ARM64 CLI and
shipped Swift compatibility library were installed only inside disposable guests.
[Input provenance](evidence/client-baseline/peekaboo-pilot-inputs.json) and
[results with 779 private-file hashes](evidence/client-baseline/peekaboo-pilot-result.json)
retain both attempts. No host/global installation or runtime dependency added.

First run `20261107T171635Z-8d74599c3c32` safely aborted on type exit 1:
INTERACTION_FAILED, indeterminate/may_have_dispatched, retry unsafe. Independent
RFB and Bridge frames nevertheless show the exact synthetic path. The correction
preserves that error and resolves it once with a mandatory fresh Finder AX read;
no input replay. Guard regressions for accepting local capture and skipping the
fresh field assertion both fail; restored code passes all 46 kept guards/Ruff.

Continuation `20261107T172734Z-0f7178a10a0d` completed the fixed scenario and
explicit finish after 21 queue actions. Normal signed app Screen Recording and
Accessibility enrollment, macOS Quit & Reopen/Allow, then normal app Quit and
interactive relaunch yield all three canonical GUI Bridge grants. Full-window
Bridge screenshots agree with independent native RFB frames. Semantic Go menu
selection opens Finder Go to Folder; fresh AX sees PathTextField. Type returns
the retained indeterminate error, then uncached AX and both image sources confirm
`/tmp/ft-input-probe`. Escape visibly closes the dialog; Peekaboo foreground
`cmd+shift+g` reopens it, with fresh AX and independent frames confirming an empty
path field. CLI dispatch status alone is never acceptance. Native RFB Meta+q did
not quit; that older chord path is not claimed fixed. Follow-up RFB timeouts remain
possible; first native frames plus immediate Bridge screen/clock are retained.

Both owned guests cleaned; independent inventory verifies five original stopped
VMs and no disposable clone/QA/Peekaboo/Tart/Softnet processes. Initial blocked
captures and restart gaps retain errors plus independent guest clocks. Raw frames,
JSON, signing output and logs remain private; unrelated files and cleaned pointer
preserved. Tool footprint is explicit, not a pristine tool-free consumer claim.
No FingerTrap app validation occurred. Baseline remains one input PASS/eleven
BLOCKED, Pi v0.85.1-psmfd.1, #219 deferred and all existing gates unchanged.

**Decision:** this bounded Finder workflow has usable scripted input and real
window capture through the signed GUI Bridge. Production adoption, unattended
permission enrollment, and broad app reliability are not established. Keep fresh
AX and image assertions around every mutation, including indeterminate outcomes.
Implementation `5fa59ffe8ae86a117d9a8ce7b8359dd6ae884490` is committed,
secret-scanned (zero findings), remotely backed up and CI-verified in open
draft #224 against dev. CI [42397076555](https://github.com/psmfd/FingerTrap/actions/runs/42397076555)
passes all fourteen jobs; title/gitflow pass. Full CodeQL/review and app acceptance
remain pending. All macOS jobs hosted, no local drain; zero runner registrations.
Following documentation checkpoints skip CI and do not change the tested source.

**Concrete next action:** checkpoint/review this pilot, then name CF-1C-AUTOMATION-2
for a bounded packaged-client launch/observation-only slice using the pinned GUI
Bridge. Record exact app window/ready-state assertions and private evidence before
boot; retain existing app acceptance/provider and activation/backend/release gates.
Do not promote any app row from this Finder pilot. Reproduction:

```sh
python3 -m unittest discover -s scripts/tests -p 'test_*.py'
python3 scripts/client_baseline_peekaboo.py \
  --archive .artifacts/pr-merge-20261021/golden-fix-apple/candidate.zip \
  --runtime-archive .artifacts/client-baseline/inputs/pi-darwin-arm64-v0.85.1-psmfd.1.tar.gz \
  > .artifacts/client-baseline/peekaboo-pilot-NEW-UNIQUE.log 2>&1
```

Use a new private log with restrictive umask/no-clobber. The process owns boot,
1200s observation, 1500s guest hold, at most 40 bootstrap queue actions, fixed
scenario, immediate per-step readbacks and cleanup. Select only the newly printed
explicit run, never active-console.json. Queue fixed actions pilot-status,
pilot-permissions-ui and pilot-scenario in that run's peekaboo-pilot/request-NN.json;
normal native click/chord/capture handles supported UI enrollment. Finish explicitly
with action finish. No arbitrary commands, operator data, models or MCP/cloud.

## 2026-11-07 — Peekaboo pilot checkpoint and CI

Implementation 5fa59ffe8ae86a117d9a8ce7b8359dd6ae884490 committed and remotely
backed up on test/client-baseline-gui, open draft #224 against dev. All 46 kept
guards, Ruff, repo checks, Markdown/Prettier, migration links and 779 private-file
digests pass; secret scan zero findings. CI 42397076555 passes all fourteen jobs;
title/gitflow pass. Full CodeQL/review and packaged-client acceptance remain open.
PR and issues #216/#217 updated with concrete tooling result and preserved gates.
Five original VMs stopped/preserved, no clone/QA/tool processes, zero runner
registrations. Hosted macOS jobs, no local drain. Source and private evidence
preserved; following docs-only checkpoint skips CI. Next concrete slice is
CF-1C-AUTOMATION-2: record bounded packaged-client launch/observation assertions
before a fresh boot; never reuse a cleaned pointer or infer an app row from Finder.

## 2026-11-07 — CF-1C-AUTOMATION-2 begins

## CF-1C-AUTOMATION-2 — approved packaged-client launch/observation

Proceed authorized from f27dcd9, branch test/client-baseline-gui, open draft #224
against dev; prior source 5fa59ff CI fourteen jobs green. Before implementation:
add one dedicated fixed client-observe queue action around the existing Peekaboo
pilot lifetime. Fresh guest, same pinned app/runtime/tool artifacts and normal
GUI Bridge permission enrollment. No custom RFB/lifetime reimplementation.

Install the already verified candidate zip into guest /Applications via ditto;
verify installed bundle ID dev.fingertrap.app and all three native binary hashes
against the prepared run before one normal LaunchServices open. Record actual
Gatekeeper/prompts without disabling, re-signing or stripping quarantine. This
is tool-assisted launch observation, not a pristine Finder installation pass.
No runtime/profile/provider/keychain changes or app command input in this slice.

Predeclared observed-ready criterion: exactly one positive-size FingerTrap window,
uncached complete AX observation targeting FingerTrap with session-browser and
status-panel controls visible, two fresh observations five seconds apart, plus
private independent native RFB and Bridge screen/clock after each step. Record
app/sidecar PIDs and installed paths independently; process existence alone is
not UI readiness. At most three read-only observation attempts; launch never
replayed. If webview AX is unavailable, preserve pixels/processes and mark the
criterion blocked rather than weakening it to CLI success. Preserve all failures.

Bounds unchanged: 1200s observer, 1500s guest hold, 40 queue actions, command cap
30s (installation 60s), native probe 45s. Boot, enrollment, launch, observations,
explicit finish and verified owned cleanup in one process, stdout/stderr private.
Do not reuse cleaned pointer. Validate new guards with intentional regression,
existing suite/Ruff/repo/docs/hash/secret checks, then one bounded live run.
Baseline stays one input PASS/eleven BLOCKED; Pi v0.85.1-psmfd.1, #219 deferred,
all activation/backend/release/provider acceptance gates intact. Next: implement
fixed installed-identity and fresh-window assertions, then run the owned slice.

AUTOMATION-2 implementation prepared locally: dedicated fixed client-observe
module reuses pinned Peekaboo staging/enrollment and existing atomic owner;
installed ID/three binary hashes checked before single LaunchServices open.
Mandatory native/Bridge screen+clock follows every install/launch/window/AX/
process probe. Fifty kept guards/Ruff pass. Intentional cached/empty/wrong-app
UI acceptance regression fails kept guard; restored code passes. Prior source
CI remains green and draft/head verified. Next: bounded live run with normal
permissions, at most three read-only observations, explicit finish and cleanup.

AUTOMATION-2 run 20261107T175616Z-5064033904d3 completed three uncached AX and
native/Bridge readbacks, installed identity proof and independent process inventory;
explicit finish after 21 queue actions. App PID 933 and child sidecar 940 use the
verified installed paths. Visible window 80 exposes both required controls and
an actionable missing-Pi default pane. No Pi/profile/provider setup performed.
Original live guard blocked because it counted unnamed off-screen helper 85.
Before correction: classify exactly one observed visible titled FingerTrap main
window, bind its ID to the fresh AX target and retain partial-inventory warnings.
Do not claim complete global window inventory. Preserve original blocked result;
validate corrected classification by replay of the retained independently acquired
snapshots/screens/clocks after cleanup, not by relaunching or fabricating live
callback success. Add kept real-inventory regression fixture. Baseline unchanged.

## 2026-11-07 — packaged-client observation and classification result

## CF-1C-AUTOMATION-2 — packaged-client window observed

Run `20261107T175616Z-5064033904d3` installed the pinned candidate inside a fresh
owned guest, verified bundle ID dev.fingertrap.app and all three native binary
hashes, then launched exactly once through normal LaunchServices. No Gatekeeper
bypass, quarantine removal, re-signing or host/global install. Peekaboo GUI Bridge
grants came through the same normal guest UI enrollment/relaunch as the pilot.

**Observed:** native RFB and independent Bridge screenshots show FingerTrap's
visible window and controls. Three distinct uncached AX snapshots, each at least
five seconds apart, expose Toggle session browser and Toggle status panel in
window 80 (1024x686 logical bounds). Process readback independently records app
PID 933 and child sidecar PID 940 at the verified /Applications bundle paths.
The default Pi pane visibly reports an actionable missing-executable error. This
slice did not install/configure Pi or send app command/provider/keychain input;
do not claim terminal transport, provider acceptance or clean Finder installation.

**Guard correction preserved:** original live result is blocked because its
window guard counted an unnamed off-screen helper (85) as well as the visible
FingerTrap main window (80). The actual inventory is partial and keeps its warnings.
Corrected classification binds exactly one observed visible titled main window
to fresh AX; it does not assert a complete global window count. The original
blocked result remains untouched. Corrected guard passes offline replay of all
three retained live observations/screens/clocks after cleanup; no second launch,
no fabricated live callback success. Acquisition commands unchanged; final
corrected live callback was not re-executed. A kept sanitized real-inventory
fixture rejects restoration of the old count-all-windows guard. An intentional
cached/empty/wrong-app UI acceptance regression also fails the kept guard.

[Sanitized result and 350 private-file hashes](evidence/client-baseline/client-launch-observation.json)
retain exact identities, original failure, classification mode, process/image
review and cleanup. All 51 kept guards/Ruff/repo/docs/link/hash checks pass; secret scan zero.
Source `237afcdc2a5afcebcf3194200e2933be0dc43bac` is committed and remotely backed
up in open draft #224 against dev. CI
[42399603531](https://github.com/psmfd/FingerTrap/actions/runs/42399603531)
passes all fourteen jobs; title/gitflow pass. Full CodeQL/review and maintainer/
app acceptance remain open. Zero runner registrations, hosted macOS jobs; no local
drain. Following documentation-only checkpoint skips CI; tested source unchanged. Raw images/JSON/logs
remain private; unrelated files and cleaned active-console pointer preserved.
Explicit finish after 21 queue actions; independent cleanup shows five original
stopped VMs, no clone/QA/Peekaboo/Tart/Softnet processes. Baseline unchanged:
one input PASS/eleven BLOCKED, Pi v0.85.1-psmfd.1, comparison #219 deferred,
all provider/activation/backend/release and maintainer acceptance gates intact.

**Next concrete slice: CF-1C-AUTOMATION-3.** Record a bounded first shell-pane
input/output observation before boot: reuse corrected main-window admission in
a fresh guest, select New shell tab through observed AX, deliver one synthetic
printf probe, then observe exact output independently. No provider/runtime setup,
no baseline row promotion without its full case evidence. This next slice also
exercises the corrected classifier in the live callback. First checkpoint and
refresh current-source CI. Reproduce current acquisition with:

```sh
python3 scripts/client_baseline_client_observe.py \
  --archive .artifacts/pr-merge-20261021/golden-fix-apple/candidate.zip \
  --runtime-archive .artifacts/client-baseline/inputs/pi-darwin-arm64-v0.85.1-psmfd.1.tar.gz \
  > .artifacts/client-baseline/client-observe-NEW-UNIQUE.log 2>&1
python3 scripts/client_baseline_client_observe.py \
  --review-run .artifacts/client-baseline/20261107T175616Z-5064033904d3
```

Use restrictive umask/no-clobber and a fresh explicit run for acquisition. Existing
1200s observer/1500s guest hold/40-action/30s guest-command/45s native-probe bounds
remain; fixed install verification cap 60s. Queue client-observe only after Bridge
permission enrollment; finish explicitly. Review mode only reads a cleaned owned
run, verifies retained image digest and timestamps, and never starts a guest.

## 2026-11-07 — packaged-client observation checkpoint and CI

Source 237afcdc2a5afcebcf3194200e2933be0dc43bac committed and remotely backed up
on test/client-baseline-gui, draft #224 against dev. All 51 kept guards/Ruff,
repo/docs checks, migration local links and 350 private hashes pass; secret scan
zero. Source CI 42399603531 passes all fourteen jobs; title/gitflow pass.
Draft and issues #216/#217 record the original live guard failure separately
from corrected offline evidence replay. Full CodeQL/review/app acceptance remain
pending. Five original stopped VMs, no disposable clone/QA/tool processes, zero
runner registrations; hosted macOS jobs/no local drain. Private evidence and
unrelated files preserved. No baseline row promoted: one input PASS/eleven BLOCKED,
Pi v0.85.1-psmfd.1, #219 deferred and every existing gate unchanged.
Next concrete action CF-1C-AUTOMATION-3: record bounded first shell-pane synthetic
input/output assertions before a fresh owned boot, including corrected live
window admission. Following docs-only checkpoint records this source SHA/CI and
skips CI. Never reuse a cleaned active-console pointer.

## 2026-11-07 — approved bounded shell probe begins

## CF-1C-AUTOMATION-3 — approved bounded shell-pane input/output

Start e8ff56b24ed7ad67a742cbb0f8bd913f56d0b44e, branch test/client-baseline-gui,
draft #224 against dev. Prior implementation 237afcd CI fourteen jobs green.
Before changes: add a fixed shell-probe queue action in a dedicated QA module,
reuse the existing client install/one-launch, corrected live admission, pinned
Peekaboo GUI Bridge and atomic Tart/VNC lifetime. No product/runtime changes.

Fresh guest and normal permission enrollment. Require corrected live stable
main-window admission. Select exactly one New shell tab AX button from a fresh
explicit snapshot, click it once, then observe the newly added shell tab/terminal
receiver. Deliver one fixed synthetic shell command through Peekaboo UI input.
The command base64-decodes a run-bound marker and tees it to a single owned guest
/tmp/ft-cf1-shell-proof file. Verify that file absent before UI input; only the
interactive shell may create it. Independent guest read must exactly match the
expected marker; decoded marker must be visible in fresh terminal evidence, not
merely echoed as part of the typed command. No remote command writes the proof.

Every probe/mutation gets independent native RFB plus Bridge screen/clock. No
blind replay after an indeterminate input. Observe exact current receiver/value
before deciding to send Return; otherwise stop with evidence. At most one tab
creation, one command delivery and one Return; bounded read-only observations
may repeat. Record app/sidecar/shell ancestry independently. Do not close siblings,
install/configure Pi, access provider/keychain/host clipboard or use operator data.
Bounds: 1200s observer, 1500s guest hold, 40 queue actions, 30s guest-command,
60s fixed install verification, 45s native probe. Private stdout/stderr; explicit
finish/owned cleanup in the same execution; never use cleaned active pointer.

Validation: real-path guard regressions for stale/ambiguous receiver and forged
or pre-existing proof, kept suite/Ruff/repo/docs/hash/secrets checks, one bounded
live run. This is partial CF1-03 evidence, not its full resize/split/sibling case;
baseline remains one input PASS/eleven BLOCKED. Pi v0.85.1-psmfd.1, comparison
comparison #219 deferred and all activation/backend/release/provider gates intact. Next:
implement fixed scenario and evidence assertions before boot.

AUTOMATION-3 delivery refinement before implementation: one compound Peekaboo
foreground type call carries the fixed printf/base64-decode/tee command plus its
single trailing Return, using --accept-dispatched only to preserve unverified
receipts without treating them as success. No separate Return after uncertain
input. Fresh AX must first identify the shell tab and focused terminal receiver.
Read-only proof-file bytes plus independent terminal pixels decide execution;
encoded command echo does not contain the decoded output marker. Never replay.

AUTOMATION-3 preflight: dedicated fixed shell-probe module implemented; reuses
corrected live client admission and existing atomic owner without reimplementation.
All 56 kept guards/Ruff pass. Intentional echoed-command/foreign-run proof
acceptance regression fails; production restored. Fresh AX snapshot/unique button,
shell tab/focused receiver and absent proof are enforced before delivery. One
compound input with Return; all subsequent probes read-only. Next: bounded live
run, independent file/pixel/ancestry review, explicit finish/verified cleanup.

AUTOMATION-3 live run 20261107T181834Z-f41358dcd0f5: corrected main-window
classifier now passes the actual live callback, two uncached snapshots of window 80. Installed identities and independent readbacks verified; guest proof absent
before UI mutation. Next fixed step is unique fresh New shell tab action and
one command/Return, followed only by proof/image/ancestry observations.

AUTOMATION-3 first run 20261107T181834Z-f41358dcd0f5 failed safely: corrected
live window admission passed, one New shell tab and focused AXTextArea observed.
Single UI command/Return dispatched unverified. Native/Bridge images show at
least the first intended delimiter lost: zsh reports command not found printf%s,
versus requested printf %s. The tee-created proof file is empty; exact-byte guard
rejects it and owned cleanup verifies. No input replay, no positive shell proof.

Next bounded atomic comparison CF-1C-AUTOMATION-3B: fresh guest, same pinned
inputs, app admission, tab selection and encoded marker command; change only
foreground typing cadence to 50ms per character (under existing 30s command cap).
One delivery/Return, observe proof and pixels, never retry input in the same guest.
Retain original failed run as real red proof of false-success prevention. Existing
CF-1/shell approval persists; no product/runtime/provider/gate changes.

## 2026-11-07 — shell input/cadence comparison results

## CF-1C-AUTOMATION-3/3B — bounded shell input/output verified

Two fresh owned runs used the same pinned candidate, production runtime inputs
and signed Peekaboo v4.9.8 GUI Bridge. Corrected main-window admission now passes
in both actual live callbacks: two uncached snapshots of the same visible main
window. A unique fresh New shell tab AX target creates one shell tab; fresh AX
confirms a focused Terminal input AXTextArea in that exact window before input.

**Original failure retained:** `20261107T181834Z-f41358dcd0f5` at default cadence
shows `printf%s` and zsh command-not-found in independent native/Bridge frames,
versus intended `printf %s`. The tee-created proof file is empty. Exact-byte guard
rejects it despite the CLI's accepted/unverified dispatch. Exception-triggered
owned cleanup verifies. At least the first delimiter was lost; root cause is
unassigned between automation delivery and the client input path. No blind replay
or product-fix claim. This is real failed-world evidence for the proof guard.

**Bounded positive comparison:** `20261107T182716Z-cc826ea8ca2a` changes foreground
typing cadence to 50ms per character. One fixed encoded-marker printf/decode/tee
command and its single Return produce exactly the expected run-bound guest file.
Independent RFB and Bridge images visibly show decoded marker output followed by
new shell prompt; the marker does not occur in the echoed encoded command. CLI
still truthfully reports dispatched_unverified; --accept-dispatched is not proof.
Measured CLI execution 0.912s default versus 8.153s paced; no general throughput
or arbitrary rapid-typing claim. Per-run markers differ to bind each trial.

Independent process readback confirms app PID 917 -> sidecar 924 -> /bin/zsh 1105
at the expected installed paths. Original exited pi 1 tab remains present. No
Pi/profile/provider/keychain/host clipboard configuration or credentials used.
Both attempts deliver input only once; subsequent checks are read-only. Positive
run explicitly finishes after 21 queue actions. Both owned guests cleaned;
independent inventory: five original stopped VMs, no disposable clone/QA/tool
processes. Raw evidence private; unrelated files and cleaned pointer preserved.

[Sanitized comparison and 828 private-file hashes](evidence/client-baseline/shell-input-comparison.json)
retain both runs, requested commands, exact proof bytes, dispatch outcomes,
independent image/process review and cleanup. All 56 kept guards/Ruff/repo checks
pass. Intentional stale/ambiguous-target and echoed-command/foreign-run proof
regressions fail; restored code passes. Source `ea433e6f8a1792a7e314b2415387269a17a57994` is committed and remotely
backed up in open draft #224 against dev. Source CI
[42402534267](https://github.com/psmfd/FingerTrap/actions/runs/42402534267)
passes all fourteen jobs; title/gitflow pass. Full CodeQL/review and maintainer/
app acceptance remain pending. All 56 guards and 828 private hashes pass locally;
secret scan zero. Zero runner registrations, hosted macOS CI/no local drain.
Following documentation-only checkpoint skips CI and leaves tested source intact. No baseline row promoted:
one input PASS/eleven BLOCKED. This is partial CF1-03 input/output evidence;
resize/split/sibling-close remain untested. Pi v0.85.1-psmfd.1, comparison #219
deferred and all activation/backend/release/provider/maintainer gates unchanged.

**Next concrete action: CF-1C-AUTOMATION-4.** Record a bounded shell resize/split/
sibling-close scenario before a fresh boot, including exact dimensions/process
ancestry and independent marker outputs before/after. Use the observed 50ms
cadence as a limited QA condition; default-cadence delimiter loss remains open
in the baseline tracking. Never treat a CLI receipt or encoded command echo as
execution. Do not promote CF1-03 until its full case is independently observed.

```sh
python3 -m unittest discover -s scripts/tests -p 'test_*.py'
python3 scripts/client_baseline_shell_probe.py \
  --archive .artifacts/pr-merge-20261021/golden-fix-apple/candidate.zip \
  --runtime-archive .artifacts/client-baseline/inputs/pi-darwin-arm64-v0.85.1-psmfd.1.tar.gz \
  > .artifacts/client-baseline/shell-probe-NEW-UNIQUE.log 2>&1
```

Use restrictive umask/no-clobber, fresh explicit printed run and normal guest
permission enrollment. Fixed queue action shell-probe runs one client launch and
single tab/command/Return with mandatory readbacks; finish explicitly. Bounds
remain 1200s observer/1500s guest hold/40 queue actions/30s guest commands/60s
install verification/45s native probes. Never reuse cleaned active-console.json.

## 2026-11-07 — shell input comparison checkpoint and CI

Source ea433e6f8a1792a7e314b2415387269a17a57994 committed and remotely backed up
on test/client-baseline-gui, open draft #224 against dev. All 56 kept guards,
Ruff/repo/docs checks, local links and 828 private hashes pass; secret scan zero.
CI 42402534267 passes all fourteen jobs; title/gitflow pass. Full CodeQL/review/
maintainer/app acceptance remain pending. Draft and issues #216/#217 preserve the
default-cadence delimiter loss/empty proof, bounded 50ms positive file+pixel proof
and corrected live window admission. No product/tool root-cause fix claimed.
Both owned guests cleaned, five originals stopped, no clone/QA/tool processes,
zero registrations. Hosted macOS CI/no local drain. Unrelated files/private
raw evidence/cleaned pointer preserved. Baseline stays one input PASS/eleven
BLOCKED; Pi v0.85.1-psmfd.1, #219 deferred and all gates intact.
Next concrete CF-1C-AUTOMATION-4: record bounded resize/split/sibling-close
assertions before a new owned boot, maintaining proof after every paced input.
Default-cadence loss remains an open qualification limitation. Following docs-only
checkpoint records source/CI identity and skips CI; never reuse cleaned pointer.

## 2026-11-07 — approved shell layout slice begins

## CF-1C-AUTOMATION-4 — approved bounded resize/split/sibling-close

Start 0cb0be6ef2d0748ff89b0cae790424bc2b39219d, branch test/client-baseline-gui,
open draft #224 against dev. Prior source ea433e6 CI fourteen jobs green. Before
implementation: dedicated fixed layout scenario reuses current atomic owner,
pinned candidate/runtime/Peekaboo GUI Bridge, supported permission enrollment,
corrected live window admission and proven 50ms input cadence. No product edits.

Source inspection shows split uses configured default kind rather than inheriting
shell. Explicit guest-only supported ADR-0014 settings fixture before first app
launch: {version:1,pane:{defaultKind:"shell"}} at ~/Library/Application Support/
fingertrap/settings.json. Require absent file/no overwrite. This is a disclosed
shell-configured test condition, not a pristine default-Pi installation. No Pi
executable/profile/provider/keychain or activation/backend/release setting changes.

One initial shell; four fixed paced UI command/Return deliveries generate distinct
run-bound guest proof files only through the interactive shell. Each proof records
decoded marker, $$ PID and /bin/stty size. Check absence before input and exact
marker/parseable positive PID/rows/columns after, with independent native RFB and
Bridge pixels/clock after every probe. No blind retry of a mutation.

Predeclared sequence/assertions: original proof; resize exact main window to
900x600 logical pixels, fresh AX confirms geometry and same shell PID with smaller
rows/columns; Cmd+Shift+D once splits right; fresh AX exposes two terminal input
areas and sibling proof has distinct shell PID/narrower columns. Independently
verify app->sidecar->both shells ancestry. Cmd+Shift+W once closes focused sibling
only; fresh AX returns to one terminal, sibling PID disappears, original stays.
Final original proof has same PID and restored post-resize PTY dimensions. At most
three read-only process observations for asynchronous sibling exit. No external
kill, no closing whole tab/window or unrelated processes. Clean entire owned guest
at explicit finish; never use cleaned active-console pointer.

Bounds reviewed before boot: 1500s observer/1800s guest hold (extra fixed layout
probes), 40 bootstrap queue actions, finite four-proof/one-resize/one-split/one-close
scenario, 30s guest commands/60s install verification/45s native probes. stdout/
stderr redirected privately. Validate target/PID/geometry/proof regression guards,
kept suite/Ruff/repo/docs/hash/secrets, then one live owned run and cleanup.
Baseline stays one input PASS/eleven BLOCKED pending review of complete evidence;
default-cadence loss remains unresolved. Pi v0.85.1-psmfd.1, #219 deferred and all
acceptance/activation/backend/release/provider gates intact. Next: implement and
validate fixed scenario before fresh boot.

AUTOMATION-4 identity refinement before implementation: first shell command also
sets an unexported run-bound FT_CF1_SURVIVOR variable in that interactive shell.
Each proof adds its sentinel value. Original proofs must retain the value; newly
spawned sibling must not inherit it. PID+ancestry+sentinel checks prevent treating
a replacement shell as the surviving original. This is guest shell state only.

AUTOMATION-4 preflight: fixed guest-shell-default fixture, four run-bound
PID/stty/sentinel proofs, exact resize and guarded split/sibling-close implemented.
All 62 kept guards/Ruff pass; guest fixture Python/JSON syntax verified. Intentional
bypass of sibling identity admission fails the kept mutation-path guard; production
restored. Commands fit 30s cap at 50ms cadence. Next: one bounded live owned run,
normal Bridge permissions, proof/image/process review and explicit finish/cleanup.

AUTOMATION-4 failed run 20261107T200710Z-adb15f1b0c25: supported shell-default
fixture and live window admission verify. First 254-character proof at 50ms
cadence returns INTERACTION_FAILED/indeterminate/response_lost, NSPOSIX timeout;
independent pixels show partial line, no proof file exists. It stops before resize,
split or close, no blind input replay, owned cleanup verified. The outer 30s cap
was insufficient to predict the Bridge response budget; preserve actual failure.

Next bounded CF-1C-AUTOMATION-4B: fresh guest, same fixture/input artifacts/50ms
cadence and assertions. Compact each proof to at most 150 characters, using the
run's existing 12-hex nonce in marker/unexported FT4 sentinel and short owned
/tmp/ft4-{b,r,s,a} paths. Preserve absolute tool paths, PID/stty/sentinel fields,
one command/Return per phase, absent-file guard and exact marker binding. No
chunked or repeated input; all other bounds/cleanup/gates unchanged. Add a kept
command-length guard that rejects the original longer pattern before boot.

AUTOMATION-4B preflight: compact proof lengths 144/127/127/127 characters; 50ms
cadence unchanged. All 62 guards/Ruff pass, including 150-character maximum;
intentional long-command regression fails. Fresh run 20261107T203154Z-15a8da01a49a
has all three canonical GUI Bridge grants and is executing the fixed scenario.
Original 254-character timeout attempt remains private and independently cleaned.

## 2026-11-07 — bounded layout comparison results

## CF-1C-AUTOMATION-4/4B — resize/split/sibling-close observed

Pinned candidate and Peekaboo GUI Bridge unchanged. Disclosed guest-only supported
ADR-0014 fixture sets version 1 pane.defaultKind=shell before the single app launch;
this makes the app's normal split action create a shell sibling rather than Pi.
No host/product/Pi/profile/provider/keychain/activation/backend/release changes.

**Original failure preserved:** run `20261107T200710Z-adb15f1b0c25` verifies fixture
and live admission, but its 254-character first proof at 50ms cadence returns
INTERACTION_FAILED/indeterminate/response_lost with a native timeout. Independent
frames show partial input; no first proof verifies. It stops before resize/split/
close without blind replay, and owned cleanup verifies. The outer 30s cap did not
predict the Bridge response budget. Do not claim the application failed layout.

**Bounded positive comparison:** fresh run `20261107T203154Z-15a8da01a49a` retains
50ms cadence and all checks but uses compact 144/127/127/127-character proofs,
bound to the run nonce, phase and four absent-before-input guest files. One UI
command/Return per phase; proof bytes and native/Bridge pixels independently agree.

| Phase                    | Shell PID | PTY rows × columns | Original sentinel         |
| ------------------------ | --------- | ------------------ | ------------------------- |
| Before resize            | 1044      | 39 × 126           | retained                  |
| 900×600 logical window   | 1044      | 34 × 108           | retained                  |
| New right sibling        | 1328      | 34 × 53            | absent, independent shell |
| After sibling-only close | 1044      | 34 × 108           | retained                  |

Fresh AX confirms exact 900×600 main window, two visible Terminal input areas
after Cmd+Shift+D, then one after Cmd+Shift+W. Process reads prove app PID 987 ->
sidecar 994 -> both shells before close; afterward only original 1044 remains,
sibling 1328 absent. Original PID/unexported sentinel and PTY dimensions survive;
no external kill or whole-tab/window close. Independent images retain decoded
markers, split geometry and final original prompt. Explicit finish after 21 queue
actions. Both guests cleaned; five originals stopped, no clone/QA/tool processes.

[Comparison and 1,031 private-file hashes](evidence/client-baseline/shell-layout-comparison.json)
retain failure, fixture, exact proofs, images/process review and cleanup. All 62
kept guards/Ruff/repo checks pass. Intentional unproven-sibling close, replacement/
sentinel/geometry acceptance and overlong-command regressions fail kept guards;
restored source passes. Source checkpoint
`cc86814ce59e54d6816d7b1130427fb3b39e2ae0` is committed and remotely backed up on
`test/client-baseline-gui`, open draft #224 against `dev`. All fourteen jobs in
[CI run 42413472484](https://github.com/psmfd/FingerTrap/actions/runs/42413472484)
pass; title/gitflow checks also pass. All 1,031 private-file hashes reverify and
secret scanning reports zero findings. Five original VMs remain stopped, no
QA/clone processes remain, and runner registrations are zero; macOS CI was hosted.
Draft #224 and issues #216/#217 record the results. Full CodeQL/review and
maintainer acceptance remain pending. Private evidence/unrelated files/cleaned
pointer preserved. No baseline row promoted pending review: one input
PASS/eleven BLOCKED. These are shell-configured, paced, bounded observations;
default-cadence delimiter loss and longer-input response budget remain limitations.
Pi v0.85.1-psmfd.1, comparison #219 deferred and every existing gate unchanged.

**Next concrete slice: CF-1C-AUTOMATION-5.** Record a bounded normal GUI quit check
before a fresh boot: capture exact app/sidecar/shell descendants and an independent
unrelated guest fixture process, invoke supported GUI Quit once, observe only
owned descendants terminate while unrelated fixture stays. No external kill
accepted as normal shutdown proof. Preserve all existing acceptance gates.

<!-- coderabbit-ai-generated:v1 -->

<!-- coderabbit-ai-generated-section:v1:fc6346d4d8e81e67651c84c029d17aab04b3e0edd25af55a1d8db649edcd4c94 -->

```sh
python3 -m unittest discover -s scripts/tests -p 'test_*.py'
# Terminal 1: keep the observer running while Terminal 2 queues requests.
umask 077
set -C
python3 scripts/client_baseline_layout_probe.py \
  --archive .artifacts/pr-merge-20261021/golden-fix-apple/candidate.zip \
  --runtime-archive .artifacts/client-baseline/inputs/pi-darwin-arm64-v0.85.1-psmfd.1.tar.gz \
  > .artifacts/client-baseline/layout-probe-NEW-UNIQUE.log 2>&1
```

In Terminal 2, read the fresh run directory from the private observer log
(the command above redirects stdout/stderr). Its request
directory is `<fresh-run>/layout-observation/`. Read that directory's
`session.json`; when `phase` is `waiting`, use its `nextIndex` for the filename
`request-NN.json` (zero-padded to two digits). Complete normal Bridge permission
enrollment through this queue, then write `{"action":"layout-probe"}` at the
current index. Wait for `session.json` to advance before writing
`{"action":"finish"}` at the new index. Use restrictive umask and no-clobber
in Terminal 2 as well; never overwrite an existing request or reuse an old run.

<!-- /coderabbit-ai-generated:v1 -->

Use restrictive umask/no-clobber, fresh printed run, normal Bridge permission
enrollment, fixed layout-probe queue action and explicit finish. Bounds 1500s
observer/1800s hold/40 bootstrap actions, finite four-proof/one-resize/one-split/
one-close scenario, 30s guest/60s install/45s native probe. Never select cleaned
active-console.json. No automatic row promotion, runtime adoption or publishing.

<!-- coderabbit-ai-generated:v1 -->

<!-- coderabbit-ai-generated-section:v1:0e85c9fd270da5fd4ad9c9285de8f96939afb6d21f4b83b1e66a6e2594313653 -->

## 2026-11-07 — baseline probe review corrections

<!-- coderabbit-ai-generated:v1 -->

<!-- coderabbit-ai-generated-section:v1:3681788a028869df2b213358b20af71b4e878b35a8d09da83ac9e67d49ab7ef8 -->

CF-1C-AUTOMATION-4 follow-up: latest consecutive window IDs, bounded partial
proof readback and concurrent operator queue instructions. All 66 Python guards,
`scripts/check.sh` and focused Ruff correctness checks pass. Each original
implementation fails its new guards; restored fixes pass. Full Ruff reports
existing import/style/executable-mode findings in the review environment. These
fixes are committed in `287caafada5d5851593b79121066a9c78fa90833`; all fourteen
CI jobs and current CodeQL checks pass. No live macOS run was performed by that
review task; historical live evidence and baseline gates remain unchanged.

<!-- /coderabbit-ai-generated:v1 -->

## 2026-11-07 — layout source checkpoint and CI

<!-- /coderabbit-ai-generated:v1 -->

Source `cc86814ce59e54d6816d7b1130427fb3b39e2ae0` is committed and remotely
backed up on draft #224 (`test/client-baseline-gui` against `dev`).
[CI run 42413472484](https://github.com/psmfd/FingerTrap/actions/runs/42413472484)
passes all fourteen jobs; title/gitflow pass. Local validation includes 62 kept
guards, restored-source regression checks, 1,031 private evidence hashes, and
zero secret findings. Draft #224 and issues #216/#217 are updated. Both owned
runs cleaned; five originals stopped, no clone/QA process, zero runner
registrations. CI macOS jobs were hosted.

Next: predeclare CF-1C-AUTOMATION-5 normal GUI Quit and descendant termination,
with an unrelated guest process as survival control. No baseline promotion:
one input PASS/eleven BLOCKED; shell fixture, paced compact input and retained
long-input timeout remain explicit. Pi stays v0.85.1-psmfd.1, #219 comparison
deferred, and every activation/backend/release/review gate remains intact.

## 2026-11-07 — approved CF-1C-REVIEW-1 corrections

## CF-1C-REVIEW-1 — approved merge corrections in progress

Starting from reviewed `287caafada5d5851593b79121066a9c78fa90833` on
`test/client-baseline-gui`, ready PR #224 against dev. Approved scope: discard a
failed native-console connection, reconnect before the next request with ownership
revalidation, preserve failure receipts and never replay input. Add a regression
that fails on the original code. Correct stale checkpoint/queue instructions and
formatting. Validate all kept guards/Ruff/repo/docs, then one fresh bounded owned
layout confirmation with immediate independent screens/clocks and private output.
Boot/probes/cleanup remain one owner execution; never use a cleaned pointer.
Historical evidence stays historical; baseline one PASS/eleven BLOCKED, pinned
Pi v0.85.1-psmfd.1, #219 deferred and all gates unchanged. No merge authorized.

## 2026-11-07 — CF-1C-REVIEW-1 correction results

## CF-1C-REVIEW-1 — corrections implemented and locally/live verified

Failed native-console requests now close/discard their socket; the next request
revalidates ownership and creates a fresh Console. Failure receipts stay intact;
no input is replayed. New regression uses text input followed by capture, verifies
old-socket closure before replacement and exactly one text dispatch. Reviewed
original source fails it (one connection rather than two); corrected source passes.
All 67 kept guards, baseline Ruff/format, repository and Markdown/Prettier checks
pass. Secret scanning passes with zero findings after authorized report-directory
access; the initial sandbox report-write failure is not a passing scan.

Fresh run `20261107T214721Z-76b7d2bd30f0` verifies normal guest-only Bridge
enrollment, stable main window 82 and all four compact 50ms shell proofs. App 973
-> sidecar 980 -> original shell 1030; sibling 1311 is independently verified and
closed. Original PID/sentinel survive 39x126 -> resized 34x108 -> sibling 34x53 ->
original 34x108. Fresh native/Bridge images show decoded output and final prompt.
All proof reads completed at attempt 00: partial reads and connection failure
recovery are regression-tested, not live fault-injected. Supported guest-only
shell-default fixture and all prior typing limitations remain explicit.

**Execution limitation retained:** layout result completes, then the 1500s observer
deadline expires awaiting final finish. Owner exits 1 with TimeoutError; the later
finish helper rejects the already-cleaned run without queuing input. This is not
an overall successful execution or app Quit proof. Exception-triggered owned
cleanup and independent atomic cleanup both verify. Five original VMs stopped,
no disposable clone or QA/tool processes remain; never reuse the cleaned pointer.
[Evidence summary and 652 private-file hashes](evidence/client-baseline/review-corrections.json)
preserve the actual result and timeout. Original private evidence is unchanged.

Implemented and locally/live verified; scoped commit, remote backup, current-source
CI/CodeQL and review-thread closure are next. No merge performed or authorized.
Baseline stays one input PASS/eleven BLOCKED; Pi v0.85.1-psmfd.1, #219 deferred,
and all acceptance/provider/activation/backend/release gates remain intact.
Next after merge review: CF-1C-AUTOMATION-5 normal GUI Quit/descendant termination
with unrelated guest process survival control, predeclared before fresh boot.

## 2026-11-07 — CF-1C-REVIEW-1 source checkpoint

Source `74f9d3b08d4f86e9e71ee339e94c2534992396c3` committed and remotely backed
up on ready PR #224 against dev. All fourteen CI jobs in run 42420784750 and
title/gitflow checks pass; all six review threads are resolved. CodeQL Rust is
still running; the neutral aggregate is not accepted as a green security gate.
No merge. Next: finish security verification and final documentation checkpoint.

Timeout clarification: the latest failure is the outer 1500-second wall-clock
observer deadline after layout result completion, including normal permission
enrollment and operator/approval queue waits. VM load has not been isolated as
a cause. The earlier long typing failure is an inner Bridge response timeout;
raising the outer deadline would not fix it. For the next fixed slice, predeclare
automatic completion or separate enrollment/scenario budgets rather than quietly
increasing the overall deadline. No timeout changes made in this correction.

## 2026-11-07 — CF-1C-REVIEW-1 merge-review handoff

Correction source `74f9d3b08d4f86e9e71ee339e94c2534992396c3` is remotely backed
up. All fourteen CI jobs (42420784750), all five CodeQL language analyses and
the aggregate (42420782243), title and gitflow checks pass. Rust completion
cleared the temporary neutral aggregate. All six review threads resolved; ready
PR #224 remains open against dev. PR body records the correction and actual
wall-clock timeout/verified cleanup. Automatic CodeRabbit review remains skipped
for the dev base; the addressed review and regression validation are retained.

Both requested corrections are complete. Recommend maintainer merge as a tooling/
evidence checkpoint after final review; no merge executed. Keep one PASS/eleven
BLOCKED and all app acceptance/activation/backend/release gates. The live layout
proofs pass, but owner exit 1 on the final-wait deadline is retained explicitly.
Next implementation action: predeclare CF-1C-AUTOMATION-5 normal GUI Quit and
owned descendant termination with unrelated guest survival control, and a finite
completion path that avoids waiting for a final operator request after all proofs.
Do not silently increase deadlines or infer a VM-performance root cause.

## 2026-11-07 — approved #224 merge, current-head validation

## CF-1C-REVIEW-1 — maintainer merge approval and required head checks

Maintainer approved merging PR #224 into dev after the correction review.
Correction source `74f9d3b08d4f86e9e71ee339e94c2534992396c3` passes all fourteen
CI jobs and all CodeQL analyses; documentation checkpoint
`e80d638296dde1e4b348c852c3bd2d3dd95c20c4` passes CodeQL. Its skip-ci marker
left required workflow checks absent at the current PR head, so merge protection
blocks it. This scoped documentation update deliberately runs normal CI. Do not
bypass protection, synthesize check statuses or alter any gates. Once current-head
required checks and security pass, execute the approved squash merge and verify
PR merged/base dev plus the resulting commit. Record that actual merge identity
in the next migration status update. No new guest execution is needed.

Baseline stays one input PASS/eleven BLOCKED; Pi v0.85.1-psmfd.1, #219 deferred
and all application acceptance/provider/activation/backend/release gates unchanged.
Next implementation slice remains predeclared normal GUI Quit/descendant checks;
this approval authorizes merge, not baseline promotion or activation.

## 2026-11-07 — CF-1C-AUTOMATION-5 predeclared

## CF-1C-AUTOMATION-5 — approved normal GUI Quit slice

PR #224 squash-merged into dev as `91bf7c53774e8559c486d3b59ad71be7b43e7f1b`;
all fourteen final-head CI jobs and CodeQL passed before merge. Current branch
`test/client-normal-quit` starts at that merge. Existing CF-1 approval persists.

Before implementation/boot: reuse pinned artifacts, signed guest GUI Bridge,
supported guest-only shell-default fixture, latest-pair app admission and one
compact paced UI shell proof. Create an unrelated guest-only `/bin/sleep 1200`
control outside app ancestry. Independently bind exact app/sidecar/shell ancestry
and all app descendants plus control PID/start-time/executable identity. Invoke
normal foreground Cmd+Q once against the admitted exact window. Never externally
kill app/sidecar/shell as shutdown proof. At most three read-only post-Quit process
observations must show all recorded descendants gone and unchanged unrelated
control alive. Native/Bridge frames and independent guest clock follow every
probe, including failures. Whole owned VM cleanup happens afterward.

Bounds: existing 1500s observer/1800s guest hold, 40 enrollment queue actions,
reserve 360s before starting fixed scenario (enrollment must finish by 1140s),
30s guest commands/60s install/45s native probes. A successful fixed quit dispatch
automatically finishes the queue; no final operator request. Failed verification
must never mark completion. stdout/stderr private, restrictive umask/no-clobber;
no cleaned pointer, host installs, TCC database edits, credentials or product edits.

Validate ancestry/control identity, survivor/reparented-child refusal, one Quit
mutation and automatic completion regressions, full kept suite/Ruff/repo/docs/
links/hash/secrets, then one fresh owned live comparison and cleanup. Baseline
one PASS/eleven BLOCKED, Pi v0.85.1-psmfd.1, #219 deferred; every application
acceptance/provider/activation/backend/release gate remains unchanged.
Next: implement fixed probe and its regression guards before boot.

CF-1C-AUTOMATION-5 implemented locally: fixed quit probe and explicit successful-
dispatch queue completion hook. All 73 kept guards pass; intentional bypass of
shutdown attribution and removal of automatic completion fail the new guards.
Ruff/format/repo/docs checks pass. No product code changed. Next: one fresh owned
run with the predeclared 1500s observer, 360s reserved scenario and automatic finish.

## 2026-11-08 — normal GUI Quit result and cleanup

## CF-1C-AUTOMATION-5 — bounded normal GUI Quit observed, 2026-11-08

PR #224 merged into dev as `91bf7c53774e8559c486d3b59ad71be7b43e7f1b` after
all fourteen required CI jobs and CodeQL passed. Follow-up branch
`test/client-normal-quit` adds a fixed shell-configured Quit scenario and optional
successful-dispatch completion hook to the existing atomic queue; no product edits.

Run `20261108T152238Z-db6bbf2949e6`: pinned guest-only GUI Bridge grants verify;
live main-window admission yields window 82 twice. One compact UI shell proof at
50ms cadence independently verifies decoded output, PID 1063, 39x126 PTY and
run-bound sentinel. Final independent process inventory binds app 1006 -> sidecar
1013 -> shell 1063, plus unrelated adopted `/bin/sleep` control 961 with recorded
start time, executable and parent. One normal foreground Cmd+Q targets window 82.
The first post-Quit read finds every recorded app descendant gone while control
961 retains its complete identity. Native RFB and Bridge images show the app
window gone. No external app/sidecar/shell kill or reparenting accepted as proof;
no claim about the internal graceful-versus-watchdog shutdown path.

**Automatic completion passed:** successful dispatch finishes after 21 actions,
without a final operator request. Owner exits 0; no timeout or error events.
Owned cleanup plus independent atomic cleanup verify; five originals stopped,
no clone/QA/tool processes remain. The unrelated control survives Quit and is
removed only with the owned VM. Existing 1500s observer/1800s hold bounds retained,
360s reserved for scenario admission, max three read-only post-Quit observations.

[Quit evidence and 472 private-file hashes](evidence/client-baseline/normal-quit-observation.json)
retain process identities, UI proof, raw frames/readbacks and cleanup. Original
1,031 layout and 652 review-correction hashes reverify unchanged. All 73 kept
guards pass; bypassing termination attribution or automatic completion fails the
new guards and restored source passes. Ruff/format and repository checks pass.
Implemented and locally/live verified; scoped commit, remote backup, draft PR,
current-head CI/CodeQL and review remain next. Unrelated local files preserved.

This is a bounded observation in the supported guest shell-default fixture, with
paced input and pinned automation installed. The final desktop still contains
an unrelated Peekaboo Screen Recording permission dialog despite successful
Bridge capture/grant readbacks; unattended enrollment is not claimed resolved.
No pristine Finder-install, live Pi/RPC pane cleanup, forced fallback or full
acceptance case is inferred. Baseline stays one input PASS/eleven BLOCKED pending
review; Pi v0.85.1-psmfd.1, #219 deferred, and every acceptance/provider/activation/
backend/release gate unchanged. Never reuse the cleaned console pointer.

**Next concrete slice: CF-1C-AUTOMATION-6.** Before a fresh boot, predeclare a
bounded repeated normal LaunchServices launch against an already observed app.
Verify same app/sidecar/proven shell and visible window identity, no duplicate
process tree, fresh focus/input proof, and automatic owned cleanup. Keep hidden/
minimized/startup/quit-race variants separate; no Pi/provider/credential changes.

Reproduce with restrictive umask/no-clobber and private stdout/stderr:

```sh
python3 -m unittest discover -s scripts/tests -p 'test_*.py'
python3 scripts/client_baseline_quit_probe.py \
  --archive .artifacts/pr-merge-20261021/golden-fix-apple/candidate.zip \
  --runtime-archive .artifacts/client-baseline/inputs/pi-darwin-arm64-v0.85.1-psmfd.1.tar.gz \
  > .artifacts/client-baseline/quit-probe-NEW-UNIQUE.log 2>&1
```

Terminal 1 runs the observer; Terminal 2 reads the explicit fresh run path from
that private log, then `<fresh-run>/quit-observation/session.json`. At each waiting
`nextIndex`, write a new no-clobber `request-NN.json` (two digits). Complete normal
permission enrollment using freshly observed native coordinates. Queue only
`{"action":"quit-probe"}` once at the current index; successful verification
finishes automatically. No final finish request and no cleaned pointer reuse.

## 2026-11-08 — normal Quit source checkpoint and draft review

Source `2969e5e0133a2d95bcbf650c1e1ceeeae54ce04d` is committed and remotely
backed up on `test/client-normal-quit`. Draft PR #227 targets dev and records
the 73 kept guards, observed normal Quit/control survival, automatic finish/exit 0,
472 private hashes and verified cleanup. Original 1,031 + 652 evidence hashes
remain unchanged. Secret scan zero findings. The supported shell-default fixture
and remaining automation permission dialog are explicit limitations.

PR #224 merge identity is `91bf7c53774e8559c486d3b59ad71be7b43e7f1b`. This
follow-up is not merged; maintainer review and current-head CI/CodeQL remain gates.
Read [current PR checks](https://github.com/psmfd/FingerTrap/pull/227/checks) rather
than treating historical source checks as final-head results. This documentation
checkpoint uses normal CI, not skip-ci, so required checks run on its head.
Next concrete implementation action after review is predeclared
CF-1C-AUTOMATION-6 repeated normal LaunchServices launch with stable identities,
fresh focus/input proof and automatic cleanup. No additional live slice performed.
Baseline one PASS/eleven BLOCKED and all existing gates remain intact.

## 2026-11-08 — clarified approval: repeated launch then Quit rerun

## CF-1C-AUTOMATION-6 then Quit rerun — approved order, 2026-11-08

Maintainer explicitly selected: add bounded repeated-launch validation to existing
draft #227, then rerun bounded Quit on that updated branch before readiness.
Branch remains `test/client-normal-quit`, starting head
`6464b466c436edcecf216209e83e6223b0177771`; #227 is draft/unmerged. Do not merge.
After both runs and current-head CI/CodeQL pass, mark ready for maintainer review;
maintainer may request CodeRabbit scanning and alone decides the merge.

CF-1C-AUTOMATION-6: same pinned candidate/runtime/guest-only GUI Bridge and
supported shell-default fixture. First admit one visible app/window and a proven
shell; capture app/sidecar/shell/control PID/start-time/executable identities.
Bring Finder forward through normal LaunchServices, then invoke normal
`open /Applications/FingerTrap.app` once (no force-new-instance flag). Fresh AX
and window inventory must bind the same visible main window and focused process.
A second compact 50ms UI proof must retain shell PID, unexported sentinel and PTY
geometry. Independent process inventory must retain the entire admitted app tree
and unrelated control, with no duplicate app/sidecar. Record native/Bridge frames
and independent clock immediately after every probe. Complete automatically after
verified fixed dispatch, then owned cleanup. No hidden/minimized/startup/quit-race
variants, product/runtime edits, operator/provider credentials or host changes.

Predeclared bounds: 1500s observer/1800s guest hold, 480s reserved repeat scenario
(extra second proof/focus observations), enrollment finishes by 1020s, max 40
queue actions, one Finder activation/one repeat app launch/two proof deliveries;
existing 30s guest/60s install/45s native probes. Do not increase the outer budget.
Commit source before live execution. Preserve both red guards and local checks.
After repeat evidence is recorded on this branch, run the existing normal Quit
probe in a second fresh owned guest with its unchanged 360s scenario reservation.
No code changes between these two validations unless a real failure requires a
separately recorded correction. Final evidence-only documentation may follow;
record the exact tested source heads and distinguish documentation-only changes.

stdout/stderr remain private with restrictive umask/no-clobber; preserve all
existing evidence and unrelated files. Never reuse a cleaned active-console
pointer. Five original VMs stopped and no disposable clone/QA process at entry.
Baseline stays one input PASS/eleven BLOCKED; Pi v0.85.1-psmfd.1, #219 deferred,
and all acceptance/provider/activation/backend/release gates remain unchanged.
Next: implement/guard repeated launch, checkpoint source, then the two live runs
in the selected order. No readiness transition until both validations complete.

CF-1C-AUTOMATION-6 implementation is locally verified: all 77 kept guards pass,
including normal single-delivery LaunchServices ordering/failure handling and
process/window/shell/nonce/geometry replacement refusal. Intentional acceptance
bypass fails new guards; restored source passes. Ruff/repo/docs checks pass.
Next: commit source before the ordered repeated-launch and Quit live validations.
The remote PR remains draft at 6464b46 until the completed update is pushed.
