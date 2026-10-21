# Pi 1.3.0 candidate qualification

The maintained 0.87.1 campaign is settled as a qualified reference: Pi #82 and
FingerTrap #209 stay drafts; production promotion was deferred for dependency
resolution. FingerTrap #210 then selected latest stable upstream **v1.3.0**
(`71023aec1546d6d677a65669c5665c0927ce0dd0`, published 2026-10-15).

The corrected candidate is committed and backed up in
[Pi #83](https://github.com/psmfd/pi/pull/83) at
`28741c97f9baf5649e8ad3ba1c996d7bbcb5abd2`. Its ARM64 executable SHA-256 is
`901a10f67a06f04310f4d064e95f7e3e60ec523d253366731e077a239237407f`.
FingerTrap fixture source is `0143c3317db5343d004bbfa1b75fd566b87c1b6c` in
[#209](https://github.com/psmfd/FingerTrap/pull/209). These are qualification
identities; no production pin, release, SDK adoption or managed activation changed.

## Evidence and results

| Check | Evidence | Result |
| --- | --- | --- |
| Patch admission/accounting | [patch-accounting.json](patch-accounting.json) | 6 C patches / 23 paths / 2,021 lines; existing 7 / 25 / 2,200 ceiling |
| Chord replacement | [chord-retirement.txt](chord-retirement.txt) | Original overflow reproduced; 100,000-item append, replay/adoption and native controls pass |
| Source/static and full suite | [static-security.txt](static-security.txt), [tests-security.txt](tests-security.txt) | Pass; coding-agent 2,738 / 50 skipped, both durable modes 1,220 / 4 |
| Security regressions | [codeql-controls.json](codeql-controls.json), [security-red.txt](security-red.txt), [security-green.txt](security-green.txt) | Four real findings fail before and pass after; plain and Chord prototype controls |
| Remote Pi checks | [pi-fixed-checks.json](pi-fixed-checks.json), [codeql-false-positive-review.json](codeql-false-positive-review.json) | All eight pass; four fixed findings, four specifically reviewed false positives |
| Direct managed startup | [direct-local.json](direct-local.json) | All 18 cases pass, including MCP config/process canaries |
| Published host | [sidecar-local.json](sidecar-local.json) | Two-profile RPC/PTY, environment separation and process reaping pass |
| Offline disposable Tart | [tart/state.json](tart/state.json) | Both matrices pass; clone removed, base preserved |
| Real RPC recordings | [rpc-baseline/report.json](rpc-baseline/report.json), [rpc-candidate/report.json](rpc-candidate/report.json) | Both binaries: all 12 scenarios twice; production goldens restored |
| Protocol review | [protocol-review.json](protocol-review.json), [protocol-drift.patch](protocol-drift.patch) | Identical outgoing commands/process controls; upstream additions reviewed below |
| Runtime/host mutations | [runtime-mutations.json](mutations/runtime-mutations.json), [restored host tests](host-mutation/host-policy-restored-tests.txt) | Four runtime regressions and bad host policy caught; restored source/build green |
| FingerTrap platform CI | [fingertrap-source-checks.json](fingertrap-source-checks.json) | All 23 checks pass; five macOS jobs local, Windows/Linux hosted |
| Apple artifact | [apple-artifact-review.json](apple-artifact-review.json) | ZIP/app/DMG digests, identical source tree, runner identity and ARM64 binaries verified |

The published host is the preserved test host from source
`bb5f02fd64b41310a7f35aec92a60b67bdd8bad8`, executable SHA-256
`80beb8542052fe1246528d2a9a0617e2b3760c81c39163f228b0c128d97af3da`.
Production host source has not changed since that build. The ordinary-mode
0.87.1 comparison executable is `90aca42f1120a8921fea195328dd8b3e0be4a2d701aa371614f92705f66f03ff`.
Recordings use synthetic homes and a local canned provider; no operator auth or
paid model is used. Raw transcript paths/UUIDs/times are deliberately retained.

## Reviewed changes and limits

Porting uses the verified v0.87.1 **content** baseline because the old squash
ancestry is stale. Both prior managed source and exact upstream 1.3.0 are parents
of the new candidate; old history is not rewritten. Retained capabilities remain
registered. Proposed retirement of 016/017 follows upstream removal of their
experimental harness; 019 follows replacement-tracker overflow, replay, native
splice and adoption controls. Retirements still require integration review.

Security patches 022/023/024 fix imported CodeQL findings in catalog parsing,
error disclosure and streamed JSON assignment. Before/after controls and exact
paths are retained. Custom HTML escaping and fixture-only script extraction/URL
hashing have specific false-positive proof and readback. No broad scan exclusion
was added. Private upstream reporting assessment remains for the maintainer;
no external report or message was sent.

Protocol additions are `prompt` started/queued disposition, assistant timing and
thinking level, `agent_settled.aborted`, Pi version and system documentation for
MCP/codemode. Event counts and all outbound commands/process controls are checked.
First recording caught new `durationMs` nondeterminism: retain
[initial failure](rpc-candidate-initial/report.json) and both raw pairs. A
red-first test proves the narrow timing normalization; message content and token
counts retain their comparisons. Production 0.85.1 goldens remain unchanged.

Published npm pi-ai model data differs from the source release in manifest and
OpenRouter data. [Both inputs](model-data.json) and the
[selected published input](model-data-npm.json) retain independent hashes.
Final checks/build/tests select the npm package to match the release workflow.
Node 22.23.0 and Bun 1.3.14 are recorded; no lifecycle install scripts were run.

## Dependency disposition

The root audit fell from the maintained baseline's 15 findings to
[two high findings](audit-final.json), both in the optional Gondolin example
dependency chain (`@earendil-works/gondolin` → `node-forge`). No published fix is
available for [GHSA-86w9-cpqp-85rv](https://github.com/advisories/GHSA-86w9-cpqp-85rv).
The example remains present; its package source/lock does not become a blanket
waiver. The core runtime install-lock excludes both packages. Managed startup
does not discover or select the example. Production disposition remains open.

The independent sandbox example lock is refreshed to shell-quote 1.12.0 and its
[isolated audit](sandbox-isolated-audit.json) is clean. Pending patch 021 pins
development-only Vite to 8.1.6 after release/advisory/integrity review. Root audit
does not establish nested-lock safety; the initial parent-graph audit was rejected
as evidence. No security finding is called resolved merely because it is an example.

## Recovery and reproduction

Read [STATUS](../../STATUS.md) for the current next action and approval gates.
Recover Pi branch `sync/upstream-v1.3.0` in an isolated clone; preserve the
operator's sibling checkout. Rehydrate the selected npm model archive, install
the locked graph with `npm ci --ignore-scripts`, and build with the reviewed
hermetic release block. A changed executable digest requires new qualification.

Exact local artifact closure is preserved at
`.artifacts/pi-next/preserved-20261021/`: runtime, published host, selected npm
model archive, upstream source archive and Bun archive. Its 215-file inventory
SHA-256 is `67587ddb8e4d1d4d5755b4edc1aa0fe12dc76141fae1e2e83b6f1c6c52096ef6`.
This directory is ignored and local; source and essential evidence are backed up
in GitHub. Keep it across system patching. Private operator files remain untouched.

Portable controls are retained beside this file:
`verify-patch-accounting.py`, `verify-codeql-controls.py`,
`verify-runtime-mutations.py`, `verify-host-policy-mutation.py`, and
`review-protocol.py`; `verify-chord-retirement.ts` reconstructs the exact old
upstream source from Git. It explicitly uses an 850 KiB Node stack to reproduce
the original overflow while native push and the replacement accept the same
100,000 inputs. Default-stack thresholds vary; increasing inputs past the public
call limit is not valid proof. Mutation helpers require an isolated checkout and must run
sequentially with any commands consuming that checkout. They restore exact source.

The packaged-app GUI/keychain acceptance, exact release/pin promotion, SDK backend
switch and managed-profile activation retain ADR-0034's separate decisions.
The packaged sidecar's shell smoke passes; its Pi spawn check explicitly skips
because the product bundle does not install Pi. Exact managed runtime validation
is the separate direct/host/Tart matrix. The digest-bound
[promotion/rollback record](promotion-rollback.md) leaves the final #210
promotion/verification item pending a maintainer decision.
