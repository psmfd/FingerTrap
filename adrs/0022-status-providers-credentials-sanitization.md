# 0022 — Status providers: sidecar clients, shell-owned credentials, sanitization boundary

- Status: Accepted
- Date: 2026-08-15

## Context and problem statement

FT-1 slice 2 (ADR-0021) delivers native issues/PR/CI status surfaces through
the sidecar's own clients — never pi's `github-read` — plus the Azure DevOps
and local-git equivalents. `IStatusProvider` is an empty placeholder; this ADR
gives it its contract, and decides the four things ADR-0021 explicitly
deferred here: client libraries, credential storage mechanics, the
sanitization boundary, and the data-flow shape over the stdio channel.

Standing constraints (pi_config curated plan, `docs/milestones.md`):
credentials are **bounded static** — fine-grained read-only PATs with native
expiry, OS-keychain storage, never reaching the WebView; FT-3's minted leases
are the terminus. repo-dash's paid-for lessons apply: sanitize untrusted text
at the data boundary, keep a run's `status` and `conclusion` unmerged (a
`completed` run can be a failure), key runs on API `id` never `run_number`.

Two facts discovered during research bear directly on the design:

- **macOS Keychain ACLs bind to code-signing identity, per binary.** The
  sidecar is a separate Mach-O signed under (eventually) the same Team ID but
  with its own identity: a grant to the app does not cover the sidecar, so a
  sidecar that reads the keychain directly earns its own prompt — and, while
  the sidecar's publish is not byte-stable across builds, a fresh prompt per
  rebuild. The Rust shell binary is the identity users' Gatekeeper trust
  decision is actually about.
- **`csp: null` (current `tauri.conf.json`) leaves the WebView's native
  `fetch`/`XMLHttpRequest` completely unrestricted.** Tauri capabilities gate
  `invoke()` commands only. The moment untrusted GitHub/ADO text renders in
  the DOM, an XSS slip could exfiltrate directly — bypassing the
  sidecar-owns-HTTP architecture entirely.

## Considered options

**Credential store owner**

- A — Sidecar reads the OS keychain directly (`Devlooped.CredentialManager`,
  the GCM-derived .NET store wrapper — the best .NET option found).
- B — The Rust shell owns keychain access (`keyring` crate) and delivers the
  secret to the sidecar over the private stdio channel.
- C — A Tauri keyring plugin driven from the WebView. Rejected outright: both
  candidate plugins are unsuitable (the crates.io `tauri-plugin-keyring` is
  abandoned at 0.1.0/2024; the maintained fork is unpublished git-only), and
  it would put credential handles in the WebView's reach.

**Clients** — Octokit.NET vs raw `HttpClient` (GitHub); the
`Microsoft.TeamFoundationServer.Client` SDK vs plain REST (ADO); LibGit2Sharp
vs shelling out to `git` (local).

**Provider data flow** — per-item notifications vs debounced snapshot-replace.

## Decision outcome

Chosen: **B**, with **Octokit.NET / plain REST / `git` CLI**, and
**snapshot-replace** flow.

### B — the shell owns credentials; the sidecar only ever borrows them

The Rust shell stores and retrieves PATs via the `keyring` crate (v4.x,
active; macOS Keychain / Windows Credential Manager / Secret Service). On
sidecar spawn (and respawn), the shell pushes configured credentials into the
sidecar's stdin as a **`credentials/set` JSON-RPC notification**. That
direction matters: sidecar stdout is relayed wholesale to the WebView, but
stdin frames are visible only to the sidecar — a notification (no response)
means **no frame containing a secret ever travels toward the WebView**.

- Entry: the operator pastes the token into the UI once; it goes WebView →
  Rust `invoke` command → keyring, and is never sent back. Retrieval flows
  only shell → sidecar.
