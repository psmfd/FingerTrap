# Runtime migration: current status

Updated: 2026-10-21 UTC, maintained-base forward-port and requalification approved.

## Authorization and scope

The maintainer approved the
[implementation plan](implementation-plan-200-profile.html) on 2026-10-20
and explicitly requested durable tracking across failures and sessions.
The approved plan covers FingerTrap [#200][import] and [#201][profile].
The initial request said #210; the plan disclosed its failed lookup and
proposed #201. The subsequent plan approval is the working authorization
for that disclosed scope. No requirements are invented for #210.

The maintainer also approved the focused S3-A through S3-F implementation
plan on 2026-10-20. Proceed within that scope without repeating approval.
Executable/RPC and Pi `v0.85.1-psmfd.1` remain the migration baseline.
Production SDK adoption, managed distribution/activation, new trusted
approval capabilities and repository retirement retain their separate gates.

The maintainer approved S3-G1 through S3-G4 and preparation of the S3-G5
qualification report on 2026-10-21. This includes the focused Pi fork patch,
FingerTrap fixture integration, companion commits/draft PRs and unpublished
qualification artifacts. Exact-pin promotion and production activation retain
their separate decisions.

The maintainer subsequently approved the maintained-base forward-port,
capability-patch limit resolution and exact-artifact requalification. Proceed
on verified fork main `42429b94e4aca33fdbb8d094de29758b36411ba1`, preserving
its newer fixes. Record a bounded patch-020 exception and actual accounting;
do not silently raise unrestricted fork limits. Exact production pin/release
and managed activation remain separately gated.

After this campaign settles, the maintainer requests a latest-Pi release
upgrade and validation campaign. Track it here now; resolve "latest" from
upstream/fork release provenance when that campaign starts, then retain the
managed policy and required fork patches and repeat protocol/artifact tests.

## Resume here

**Current activity: S3-G6 — maintained-base forward-port and requalification.**

Next action: checkpoint and push the FingerTrap fixtures/recorder, run its
required CI with five local macOS jobs, and save the qualification report.
The repaired recorder passes all twelve scenarios twice on both binaries;
their normalized transcripts match exactly. Four runtime mutations are caught
and source restored. Pi `1f37eea` is committed/pushed
in draft [#82](https://github.com/psmfd/pi/pull/82), isolated worktree
`/private/tmp/ft-pi-maintained`, branch `fix/managed-startup-maintained`.
Static checks, full hermetic suite, exact direct/published-host/Tart isolation
pass. ADR-0039 bounds the cap exception: 7 patches / 25 paths / 2,090 lines.
Candidate binary SHA-256 is
`9a9f53131cec748801b8c176190ef4dc77e7b6e833859ff734fcfa3d297c7614`.
Original committed goldens remain unchanged. Maintained 0.87.1 adds system
message/tool-schema events compared with the production 0.85.1 baseline;
that version drift is retained separately from the zero candidate drift.
The old candidate and evidence below remain comparison history. Keep the
original Pi draft/branch intact until the replacement is backed up.

The [resolution plan](s3-discovery-resolution-plan.html) is approved. Begin
with [Pi #80](https://github.com/psmfd/pi/issues/80), ADR-0038 and the separate
candidate qualifier. Both baseline cases fail actual isolation checks while RPC lifecycle succeeds.
Pi policy and FingerTrap capability integration are implemented and locally
verified. Committed Pi direct qualification and the published host matrix pass.
The exact artifacts also pass in a clean disposable Tart VM with verified
cleanup. All twelve twice-recorded RPC scenarios match the baseline exactly;
the seven differences from committed goldens are shared path spelling only.
All 23 CI check reports pass on `c6f088c7cf7b2ef8294a104a091c5d5d08271e03`,
including the full platform matrix and Apple Silicon package/smoke. All five
macOS jobs ran locally; independent listings verify cleanup and a stopped base.
The [G5 report](s3-managed-startup-qualification.html) records exact identities,
fork accounting, protocol equality, platform limits and recovery instructions.
Maintained-base/forward-port and fork-cap work is now approved. Requalify that
candidate before an exact production pin decision.
Do not merge the old-base Pi draft over newer main or treat this as S3 complete.
No further implementation within G1–G4 or report preparation is outstanding.
Host checkpoint `bb5f02fd64b41310a7f35aec92a60b67bdd8bad8` is committed and pushed on
`feat/managed-pi-startup`; draft [#209](https://github.com/psmfd/FingerTrap/pull/209)
targets dev. Pi work is isolated at `/private/tmp/ft-pi-managed`, branch
`fix/managed-startup-v1`, based on `e7d09c9`. Pi candidate `39b729b625f50d4c82bd993151865d5ded8dd78e` is committed/pushed
with draft [Pi #81](https://github.com/psmfd/pi/pull/81). Its standalone macOS
ARM64 direct RPC/PTY/CLI matrix passes. Published host SHA-256
`80beb8542052fe1246528d2a9a0617e2b3760c81c39163f228b0c128d97af3da`
passes two profiles with real RPC/PTY children. Migration, discovery and host
capability mutations are detected and restored. All essential artifact reports
are retained under `docs/migration/evidence/s3-managed-startup/`.
Pi metadata checkpoint `b0a151df397d88db1cb3f52b7e8a526ee22deb1e` is pushed;
pending patch 020 and both guard allowlists agree, with guard logic unchanged.
It does not change tested runtime source `39b729b`. Current main's cap is full;
straight addition projects 7 patches / 25 paths / 2,081 changed lines.

1. PR [#207](https://github.com/psmfd/FingerTrap/pull/207) squash-merged into
   `dev` as `9bfc0926cf387644a1cd2512e3fcb63f22287165`. The actual candidate
   `8196b4f` passed every check, including all five local macOS jobs and CodeQL;
   the merged tree is identical. CodeQL #8 is fixed by a gateway-only canary.
2. Only the three macOS context names changed in `dev`'s ruleset and classic
   protection. App bindings, strictness and every other protection are unchanged.
   `main` retains old names until the later dev-to-main release promotion.
3. Protected [dev CI](https://github.com/psmfd/FingerTrap/actions/runs/39531687800)
   and [dev packaging](https://github.com/psmfd/FingerTrap/actions/runs/39531859652)
   passed. All 22 reported checks passed, including CodeQL and hosted lanes.
   Five local jobs used distinct VMs; artifact source/digests/runner identity
   were verified. No job VMs, runner registrations or open dev CodeQL findings
   remain.
4. Current implementation branch is `feat/managed-pi-startup`, draft #209,
   carrying the handoff documentation. Its initial checkpoint skipped CI so
   #209 could be enrolled for local macOS execution before source builds.
5. Resume migration with the [approved S3 plan](s3-launch-isolation-plan.html),
   [discovery audit](s3-discovery-audit.md) and [foundation report](s3-launch-foundation.md).
   The pinned Pi performs project migrations before trust, so arbitrary-project
   managed activation remains blocked under #201/#202. S3-G implementation is
   now approved; production pin promotion and SDK adoption remain separate.
6. Before future source pushes, run `python3 scripts/ci/tart-runner.py check`.
   Once jobs queue, run `python3 scripts/ci/tart-runner.py drain --max-jobs 5`;
   use four when only CI is queued. The controller is session-started.
   See the [runbook](../local-github-runner.md) for recovery and trusted PR
   enrollment; future PRs are not enrolled automatically.

No managed launch is enabled.
Read the latest [activity entry](WORK_LOG.md) before resuming; compare this
record with `git status`, `git log` and any active process/test output.

## Work board

| ID | Approved result | Status | Evidence / remaining work |
| --- | --- | --- | --- |
| M0 | Ownership ADR, inventory and successors | Committed and backed up | `6a999c9`; ADR-0034 accepted; inventory and #200–#206 exist |
| M1a | Token/cache-meter source package | Verified, backed up, inactive | [Report](m1a-import.md); runtime package CI passed on all three platforms at `6a999c9` |
| S1 | Profile schema, compatibility and baseline | Verified locally and in CI; backed up | `52fc6bc`; [foundation report](profile-foundation.md); complete CI and Apple Silicon package/smoke passed |
| S2 | Reviewed import slices and test registration | Verified locally and in CI; backed up | `92137c4`; [manifest report](import-manifests.md); all platform checks and Apple Silicon package/smoke passed |
| S3 | PTY/RPC launch isolation | Foundation merged; candidate qualified; pin decision pending | Production baseline still lacks managed policy; candidate evidence is in the G5 report; S3 is not complete |
| S3-G | Close the pinned-Pi startup/discovery gap | G1–G4 implemented/verified/backed up; G5 report ready | [Report](s3-managed-startup-qualification.html); exact host/direct Tart, RPC comparison and 23 CI reports pass; maintained-base/cap decision and pin promotion remain gated |
| S3-G6 | Preserve maintained fork fixes and requalify managed startup | Port and cap disposition implemented; qualification in progress | Main `42429b9`; ADR-0039; 7 patches / 25 paths / 2,090 lines; exact host/Tart/protocol evidence pending |
| Pi-next | Upgrade to latest Pi release and validate | Tracked in [#210](https://github.com/psmfd/FingerTrap/issues/210); after this campaign | Discover latest at execution time; preserve fork/managed patches, review dependencies and repeat protocol/platform/artifact qualification |
| CI-local | Reduce hosted builds with a local runner | Merged and verified on protected dev | `9bfc092`; 22 post-merge checks passed, five macOS jobs local; artifact/cleanup verified; dev protection mapped; main mapping deferred to promotion; Windows/Linux hosted |
| S4 | Stores, resume and isolated meters | Pending | Inject profile roots; preserve ADR-0028 and recovery behavior |
| S5-A | State/observability import wave | Pending | Shared closure, prefill dependency, artifact handoff |
| S5-B | Guards and typed tools | Pending | Enforcement tests, identity, tool ownership, vendor notices |
| S5-C | Worktree/subagent dispatch | Pending | PR #1170 fix, #280 reconciliation, child isolation |
| S5-D | Context/presentation | Pending | Preserve tuning; gate unsupported RPC UI |
| S5-E | Capability-dependent source | Pending | Preserve reviewed contracts/tests; #203/#204 activation gates |
| S5-F | Curated defaults | Pending | Explicit selection, rights review, restrictions and evaluations |
| S6 | Integration qualification and issue reconciliation | Pending | Real pinned runtime, independent profiles, native/process evidence, hosted CI |

“Pending” means not started. Source import is not activation. A local pass
does not imply hosted CI, package installation, provider login or release
qualification. #200 and #201 remain open until their full criteria pass.

## Durable locations and recovery

- This file: authoritative current state, next action and blockers.
- [WORK_LOG.md](WORK_LOG.md): chronological changes, checks and checkpoint IDs.
- [Issue ledger](issue-ledger.md): inherited work and successor ownership.
- [Source manifest](source-manifest.json) and
  [package provenance](../../packages/agent-runtime/provenance.json):
  immutable source identities, transformations and destination digests.
- Git commits/remote branch: recoverable source checkpoints. Record actual
  push/PR/CI state here after observing it; an attempted push is not a backup.

At an interruption, preserve dirty work. Inspect the last logged command and
process state before rerunning; read diffs before assuming a step finished.
Use the pinned source identities, not a newer branch tip. Run the affected
validation after resuming an incomplete edit and update this record.

## Current checkpoint / validation state

- S3-G proposal: [HTML review document](s3-discovery-resolution-plan.html),
  41 links and 13 unique IDs checked; script syntax, 390-pixel mobile layout,
  desktop layout and section navigation passed. No remote assets. Markdown,
  structural and whitespace checks passed. These are document checks only;
  implementation approved 2026-10-21; exact-pin promotion remains gated.
- Merged migration: `docs/pi-runtime-migration`, PR #207, squash `9bfc092` on dev.
- Active handoff branch: `docs/runtime-migration-handoff`, based on `9bfc092`.
  The remote `ci/tart-runner-pilot` branch preserves worker checkpoint
  `cf1cd50` for immutable reuse; do not delete it.
- Base: `8ffeae809f7b4579d6b4f006c3f6e8414df60620`.
- Baseline checkpoint: `6a999c9`, pushed to
  `psmfd/FingerTrap:docs/pi-runtime-migration`.
- Dependency repair: `44455f5`; S1 foundation: `52fc6bc`. Both pushed to the
  same remote branch. Source remains inactive.
- S2 import/test manifests: `92137c4`, pushed to the same remote branch.
- S1/S2 handoff: `a10f2c3`, committed and pushed. Its documentation-only
  checks are distinct from the verified S2 source checkpoint.
- S3 approval plan: `dac31ce`, committed and pushed; approved by the
  maintainer in the subsequent turn.
- S3 implementation/audit: `95424b3`, committed and pushed. PR #207 now
  describes profile/import/launch foundations and the explicit discovery gap.
- Merged review: [PR #207](https://github.com/psmfd/FingerTrap/pull/207),
  targeting `dev`; runtime activation remains disabled.
- Existing M1a local results: [recorded report](m1a-import.md).
- Checkpoint revalidation: all 20 provenance entries, lint, typecheck and
  41 meter tests passed; staged-export secret scan found no leaks.
- HTML plan: local links/anchors and JavaScript syntax verified; desktop and
  390-pixel mobile render checked; navigation works; no remote assets.
- Latest repository structural check: passed, zero errors/warnings, during
  baseline checkpoint preparation.
- S1 local: package provenance/lint/typecheck; 69 runtime tests; Release
  sidecar build zero warnings/errors; 359 sidecar tests passed, 12 skipped.
  Managed fallback mutation detected by both guard cases; restored suite green.
- Baseline CI: runtime/UI matrix, structural checks and CodeQL passed. All
  sidecar/Tauri jobs failed restore on MessagePack 2.5.302 (NU1902).
  Patched 2.5.303 plus affected locks pass local audited locked restore and
  the S1 hosted matrix.
- S1 hosted: all checks passed, including sidecar/runtime/UI/Tauri on Linux,
  Windows and macOS, CodeQL, and Apple Silicon package/smoke.
  [CI run](https://github.com/psmfd/FingerTrap/actions/runs/39443316974);
  [Apple Silicon run](https://github.com/psmfd/FingerTrap/actions/runs/39443316965).
- S2 local: complete 35-file package inventory, eight suites, 84 passing tests,
  independent offline frozen restore/validation, and omitted-suite mutation
  detected. No imported implementation changed.
- S2 hosted: all checks passed for `92137c4`, including the full platform
  matrix, CodeQL and Apple Silicon package/smoke.
  [CI run](https://github.com/psmfd/FingerTrap/actions/runs/39445278440);
  [Apple Silicon run](https://github.com/psmfd/FingerTrap/actions/runs/39445278068).
- The final status/log update changes documentation only. Its new head may
  have a fresh CI run; the source evidence above names the verified commits.
- S3 planning: standalone HTML, 42 links and 12 unique IDs checked; script
  syntax, 390-pixel mobile layout, desktop layout and current-section
  navigation passed. Markdown, structural and whitespace checks passed.
  These are document checks, not S3 implementation acceptance.
- S3 local: [report](s3-launch-foundation.md), 401 passing sidecar tests,
  12 existing skips, zero failures; four representative regressions detected
  and reverted. Published ARM64 sidecar/native companion smoke passed.
  Exact-release migration blocker reproduced and tracked in the audit.
- Local runner: `a6bdb88` pilot, `a403061` Bash correction, `cf1cd50` reusable
  worker, `e042746` routing. All backed up. Source `e042746` passed 23 checks,
  including five macOS jobs on Tart and hosted Windows/Linux/CodeQL.
  Downloaded candidate digests/source/runner identity verified; cleanup checked
  independently. Prior documentation handoff `d701bdf` was saved with CI
  skipped; its head is distinct from the verified source checkpoint. The later
  merge and protected-dev evidence are recorded below.

## Commands for the next session

```sh
git status --short --branch
git log -5 --oneline
gh run list --repo psmfd/FingerTrap --branch dev --limit 5
python3 scripts/ci/tart-runner.py check
pnpm --dir packages/agent-runtime check:provenance
pnpm --dir packages/agent-runtime test
```

Use the repository-authorized GitHub identity when checking a private source
or writing this branch; do not change the operator's global active account.
For sidecar changes, the reliable local path is audited locked restore, then
`dotnet build src-sidecar -c Release --no-restore -m:1 -p:UseSharedCompilation=false`
and `dotnet test src-sidecar -c Release --no-build --no-restore`.
Native/process checks may need the tool's filesystem/process sandbox approval.
After a new source push queues the local jobs, run
`python3 scripts/ci/tart-runner.py drain --max-jobs 5`. Do not claim a later
documentation-only or changed head has CI evidence from a different commit.

## Known gates and protected local state

- Exact-fork discovery and credential-source controls require verification.
  A missing control blocks managed activation; it does not authorize a pin
  bump or SDK switch.
- User-managed mode stays compatible. No automatic operator auth/settings/
  session migration; no global npm, shell-rc or operator `~/.pi` writes.
- Preserve `.local-testing/`, `LEGACY_pi_config_3579dc1.patch`,
  `NEXT_SESSION_pi-fold-reassessment.md` and pi_config's dirty local notes.
  The legacy patch is private evidence until a reviewed fix is reconstructed.
- The current `origin` uses the repository's older owner URL. GitHub confirms
  the canonical destination is public `psmfd/FingerTrap`; use the authorized
  repository account when backing up commits.

[import]: https://github.com/psmfd/FingerTrap/issues/200
[profile]: https://github.com/psmfd/FingerTrap/issues/201

## Approved merge evidence

- Maintainer approved coordinated merge and protected-branch verification.
- Candidate `8196b4f`: [CI](https://github.com/psmfd/FingerTrap/actions/runs/39530284288)
  and [package](https://github.com/psmfd/FingerTrap/actions/runs/39530284279) passed;
  all 23 distinct checks passed (25 reports including repeated title/gitflow).
- Nine controller tests pass locally and in `repo-check`; wildcard-binding
  mutation fails the real socket-binding assertion. Live gateway-only canaries
  passed before all five registrations. CodeQL #8 is fixed, not dismissed.
- Merge `9bfc092` has the exact tested tree. Independent readback confirms only
  three `dev` contexts changed; `main` and the worker source ref are unchanged.
- Local snapshots and phase/evidence JSON are in `.artifacts/tart-rollout-207/`.
  Source changes are merged and backed up; protected dev validation is complete.
- Protected dev CI `39531687800` and packaging `39531859652` passed. All 22
  reported checks passed; five local job VMs/registrations cleaned up. Artifact
  `12316018036` archive SHA-256
  `ee2ec15073e50ac55173bb075fca4052be904e2ea4ef6382436840b55b7ea1d1`
  and app/DMG digests match; embedded source and runner match the merged build.
- Final handoff changes documentation only. It is saved with CI skipped on
  `docs/runtime-migration-handoff`; include it in the next implementation PR.
  #208 remains open for later main-promotion mapping and scheduling decisions.
