# Apple Silicon candidate builds and Tart acceptance testing

- Status: Accepted
- Extends: ADR-0032's Apple Silicon release scope
- Supersedes: ADR-0011's macOS release-acceptance substrate only

## Context and problem statement

Compilation and unit tests do not prove that a packaged app can find its
sidecar and PTY library or behave correctly when launched from Finder. The
development workstation can also hide missing runtime dependencies.

## Considered options

- Treat existing compilation and unit-test CI as the release gate.
- Install a candidate directly on the developer workstation.
- Build on macOS CI, then validate installation and interaction in a clean Tart VM.

## Decision outcome

Build an Apple Silicon `.app` and DMG using a pinned Tauri CLI on a macOS runner.
Verify every packaged native component is arm64 and run the existing sidecar
smoke against the executable inside the app. Archive checksums, build identity,
the smoke log and acceptance instructions as temporary Actions artifacts.

Use Tart on the operator's Apple Silicon workstation for macOS release
acceptance. Install the prebuilt artifact in a disposable VM so a full compiler
toolchain is unnecessary. Reuse an existing clean base image when available.
Do not introduce a persistent self-hosted runner or VM orchestration service.
Existing SmolVM Linux verification recipes remain applicable.

Tart runs on the workstation, not nested inside GitHub's hosted macOS runner.
The interactive checklist covers Finder, Tauri transport, real pi, keychain,
activation, recovery and shutdown. A skipped real-pi smoke is not acceptance;
`--require-pi` makes that gate explicit when testing with pi installed.

## Consequences

- Bundle layout and architecture failures become reviewable CI failures.
- Clean guest acceptance requires workstation access and a macOS image;
  downloading an uncached image can dominate initial setup time.
- Headless sidecar checks cannot certify GUI behavior. Manual results remain
  required against the exact archived candidate commit.
- Artifacts are unsigned and unnotarized. Signing, version reconciliation and
  release promotion remain separate gates; the workflow publishes no release.
