# S3-M1: source PR integration review

Maintainer authorization: 2026-10-21 request to review/merge open PRs and leave
a clean continuation baseline. Source integration is authorized; release/tag,
production pin, SDK adoption and managed activation remain separately gated.

## Initial inventory and intended disposition

| Repository / PR | Reviewed head                                                 | Disposition / validation                                                                                                  |
| --------------- | ------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------- |
| Pi #83          | `28741c97f9baf5649e8ad3ba1c996d7bbcb5abd2`                    | Merge after thread disposition; eight exact-head checks pass; qualified binary unchanged                                  |
| Pi #82          | `95e95f0eca5dd6638f4e8c308932134a6d071798`                    | Close as superseded after #83 merges; preserve reference branch/artifact                                                  |
| Pi #81          | `b0a151df397d88db1cb3f52b7e8a526ee22deb1e`                    | Close old-base experiment as superseded; never merge its old tree                                                         |
| FingerTrap #209 | `a9693e40525916d0d177d0b8ed21ab31c1837984` before this review | Fresh protected CI for review checkpoint, then squash to dev                                                              |
| FingerTrap #188 | `f44f5910de6c7281404d2226ef0fe9a36ccf0153`                    | Close stale proposal: ADR-0034 onward and #200/#201/#210 supersede its open architecture questions; #186/#187 remain open |
| FingerTrap #199 | `1e018dcff0fc0f64f3e10ffe23b1b0fc68bf0aba`                    | Review source-map-js 1.2.2 in combined lock; do not merge feature dependency directly into main                           |
| FingerTrap #198 | `b952b65011d8228ddb3d9fce995c486c2208b61c`                    | Review eight Cargo updates and combined native checks                                                                     |
| FingerTrap #197 | `650e2e7344e5446de8738921b7b52b0d6ee1ac21`                    | Review immutable semantic-release-action 8.1.2 pin; workflow remains gated on successful main CI                          |
| FingerTrap #195 | `1e89b52bb6f228075d08a91c9ed520b5d4595e45`                    | Review six UI updates in combined lock and UI suite                                                                       |
| FingerTrap #190 | `f42ae760e0c3fd2a902d6df25fcdb2e46010f3b1`                    | Review Vitest 5 migration against actual tests                                                                            |
| FingerTrap #165 | `09aa27edf2fa5f7dcd8d6ee39a93e804507eee5f`                    | Review jsdom 30 migration and Node minimum before accepting                                                               |

The six dependency proposals overlap in locks and predate the current CI routing.
Integrate their reviewed intent in one reconciliation PR against current dev,
test the resulting graph, then close originals only when their changes are
included in the merged result. This avoids stale lock graphs and preserves one
reviewable combined dependency state. Do not close an unimplemented update as
completed. macOS CI remains local Tart; Linux/Windows remain hosted.

## Pi #83 remaining review comments

All three files below are byte-identical to upstream v1.3.0
`71023aec1546d6d677a65669c5665c0927ce0dd0`. These are reviewed quality comments,
not undisposed security findings. No new source patch or cap exception is needed.

- `PRRT_kwDOS34l5M6u4yPu` and `PRRT_kwDOS34l5M6u4yP-`,
  `packages/ai/test/model-types.test.ts`: the first model assignments deliberately
  infer each getter's return type. Reassigning a different model ID is the stated
  compile-time regression check. Replacing inference with a broad ReturnType
  declaration would weaken the check. Retain both assignments.
- `PRRT_kwDOS34l5M6u4yQE`,
  `packages/chord/test/delta-benchmark/benchmark.worker.ts`: clearing
  borrowedListeners before GC releases references before releasedRetainedMiB
  is sampled. The assignment has a memory-measurement purpose even though the
  variable is not read again. Retain it.
- `PRRT_kwDOS34l5M6u7OZD`,
  `packages/durable/test/examples/24-child-tasks.ts`: the reopened harness calls
  root before waiting for resumed work. The returned variable is unused, but
  the awaited call can initialize state. This example-only assignment is a
  nonblocking quality nit; preserve upstream behavior and source identity.

Security patches 021–024 and retirements 016/017/019 retain the detailed
[qualification record](../pi-next/README.md). Two optional Gondolin/node-forge
high findings remain a production dependency disposition gate. Merging source
does not waive those findings or publish a runtime. Pi releases require an
explicit workflow dispatch; no release dispatch is part of S3-M1.

## Results

Pi #83 merged with a merge commit at
`5f1f1dfbacb508086ed9b68563c78c3aa1456d7c`; both parents and qualified tree
`46fd5cc4faf0a5c7a2e053cea52359a09ad5ceb2` are verified. All six post-merge
checks pass. Pi #81/#82 are closed as superseded. GitHub removed #82's branch
during integration; the exact `95e95f0` reference was restored and verified.

FingerTrap #209 merged by protected squash at
`cc3bf8855b0f748c04c44a92093a9a708381f264` after all 23 checks passed at
`d1891e7c3a8f76c0bee89ccd54f58363b78dba67`. Merged tree equals checked head.
All 19 post-merge checks pass. #188 is closed as superseded; its implementation
issues remain open. [Source check snapshot](source-checks.json) retains URLs.

## Combined dependency review

