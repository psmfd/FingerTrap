# 0028 — Retain the session-store scan; defer list_sessions migration until a side-effect-free spawn exists

- Status: Accepted (amends [0025](0025-ft2-rpc-supervisor-native-pane.md): decision 2's "pi#54's arrival deletes it cleanly" clause)
- Date: 2026-08-20

## Context and problem statement

ADR-0025 decision 2 ("No headless inspector children") made the session
browser's data source a direct bounded scan of `~/.pi/agent/sessions/`
(`SessionStore`), labeling it "the psmfd/pi#54 workaround, isolated in one
class so pi#54's arrival deletes it cleanly." pi#54 has now arrived: the
pinned fork (`v0.84.2-psmfd.1`, psmfd-patch-011) ships a `list_sessions`
RPC command, golden-pinned, with header-fields-only projection.

The deletion clause does not survive contact with the same ADR's own core
principle. `list_sessions` runs only **inside a live pi child**, and the
session browser's primary use case is **zero panes open** — the operator is
choosing what to resume. Serving `sessions/list` from the fork command
would therefore require a headless `pi --mode rpc` child, which is
precisely what decision 2 forbids and why: operator extensions load in RPC
mode and fire at session start with real side effects (worktree isolation
creates worktrees, guards arm). The hazard has not changed; only the
command's availability has. Meanwhile #140 wants unparseable session files
surfaced as a visible count rather than a silent absence — a scan-side
deliverable that does not depend on the migration either way.

## Considered options

- Option A — Retain the scan as the single data source; add the
  `skippedFiles` visibility; defer the migration behind named
  prerequisites.
- Option B — Migrate now via a headless inspector child (revisit decision
  2 wholesale).
- Option C — Hybrid: `list_sessions {all: true}` through a live pane's
  client when one exists, scan otherwise.

## Decision outcome

Chosen option: **Option A**.

- Option B re-creates the exact hazard decision 2 was written against:
  a headless child binds the full operator extension suite, so every
  browser refresh could create a worktree and arm guards. The fork command
  cannot avoid this from inside pi today — there is no side-effect-free
  spawn mode. Rejected *for now*, not forever: the operator has directed
  that headless listing be revisited once the prerequisites below exist.
- Option C makes the browser's contents depend on whether a pane happens
  to be open (two sources, different cwd scoping, different failure
  modes) for no operator-visible gain. Rejected.
- The scan therefore stays the session browser's single source at every
  pin, and `SessionsListResult` gains `SkippedFiles` (#140): files the
  scan attempted but could not parse are counted and rendered, per-file
  isolation unchanged. Cap-excluded files are unattempted, not skipped.
  `list_sessions` remains available to live panes through the generic
  command passthrough — this ADR constrains the *browser's* data source,
  not the command's use.

This amends ADR-0025 in one clause only: "pi#54's arrival deletes it
cleanly" is superseded by this record. Decision 2's principle — RPC
children exist only as live pane sessions — is reaffirmed, not weakened;
its Status line points here. The rest of ADR-0025 stands untouched.

### Consequences

- Good: zero-pane browsing keeps working with no spawn cost or extension
  side effects per refresh; corruption becomes visible (#140); the
  migration path is written down instead of implied.
- Bad: two session-listing implementations exist in the ecosystem (pi's
  and this scan), and scan-vs-pi drift in header parsing remains possible
  until migration — mitigated by the pinned-version re-verification
  ritual in docs/rpc-contract.md.
- Neutral: FT#148's capability advertisement (`Supports("list_sessions")`)
  has no browser consumer under this decision; it remains the discovery
  surface the migration (and pi#55 `cwdOverride`) will gate on.

## Known limitations and deferred work

- Deferred: the migration itself — tracked as
  [#151](https://github.com/psmfd/FingerTrap/issues/151), revisit
  prerequisites: (1) a side-effect-free listing spawn in the fork (e.g.
  `--no-extensions` RPC mode or a dedicated headless list entrypoint that
  binds no extensions and creates no session — a new psmfd/pi C-class ask
  under pi_config ADR-0138's caps), (2) a fork-side skipped-count surface
  so this ADR's visibility carries over (fork-side isolation currently
  degrades a corrupt file to a silent absent row), (3) single-source
  cutover deleting the `SessionStore` parse class — no hybrid.
- Deferred: classing `skippedFiles` as unreadable vs malformed-header —
  the issue marks it optional and the UI renders one line either way; add
  it when a consumer needs the distinction.
