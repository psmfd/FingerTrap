# Single-instance activation and Apple Silicon release scope

- Status: Accepted
- Resolves: #102 and the macOS layout documentation mismatch in #112
- Supersedes: ADR-0010's macOS Frameworks placement only

## Context and problem statement

A second macOS app instance can start a sidecar without bringing up a usable
pane. FingerTrap is the operator's persistent Home window, so a repeated launch
should activate that window and preserve its sessions.

## Considered options

- Support concurrent app instances and separate their WebKit and recovery state.
- Enforce a single instance and activate the existing window.

## Decision outcome

Register Tauri's single-instance plugin first, before sidecar initialization.
Its callback queues window activation on the main thread: show the app/window,
unminimize and focus it. Preserve a pending activation until Ready if the first
window is not yet available. Ignore activation once shutdown begins. Repeated
launch arguments do not become commands, session changes or new panes.

The next packaged release targets macOS Apple Silicon only (`osx-arm64` sidecar,
`aarch64-apple-darwin` shell). Intel Macs are unsupported for the foreseeable
future. Existing Linux and Windows CI remains regression coverage, not a promise
of packaged release support. Signing, notarization and auto-update remain N-2
work; this change does not expand that milestone.

Retain the working macOS companion-library placement: `bundle.macOS.files`
places `libporta_pty.dylib` in `Contents/MacOS/` beside the sidecar. This supersedes
ADR-0010's proposed Frameworks layout; Linux resource/RPATH behavior is unchanged.

## Known limitations and deferred work

- Native focus and LaunchServices behavior require a packaged Apple Silicon
  smoke test; a mock window cannot certify them.
- A launch during shutdown does not cancel exit or automatically start another
  app. The operator can relaunch once shutdown completes.
- Different bundle identifiers remain different applications. Cross-version
  installations using the same identifier must be included in release smoke.
