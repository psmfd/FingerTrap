# 0025 — FT-2 architecture: pi RPC supervisor and native RPC pane

- Status: Accepted
- Date: 2026-08-17

## Context and problem statement

FT-2's gate is closed ([#120](https://github.com/psmfd/FingerTrap/issues/120),
[docs/rpc-contract.md](../docs/rpc-contract.md) verified against pi
`v0.84.1-psmfd.1`). The roadmap scope is structured control + observability:
the sidecar drives `pi --mode rpc` (session list/resume, worktree-orphan
surfacing, model/status readouts), plus read-only meter dashboards and,
gated, the expertise drafts-review panel.

The contract study settled the protocol facts (LF-only JSONL, id-correlated
responses, `agent_settled` as the sole turn boundary, one process = one
attached session, stdin-EOF graceful shutdown, no version handshake —
psmfd/pi#56). What it forced into the open is a scope decision: an RPC-driven
pi session has **no TUI**. If FingerTrap only inspects over RPC, interactive
sessions stay PTY panes and FT-2 is small; if FingerTrap *renders* the
conversation, that is a native chat pane — a substantially larger deliverable
that is also the maintainer's stated end-state for the Home interface.

Constraints in force:

- FingerTrap renders and hosts; it never owns policy, credential policy, or
  approval semantics (milestones, curated plan).
- The FT-1 chrome (tabs, splits via the ADR-0024 layout tree, palette,
  keymap, focus) is pane-kind-agnostic in structure, but `Pane` currently
  assumes an xterm `Terminal` instance.
- The operator's pi config loads a full extension suite — guard trio,
  widgets, worktree isolation (pi_config ADR-0120: every session
  worktree-isolates). Extensions load under `--mode rpc` and their UI rides
  the `extension_ui_request`/`extension_ui_response` channel; a host that
  ignores that channel half-breaks the operator config.
- Rendering model/tool output natively is untrusted-content territory: the
  FT-1 lesson (sanitize at the data boundary, ADR-0022) and the slice-2 CSP
  apply in full.

## Considered options

- **Option A — inspector-only control plane.** RPC children exist only as
  ephemeral query processes; resume opens a normal PTY pane
  (`pi --session <path>`). Smallest FT-2; the native pane waits for a future
  phase.
- **Option B — native RPC pane, staged MVP-first.** Commit to the native pane
  now, built in slices on top of the supervisor; PTY panes remain first-class
  forever and are the escape hatch while the native pane is sub-parity.
- **Option C — native RPC pane at TUI parity before shipping.** Build the
  full experience (rich markdown, session tree/fork UI, complete widget
  support) and land it at once.

## Decision outcome

Chosen option: **Option B — native RPC pane, staged MVP-first.**

The native pane is the declared end-state, so Option A would spend a slice on
a session-detail view the pane immediately obsoletes, and would re-enter this
protocol months from now against a stale study. Option C is a parity chase
with a moving target (pi's TUI evolves continuously) and delays every
increment behind the hardest ones. Option B ships the supervisor and pane
skeleton against the fresh contract, keeps each slice independently usable,
and prices the parity chase honestly: **PTY panes remain first-class; the
native pane is additive**, and any pi TUI feature the native pane lacks is
reachable by opening the same session in a PTY pane instead.

### Architecture decisions

1. **Supervisor (`PiRpcClient`, sidecar).** One pi child per attached native
   session, spawned by the sidecar (not through the PTY service — this is not
   a terminal). Discipline per the contract note: LF-only JSONL codec,
   id-correlated pending-request map with per-request timeout, event demux
   (response-with-known-id vs event), `agent_settled` turn tracking, child
   exit/error rejects all in-flight requests with captured stderr, shutdown =
   stdin-EOF then SIGTERM → bounded grace → SIGKILL. Tested against a
   **fake pi** (scripted stdio double) with conformance cases keyed to
   `docs/rpc-contract.md`; the fake is the seam, as CannedHandler is for the
   status providers.
2. **No headless inspector children.** The session browser's list and detail
   metadata come from parsing the session store directly
   (`~/.pi/agent/sessions/**.jsonl`, the psmfd/pi#54 workaround, isolated in
   one class so pi#54's arrival deletes it cleanly). RPC children exist
   *only* as live pane sessions, where extension side effects (worktree
   isolation, guards) are not hazards but correct behavior. This dissolves
   the side-effect-free-spawn problem instead of solving it.
3. **Thin relay.** The sidecar forwards pi RPC events to the UI as JSON-RPC
   notifications essentially verbatim (within the existing 4 MB frame
   ceiling), tagged with the pane's session identity. It does not interpret,
   aggregate, or render. Sanitization happens at the UI's data boundary at
   render time, exactly as ADR-0022 does for status text. The sidecar's only
   protocol intelligence is the supervisor discipline above.
4. **Pane content abstraction.** `Pane`'s xterm coupling is refactored behind
   a content interface so the registry/layout tree/tab chrome host either an
   xterm-backed PTY pane or the native RPC pane. ADR-0021/0024 structures are
   unchanged; this is the enabling refactor, not a redesign.
5. **Extension UI channel is in-scope, early.** `select`/`confirm`/`input`/
   `editor` round-trips and the fire-and-forget set (`notify`, `setStatus`,
   `setWidget`, `setTitle`, `set_editor_text`) are rendered natively. The
   guard trio's confirmations must work in a native pane before the pane can
   be called usable; `set_editor_text` feeds the host-owned composer
   (reference-into-prompt stays a native picker — repo-dash's panel is
   TUI-only, psmfd/pi_config#1018).
6. **Composer is host-owned state.** pi has no draft buffer over RPC
   (`getEditorText()` is a stub); the composer lives entirely in the UI, and
   submits via `prompt`/`steer`/`follow_up` with queue state rendered from
   `queue_update`.

### Slices

| Slice | Deliverable |
|---|---|
| 1 | Supervisor + fake-pi conformance tests (sidecar-only) |
| 2 | Pane content abstraction + event relay — native pane walking skeleton rendering raw streamed text |
| 3 | Transcript + composer MVP: turns, tool blocks, steer/follow-up/abort, model/thinking picker, context meter |
| 4 | Extension UI channel: dialogs, widgets, editor-text |
| 5 | Session browser + resume (into either pane kind) + worktree-orphan surfacing |
| 6 | Markdown rendering + sanitization for the transcript (vendored renderer + sanitizer under the CSP) |
| 7+ | Meter dashboards; expertise drafts-review panel (gated on pi_config Track 5 rung 4 shipping its artifact/ledger) |

Slice issues are filed as each slice starts (FT-1 practice); #120 tracks the
ledger.

### Consequences

- Good: every prerequisite of the end-state lands now, against a verified
  contract; each slice is independently shippable; the browser needs no
  side-effect-free spawn story at all; PTY fallback removes ship-at-parity
  pressure.
- Bad: FT-2 grows to roughly FT-1's size (~6–8 PRs beyond the supervisor);
  the native pane accepts a standing parity gap with pi's TUI; the missing
  protocol version handshake (psmfd/pi#56) means pin bumps require
  re-verifying `docs/rpc-contract.md` before the pane is trusted against a
  new pi.
- Neutral: transcript rendering quality (markdown, diffs) is deliberately
  last; dashboards and the drafts panel trail the pane rather than leading
  the phase.
