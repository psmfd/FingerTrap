# Runtime migration: current status

Updated: 2026-10-21 UTC, resumed after system patching; companion review in progress.

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

## Patching handoff — 2026-10-21

**Paused at the maintainer's request for system patching.** No implementation
or qualification command is awaiting completion. Resume the next action below
when the maintainer starts the next session; existing approvals persist.

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

**Active slice S3-G7: companion disposition and review correction.** Local and
fetched FingerTrap head matches handoff `7f99454`; remote #209/#82 remain open
drafts at `7f99454` / `1f37eea`, with unchanged bases. Recovered Pi into isolated
clone `/private/tmp/ft-pi-resume` after patching removed the old worktree.
Do not use the sibling operator checkout. Tart readiness check passes and all
VMs are stopped. Default Node is now 24.17.0; recover the reviewed Node 22/Bun
toolchain before Pi checks. No completed artifact tests need repeating yet.

Review found unresolved Pi CodeQL alert 87 in test fixture code construction
at `managed-startup-policy.test.ts:108`; successful CI reports did not establish
zero security findings. Replace generated path interpolation with a static
module-relative fixture, run affected tests and full `npm run check`, recount
patch 020, checkpoint and verify remote analysis. Production promotion stays
deferred for dependency disposition. Keep both qualified drafts as integration
candidates until their separately gated merge/pin decisions. Once this review
correction is settled, proceed with #210 in an isolated candidate branch.

Pi-next discovery readback: upstream latest stable is `v1.3.0`, published
2026-10-15; fork latest remains `v0.87.1-psmfd.1`. Resolve exact tag commits,
review release/security changes and reconcile every patch before porting.
Recount against the new upstream base and stop on an ADR-0039 ceiling breach.
Next validation: fixture regression, static checks, patch accounting and
CodeQL readback; then latest-release source/provenance review.

G7 correction `95e95f0eca5dd6638f4e8c308932134a6d071798` is committed and
pushed to Pi #82. All 23 policy tests and full `npm run check` pass on Node
22.23.0 after hydrating 42 JSON files from the preserved, digest-verified
0.87.1 model-data archive. Initial unhydrated checks failed on missing data;
they are not policy regressions. Production runtime source is identical to
`1f37eea`; the previously qualified binary retains that original source identity.
Accounting is unchanged at 7 / 25 / 2,090. Remote allowlist and secret checks
pass; CodeQL is running. No completed artifact qualification was repeated.

**Pi-next N1: latest-release reconciliation and isolated port preview.**
Selected upstream `v1.3.0` resolves to
`71023aec1546d6d677a65669c5665c0927ce0dd0`. Its source is in independent
worktree `/private/tmp/ft-pi-latest`, branch `feat/fingertrap-pi-1.3`.
This is a preparation candidate; do not infer fork integration from it.
Retain G6 as a qualified reference, defer its production promotion for the
recorded dependencies, and carry the managed policy into this next campaign.
N1 validation: compare against verified v0.87.1 content baseline (the fork's
squash ancestry is stale), reconcile every live patch, preserve new upstream
behavior, record every conflict and recount before any candidate build.
Never restore upstream-deleted implementations solely to preserve old patches.
Patch retirement requires equivalent-behavior or removed-reachability evidence.
No ceiling increase, merge, release or activation is authorized by this preview.

**S3-G6 implementation and qualification are complete; production promotion
remains unapproved.** Read the
[maintained-base report](s3-managed-startup-maintained-qualification.html).

Next action: review the qualified companion drafts
[Pi #82](https://github.com/psmfd/pi/pull/82) and
[FingerTrap #209](https://github.com/psmfd/FingerTrap/pull/209), settle this
campaign's integration/promotion disposition, then execute the requested
[latest-Pi upgrade and validation #210](https://github.com/psmfd/FingerTrap/issues/210).
Resolve the recorded dependency findings before a production pin: the unchanged
baseline has 15 npm findings, including vulnerable brace-expansion 5.0.9 in
the runtime shrinkwrap. No merge, release, installed pin or activation changed.

- Pi `1f37eea4bf71f95ed9a4e62674949de3a9feb8b0` is committed/pushed in #82,
  based on maintained `42429b9`. Isolated worktree `/private/tmp/ft-pi-maintained`,
  branch `fix/managed-startup-maintained`; all seven Pi check reports pass.
- FingerTrap `faa4242f1add364191078d9bc12f4aaf154983e6` is committed/pushed
  in #209. All 23 exact-source checks pass, including full platform CI and
  Apple Silicon packaging. Five macOS jobs used local Tart; independent
  listings confirm zero runner registrations/clones and a stopped base.
- Qualified Pi binary SHA-256:
  `9a9f53131cec748801b8c176190ef4dc77e7b6e833859ff734fcfa3d297c7614`.
  Exact direct, two-profile published host and clean Tart matrices pass.
  Four Pi mutations and the host policy mutation are caught and restored.
- Twelve scenarios record twice per artifact and match the attested 0.87.1
  release exactly after documented volatile-value normalization. Both raw
  recordings are retained; current production-pin goldens remain unchanged.
  The report separately records 0.85.1-to-0.87.1 system-message/version drift.
- ADR-0039 bounds patch 020: actual 7 patches / 25 paths / 2,090 changed
  lines; maximum 7 / 25 / 2,200. Both guard sets and text allowlist agree;
  guard logic and existing fixes are preserved.
- Package artifact `12354825465` and app/DMG digests verify. Its PR merge tree
  equals `faa4242`; sidecar/native-shell smoke passes. Product Pi smoke skips
  because Pi is not bundled; managed-runtime evidence is the separate matrix.
- Evidence/report `0cf4c98188204489a9e45c21e4895f9cc1f1090b` and retained
  command logs `7d081d01603761fc1f057c46d31a61aab1476b11` are committed and
  remotely backed up. All 102 evidence files are tracked; scoped secret scans
  pass. Companion drafts and #210 descriptions were updated and independently
  read back. This final recovery note follows those observed checkpoints.
  Documentation commits skip CI; source CI belongs to `faa4242`.

The [G5 report](s3-managed-startup-qualification.html), Pi #81 and its
`39b729b` source remain historical evidence. Do not merge that old-base branch
over maintained main. S3 as a whole, #200, #201 and #202 remain open.

Before future source pushes, run `python3 scripts/ci/tart-runner.py check`.
After jobs queue, run `python3 scripts/ci/tart-runner.py drain --max-jobs 5`;
use four for CI without packaging. Do not run that controller concurrently
with standalone Tart qualification. Only enrolled PRs use this local route;
see the [runbook](../local-github-runner.md). No managed launch is enabled.

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
