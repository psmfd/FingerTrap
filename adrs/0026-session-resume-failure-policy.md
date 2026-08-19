# 0026 — Session resume-failure policy: never mutate, disable RPC resume on missing cwd

- Status: Accepted
- Date: 2026-08-19

## Context and problem statement

The session browser (FT-2 slice 5, [#136](https://github.com/psmfd/FingerTrap/issues/136))
resumes stored pi sessions into panes. A session's header records the cwd it
ran in, and on this host a large share of stored sessions ran in per-session
worktrees (`<repo>/.worktrees/<sid>`) that the pi_config worktree extension
reaps after the session ends — so a missing cwd is the *normal* state of a
finished session, not a corruption.

pi's behavior at the pinned version differs by mode and timing (verified
against the source during the slice-5 research):

- **RPC mode, spawn-time `--session` with a missing cwd** — pi writes to
  stderr and hard-exits(1) before the JSONL channel is up. There is no JSON
  error to relay; the pane child just dies.
- **RPC mode, mid-session `switch_session` with a missing cwd** — a normal
  JSON error response; the process lives and the old session stays intact
  (the check runs before teardown). Not usable for the browser: pane spawn
  is where selection happens (ADR-0025, docs/rpc-contract.md).
- **Interactive (PTY) mode** — pi itself shows a Continue/Cancel fallback
  prompt and falls back to the process cwd on Continue.

The only pi-side workarounds that make a dead-cwd RPC resume "work" are
mutations: `mkdir -p` of the dead worktree path, or editing the session
header's cwd. Both fabricate state — a recreated worktree directory is not a
worktree (git metadata is gone), and a rewritten header lies about where the
session ran. psmfd/pi#55 (cwd override at spawn) is the real fix and is open
upstream.

## Considered options

- Never mutate; disable RPC-pane resume when the cwd is missing, offer
  PTY-pane resume with the original repo as the pane cwd
- `mkdir -p` the dead cwd before an RPC resume so pi's check passes
- Rewrite the session header's cwd to an existing directory before resume
- Block resume entirely (both pane kinds) when the cwd is missing

## Decision outcome

Chosen option: **never mutate; disable RPC resume on missing cwd, offer PTY
resume with a fallback cwd**.

FingerTrap never mutates session state to force a resume — no `mkdir -p` of
dead cwds, no header edits. Concretely:

- `SessionSummary.CwdMissing` (sidecar-computed, `!Directory.Exists(cwd)`)
  ⇒ **RPC-pane resume is DISABLED** in the browser UI, annotated with why —
  a spawn would hard-exit(1) with no JSON error to render.
- **PTY-pane resume stays OFFERED** in that state: interactive pi owns the
  fallback conversation (Continue/Cancel, fallback = process cwd). For a
  reaped worktree, the browser passes `SessionSummary.OriginalRepo` — the
  cwd prefix before `/.worktrees/` — as the pane cwd, so "Continue" lands
  the session in the repo the worktree hung off rather than in `$HOME`.
- `OriginalRepo` is sanitized display-derived data like every other
  content-derived field; a hostile header cwd can therefore yield an
  invalid fallback directory, in which case pi errors visibly. Accepted:
  `SessionPath` (sidecar-enumerated) is the only functional resume key,
  and the fallback cwd failing loudly is the same failure shape as any
  wrong cwd.
- The restriction lifts fully when psmfd/pi#55 (spawn-time cwd override)
  lands upstream; the RPC path then resumes with an override cwd exactly
  where the PTY path uses its fallback today.

Rejected because: the `mkdir` hack fabricates a directory that looks like a
worktree but is not one (and silently un-reaps what the worktree extension
deliberately reaped); header rewriting corrupts the historical record every
later consumer reads; blocking both pane kinds throws away a resume path pi
itself already handles gracefully.

### Consequences

- Good: session files remain read-only to FingerTrap — the browser can
  never damage what it browses.
- Good: the PTY fallback gives every session a working resume path today,
  in the pane kind whose UX (pi's own prompt) already explains the
  situation.
- Bad: dead-cwd sessions cannot resume as native RPC panes until pi#55
  lands — the richer pane kind is unavailable exactly for reaped-worktree
  sessions, which are common here.
- Neutral: the UI must render the disabled state with a reason, not hide
  the action (slice 5b).
