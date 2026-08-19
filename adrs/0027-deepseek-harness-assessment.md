# 0027 — DeepSeek Harness assessment: remain on pi, adopt patterns

- Status: Proposed
- Date: 2026-08-19

## Context and problem statement

DeepSeek Harness (`dsh`,
[deepseek-ai/deepseek-harness](https://github.com/deepseek-ai/deepseek-harness))
shipped as an MIT developer preview on 2026-08-13 — a TypeScript agent
harness in which every capability (model adapter, tools, sessions, sandbox,
agent loop, UI) is a Cordis plugin — and drew one of the fastest adoption
curves GitHub has recorded (~166k stars in its first week). The question
this ADR answers: does adopting or migrating any layer of the
pi + pi_config + FingerTrap stack onto dsh make sense, and if not
wholesale, what is worth taking?

Assessed 2026-08-19 from dsh source at `v0.1.0-rc.8` (commit `141eb6f`),
against this stack as of the FT-2 slice-5 merge (`92dc6fa`, ADR-0026) and
the pi fork (pin `v0.84.1-psmfd.1`; fork HEAD read at `9409e15`).

Facts the decision turns on, all verified from source rather than press:

- **dsh's programmatic surfaces are automation-shaped, not host-shaped.**
  Its ACP server is deliberately minimal: fresh sessions only (no
  list/resume/fork), committed text only (no streaming deltas, tool
  activity, or model switching), one-shot permission prompts as the only
  dialog. Its SDK protocol is three requests (`initialize`,
  `session/prompt`, `shutdown`) with **no cancel** and, per its own README,
  "no compatibility promise". Everything the FT-2 native pane consumes from
  `pi --mode rpc` — steer/follow-up/abort, streaming deltas, tool events,
  the extension-UI channel, model switching, fork/resume
  ([docs/rpc-contract.md](../docs/rpc-contract.md)) — is absent or
  explicitly out of scope on both.
- **dsh's only full-fidelity client is its own web app**, over an internal
  client/host protocol. dsh ships no TUI, so ADR-0025's escape hatch — the
  PTY pane at TUI parity — would not exist during any parity chase.
- **The provider layer is already shared.** dsh's multi-provider adapter is
  built on `@earendil-works/pi-ai ^0.82.1`, the same library pi uses. Model
  access, DeepSeek models included, is not a differentiator.
- **Migration would replace the differentiated layers.** pi_config's
  guard/approval/credential/worktree governance would need rebuilding as
  Cordis plugins on a base that promises compatibility-breaking changes
  (rc.7 → rc.8 shipped within days of launch).
- **dsh is genuinely ahead in specific, portable places:** fail-closed
  syscall sandboxing (bwrap/Landlock/Seatbelt/Windows-ACL behind one seam,
  with `landlock-run` published as a standalone MIT npm artifact), settings
  and credential semantics, session query discipline, an advisory
  repeat-tool guard, and a record–replay snapshot testing culture — each
  documented well enough to port without tracking dsh's code.

## Considered options

- **A — Migrate the hosted agent: pi → dsh.**
- **B — Re-platform the FT-2 native pane onto a dsh surface (ACP or SDK).**
- **C — Remain on pi; adopt selected dsh patterns and artifacts.**
- **D — No action.**

## Decision outcome

Chosen option: **C — remain on pi; adopt patterns**.

A and B fail on the same fact: the interactive host protocol the native
pane needs does not exist in dsh. Either option means authoring
FingerTrap-owned Cordis plugins plus a bespoke bridge protocol on a moving
release-candidate base, with no TUI fallback while sub-parity — the parity
chase ADR-0025 priced honestly, minus the valve that made it acceptable.
A additionally replaces the governance and isolation layers where this
stack's investment is deliberate. D leaves cheap, verified wins unclaimed.
Sunk cost is not the argument: even greenfield, pi's RPC mode is the only
protocol on the table that matches the pane's requirements.

### Adoption ledger

Windows reference existing plans; none of these change FT-2/FT-3
sequencing. Efforts: S/M/L.

| # | Adoption | Lands in | Effort | Window |
|---|---|---|---|---|
| P1 | Record–replay RPC contract goldens: opt-in recorder drives real `pi --mode rpc` through the contract-study scenarios (resume cases included); keyless CI replays them through the supervisor; separate record vs refresh modes; volatile fields tokenized, one scenario fully pinned. Pin bumps become a diff instead of a source re-read | FingerTrap | M | FT-2 slice 6 window |
| P2 | `hello` line in `pi --mode rpc` — first stdout line carries `piVersion`, protocol number, capabilities; doubles as the ready signal. Supervisor gates on it with a legacy fallback for older pins. Implements psmfd/pi#56 in the fork; ACP's `initialize` negotiation is the prior art | psmfd/pi + FingerTrap | S | next fork pin bump |
| P3 | `list_sessions` RPC command exposing the existing library-only `SessionManager.list()` (closes psmfd/pi#54; retires the ADR-0025 SessionStore scan on schedule). Interim FingerTrap hardening: `sessions/list` surfaces a skipped-unparseable-files count instead of silent omission | psmfd/pi + FingerTrap | S | next fork pin bump |
| P4 | Documentation conventions: a "Context Experience" section per pi_config extension (what it injects into model context, when, and the prefix-cache consequence) and a known-limitations ledger convention for ADRs | pi_config + FingerTrap | S | now |
| P5 | Repeat-tool advisory guard: chains keyed on (tool, canonicalized args); excluded bookkeeping tools transparent to the chain; denied calls count; escalating thresholds; advisory message delivery, never a result mutation; fail-loud config | pi_config | M | pi_config cadence |
| P6 | Settings semantics for N-1: validate-then-persist with rooted error paths; last-known-good on external corruption; write revisions with conflict rejection; path-op mutations for holders of redacted views; value-changed vs document-changed events | FingerTrap | M | N-1 |
| P7 | Credential doctrine for the Track 3 broker and FT-3: configuration carries references, never secrets; resolve per operation; empty value is absent; describe-not-reveal; mandatory redaction on every wire surface | pi_config + FingerTrap | S | Track 3 design |
| P8 | Fail-closed sandbox for session bash: wrap the bash tool's argv via `landlock-run` (Linux) then a Seatbelt profile (macOS); the session worktree is the write grant. Changes runtime behavior — gated on its own ADR | pi_config | M–L | gated |
| P9 | Session query-surface seam discipline only — keep `SessionStore`'s read surface narrow enough that a future engine swap replaces the inside of one class (the fork already carries `session-backends/sqlite-node`) | FingerTrap | — | watch |
| P10 | Testing doctrine: verify the world, not the self-report; a guard only guards if the regression fails it (introduce, watch red, revert); test the real entry path | pi_config + FingerTrap | S | now |

Slice 5 independently validated P3's listing discipline before this ADR
was written: `SessionStore.cs` converged on bounded newest-first parsing,
header-line reads, and per-file fault isolation without reference to dsh.
Convergent evolution is treated as evidence for the pattern, not a reason
to skip the remaining hardening.

### Deliberate non-adoptions

Recorded so they are not re-litigated at the next hype cycle:

- **Cordis / everything-is-a-plugin as an architecture** — wrong scale for
  a three-process app and a single-operator governance stack; the ADR
  discipline is this stack's composability answer.
- **Profile/bundle configuration layering** — solves multi-distribution
  problems this stack does not have; N-1's single versioned settings file
  is the right size.
- **OpenTelemetry now** — the meter JSONL files serve FT-2 slice 7's
  observe-from-outside design; telemetry infrastructure would invert it.
- **SQLite-first store swap** — the fork already carries a sqlite session
  backend; the gap was the query surface (P3), not the engine.
- **Embedding the dsh web client** — hosts their product instead of
  integrating it.

An ACP subagent pilot (dsh as a contained, version-pinned child under
pi_config's subagent extension) is compatible with this decision and is
sequenced in pi_config, not here.

### Revisit triggers

Re-open this decision when any of the following holds:

- dsh ships a stable interactive client protocol, or its ACP server gains
  session load/list/resume, streaming output, or tool activity.
- dsh reaches 1.0 with a stated compatibility promise.
- pi upstream stalls or the fork burden grows — psmfd/pi#54–#56 rejected
  upstream while every pin bump keeps paying manual re-verification.
- `pi-ai` stewardship changes — dsh's dependency on it makes the provider
  layer shared infrastructure between the two ecosystems.
- pi_config Track 3's credential broker proves too costly to build
  natively; dsh's credential plane then re-enters as an alternative.

### Consequences

- Good: FT-2/FT-3 sequencing is untouched. The P1+P2+P3 cluster turns
  pin-bump re-verification — ADR-0025's standing cost, re-paid during the
  slice-5 research per ADR-0026 — into a mechanical diff plus a checked
  handshake.
- Good: the rejections above are recorded with their reasons, so the
  migration question has a written answer.
- Bad: pattern ports can drift against a fast-moving source. Mitigated by
  adopting semantics and pinned artifacts (the `landlock-run` binary,
  verbatim reminder texts) rather than tracking dsh code, and by
  re-reading dsh only on the revisit triggers.
- Bad: the fork carries two more psmfd patches (`hello`, `list_sessions`)
  until upstream accepts them.
- Neutral: P8 changes runtime behavior and is deliberately gated on its
  own ADR rather than adopted here.
