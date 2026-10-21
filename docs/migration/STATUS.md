# Runtime migration: current status

Updated: 2026-10-21 UTC, maintained-base qualification and exact-source CI complete.

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
