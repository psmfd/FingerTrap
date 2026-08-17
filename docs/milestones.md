# Milestones

FingerTrap is sequenced as **two parallel tracks**.

The **Home track** (FT-0 … FT-3) delivers the pi Home interface — the shell the
operator lives in. Its phases and gates are defined by `pi_config`'s curated
feature plan (`notes/curated-feature-plan.md`, Track 6), which elevated
FingerTrap from a long-horizon item to the Home interface on 2026-08-11.
`pi_config` remains the governance home: the phase gates, the credential
policy, and the migration map live there. FingerTrap **renders and hosts; it
never owns policy, credential policy, or approval semantics**.

The **Native track** (N-1 … N-4) carries FingerTrap's own terminal features —
the ones that predate the Home framing and have no place in it. They are
sequenced separately so that elevating the Home work does not silently drop
them. See [ADR-0012](../adrs/0012-home-phase-resequencing.md) for why the plan
is shaped this way and how the original M-numbers map onto it.

The tracks interleave by capacity. Neither blocks the other, except where an
individual item says otherwise.

## Status at a glance

| Item | Track | State |
|---|---|---|
| M0 — Skeleton | foundation | **complete** |
| M1 — Local PTY (Linux + macOS) | foundation | **complete** (v0.3.0) |
| FT-0 — Revive + host | Home | **complete** ([#46](https://github.com/psmfd/FingerTrap/issues/46), PRs #47–#50, #53) |
| FT-1 — Chrome | Home | **in progress** ([#58](https://github.com/psmfd/FingerTrap/issues/58), ADR-0021; slices 1–3 landed — splits and slice-2 follow-ups [#70](https://github.com/psmfd/FingerTrap/issues/70)/[#72](https://github.com/psmfd/FingerTrap/issues/72) remain) |
| FT-2 — Structured control + observability | Home | not started |
| FT-3 — Tool host | Home | not started |
| N-1 — Settings and persistence | Native | **in progress** ([#51](https://github.com/psmfd/FingerTrap/issues/51); settings foundation landed, ADR-0014) |
| N-2 — Packaging | Native | partially standing (semantic-release wired) |
| N-3 — SSH terminal | Native | not started |
| N-4 — SFTP tree | Native | not started |

Windows remains deferred across the PTY layer: `pty/spawn` throws
`PlatformNotSupportedException` until a ConPty backend lands.

## Completed foundations

### M0 — Skeleton

Three-process scaffold. JSON-RPC `ping` round-trips between TS UI and .NET
sidecar through the Tauri shell. CI green on Windows, macOS ARM64, and
Linux. Initial ADRs (0001–0005) land.

**Acceptance:** `pnpm tauri dev` opens a window. The Rust shell spawns
`fingertrap-sidecar` as a child process. The TS UI calls `api.ping("hello")`
from a button click. The sidecar's `RpcSurface.PingAsync` returns
`"pong: hello"`. The reply renders in the window. CI is green on all three
runners. `scripts/check.sh` passes locally.

### M1 — Local PTY integration (Linux + macOS)

First real keypress end-to-end. `IPtyService` implementation spawns a
local shell over a pseudoterminal. xterm.js renders sidecar PTY output
via JSON-RPC notifications (`pty/output`). Keystrokes flow back through
`pty/write`. Resize is debounced on the .NET side and forwarded via
`pty/resize`. Sidecar emits `pty/exit` when the shell terminates.

The Linux backend uses direct libc P/Invoke (`posix_openpt` +
`posix_spawn` with `POSIX_SPAWN_SETSID`); see ADR-0006. The macOS
backend mirrors that shape with Darwin-specific constants and
`TIOCPTYGNAME` in lieu of `ptsname_r`; see ADR-0007. Windows is
deferred — `pty/spawn` throws `PlatformNotSupportedException` on
Windows until a ConPty backend lands.

**Acceptance (Linux + macOS):** `pnpm tauri dev` opens a window with a
shell prompt rendered in xterm.js. `ls` produces correct output.
Keystrokes echo. Window resize updates the PTY size and the prompt
redraws cleanly. CI is green on all three runners (sidecar/ui/tauri
matrices build everywhere; runtime PTY behavior is exercised
manually on Linux and macOS, automatically only at compile/link in
CI).

## Home track

### FT-0 — Revive + host

**Gate:** none. Startable at any time; everything else in the Home track waits
on it.

Re-sequenced milestone plan (this document). Toolchain refresh after the
2026-05 → 2026-08 dormancy. pi running in an xterm.js PTY pane as a
**first-class pane type** — a typed pane the app knows about, not "whatever
shell you get".

The bar is deliberately **functional parity with "pi in any terminal"**. The
point is the foundation, not features; chrome arrives in FT-1.

Tracked as [#46](https://github.com/psmfd/FingerTrap/issues/46) —
[#43](https://github.com/psmfd/FingerTrap/issues/43) (this re-sequence),
[#44](https://github.com/psmfd/FingerTrap/issues/44) (toolchain),
[#45](https://github.com/psmfd/FingerTrap/issues/45) (pi pane type).

**Acceptance:** `scripts/check.sh` passes and CI is green on all three
runners after the refresh; M1's acceptance still holds; a pi pane opens as a
distinct pane type; interactive pi is fully usable in it, including
`repo-dash`'s `ctx.ui.custom` overlay panels (`/issues`, `/prs`, `/ci`) as an
end-to-end check that the PTY is a real enough terminal for pi's TUI layer.

### FT-1 — Chrome

**Gate:** FT-0, **and** `pi_config`'s `repo-dash` landed and in use. The
repo-dash half is satisfied — landed 2026-08-13 (pi_config#986, ADR-0137),
Phase 2 `/ci` panel and idle-gated CI widget 2026-08-14 (ADR-0140),
branch-scoped (ADR-0141), shipped in pi_config v1.32.0 and in daily use from
2026-08-15.

That gate exists because repo-dash's data-layer and usage lessons de-risk the
native panels, so they are worth reading rather than merely counting: pi-tui
is not a sanitizing layer, untrusted GitHub text must be cleaned at the data
boundary, and a workflow run's `conclusion` — not its `status` — is what
carries success or failure.

Delivers native issues/PR/CI status surfaces through the sidecar's own
Octokit/ADO clients (**not** through pi's `github-read`); pane and tab chrome,
including the persistent tab bar; and command-palette actions that inject
references into the pi pane — the host-side counterpart of repo-dash's `c`-key
pattern.

Absorbs the pane chrome from the original M2 (splits, focus management, pane
lifecycle through `RpcSurface`), the whole of M5 (`IStatusProvider` plumbing —
Git via LibGit2Sharp, GitHub PRs via Octokit, Azure DevOps work items — with
the status bar rendering providers dynamically off provider events), and the
whole of M6 (global keymap, command palette on Cmd/Ctrl+P, configurable
bindings, commands invoking RPC methods or UI actions).

Credentials at this phase are **bounded static** under policy stated in
`pi_config`: fine-grained PATs or ADO equivalents, read-only scopes only,
native expiry required (no non-expiring tokens), OS-keychain-or-equivalent
storage. FT-3's minted leases are the terminus.

### FT-2 — Structured control + observability

**Gate:** FT-1, **and** an RPC-contract study note — in `pi_config`'s `notes/`
or this repo's `docs/` — enumerating the `pi --mode rpc` methods and events
FT-2 consumes, verified against the pinned pi version, with gaps filed as
issues.

The sidecar drives `pi --mode rpc`: session list and resume, worktree-orphan
surfacing, model and status readouts. Read-only observability dashboards over
`pi_config`'s meter JSONL files — **observe from outside**: FingerTrap
renders, the recorders stay in `pi_config`.

Also the native counterpart of the expertise drafts-review panel (`pi_config`
curated plan, Track 5 rung 4) — **display and intent only**. The single-use
approval ledger and its semantics stay in `pi_config`.

### FT-3 — Tool host

**Gate:** `pi_config`'s Track 3 Phase 2 — credential broker and grant
enforcement — live. Phase 1's engine alone is insufficient, because FT-3's
deliverable is a credential-holding backend.

Sidecar JSON-RPC methods exposed as digest-reviewed descriptors: the .NET
tool-authoring path. The sidecar becomes a credential-holding backend behind
the broker's discipline, using minted leases.

Descriptor authoring, review, digests, grants, and approval ledgers remain in
`pi_config`. FingerTrap hosts tool *backends*, never tool *authority*.

## Native track

FingerTrap's own terminal features. Ordered so that the two items other work
already depends on come first:
[#41](https://github.com/psmfd/FingerTrap/issues/41) depends on the settings
system, and packaging is what makes the app installable. SSH and SFTP are real
features, but nothing blocks on them.

### N-1 — Settings and persistence (was M7)

User config (theme, font, default shell, profiles). Persisted layout (panes,
sizes, focus). Settings UI. Settings stored in
`<app-data>/fingertrap/settings.json` with a versioned schema.

Note the overlap with the Home track: FT-1's configurable keybindings and
FT-0's pi-binary resolution both want somewhere to live. Either N-1 lands
first, or those phases carry interim configuration that N-1 absorbs — decide
at the time rather than assuming.

### N-2 — Packaging (was M8)

`tauri build` with signed installers:

- macOS: hardened runtime, notarization, JIT entitlement (or NativeAOT
  switch — see ADR-0005 successor)
- Windows: code signing (EV or OV)
- Linux: `.deb` and AppImage

Auto-update channel via Tauri updater. Sidecar publish revisited per
ADR-0005 successor (trimming/AOT migration).

Partially standing already: `semantic-release` runs on `main` and has cut
releases through v0.3.0. What remains is signing, notarization, installer
formats, and the update channel.

### N-3 — SSH terminal (was M3)

SSH.NET integration. `ISshService` owns SSH sessions and exposes an internal
accessor that N-4's SFTP service reuses — no second auth path. Connection
profiles persisted (encrypted at rest using OS keychain). Remote PTY
through xterm.js.

### N-4 — SFTP tree (was M4)

SFTP file browser sharing the SSH connection from N-3. Tree view in left
rail. File download and upload. Drag-and-drop into the active terminal pane.

Depends on N-3 — the shared-auth constraint above is the reason the two are
adjacent and ordered.

## M-number mapping

Existing issues and ADRs cite M-numbers. This table keeps those references
resolvable; the M-numbers themselves are retired.

| Was | Now | Note |
|---|---|---|
| M0 | complete | unchanged, retained above |
| M1 | complete | unchanged, retained above |
| M2 — local terminal panes | **split**: pane *type* → FT-0; splits, focus, lifecycle chrome → FT-1 | the typed-pane concept is FT-0's deliverable; the chrome around it is FT-1's |
| M3 — SSH terminal | **N-3** | |
| M4 — SFTP tree | **N-4** | |
| M5 — status providers | **FT-1** | already describes Octokit and ADO clients, which is precisely FT-1's brief |
| M6 — command palette and keymap | **FT-1** | |
| M7 — settings and persistence | **N-1** | [#41](https://github.com/psmfd/FingerTrap/issues/41) cites M7 |
| M8 — packaging | **N-2** | |
| M5+ (plugin host, per [#39](https://github.com/psmfd/FingerTrap/issues/39)) | unscheduled | that ADR is doc-only and unwritten; the host itself has no phase yet |
