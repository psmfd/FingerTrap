# 0023 — Status row links and the URL-open policy

- Status: Accepted
- Date: 2026-08-16

## Context and problem statement

Operator QA of the ADR-0022 status panel surfaced the obvious next need:
rows are inert text, so there is no way to get from "CI run failed" to the
run itself. The rows should open in the browser. But every row is built
from remote API data, and "click opens a URL the API supplied" is a
classic injection surface: a compromised or spoofed API response could
plant `file:`, `javascript:`, or an attacker-host `https:` URL behind an
innocent-looking title. ADR-0022's sanitization boundary covers text
rendering; it deliberately did not decide a navigation/open policy.

The WebView itself is locked down (ADR-0022 CSP: `connect-src` pinned,
no remote origins), and that property must survive this feature.

## Considered options

- **A — `<a href>` links in the panel.** Rejected: turns row data into
  WebView navigation, exactly the surface the CSP exists to close, and
  in-WebView navigation is wrong for an app shell anyway.
- **B — expose the official opener plugin's JS API to the WebView.**
  Rejected: `opener:allow-open-url` grants the WebView the general
  "open anything" primitive; a scoped grant helps but the policy would
  live in a capability file rather than testable code.
- **C — a dedicated `open_url` command with a validated allowlist,
  opener plugin used only from Rust.** Chosen.

## Decision outcome

Chosen: **C** — links flow *sidecar-validated → shell-revalidated → OS
default browser*. The WebView never navigates and never holds a general
open primitive.

- **Sidecar (first gate):** row DTOs gain an optional `Url`, populated at
  row construction — the same place text is sanitized — only if the
  candidate passes `StatusUrls.Validate`: absolute `https:`, empty
  userinfo, host exactly on the provider's allowlist (`github.com`,
  `dev.azure.com`). A failing URL becomes `null` (the row renders
  unlinked), never an error. ADO URLs are not taken from the API at all;
  they are constructed from the already-validated org/project and the
  work-item id.
- **Shell (second gate):** the `open_url` Tauri command re-runs the same
  scheme/userinfo/host checks in Rust before calling the opener plugin's
  Rust API. The WebView is untrusted by definition here — a compromised
  renderer must not be able to open arbitrary URLs by skipping the
  sidecar. The opener plugin is registered but **no opener permissions
  are granted** in `capabilities/`, so its JS surface is unreachable.
- **UI:** linked rows render as `<button>` (textContent-only, per
  ADR-0022) invoking `open_url`; never `<a href>`.

The two validators are small enough to keep in lockstep by test, not by
shared code (they live on opposite sides of an IPC boundary and in
different languages — the same accepted shape as the frame-ceiling pair).

### Consequences

- Good: a hostile URL in API data degrades to an unlinked row; a
  compromised WebView still cannot open anything off-allowlist.
- Good: policy is ordinary testable code on both sides.
- Bad: a new provider host means touching both allowlists (test-pinned).
- Accepted gap: `open_url` hands the URL to the OS default browser;
  what happens beyond that handoff is the browser's trust domain.
