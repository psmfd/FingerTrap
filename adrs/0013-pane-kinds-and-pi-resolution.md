# 0013 — Panes have a kind, and a pi pane refuses to open without pi

- Status: Accepted
- Date: 2026-08-15

## Context and problem statement

Home phase FT-0 ([ADR-0012](0012-home-phase-resequencing.md), issue #45) asks
for "pi running in an xterm.js PTY pane as a **first-class pane type**", with a
bar of "functional parity with pi in any terminal".

M1 already delivers the hard part: `IPtyService` spawns a shell over a
pseudoterminal, xterm.js renders `pty/output`, keystrokes return via
`pty/write`, resize is debounced and forwarded. Running pi in it needed nothing
new — `PtySpawnOptions.Shell` is an arbitrary executable path, so passing pi's
path already worked.

That is exactly why this needed a decision rather than a one-line change. "Pass
a different string for `Shell`" is not a *pane type*; it is a pane that happens
to contain pi, indistinguishable to the application from any other. Three
things follow from the difference:

1. **Something must know how to find pi.** A caller supplying an absolute path
   pushes the problem to whoever configures the caller, and there is no
   settings system yet (Native track N-1).
2. **A missing pi must not become a shell.** `ResolveShell` ends in a fallback
   chain — `$SHELL`, then `/bin/zsh`, `/bin/bash`, `/bin/sh` — because some
   shell essentially always exists. Reusing that path for pi means asking for
   pi and silently getting zsh: the operator types at something that is not
   what they asked for, and the difference is not obvious at a glance.
3. **Something must decide what an unqualified pane is.** FingerTrap is the pi
   Home; opening it should not require asking for pi every time.

## Considered options

- **A — No kind. Callers pass pi's path in `Shell`.** Smallest change. Leaves
  resolution and the missing-pi behaviour to every caller, and makes "is this a
  pi pane?" unanswerable from inside the app.
- **B — Kind enum; pi resolution falls back to a shell when pi is absent.**
  Always leaves a usable pane. Reintroduces the silent-substitution problem the
  kind concept exists to remove.
- **C — Kind enum; pi resolution throws when pi is absent; host default is pi.**
- **D — As C, but the host default stays `shell`.** Nothing regresses, and
  FT-0's headline deliverable ships switched off.

## Decision outcome

Chosen option: **C**.

`PaneKind` (`Shell` | `Pi`) joins `PtySpawnOptions`. `PtySpawnRequest` carries
it as an optional wire string. Resolution is deliberately **asymmetric**:

| Kind | Resolution order | Not found |
| --- | --- | --- |
| `Shell` | explicit path → `$SHELL` → `/bin/zsh` → `/bin/bash` → `/bin/sh` | cannot happen in practice |
| `Pi` | explicit path → `$FINGERTRAP_PI` → `PATH` | **`PiNotFoundException`** |

The asymmetry is the decision. Guessing at a shell is harmless; guessing at pi
is a silent substitution of the one thing the pane exists to host.

`PiNotFoundException` is a distinct type, not a bare
`InvalidOperationException`, so a caller can tell "pi is not installed here" —
operator-fixable, known remedy — from a genuine spawn failure. Its message
names both places searched and all three remedies, because it surfaces in the
terminal pane the spawn failed to fill, which is where the operator is already
looking.

### The host default is pi, and lives in the sidecar

An unqualified `pty/spawn` opens a **pi** pane. `FINGERTRAP_PANE_KIND=shell`
overrides it — an interim knob until N-1's settings system absorbs it.

The default lives sidecar-side rather than in the UI for a mechanical reason:
the UI is a WebView and cannot read process environment. Putting it there also
keeps it unit-testable. `main.ts` therefore sends **no** `kind` at all, so it
cannot shadow the host default by accident.

Option D was rejected because FT-0's deliverable would ship switched off, and
FT-1 would flip it anyway. The cost is real and accepted: **on a machine
without pi, FingerTrap opens an error instead of a terminal.** That is the
correct posture for the pi Home — and the error says how to get a shell.

### An unrecognised kind is an error

`FINGERTRAP_PANE_KIND=pie` throws rather than falling back to the default. A
typo that silently opened the default pane would be indistinguishable from the
variable working, which is the failure mode this exists to prevent.

### PATH search is hand-rolled

`FindOnPath` walks `PATH` and checks the executable bit rather than shelling
out to `which`/`where`. Spawning a process to decide what to spawn adds a
dependency on yet another binary, on the pane-open path. The executable-bit
check is what stops a same-named directory or a non-executable file being
returned as a usable answer — it would otherwise fail at spawn with a far less
clear error.

### What did not need doing

The PTY inherits the full process environment: Porta.Pty's `MergeEnvironment`
seeds from `Environment.GetEnvironmentVariables()` and then overlays
`TERM=xterm-256color`. So pi finds `HOME`, `~/.pi`, and a sane `TERM` with no
work here. This was verified rather than assumed, because a pi that cannot
locate `~/.pi` would fail in confusing ways.

No new RPC methods, so the ADR-0003 `RpcSurface`/`api.ts` pairing count is
unchanged at 4 requests + 2 notifications.

### Consequences

- Good: asking for pi and getting a shell is now impossible. The failure is
  loud, typed, and carries its own remedy.
- Good: verified end to end against the real sidecar over stdio, not only by
  unit test — default kind spawns pi v0.84.1 (21.5 KB of TUI output),
  `kind: "shell"` spawns zsh, and a stripped `PATH` produces
  `PiNotFoundException` with no shell spawned.
- Bad: **no pi means no usable app.** Accepted deliberately; `FINGERTRAP_PANE_KIND=shell`
  is the escape hatch, and the error message names it.
- Bad: two environment variables (`FINGERTRAP_PI`, `FINGERTRAP_PANE_KIND`) are
  configuration living outside any settings system. Both are interim and both
  belong to N-1 when it lands; recorded here so they are migrated rather than
  discovered.
- Neutral: `PtySpawnOptions.Shell` now means "explicit executable override for
  whichever kind is in force", which is a slightly awkward name for a pi pane.
  Renaming it would break the wire contract for no functional gain; documented
  instead.
- Neutral: pane *kind* exists, but panes are still singular — there is one pane
  and it is created at startup. Multiple panes, splits, and lifecycle are FT-1.
