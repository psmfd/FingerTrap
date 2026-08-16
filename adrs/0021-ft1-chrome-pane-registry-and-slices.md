# 0021 — FT-1 chrome: UI pane registry, tab bar, and slice sequencing

- Status: Accepted
- Date: 2026-08-15

## Context and problem statement

FT-1's gate is open ([#58](https://github.com/psmfd/FingerTrap/issues/58)):
FT-0 is complete and `pi_config`'s repo-dash is landed and in daily use. The
phase absorbs three retired milestones — M2 pane chrome, M5 status providers,
M6 palette/keymap — which is too much surface for one PR or one design pass.
This ADR decides the architecture that every FT-1 slice builds on, and the
order the slices land in, so that later slices extend earlier ones instead of
reworking them.

What exists today is deliberately minimal. The UI
([`src-ui/src/main.ts`](../src-ui/src/main.ts), 108 lines, no framework) hosts
exactly one xterm.js terminal in one hard-wired `#terminal` element, spawns one
session at startup, and never closes it. The sidecar is already session-keyed —
every `pty/*` message carries a `sessionId` — so multiplicity is a UI problem,
not a protocol redesign. The RPC surface is 4 requests + 2 notifications,
count-paired between `RpcSurface.cs` and `api.ts` by `check.sh` (ADR-0003).
Pane kinds and their fail-loud resolution are decided (ADR-0013); settings,
schema versioning, and precedence are decided (ADR-0014).

Constraints inherited from the milestone and from `pi_config`'s curated plan:

- Native surfaces use the sidecar's **own** clients (Octokit.NET,
  LibGit2Sharp, ADO), never pi's `github-read`.
- Credentials are **bounded static**: fine-grained read-only tokens with
  native expiry, held in the OS keychain, sidecar-side only. FT-3's minted
  leases are the terminus.
- repo-dash's paid-for lessons apply: the terminal/UI layer is not a
  sanitizing layer — untrusted GitHub/ADO text is cleaned at the data
  boundary; a workflow run's `conclusion`, not its `status`, carries
  success/failure; runs are keyed by API `id`, never `run_number`.
- FT-1's configurable keybindings must not create a third configuration
  mechanism next to settings and environment (the debt ADR-0014 exists to
  prevent).

## Considered options

**Slice order**

- O1 — Chrome first: panes/tabs/lifecycle, then status providers + surfaces,
  then palette/keymap.
- O2 — Surfaces first, chrome later (visible value early; chrome retrofitted).
- O3 — One integrated delivery.

**UI architecture for the chrome**

- U1 — Stay framework-free: vanilla TS modules, direct DOM, one xterm
  instance per pane.
- U2 — Adopt a component framework (React/Svelte/Lit) before adding chrome.

**Pane lifecycle ownership**

- P1 — UI-owned pane registry; the sidecar stays a session-keyed PTY host
  with no layout knowledge.
- P2 — Sidecar-owned layout/workspace service the UI mirrors.

**Closing a tab**

- C1 — Add a `pty/kill` request; closing a tab kills the session's process
  tree; a session that exits on its own marks its tab dead until closed.
- C2 — Tabs only ever detach; processes run until app exit.

## Decision outcome

Chosen: **O1 + U1 + P1 + C1**.

### O1 — chrome first

Surfaces and palette both *land in* chrome: a CI panel needs a pane to live
in; palette actions target the focused pi pane. Building surfaces first (O2)
means building them into the single hard-wired pane and moving them once
chrome exists. O3 is a month-scale PR nobody can review. Slices, in landing
order, each preceded by its own PR (and an ADR only where marked):

1. **Pane registry + tab bar** (this ADR is its design): multiple sessions,
   persistent tab bar, focus management, `pty/kill`, dead-tab state. Splits
   are **deferred to a later FT-1 slice** — tabs deliver the multiplexing
   value at a fraction of the layout complexity, and persisted layout (N-1's
   deferred half) wants splits to exist before it freezes a schema.
2. **Status providers + native surfaces** (own ADR: provider model,
   credential storage mechanics, sanitization boundary — the ADR-0022
   candidate). `IStatusProvider` (today an empty placeholder) gets its real
   contract there, not here.
3. **Command palette + keymap** (decision pre-made here: bindings live in the
   ADR-0014 settings file; see below).

### U1 — no framework

