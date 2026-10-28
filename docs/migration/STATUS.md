# Runtime migration: current status

Updated: 2026-10-28. Paused at the maintainer's request for a machine reboot.

## Reboot handoff — start here

Read the [exact GUI continuation handoff](HANDOFF-client-baseline-gui.md) and
the latest WORK_LOG entry. Branch `test/client-baseline-gui` is based on verified
dev `8e5fd0cb60748ba197c60928e3b0c4895fbb1ba8` (#220: 22 head/19 merged
checks passed). The continuation is a work-in-progress checkpoint, not CI-verified
or accepted. All 23 current local guards and Ruff checks pass. All VMs are
stopped, no disposable clone or QA/CI controller remains, and private inputs
are preserved. The last interrupted launch did not create a new run.

**One resume action:** after rechecking the saved branch/remote state and host
readiness, run the atomic VNC driver, then confirm that its desktop remains
alive and responds to one guest-only Go To Folder chord. The dedicated input
pipe/persistent VNC fix is implemented but has not had this live retest.
Keep all eleven app acceptance rows BLOCKED until actually observed. Pi remains
`v0.85.1-psmfd.1`; #219 comparison stays deferred. New Dependabot #221/#222/#223
were inventoried for the next session, not reviewed or merged during shutdown.

**CF-1C — observable guest GUI, paused for reboot.** The maintainer says "Proceed"
after #220 integration. Reconcile its final 22 head/19 post-merge checks and
saved inputs, then establish observable input/frame capture in one owned clean
Tart guest. Investigate Tart's native VNC console as an alternative to the
timed-out guest AppleEvents path; preserve normal macOS privacy/Gatekeeper
behavior. Name any harness changes and their validation before implementation.
Continue keyless client cases, select and verify a prior QA app artifact, and
record provider-login inputs still needed. Production Pi remains
`v0.85.1-psmfd.1`; #219 comparison and all production activation gates stay separate.

**CF-1A/B — captured with GUI/provider blockers; acceptance open.** The
maintainer's "Proceed" approves the recorded CF-1A–F plan against production
Pi `v0.85.1-psmfd.1` only. Implemented `scripts/qualify-client-baseline.py` and
15 meaningful guard regressions in `scripts/tests/test_qualify_client_baseline.py`;
verified exact package/runtime provenance and inventoried Tart inputs, staged
only owned guests, and preserved sanitized evidence here. Wrong bytes, unsafe
archive paths, unowned cleanup and incomplete acceptance are rejected. Failure
history is retained. Continue from draft #220, preserving private state.
The second run `20261022T175149Z-c22f92748f50` records one input-verification
PASS and eleven BLOCKED cases. Exact production Pi works in the supplementary
packaged-sidecar smoke (shell echo, real Pi output and Pi termination), which
does not pass GUI rows. Finder/UI automation did not establish a usable app
window, supported test login is unavailable, and prior-app upgrade bytes are
not selected. Both owned guests are removed and original VMs preserved.
Read [inputs/results and reproduction](evidence/client-baseline/README.md).
Fifteen harness guards pass, with two red-first mutations retained. Complete
protected PR verification, then arrange an observable guest session and repeat
blocked rows. No CF-2 advance, production pin, SDK adoption, managed activation
or Pi 1.3.0 comparison is authorized by this slice.

Checkpoint `11f92741c0ee6f1779c8028e297301a7e62e81d8` is committed,
secret-scanned and remotely backed up in #220; #217 is independently read back.
Protected source head `f86413f7e7bf31a69e874e9bf88367d7d7b8c56d` passes all
22 checks; its four local macOS CI guests are cleaned. Review has zero unresolved
threads. Read the [source CI snapshot](evidence/client-baseline/source-ci.json).
The subsequent documentation checkpoint must also pass protected checks before
normal merge; final head/merge readback is recorded in #220 and #217. CI is not
baseline acceptance.