The reconciliation branch starts at merged dev cc3bf88. Cargo.lock applies
the eight updates proposed in #198 without modifying dependency declarations.
The UI retains the proposed versions: Tauri API 2.12.0, vscode-jsonrpc 9.0.3,
ESLint 10.11.0, Prettier 3.9.9, typescript-eslint 8.71.0, Vitest 5.0.0 and
jsdom 30.0.1. The combined lock also includes source-map-js 1.2.2 from #199,
without reverting to main's old TypeScript/ESLint versions or peer graph.

The first audit found nine advisories (three high) in the stale proposed graph.
Vite is raised from proposed 8.3.1 to fixed 8.3.3, and transitive
brace-expansion from 5.0.9 to 5.0.12. These stay within existing major versions.
The [before](ui-audit-before.json) and [after](ui-audit-after.json) records show
the advisory IDs, patched ranges and final zero findings. No security waiver or
new fork patch is used.

The [Vitest 5 migration guide](https://vitest.dev/guide/migration/) requires
Node >=22.12 and Vite >=6.4; top-level mocks and existing per-test setup work
without configuration changes. Its automatic mock clearing introduces no test
failures. [jsdom 30](https://github.com/jsdom/jsdom/releases/tag/v30.0.0) raises
Node to `^22.22.2 || ^24.15.0 || >=26.0.0`; package engines, contributor guidance
and setup validation now match. The registry archive contains generated runtime
files; installation with lifecycle scripts disabled and all UI tests pass.

The semantic-release action stays immutably pinned, now to v8.1.2 commit
`03595b5eaf44910cf239ff20904cc52d410f97d4`. Its action.yml uses Node 24; the
[v8 breaking change](https://github.com/cycjimmy/semantic-release-action/releases/tag/v8.0.0)
requires semantic-release >=20. This repository has no older semantic_version
override; the action ships semantic-release ^25.0.9. Existing extra_plugins and
release configuration remain compatible. Release still requires successful
main CI, so merging to dev does not publish. Actionlint passes; a real release
will be checked during the separate main-promotion transaction.

Local validation: all 100 UI tests (12 files) pass on Node 22.23.0; TypeScript
and Vite production build, ESLint, eight native tests, repository structural
check, actionlint, shellcheck and whitespace checks pass. The first native run
lacked the isolated checkout's required sidecar binary; staging existing local
fixtures resolves that prerequisite. CI will stage fresh source-built binaries
and validate all three platforms and Apple Silicon packaging. Canvas capability
messages and the existing large-chunk warning remain informational.

Dependency reconciliation #211 merged at
`260aec5067d37247b240a8641829bc520ae74bf3` after all 23 checks pass at
`42bb1b2a210acf30e17de1744257a60988398ecc`. Merged tree equals reviewed tree
`1dfecf4beec8fabb5df68a172690938cba00d14a`. The six original proposals are
closed as incorporated. [Check URLs](dependency-head-checks.json) and
[settled PR states](settled-prs.json) retain the exact identities. GitHub marks
Pi #82 merged through #83's same merge commit; no separate old-tree merge ran.
After these dispositions, both repositories have zero open source-backlog PRs.
All 19 [post-merge checks](dependency-merge-checks.json) pass on 260aec5; its four
disposable macOS VMs and registrations are cleaned. Final tracking PR #212
records these results and is enrolled in the same restricted Tart policy.

Apple artifact **12382323858**, run **39677894315**, verifies GitHub's archive
SHA-256 `452d2f93ff10ad5bf677df54581192a7a371cf72cf4fa42079da92615fe57f6e`,
app/DMG manifest digests and all three ARM64 binaries. Build merge parents are
cc3bf88 / 42bb1b2, and its tree equals the checked head. Retain the
[artifact review](apple-artifact-review.json) and
[packaged shell smoke](packaged-sidecar-smoke.txt). Pi spawn explicitly skips;
the product does not install Pi. No GUI/keychain acceptance is claimed.

The final tracking checkpoint refreshes source-integration wording, the Node
setup summary and the qualification metadata index. Production/test code and
qualified Pi bytes are unchanged. Release/tag/pin and managed activation remain
gated by the [promotion record](../pi-next/promotion-rollback.md).

## Final CI diagnosis

The documentation merge #212 exposed a Linux golden replay ordering race.
Test-only follow-up #213 has a delayed-hello red/green regression and preserves
production code and golden bytes. It passes all 23 distinct head checks and
merges at `b0292a9fb3c38f70abfec534d004c6ccf13dbac4`; all 19 dev checks pass.
See [golden evidence](../golden-handshake/README.md) and the
[exact final check snapshot](final-ci-readback.json).

Late Dependabot #214 proposes Vite 8.1.6 against older main. Both changed files
are reviewed; #211's audited Vite 8.3.3 supersedes that intent. Gitflow correctly
rejects its main base. All six sidecar/Tauri failures on macOS, Windows and Linux
are NU1902 for main's MessagePack 2.5.302 (GHSA-qhrr-8q5h-9q3h); dev explicitly
selects 2.5.303 since #207 and passes those six jobs. Exact error excerpts and
job URLs are in the snapshot. The proposal is
[closed as superseded](https://github.com/psmfd/FingerTrap/pull/214#issuecomment-6328122599).
Main remains older until normal dev-to-main release promotion. This closure
does not remediate main, suppress NuGet audit or dismiss a security alert.
