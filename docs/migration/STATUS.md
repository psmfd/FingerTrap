# Runtime migration: current status

Updated: 2026-10-20, after the S3 package retry and local-runner request.

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

## Resume here

**Current activity: correct the local pilot's Bash prerequisite; S3 checks passed.**

1. Follow the [approved S3 plan](s3-launch-isolation-plan.html).
2. Read [S3 discovery audit](s3-discovery-audit.md): project migrations precede
   trust in the pin, blocking arbitrary-project activation. ADR-0036 records
   the controlled-fixture restriction and launch contract.
3. Read the [S3 implementation report](s3-launch-foundation.md). Source
   `95424b3` is committed/pushed. Hosted sidecar/runtime/UI/Tauri passed on all
   three platforms. Rust analysis passed. The unchanged-source Apple Silicon
   retry passed packaging, packaged-sidecar smoke and artifact upload
   ([job](https://github.com/psmfd/FingerTrap/actions/runs/39469041145/job/118790720905)).
4. The maintainer requested local GitHub Actions capacity, potentially Tart,
   to reduce hosted builds. Inspect and prepare an isolated macOS runner first;
   macOS-first pilot is the working scope. See the
   [runner runbook](../local-github-runner.md). No new hosted builds during
   setup. Pilot source `a6bdb88` is committed/pushed. Group 5 is restricted
   and network isolation passed. Local
   [job 118825790114](https://github.com/psmfd/FingerTrap/actions/runs/39478496007/job/118825790114)
   failed before application tests because system Bash lacks `mapfile`.
   Cleanup passed. Homebrew Bash 5.3.15 is now verified in the stopped template.
   Publish the explicit-Bash workflow correction and rerun before CI cutover.

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
| S3 | PTY/RPC launch isolation | Foundation backed up and CI verified; discovery blocked | `95424b3`; [report](s3-launch-foundation.md); package retry passed; exact-pin project migrations prevent full qualification |
| CI-local | Reduce hosted builds with a local runner | Isolation/lifecycle verified; Bash prerequisite repair active | `a6bdb88`; group 5; local job 118825790114 failed before tests; clone/runner removed; routine routing unchanged |
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

- Migration branch: `docs/pi-runtime-migration`; draft PR #207.
- Active infrastructure pilot branch: `ci/tart-runner-pilot`, based on the
  S3 source checkpoint. This keeps pilot pushes outside the PR's hosted matrix.
  The pilot branch also preserves the latest S3 handoff.
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
- Draft review: [PR #207](https://github.com/psmfd/FingerTrap/pull/207),
  targeting `dev`; no merge or activation.
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

## Commands for the next session

```sh
git status --short --branch
git log -5 --oneline
gh pr checks 207 --repo psmfd/FingerTrap
pnpm --dir packages/agent-runtime check:provenance
pnpm --dir packages/agent-runtime test
```

Use the repository-authorized GitHub identity when checking a private source
or writing this branch; do not change the operator's global active account.
For sidecar changes, the reliable local path is audited locked restore, then
`dotnet build src-sidecar -c Release --no-restore -m:1 -p:UseSharedCompilation=false`
and `dotnet test src-sidecar -c Release --no-build --no-restore`.
Native/process checks may need the tool's filesystem/process sandbox approval.

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