**CF-0S — baseline scope decision.** The maintainer selected the current
production runtime baseline only for CF-1/#217: `v0.85.1-psmfd.1`. Pi 1.3.0
comparison is separately tracked in deferred
[#219](https://github.com/psmfd/FingerTrap/issues/219), outside CF-1. The live
Issues #216/#217/#210 are updated and independently read back. Scope checkpoint
`674805a8dc4a26a36edc5adb6d74b266934b01b5` is committed, secret-scanned and
remotely backed up in draft [#220](https://github.com/psmfd/FingerTrap/pull/220).
Focused documentation checks pass; full protected CI and owned-PR Tart enrollment
remain pending the rest of the plan review and are required before merge.
The subsequent CF-1 execution approval is recorded above; this earlier scope
checkpoint alone did not authorize the entire implementation plan.

**CF-0 — client foundation tracking, prepared.** The maintainer selected
client acceptance, complete profile isolation and a supervised JavaScript/Pi SDK
worker as the next campaign, with extension catalog imports/review deferred until
the foundation passes. Umbrella [#216](https://github.com/psmfd/FingerTrap/issues/216)
and packaged-client baseline [#217](https://github.com/psmfd/FingerTrap/issues/217)
are created and independently read back. The
[ordered foundation and item-1 plan](client-foundation-plan.html) and
[baseline evidence contract](evidence/client-baseline/README.md) are prepared.
Current source baseline is `6fe2627d040b385fa96fdd91f7341c39214a112f`, with all
19 post-merge checks passed. Tracking checkpoint `7f75c70` is committed,
secret-scanned and remotely backed up in [#218](https://github.com/psmfd/FingerTrap/pull/218).
It merged at `538ff32df0759c2237ea685997d519df3501dd8b` after all 22 head and
19 post-merge checks passed. The subsequent CF-1 approval is recorded above.
Tracking preparation does not imply
baseline execution, a production SDK switch or runtime activation. No new
extension catalog import/review starts before the foundation exit.

**S3-M3 — final CI readback.** Late Dependabot #214 is reviewed and closed as
superseded by #211. Current source dev is
`b0292a9fb3c38f70abfec534d004c6ccf13dbac4`; all 19 post-merge checks pass.
PR #214 targets older main: all six sidecar/Tauri failures are NuGet audit NU1902
for MessagePack 2.5.302; Gitflow rejects a dependency branch targeting main.
Dev already contains MessagePack 2.5.303 and audited Vite 8.3.3. Main still needs
normal dev-to-main release promotion; closing the duplicate does not remediate
main. No audit/protection is suppressed. Read the
[exact CI snapshot](evidence/pr-merge-20261021/final-ci-readback.json) and
[#214 disposition](https://github.com/psmfd/FingerTrap/pull/214#issuecomment-6328122599).
Final tracking [#215](https://github.com/psmfd/FingerTrap/pull/215) merged at
`6fe2627d040b385fa96fdd91f7341c39214a112f` after all 22 head checks; all 19
post-merge checks pass. The new foundation campaign above is the active sequence.

**S3-M2 — golden handshake ordering follow-up.** #212 merged at `9d8b892`;
its dev run 39683025954 exposed a Linux golden-scenario ordering race (408 pass,
one failure, 12 skips). The parse response may arrive before startup hello, and
the scenario could record EOF first. A one-second delayed-hello regression proves
the failure. The scenario now awaits readiness with its existing event budget;
all 13 focused cases and 410 full-suite tests pass locally (12 opt-in skips).
Only tests/harness change; production code and golden bytes are intact. See the
[red/green evidence](evidence/golden-handshake/README.md). Protected verification
and merge state are recorded in [#213](https://github.com/psmfd/FingerTrap/pull/213).
It merged at `b0292a9fb3c38f70abfec534d004c6ccf13dbac4` after all 23 head
checks passed; all 19 post-merge checks pass. The digest-verified Apple artifact
12385811971 matches the checked source tree and passes packaged shell smoke.

Current candidate: Pi #83 at `28741c97f9baf5649e8ad3ba1c996d7bbcb5abd2`;
FingerTrap fixture source #209 at `0143c3317db5343d004bbfa1b75fd566b87c1b6c`.
Both are committed, remotely backed up and CI verified: all eight Pi and 23
FingerTrap checks pass. Read [qualification/evidence](evidence/pi-next/README.md)
and the [promotion/rollback record](evidence/pi-next/promotion-rollback.md).
Production release/pin and activation retain their separate approval gates.

**S3-M1 source integration complete.** Maintainer authorization covers source
merges and superseded drafts; release/tag/pin, SDK and managed activation retain
their separate gates. Pi #83 merged at `5f1f1dfbacb508086ed9b68563c78c3aa1456d7c`
with exactly the qualified tree. GitHub marks #82 incorporated through that
merge; its exact reference branch is restored. #81 is closed without merging
its older tree. All six Pi post-merge checks pass.

FingerTrap #209 merged at `cc3bf8855b0f748c04c44a92093a9a708381f264`
after 23 head checks; all 19 post-merge checks pass. #188 is superseded by the
accepted migration ADRs. Combined dependency #211 merged at
`260aec5067d37247b240a8641829bc520ae74bf3` after all 23 checks and artifact
verification, incorporating #199/#198/#197/#195/#190/#165, which are now closed.
All 19 checks also pass on the merged dependency commit.
The combined UI audit reports zero findings. Read the
[integration review and evidence](evidence/pr-merge-20261021/README.md).
The original backlog has zero open PRs in both repositories. This subsequent
tracking checkpoint records those results; its own CI is visible in #212.

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

The maintainer subsequently selected the ten-area client foundation sequence in
issue #216: client baseline, shared contracts, completed profiles/credentials, a
supervised JavaScript/Pi SDK worker, lifecycle integration, failure/isolation
proof, complete packaging, final qualification and foundation review. Further
extension catalog import/review is deferred until that exit. The current request
authorized durable trackers and proposed item-1 plan preparation, followed by
discussion before execution. The maintainer subsequently said "Proceed" after
the production-only plan discussion, authorizing CF-1A–F execution. Later
implementation plans and existing production
release/pin, SDK adoption, native authority and managed activation gates remain
explicit; this roadmap does not mark any of them passed.

## Historical patching handoff — 2026-10-21

This historical handoff was resumed and verified on 2026-10-21. Saved branches,
remote PRs and all 426 preserved artifact entries matched. G7 corrected a test
fixture alert. The current resume action below supersedes the old restart action;
the original evidence follows for recovery.

- Repository: `/Users/pdavis/projects/_personal/FingerTrap`, branch
  `feat/managed-pi-startup`. Before this handoff, local and remote head were
  `42aab08fffc0a854f5664f11630bba68721c0e36`; this documentation-only commit
  follows it. Read `git log -1` for the handoff commit rather than resetting
  to the older checkpoint. Tested source remains `faa4242`.
- Pi branch `fix/managed-startup-maintained` is clean and remotely backed
  up at `1f37eea4bf71f95ed9a4e62674949de3a9feb8b0`. The temporary worktree
  `/private/tmp/ft-pi-maintained` is convenient, not the recovery authority.
  If patching removes it, recover the exact remote branch into a fresh
  isolated worktree; preserve the operator's sibling Pi checkout and any
  unrelated edits. Do not reuse the historical `fix/managed-startup-v1`.
- Independent stop-time checks: #209 and Pi #82 remain open drafts at the
  expected heads; #210 is open. GitHub runner registrations: zero. All Tart
  VMs are stopped; no job or qualification clone remains. No build drain or
  standalone qualification needs restarting merely because the session ended.
- Exact runtime, host, maintained comparison artifact, model-data archive
  and metadata were copied out of temporary storage. All 426 copied files
  and symlinks were checked against their sources, including the qualified
  executable hashes. Local preservation directory:
  `.artifacts/s3-discovery/patching-handoff-20261021/`.
  Its `inventory.json` SHA-256 is
  `8ae0a0357b0614696989a675f6b43ff761f80b6eb6bfe01412bd9fa90ff531a5`.
  These binary copies are ignored and local only; source and the 102 retained
  qualification evidence files are backed up through GitHub. Keep `.artifacts`
  during patching. The downloaded product package remains separately in
  `.artifacts/s3-discovery/maintained-package/`.
- Preserved artifact paths under that directory: runtime `candidate/darwin-arm64`,
  published host `host`, released comparison `baseline/pi`. Verify hashes
  before reuse. A rebuilt binary is a new artifact if its digest differs;
  previous exact-artifact results do not automatically qualify it.
- Preserve `.local-testing/`, `LEGACY_pi_config_3579dc1.patch`, and
  `NEXT_SESSION_pi-fold-reassessment.md`; they are unrelated untracked
  private state and have not been added to this handoff.

On restart, read this file and the latest WORK_LOG entries, inspect local
changes, fetch/read the companion PR heads with the repository-authorized
`TheSemicolon` identity without changing the global active account, and
reconcile any intervening changes before editing. Temporary helper scripts
under `/private/tmp` are not required recovery inputs. Recheck toolchain and
Tart readiness after patching before new builds; retain local macOS execution
and the existing hosted Windows/Linux lanes. Do not rerun completed tests
without a changed input or an outstanding concern.

Suggested restart text:

> Resume FingerTrap's Pi migration from the patching handoff in
> docs/migration/STATUS.md and the latest WORK_LOG.md entries. Verify saved
> branches and remote PR state, then continue the recorded next action within
> existing approvals. Once this campaign is settled, proceed with the latest-Pi
> upgrade and validation tracked in #210. Keep status and evidence durable.

## Resume here

**Next action: resume the saved native-VNC live-input retest after reboot.** Read
[the reboot handoff](HANDOFF-client-baseline-gui.md),
[the implementation proposal](client-foundation-plan.html#cf1-plan),
[the worksheet](evidence/client-baseline/README.md) and #217. The runtime scope
is settled: production `v0.85.1-psmfd.1` only; comparison belongs to #219.
The maintainer approved execution after the production-only scope decision.
Inputs and harness/source CI checks pass; GUI/provider acceptance remains
blocked. Verify the final #220 documentation/merge readback, then arrange an
observable interactive guest session, supported test login and exact previous
app before rerunning blocked cases. Use the worksheet's reproduction commands.
CF-2 through CF-10 remain planned; #217 stays open until maintainer acceptance.

The prior Pi source review and exact-artifact qualification are complete. #210
remains open in parallel for the separate runtime release/pin decision: review
the [promotion/rollback record](evidence/pi-next/promotion-rollback.md), resolve
the remaining Gondolin/node-forge example dependency disposition, then approve
the exact release/tag/pin transaction before publication or install.
Production remains v0.85.1-psmfd.1, with managed selection disabled. #210's first
five qualification items are complete; its production verification item stays
open. SDK/backend adoption, GUI/keychain acceptance and managed activation retain
ADR-0034's separate gates. No qualification test is pending for the named bytes.

Known blocker for a clean production dependency recommendation: two high findings
remain in the optional Gondolin example chain, with no published node-forge fix.
Core install-lock exclusion and managed non-discovery are recorded evidence,
not a blanket waiver. Do not promote without the recorded disposition decision.

- Latest stable upstream was rechecked at completion: v1.3.0,
  `71023aec1546d6d677a65669c5665c0927ce0dd0`; fork release remains
  v0.87.1-psmfd.1. Pi source `28741c97f9baf5649e8ad3ba1c996d7bbcb5abd2`
  is integrated by #83 into protected main `5f1f1dfbacb508086ed9b68563c78c3aa1456d7c`.
- Qualified ARM64 executable SHA-256:
  `901a10f67a06f04310f4d064e95f7e3e60ec523d253366731e077a239237407f`.
  Direct 18-case, published-host two-profile RPC/PTY and offline Tart matrices
  pass, including MCP canaries/rejection. Four runtime regressions and the
  host policy mutation are caught; restored source/builds pass.
- Node 22.23.0 / Bun 1.3.14 and integrity-verified npm pi-ai 1.3.0 data:
  static/full hermetic tests and actual release build pass. Source-release/npm
  model differences are retained. Hosted Linux/Bubblewrap passes.
- All 12 RPC scenarios record twice for baseline and candidate. New durationMs
  volatility failed the first recorder pass; a red-first narrow normalizer
  correction resolves it. Outbound commands/process controls match exactly;
  upstream inbound additions are reviewed. Raw pairs/drift remain; production
  0.85.1 goldens are unchanged.
- Actual C accounting is 6 patches / 23 paths / 2,021 lines (020: 682), within
  unchanged 7 / 25 / 2,200 limits. Manifest/text/both guards agree, with no
  unexplained upstream residual. Four real CodeQL findings are fixed; four
  false positives have specific proof, rationale and verified dismissal.
- FingerTrap `0143c33` passes all 23 exact-source checks. Five macOS jobs ran
  locally on Tart; Windows/Linux remain hosted. Apple artifact 12376529013
  verifies download/app/DMG digests and ARM64 binaries. Merge `5b88cdff` has
  parents `9bfc092` / `0143c33` and identical source tree. Packaged shell smoke
  passes; product Pi smoke explicitly skips because Pi is not installed.
- All five disposable CI VMs and registrations are removed. Independent
  readback: zero GitHub runners; all remaining Tart VMs stopped; base preserved.
- Source/evidence checkpoint `0143c33` is committed and remotely backed up.
  Final qualification/decision checkpoint `f8225ebc125aede6cc226fdf8bf74222ac550107`
  is secret-scanned and remotely backed up. Pi #82/#83, FingerTrap #209 and
  #210 descriptions were updated and independently read back. This subsequent
  tracking-readback checkpoint does not inherit fresh source CI.

**Recovery:** keep `.artifacts/pi-next/preserved-20261021/` across patching.
All 215 copied files match sources: runtime, host, npm model archive, upstream
source archive and Bun archive. Inventory SHA-256:
`67587ddb8e4d1d4d5755b4edc1aa0fe12dc76141fae1e2e83b6f1c6c52096ef6`.
Copies are ignored/local. Recover merged Pi commit `5f1f1df` into an isolated clone;
temporary worktree `/private/tmp/ft-pi-latest` is convenient, not authoritative.
Preserve the sibling operator checkout and three private untracked entries.
Changed executable digests require fresh qualification. Reproduction and
mutation scripts are retained with the evidence.

**Historical disposition, superseded by S3-M1:** retain Pi #82 and its qualified 0.87.1 artifact as a
reference draft, defer its production promotion, and use #83 for latest-release
review. G7 `95e95f0` is backed up in #82; all seven checks pass and alert 87 is
fixed. Runtime source remains identical to qualified `1f37eea`; its binary keeps
that source identity. The maintained report/102 evidence files remain historical.
Pi #81 / `39b729b` is the old-base experiment and must not overwrite maintained
main. S3, #200, #201 and #202 remain open.

Before future source pushes, run `python3 scripts/ci/tart-runner.py check`.
After jobs queue, run `python3 scripts/ci/tart-runner.py drain --max-jobs 5`;
use four for CI without packaging. Do not run the controller concurrently with
standalone Tart qualification. Repeat completed checks only for changed inputs
or an outstanding concern. See the [runner runbook](../local-github-runner.md).

## Work board

| ID       | Approved result                                                            | Status                                                         | Evidence / remaining work                                                                                                                                                 |
| -------- | -------------------------------------------------------------------------- | -------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| M0       | Ownership ADR, inventory and successors                                    | Committed and backed up                                        | `6a999c9`; ADR-0034 accepted; inventory and #200–#206 exist                                                                                                               |
| CF-0     | Durable client foundation tracking                                         | Prepared; checkpoint/verification recorded below               | #216 umbrella, #217 baseline, [plan](client-foundation-plan.html); tracking only                                                                                          |
| CF-1     | Packaged-client baseline                                                   | Captured with blockers; not accepted                           | [Approved plan](client-foundation-plan.html#cf1-plan), [worksheet](evidence/client-baseline/README.md); actual GUI/provider acceptance remains open                       |
| CF-2–10  | Contracts, isolated profiles, JS worker and final foundation qualification | Planned                                                        | #201/#202/#186/#187 retain implementation ownership; SDK go/no-go and foundation exit precede more catalog import/review                                                  |
| M1a      | Token/cache-meter source package                                           | Verified, backed up, inactive                                  | [Report](m1a-import.md); runtime package CI passed on all three platforms at `6a999c9`                                                                                    |
| S1       | Profile schema, compatibility and baseline                                 | Verified locally and in CI; backed up                          | `52fc6bc`; [foundation report](profile-foundation.md); complete CI and Apple Silicon package/smoke passed                                                                 |
| S2       | Reviewed import slices and test registration                               | Verified locally and in CI; backed up                          | `92137c4`; [manifest report](import-manifests.md); all platform checks and Apple Silicon package/smoke passed                                                             |
| S3       | PTY/RPC launch isolation                                                   | Foundation merged; candidate qualified; pin decision pending   | Production baseline still lacks managed policy; maintained candidate is qualified in G6; S3 is not complete                                                               |
| S3-G     | Close the pinned-Pi startup/discovery gap                                  | G1–G6 implemented, qualified and source backed up              | [Maintained report](s3-managed-startup-maintained-qualification.html); production pin/release and activation remain gated                                                 |
| S3-G6    | Preserve maintained fork fixes and requalify managed startup               | Complete; source backed up; CI verified                        | Pi `1f37eea` / FT `faa4242`; exact host/Tart/protocol/mutations pass; 7 Pi and 23 FT checks pass; dependency disposition required before production promotion             |
| Pi-next  | Upgrade to latest Pi release and validate                                  | v1.3.0 qualified and source merged; promotion pending          | Pi main `5f1f1df` / FT #209 `cc3bf88`; [evidence/decision record](evidence/pi-next/README.md); #210 stays open for release/pin decision                                   |
| S3-M1    | Review/integrate open source PRs                                           | Original backlog settled; source checks and artifacts verified | #83/#209/#211 merged; alternatives/dependency originals reconciled; [review](evidence/pr-merge-20261021/README.md)                                                        |
| CI-local | Reduce hosted builds with a local runner                                   | Merged and verified on protected dev                           | `9bfc092`; 22 post-merge checks passed, five macOS jobs local; artifact/cleanup verified; dev protection mapped; main mapping deferred to promotion; Windows/Linux hosted |
| S4       | Stores, resume and isolated meters                                         | Pending                                                        | Inject profile roots; preserve ADR-0028 and recovery behavior                                                                                                             |
| S5-A     | State/observability import wave                                            | Pending                                                        | Shared closure, prefill dependency, artifact handoff                                                                                                                      |
| S5-B     | Guards and typed tools                                                     | Pending                                                        | Enforcement tests, identity, tool ownership, vendor notices                                                                                                               |
| S5-C     | Worktree/subagent dispatch                                                 | Pending                                                        | PR #1170 fix, #280 reconciliation, child isolation                                                                                                                        |
| S5-D     | Context/presentation                                                       | Pending                                                        | Preserve tuning; gate unsupported RPC UI                                                                                                                                  |
| S5-E     | Capability-dependent source                                                | Pending                                                        | Preserve reviewed contracts/tests; #203/#204 activation gates                                                                                                             |
| S5-F     | Curated defaults                                                           | Pending                                                        | Explicit selection, rights review, restrictions and evaluations                                                                                                           |
| S6       | Integration qualification and issue reconciliation                         | Pending                                                        | Real pinned runtime, independent profiles, native/process evidence, hosted CI                                                                                             |

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

## Historical foundation checkpoints / validation state

- S3-G proposal: [HTML review document](s3-discovery-resolution-plan.html),
  41 links and 13 unique IDs checked; script syntax, 390-pixel mobile layout,
  desktop layout and section navigation passed. No remote assets. Markdown,
  structural and whitespace checks passed. These are document checks only;
  implementation approved 2026-10-21; exact-pin promotion remains gated.
- Merged migration: `docs/pi-runtime-migration`, PR #207, squash `9bfc092` on dev.
- Historical handoff branch: `docs/runtime-migration-handoff`, based on `9bfc092`.
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

- Authorized tests are limited to owned repositories, synthetic data, loopback
  fixtures and disposable environments. The maintainer clarified this scope after
  reporting additional platform safety checks. Keep defensive proof minimal;
  retain exact rejection context if a block occurs. Do not bypass safeguards.

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

## Approved foundation merge evidence

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