- `credentials/set` is excluded from any verbose/frame-dump logging on both
  sides, and the shell zeroizes its copy after handoff. The sidecar holds the
  token in memory only, rebuilds clients from it, and never caches it to disk.
  (.NET cannot promise more: `SecureString` is deprecated guidance-wise and
  string zeroing is theater — the honest contract is "brief plaintext
  lifetime, never persisted, never echoed", the same accepted-gap shape as
  ADR-0020's IPC-transit guardrail.)
- Option A was rejected on the macOS ACL-identity problem above, not library
  quality — `Devlooped.CredentialManager` is healthy and stays the fallback
  if shell-side ownership proves untenable. B also positions the shell as the
  credential broker FT-3's minted leases will need anyway.
- **Fail-closed store, stated bluntly:** if the platform store is unavailable
  (headless Linux with no Secret Service is the realistic case) or errors,
  the feature degrades to **off** with a named, remediable status — never a
  plaintext file, never a silent in-memory-only mode that trains the operator
  to re-paste tokens. Probed at startup, not discovered in an error path.
- macOS pre-N-2 accepted rough edge: the app is not yet signed, so keychain
  ACL grants re-prompt after rebuilds. Recorded here; fixed by N-2 signing
  (stable Designated Requirement), items stored
  `WhenUnlockedThisDeviceOnly` so nothing syncs off-device.

### Clients

- **GitHub: Octokit.NET** (zero transitive dependencies; fine-grained PATs
  are plain bearer tokens to it). Two costs budgeted, not discovered:
  conditional requests (`If-None-Match`/304) are manual — the provider keeps
  its own ETag store; and fine-grained PATs return **403, not 404**, for
  out-of-grant repos — rendered as "no access", distinct from "not found".
- **ADO: plain `HttpClient`** against the WIQL + work-items REST surface with
  source-generated `System.Text.Json` contexts (trim/AOT-neutral from day
  one). The official SDK is closed-source, dependency-heavy, and
  reflection-based — rejected.
- **Local git: shell out to `git`**, not LibGit2Sharp. LibGit2Sharp is
  healthy, but its native binaries recreate exactly the companion-library
  bundling problem ADR-0008/0010 already paid for once — not worth it for
  remote-URL/branch/ahead-behind reads. **Load-bearing pitfall:** every
  child `ProcessStartInfo` sets `RedirectStandardOutput` and
  `RedirectStandardError` — an unredirected child inherits the sidecar's
  stdout and corrupts the ADR-0002 JSON-RPC framing.
- Scope validation is **calibration-by-use**: on save, fire the exact read
  the feature makes and surface the API's own error; fine-grained PATs have
  no scope-introspection header. GitHub's token-expiry header is advisory
  only (community-documented unreliability); the authoritative signal is a
  401, surfaced as a named auth-failed state.

### Provider contract and data flow

`IStatusProvider` (sidecar): a provider polls its source on a bounded
interval with conditional requests, maps responses into FingerTrap-owned row
DTOs **at construction** (never a raw SDK model crossing `RpcSurface`), and
publishes into one merged **`status/snapshot` notification** — debounced,
snapshot-replace, so a noisy repo cannot flood the WebView and a dropped
notification costs nothing (the next snapshot supersedes). One new request,
`status/refresh`, forces a poll. Per repo-dash: separate row types per
surface (an issue row cannot carry a run's fields), `status`/`conclusion`
kept unmerged with a single pure derivation module where unrecognized
conclusions degrade to `unknown`, rows keyed by API `id`. The snapshot also
carries each provider's state — `ok`, `not-configured`,
`store-unavailable`, `auth-failed`, `expiring` — so the UI renders states,
not blanks.

The ADR-0003 pairing check gains a documented shell-originated allowlist:
`credentials/set` exists in `RpcSurface` with no `api.ts` counterpart by
design.

### The sanitization boundary — twice, deliberately

1. **Sidecar, at DTO construction:** strip all C0/C1 controls (ESC, BEL, CR,
   LF, CSI/OSC/DCS introducers) from every free-text field; strip Unicode
   BiDi controls (U+202A–202E, U+2066–2069, U+200E/F); cap field lengths and
   page sizes. A row that exists is safe by construction — the repo-dash
   rule.
2. **WebView, at the DOM call site:** provider strings are assigned via
   `textContent`/`createElement` only — `innerHTML` never, there being no
   framework auto-escaping (ADR-0021 U1). If links are ever rendered, hrefs
   are scheme-allowlisted.

Recorded now for slice 3, because it is a different class: palette actions
that inject references into the pi pane are **synthetic keystrokes into a
live process**. Injected text is sidecar-constructed from validated narrow
tokens (numbers, opaque URLs) — never raw titles — control-stripped, and
never carries a trailing newline; only the operator's own keypress submits.

### Channel hardening shipped alongside

- Both stdio readers reject any frame whose `Content-Length` exceeds a fixed
  ceiling (4 MB) as connection-fatal — neither StreamJsonRpc nor
  vscode-jsonrpc bounds this for us, and provider payloads now share the
  channel with PTY bytes. Data-boundary caps keep legitimate payloads far
  below it.
- `csp: null` is replaced with a baseline whose load-bearing line is
  `connect-src 'self' ipc: http://ipc.localhost` — the WebView cannot reach
  GitHub/ADO/anything remote even if scripted. Ships in this slice's first
  PR, before any provider text renders.

### Implementation order (each PR against this ADR)

1. CSP baseline + stdio `Content-Length` ceiling.
2. Shell keychain plumbing: `keyring` integration, token-entry command,
   `credentials/set`, provider-state surface in settings/UI.
3. GitHub provider + panel (Octokit.NET, ETag store, snapshot flow).
4. ADO and local-git providers on the same contract.

### Consequences

- Good: secrets never appear in any WebView-bound frame, and macOS keychain
  trust anchors to the one binary whose signing is already load-bearing.
- Good: the sanitization and outcome-derivation contracts are copied from a
  system (repo-dash) that already paid for these lessons in production.
- Good: every client choice is trim/AOT-neutral or better, protecting the
  deferred M8 migration.
- Bad: the PAT transits one extra process boundary and lives in two process
  memories. Accepted: the pipe is private, delivery is log-excluded, and the
  alternative (sidecar-direct keychain) buys ACL prompts and a weaker
  signing-identity story.
- Bad: manual ETag plumbing for Octokit.NET is real work a batteries-included
  client would have given us. Accepted for the zero-dependency surface.
- Neutral: sidecar crash/respawn must re-push credentials — the shell owns
  that lifecycle already (ADR-0021's kill-path work made exit observable).
- Neutral: headless-Linux operators get a degraded-off feature with a named
  remedy rather than a workaround. If that audience materializes, revisiting
  is its own ADR.
