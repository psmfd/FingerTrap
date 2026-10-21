# 0039 — Qualify managed Pi on the maintained fork base

- Status: Accepted
- Date: 2026-10-21
- Authorization: maintainer-approved maintained-base forward-port and cap resolution
- Extends: [0038](0038-require-explicit-managed-pi-startup-policy.md)

## Context and problem statement

The first managed-startup experiment used the consumer's Pi 0.85.1 pin.
Maintained fork main `42429b94e4aca33fdbb8d094de29758b36411ba1`
(v0.87.1-psmfd.1) contains later security and correctness fixes. Merging the
old experiment over those files could discard them. The maintained fork also
uses all six capability-patch slots; adding managed startup exceeds the
existing 2,000-line aggregate budget.

Pi 0.87.1 introduced persisted crash discovery and default cache warming.
A mechanically clean port still reads and marks `crashes.json` as notified.
Qualification must follow behavior as well as source conflicts.

## Considered options

- Promote the old-base experiment: loses the maintained fixes and provenance.
- Drop or relabel existing patches: disguises needed behavior and accounting.
- Raise general limits: grants unrelated future divergence.
- Port onto maintained main with a bounded patch-specific exception and
  repeat exact-artifact qualification.

## Decision outcome

Choose the maintained-base port on a separate branch, preserving the original
experiment and its evidence. Retain all maintained fixes. Keep the executable
backend and the managed-v1 contract; explicitly disable cache warming and
persisted crash discovery/recording, and reject `/bug` under that contract.
Ordinary CLI behavior remains governed by upstream and existing fork patches.

Admit pending `psmfd-patch-020` with a bounded aggregate ceiling of **seven
capability patches, 2,200 changed lines and 25 distinct upstream paths**.
Only patch 020 receives the extra slot and aggregate line allowance. The
other six patches retain their existing eligibility and 2,000-line budget.
Count additions plus deletions against the verified upstream content baseline
for every patch's registered paths; shared paths count under every owner.
Include pending 020 in qualification accounting. Current measurement is
**2,090 lines**: 1,417 for existing patches plus 673 for 020.

Recount at every sync, including the next latest-release campaign. Exceeding
any ceiling stops the sync for a fresh decision. The exception expires when
020 is dropped or upstream adopts equivalent behavior. Keep manifest, text
allowlist and both guard sets synchronized. Classification, guard logic,
trusted-sync conditions and review requirements remain unchanged.

Build with the reviewed release toolchain and version-matched model data.
Qualify exact runtime and published host bytes with direct RPC/PTY/CLI,
two-profile host, disposable Tart, regression mutations and twice-recorded
protocol scenarios. Record platform limits and dependency findings. A local
pass does not claim release attestation or provider soak.

The protocol recorder normalizes numeric timestamp fields to one volatile
token. Coincidental equality of millisecond clock readings is not a message
identity and must not renumber other timestamps. Preserve UUID/correlation
identities, message types, payload and ordering, and retain both raw keyless
recordings beside the normalized evidence. Regression tests cover equal and
distinct clock ticks without erasing token counts or message changes.

### Consequences

- Good: the managed startup candidate preserves maintained security and
  correctness fixes with auditable limits.
- Bad: the temporary capability budget has no remaining file or patch slots;
  future changes require retirement, consolidation with evidence, or review.
- Neutral: qualification does not promote the production pin, publish a
  release, merge a PR, adopt the SDK or activate managed profiles.

## Known limitations and deferred work

- [Pi #80](https://github.com/psmfd/pi/issues/80) and
  [FingerTrap #202](https://github.com/psmfd/FingerTrap/issues/202): retain
  exact evidence and complete the separate pin/release decision.
- [#210](https://github.com/psmfd/FingerTrap/issues/210): after this campaign
  settles, discover the latest stable Pi release, reconcile patches and
  dependencies, and repeat validation. This newly created tracker is distinct
  from the earlier unresolved #210 reference in migration planning.
- [#201](https://github.com/psmfd/FingerTrap/issues/201): stores/resume/meters
  and later isolation work remain; managed production composition is disabled.
- FingerTrap owns migration tracking and implementation. Historical pi_config
  ADRs remain provenance; this decision does not resume development there.