The chrome is a tab strip, a status bar, and a palette overlay — list-shaped
DOM with simple state. xterm.js is and remains the heavyweight component, and
it is framework-agnostic by design. Adopting React (U2) to manage a tab strip
imports a build-and-dependency surface far larger than the problem, and
`src-ui` today is 345 lines total. U1 is revisitable by supersession if slice
2's panels outgrow direct DOM — that is the explicit tripwire — but the
default posture stays dependency-light.

### P1 — the UI owns the pane registry

The sidecar already has the right shape: a `sessionId`-keyed PTY host with no
opinion about presentation. A `PaneRegistry` module in `src-ui` owns the
mapping `paneId → { sessionId, kind, Terminal, container, state }`, tab order,
and focus. P2 would put layout state behind RPC where every tab switch is a
round trip, and buys nothing until remote/multi-window scenarios exist
(FT-2+). If persisted layout later needs the sidecar to *store* layout, it
stores an opaque blob under settings — storage is not ownership.

Mechanics the registry pins down (the parts that cost a debug cycle if
guessed):

- One xterm `Terminal` + one container element per pane; inactive containers
  are hidden, **not** unmounted — xterm loses scrollback and cell metrics on
  unmount. On activation: show, `fit()`, focus. The existing
  first-`fit()`-after-layout rule from `main.ts` (divide-by-near-zero cols)
  applies to every newly shown container, so activation refits behind a
  `requestAnimationFrame`, matching the startup path.
- Output for hidden panes keeps flowing into their Terminal instances
  (xterm buffers); no UI-side buffering layer.
- The existing single-session `main.ts` flow becomes "registry with one
  initial pane" — the startup pane remains an unqualified spawn so the
  ADR-0013/0014 host-default chain keeps deciding what it is.
- New-tab actions: default new tab is another unqualified spawn (host
  default, i.e. pi unless configured otherwise); an explicit "new shell tab"
  action passes `kind: "shell"`. No action ever passes `kind: "pi"`
  explicitly — there is no way to ask for pi *harder*, and the omission keeps
  the sidecar's default authoritative.

### C1 — `pty/kill`, and exit is a state, not a disappearance

Closing a tab must end the process — C2 accumulates orphaned shells for the
lifetime of the app, invisibly. `pty/kill { sessionId }` joins the surface
(5 requests + 2 notifications; `RpcSurface.cs`, `api.ts`, and the ADR-0003
pairing tests all move together). Kill is request-scoped and idempotent: a
session that already exited returns success. The existing `pty/exit`
notification drives the other direction — a process that dies on its own
marks its tab **exited** (badge + message, scrollback intact) rather than
closing it, preserving ADR-0013's fail-loud posture: a pi that crashes at
startup leaves its error visible in the dead pane instead of vanishing.

### Keybindings: decided now, built in slice 3

Configurable bindings are a `keybindings` section in the ADR-0014 settings
file — not a separate file, not environment variables. Under ADR-0014's rules
this is additive within schema v1 (unknown keys are tolerated; version is the
compatibility gate). The UI cannot read the settings file (WebView), so slice
3 adds a `settings/get` request returning the **effective** settings the
sidecar already resolved — one reader, one precedence chain, per ADR-0014.
Defaults ship in code; the settings section overrides per-chord. This is
recorded here so slice 1 does not invent an interim mechanism ADR-0014 would
then have to absorb — the exact debt shape it was written against.

### Consequences

- Good: each slice lands reviewably; surfaces and palette arrive into chrome
  that already exists instead of forcing a retrofit.
- Good: the sidecar's contract barely moves in slice 1 (one request), and the
  ADR-0013/0014 fail-loud chain is untouched — chrome multiplies panes
  without re-deciding what a pane is.
- Good: keybindings have a decided home before any code wants one.
- Bad: splits and persisted layout are explicitly deferred again. Accepted:
  tabs-without-splits is a complete, shippable multiplexer; splits-without-
  design would be schema debt for N-1's layout persistence.
- Bad: no framework means the palette overlay and provider-driven status bar
  are hand-rolled DOM in slice 2/3. Accepted with a named tripwire (U1) for
  supersession.
- Neutral: `pty/kill` changes the paired RPC counts; the pairing tests make
  that a deliberate, visible edit.
- Neutral: the surfaces/credentials ADR (slice 2) is where Octokit/ADO client
  boundaries, keychain access, and sanitization mechanics get decided;
  restating the policy constraints here keeps them from being re-litigated
  there.
