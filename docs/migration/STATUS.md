# Runtime migration: current status

Updated: 2026-10-21 UTC. Resume completed; Pi 1.3.0 candidate qualified.

Current candidate: Pi #83 at `28741c97f9baf5649e8ad3ba1c996d7bbcb5abd2`;
FingerTrap fixture source #209 at `0143c3317db5343d004bbfa1b75fd566b87c1b6c`.
Both are committed, remotely backed up and CI verified: all eight Pi and 23
FingerTrap checks pass. Read [qualification/evidence](evidence/pi-next/README.md)
and the [promotion/rollback record](evidence/pi-next/promotion-rollback.md).
Production release/pin and activation retain their separate approval gates.

**Active slice S3-M1 — review and integrate open PRs.** The maintainer now
explicitly requests review/merge of open PRs to leave a clean continuation
baseline. This authorizes source PR integration and disposition of superseded
drafts; release/tag/pin, SDK and managed-activation gates remain separate.
Inventory: FingerTrap #209, #188 and dependency PRs #199/#198/#197/#195/#190/#165;
Pi #83 plus superseded #81/#82. Review exact diffs, threads, dependency changes,
checks and protection rules; repair or close redundant proposals, merge reviewed
changes in dependency order and verify resulting branches/CI. Preserve private
local evidence. Pi #83 merged at `5f1f1dfbacb508086ed9b68563c78c3aa1456d7c`,
with exactly the qualified source tree; #81/#82 are closed as superseded.
GitHub removed #82's reference branch during integration; the exact saved head
is restored and remotely verified. #209 merged to dev at
`cc3bf8855b0f748c04c44a92093a9a708381f264` after all 23 head checks passed;
all 19 post-merge checks pass. #188 is closed as superseded. Dependency
reconciliation draft #211 is remotely backed up at `2a2c6a0`; all 100 UI tests,
eight native tests and Clippy pass locally. Fixed Vite/brace-expansion versions
replace vulnerable older proposals; combined audit reports zero findings.
Enroll #211 in the existing local Tart routing, then queue its full protected
suite and Apple Silicon packaging. The six dependency originals remain open.

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

**Next action: complete S3-M1 dependency PR integration.** Source #83/#209 are
merged and superseded drafts closed. Prepare the combined dependency PR from
dev cc3bf88, enroll it in the existing local Tart routing, run protected CI,
then merge and close the six incorporated dependency proposals. Verify post-merge
CI and synchronize the main working checkout, preserving private local evidence.
See the [integration review](evidence/pr-merge-20261021/README.md).
Release/pin and managed activation remain separately gated. #210 stays open for
the exact production decision; its first five qualification items are complete.

Known blocker for a clean production dependency recommendation: two high findings
remain in the optional Gondolin example chain, with no published node-forge fix.
Core install-lock exclusion and managed non-discovery are recorded evidence,
not a blanket waiver. Do not promote without the recorded disposition decision.

- Latest stable upstream was rechecked at completion: v1.3.0,
  `71023aec1546d6d677a65669c5665c0927ce0dd0`; fork release remains
  v0.87.1-psmfd.1. Pi branch `sync/upstream-v1.3.0` is clean and remotely
  backed up at `28741c97f9baf5649e8ad3ba1c996d7bbcb5abd2`, draft #83.
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
Copies are ignored/local. Recover the exact remote branch into an isolated clone;
temporary worktree `/private/tmp/ft-pi-latest` is convenient, not authoritative.
Preserve the sibling operator checkout and three private untracked entries.
Changed executable digests require fresh qualification. Reproduction and
mutation scripts are retained with the evidence.

**Prior disposition:** retain Pi #82 and its qualified 0.87.1 artifact as a
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

| ID | Approved result | Status | Evidence / remaining work |
| --- | --- | --- | --- |
| M0 | Ownership ADR, inventory and successors | Committed and backed up | `6a999c9`; ADR-0034 accepted; inventory and #200–#206 exist |
| M1a | Token/cache-meter source package | Verified, backed up, inactive | [Report](m1a-import.md); runtime package CI passed on all three platforms at `6a999c9` |
| S1 | Profile schema, compatibility and baseline | Verified locally and in CI; backed up | `52fc6bc`; [foundation report](profile-foundation.md); complete CI and Apple Silicon package/smoke passed |
| S2 | Reviewed import slices and test registration | Verified locally and in CI; backed up | `92137c4`; [manifest report](import-manifests.md); all platform checks and Apple Silicon package/smoke passed |
| S3 | PTY/RPC launch isolation | Foundation merged; candidate qualified; pin decision pending | Production baseline still lacks managed policy; maintained candidate is qualified in G6; S3 is not complete |
| S3-G | Close the pinned-Pi startup/discovery gap | G1–G6 implemented, qualified and source backed up | [Maintained report](s3-managed-startup-maintained-qualification.html); production pin/release and activation remain gated |
| S3-G6 | Preserve maintained fork fixes and requalify managed startup | Complete; source backed up; CI verified | Pi `1f37eea` / FT `faa4242`; exact host/Tart/protocol/mutations pass; 7 Pi and 23 FT checks pass; dependency disposition required before production promotion |
| Pi-next | Upgrade to latest Pi release and validate | v1.3.0 candidate qualified, backed up, CI verified; promotion pending | Pi #83 `28741c97f` / FT `0143c33`; [evidence/decision record](evidence/pi-next/README.md); #210 stays open for exact release/pin decision |
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
