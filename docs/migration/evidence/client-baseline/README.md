# CF-1 packaged-client baseline evidence

Status: **plan proposed; execution not started**. Foundation [#216](https://github.com/psmfd/FingerTrap/issues/216), baseline [#217](https://github.com/psmfd/FingerTrap/issues/217).

Read the [implementation plan](../../client-foundation-plan.html#cf1-plan) and
[product acceptance checklist](../../../apple-silicon-release-validation.md).
Discuss the CF-1 plan before implementing its harness or running acceptance.

## Inputs and prior evidence

The starting integration checkpoint is
`6fe2627d040b385fa96fdd91f7341c39214a112f`; all 19 post-merge checks passed.
Production runtime selection remains `v0.85.1-psmfd.1`. Resolve and independently
verify that exact supported executable before the real-Pi cases. Do not substitute
the 0.87.1 comparison artifact or silently promote the 1.3.0 candidate.

[artifact-reference.json](artifact-reference.json) records the prior #213 app
artifact's identities. Its archive/architectures/shell smoke were checked during
that campaign; this file does not constitute a new CF-1 verification or GUI pass.
Reverify the bytes and compare package-relevant source/build inputs before reuse.
Report its actual source identity, rather than attributing it to current dev.

## Mandatory worksheet

All rows are required for baseline acceptance. A blocked/not-run/skipped case is
never a pass. Record source/package/runtime identities for every row and retain
distinct results after any fix or artifact change.

| ID     | Case                                 | Required observation                                                                                                                                           | Initial status |
| ------ | ------------------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------- |
| CF1-01 | Artifact and guest inputs            | Archive/app/DMG/native architectures, exact supported Pi executable, source/build identities and clean guest/tool inventory verify.                            | NOT RUN        |
| CF1-02 | Clean installation and Finder launch | Usable app/pane before optional test tools are installed; record Gatekeeper prompts without disabling it globally.                                             | NOT RUN        |
| CF1-03 | Shell transport and panes            | Commands/output/resize work; split/close preserves sibling panes.                                                                                              | NOT RUN        |
| CF1-04 | Explicit Pi failures                 | Missing or invalid explicit Pi requests remain actionable; no silent fallback or misleading shell success.                                                     | NOT RUN        |
| CF1-05 | Real Pi PTY                          | Named runtime starts, renders, accepts input/resizes and closes; record its exact identity.                                                                    | NOT RUN        |
| CF1-06 | Native Pi RPC                        | Actual client prompt/stream/abort/dialog/resume behavior works; distinguish fixtures from real provider turns.                                                 | NOT RUN        |
| CF1-07 | Supported provider login             | Test credential entered through the supported guest path enables an authenticated minimal turn; no credential is copied from the host or recorded.             | NOT RUN        |
| CF1-08 | Keychain lifecycle                   | Test credential save/relaunch/read/clear/relaunch behaves correctly; test values never enter evidence.                                                         | NOT RUN        |
| CF1-09 | Repeat/startup/quit launch           | Existing window/session survives repeat launch, minimized/hidden states and startup races; one app/sidecar; launch during quit preserves shutdown.             | NOT RUN        |
| CF1-10 | Normal shutdown                      | App, sidecar and recorded owned shell/Pi descendants terminate; siblings/unrelated processes remain unaffected.                                                | NOT RUN        |
| CF1-11 | Hung-sidecar watchdog                | Stop only the recorded owned sidecar; app exits within its twenty-second watchdog plus recorded scheduling allowance; independently inspect/clean descendants. | NOT RUN        |
| CF1-12 | Crash, recovery and upgrade          | Guest-created sessions/settings survive supported recovery/prior-app upgrade; exact prior/new bytes and final owned process/VM cleanup are recorded.           | NOT RUN        |

A sidecar-only smoke bypasses the Tauri bridge. It supplements these observations.
Run the existing packaged smoke with `--require-pi` after the guest has the named
baseline runtime; a missing executable must fail. Synthetic fixtures may support
deterministic transport checks, but do not establish GUI/provider/login success.

## Required run record

- UTC run ID and exact app source/build/archive/app/DMG identities.
- Pi source/tag and executable digest; previous-app identity for upgrade.
- Tart version, base identity, guest macOS/build/architecture and preinstalled tools.
- Per-row expected/actual behavior, PASS/FAIL/BLOCKED/NOT RUN status, sanitized
  evidence references and defect links.
- Fixture versus real provider distinction; provider/model/login path without
  credential values, sensitive prompts or raw provider responses.
- Owned process identities before/after failures, cleanup observations and
  independently verified guest removal/host-state preservation.

Private binaries, raw logs and screenshots stay under ignored
`.artifacts/client-baseline/<run-id>/`. Commit reviewed sanitized manifests,
results, required excerpts and essential fixtures here. No raw live auth,
operator configuration, sessions, or global-home evidence is imported.

## Completion states

**Baseline captured** means every mandatory row has an observed result or an
explicit blocker with exact identities and evidence. It may contain failures.

**Baseline accepted** additionally requires mandatory rows to pass on the
selected bytes, blocking defects to have bounded fix/regression/retest evidence,
independent cleanup and recorded maintainer acceptance. Do not close #217 or
claim acceptance merely because a report exists.

A changed artifact receives new identities and affected retests. An optional
Pi 1.3.0 comparison is a separate named run, not a production pin change.
Signing/notarization, runtime release/pin, SDK adoption and managed activation
retain their existing gates.

## Resume

Discuss the proposed [CF-1 plan](../../client-foundation-plan.html#cf1-plan).
No CF-1 run, harness implementation, client correction or activation has occurred.
