# Pi 1.3.0 promotion decision record

Status: **qualified unpublished candidate; promotion decision pending**.
No merge, tag, published runtime, production pin, SDK backend or managed selector
has changed. This record completes preparation for #210's separate decision;
it does not mark that issue's promotion/verification checkbox complete.

## Review identities

| Item | Identity |
| --- | --- |
| Upstream | v1.3.0 / 71023aec1546d6d677a65669c5665c0927ce0dd0 |
| Pi draft | #83 / 28741c97f9baf5649e8ad3ba1c996d7bbcb5abd2 |
| Pi source tree | 46fd5cc4faf0a5c7a2e053cea52359a09ad5ceb2 |
| Qualified local ARM64 binary | 901a10f67a06f04310f4d064e95f7e3e60ec523d253366731e077a239237407f |
| FingerTrap source | #209 / 0143c3317db5343d004bbfa1b75fd566b87c1b6c |
| Published qualification host | bb5f02fd64b41310a7f35aec92a60b67bdd8bad8 / executable 80beb8542052fe1246528d2a9a0617e2b3760c81c39163f228b0c128d97af3da |
| Recovery inventory | 215 files / 67587ddb8e4d1d4d5755b4edc1aa0fe12dc76141fae1e2e83b6f1c6c52096ef6 |
| Production selection | Existing v0.85.1-psmfd.1; managed selection disabled |

Evidence: [qualification](README.md), [patch accounting](patch-accounting.json),
[Pi checks](pi-fixed-checks.json), [FingerTrap checks](fingertrap-source-checks.json),
[dependency audit](audit-final.json), [protocol review](protocol-review.json).

## Decisions still required

1. Review Pi #83's retained capabilities, proposed retirements 016/017/019 and
   security patches 021–024. Determine private upstream reporting for new real
   findings; no report has been sent. Keep manifest/guard data aligned at merge.
2. Resolve the remaining Gondolin/node-forge example findings or explicitly
   approve a bounded production disposition after assessing the full release
   closure. There is no published node-forge fix. Core install-lock exclusion
   and managed non-discovery are evidence of limited reachability, not a waiver.
3. Approve an exact fork merge/release and production pin transaction. Name the
   eventual release commit, tag and artifact digests in that approval. The
   unpublished binary above is not a released production pin.

## Execution after approval

Merge through the fork's reviewed import procedure; verify the resulting source
tree and pending patch/retirement metadata against this reviewed tree. A changed
production source tree invalidates this candidate's qualification claim until
affected validation completes. Dispatch the approved release workflow from the
protected source, with reviewed Node/Bun/model-data inputs and dependency locks.
Choose and verify an unused PSMFD release tag at that time; none is reserved here.

Download the published ARM64 closure, independently verify checksums and GitHub
attestation identity/source/workflow, and record every byte identity. A rebuilt
digest must undergo direct, published-host, offline Tart and paired RPC checks;
the local candidate's pass does not transfer to different release bytes. Recheck
dependency and source scan results for the final release commit.

Only then prepare the production pin change and review its 0.85.1-to-1.3.0 golden
diff. The existing production golden files have not been accepted or rewritten.
Record the exact installed pin and re-read it after installation; verify the
executable digest and existing PTY/RPC behavior. SDK adoption, managed activation,
GUI/keychain acceptance and repository retirement remain separate ADR-0034 gates.

## Rollback

Before changing a live selection, capture its exact executable/closure digest,
source/tag, descriptor/settings bytes and approved credential references. Existing
production selection is v0.85.1-psmfd.1; do not substitute the 0.87.1 comparison
or experimental managed binary as an automatic fallback. Keep the old closure
immutable until the new pin's acceptance window ends.

On failed release-byte verification, stop before installation and retain the
existing selection. On failed post-install verification, close only affected
candidate processes and restore the captured production selection/descriptor;
verify its original digest and PTY/RPC behavior. Retain failed candidate evidence
and keep managed selection disabled. Do not migrate or delete operator settings,
sessions, credentials or pi_config state, and do not fall back to ambient Pi.

There is currently no activation to roll back. The qualified artifacts can be
discarded independently while preserving both draft branches and all evidence.
