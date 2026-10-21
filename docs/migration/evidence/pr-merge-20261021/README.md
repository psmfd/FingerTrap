# S3-M1: source PR integration review

Maintainer authorization: 2026-10-21 request to review/merge open PRs and leave
a clean continuation baseline. Source integration is authorized; release/tag,
production pin, SDK adoption and managed activation remain separately gated.

## Inventory and intended disposition

| Repository / PR | Reviewed head | Disposition / validation |
| --- | --- | --- |
| Pi #83 | `28741c97f9baf5649e8ad3ba1c996d7bbcb5abd2` | Merge after thread disposition; eight exact-head checks pass; qualified binary unchanged |
| Pi #82 | `95e95f0eca5dd6638f4e8c308932134a6d071798` | Close as superseded after #83 merges; preserve reference branch/artifact |
| Pi #81 | `b0a151df397d88db1cb3f52b7e8a526ee22deb1e` | Close old-base experiment as superseded; never merge its old tree |
| FingerTrap #209 | `a9693e40525916d0d177d0b8ed21ab31c1837984` before this review | Fresh protected CI for review checkpoint, then squash to dev |
| FingerTrap #188 | `f44f5910de6c7281404d2226ef0fe9a36ccf0153` | Close stale proposal: ADR-0034 onward and #200/#201/#210 supersede its open architecture questions; #186/#187 remain open |
| FingerTrap #199 | `1e018dcff0fc0f64f3e10ffe23b1b0fc68bf0aba` | Review source-map-js 1.2.2 in combined lock; do not merge feature dependency directly into main |
| FingerTrap #198 | `b952b65011d8228ddb3d9fce995c486c2208b61c` | Review eight Cargo updates and combined native checks |
| FingerTrap #197 | `650e2e7344e5446de8738921b7b52b0d6ee1ac21` | Review immutable semantic-release-action 8.1.2 pin; workflow remains gated on successful main CI |
| FingerTrap #195 | `1e89b52bb6f228075d08a91c9ed520b5d4595e45` | Review six UI updates in combined lock and UI suite |
| FingerTrap #190 | `f42ae760e0c3fd2a902d6df25fcdb2e46010f3b1` | Review Vitest 5 migration against actual tests |
| FingerTrap #165 | `09aa27edf2fa5f7dcd8d6ee39a93e804507eee5f` | Review jsdom 30 migration and Node minimum before accepting |

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

Review and integration are in progress. No merge outcome is claimed yet.
