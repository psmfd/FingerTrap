# CF-1 packaged-client baseline evidence

Status: **baseline captured with blockers; not accepted**. Foundation [#216](https://github.com/psmfd/FingerTrap/issues/216), baseline [#217](https://github.com/psmfd/FingerTrap/issues/217).

Read the [implementation plan](../../client-foundation-plan.html#cf1-plan) and
[product acceptance checklist](../../../apple-silicon-release-validation.md).
The maintainer approved execution after selecting the production-only scope.
Individual acceptance results remain separate from harness implementation.

## Current result — 2026-10-22

[Verified inputs](inputs.json) and [case results](results.json) record run
`20261022T175149Z-c22f92748f50`: **one PASS, eleven BLOCKED**. Both disposable
guests were removed and all original VMs preserved, independently rechecked.
The stock macOS 15.7.7 base contains developer tools; its guest-only Homebrew
and Command Line Tools trees were moved aside before the Finder attempt.
This is not evidence of an untouched toolchain-free image.

Finder AppleEvents timed out before installation in both headless and visible
console attempts. A separate LaunchServices probe started the app and sidecar,
but screenshots did not establish a usable window. UI automation also timed out.
This remains a test-substrate blocker, without an established app defect.
The final GUI-context follow-up reached the already-cleaned guest and did not run.
Supported provider credentials and exact prior-app upgrade bytes are also missing.

[Supplementary packaged smoke](packaged-smoke.json) passes shell echo, real
production Pi PTY output and Pi child termination through the packaged sidecar
with a temporary, keyless HOME. See the [sanitized excerpts](packaged-smoke.txt).
It bypasses the Tauri bridge and passes no GUI/provider row.
The [guard red/green proof](guard-red-green.txt) removes archive traversal and
GUI-observation guards in isolated module copies, proves failures, and then
passes all 15 kept tests with the unchanged harness.
The [protected source CI snapshot](source-ci.json) records all 22 successful
checks on f86413f and four cleaned local macOS CI guests. Final documentation
head/merge checks are recorded in #220/#217; none passes a blocked GUI row.

## Inputs and prior evidence

The starting integration checkpoint is
`6fe2627d040b385fa96fdd91f7341c39214a112f`; all 19 post-merge checks passed.
Production runtime selection remains `v0.85.1-psmfd.1`. Resolve and independently
verify that exact supported executable before the real-Pi cases. Do not substitute
the 0.87.1 comparison artifact or silently promote the 1.3.0 candidate.
The maintainer selected this production baseline only for CF-1. Pi 1.3.0
comparison is outside this item and deferred in
[#219](https://github.com/psmfd/FingerTrap/issues/219).

[artifact-reference.json](artifact-reference.json) records the prior #213 app
artifact's identities. Its archive/architectures/shell smoke were checked during
that campaign; this file does not constitute a new CF-1 verification or GUI pass.
Reverify the bytes and compare package-relevant source/build inputs before reuse.
Report its actual source identity, rather than attributing it to current dev.

## Mandatory worksheet

All rows are required for baseline acceptance. A blocked/not-run/skipped case is
never a pass. Record source/package/runtime identities for every row and retain
distinct results after any fix or artifact change.

| ID     | Case                                 | Required observation                                                                                                                                           | Current status |
| ------ | ------------------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------- |
| CF1-01 | Artifact and guest inputs            | Archive/app/DMG/native architectures, exact supported Pi executable, source/build identities and clean guest/tool inventory verify.                            | PASS           |
| CF1-02 | Clean installation and Finder launch | Usable app/pane before optional test tools are installed; record Gatekeeper prompts without disabling it globally.                                             | BLOCKED        |
| CF1-03 | Shell transport and panes            | Commands/output/resize work; split/close preserves sibling panes.                                                                                              | BLOCKED        |
| CF1-04 | Explicit Pi failures                 | Missing or invalid explicit Pi requests remain actionable; no silent fallback or misleading shell success.                                                     | BLOCKED        |
| CF1-05 | Real Pi PTY                          | Named runtime starts, renders, accepts input/resizes and closes; record its exact identity.                                                                    | BLOCKED        |
| CF1-06 | Native Pi RPC                        | Actual client prompt/stream/abort/dialog/resume behavior works; distinguish fixtures from real provider turns.                                                 | BLOCKED        |
| CF1-07 | Supported provider login             | Test credential entered through the supported guest path enables an authenticated minimal turn; no credential is copied from the host or recorded.             | BLOCKED        |
| CF1-08 | Keychain lifecycle                   | Test credential save/relaunch/read/clear/relaunch behaves correctly; test values never enter evidence.                                                         | BLOCKED        |
| CF1-09 | Repeat/startup/quit launch           | Existing window/session survives repeat launch, minimized/hidden states and startup races; one app/sidecar; launch during quit preserves shutdown.             | BLOCKED        |
| CF1-10 | Normal shutdown                      | App, sidecar and recorded owned shell/Pi descendants terminate; siblings/unrelated processes remain unaffected.                                                | BLOCKED        |
| CF1-11 | Hung-sidecar watchdog                | Stop only the recorded owned sidecar; app exits within its twenty-second watchdog plus recorded scheduling allowance; independently inspect/clean descendants. | BLOCKED        |
| CF1-12 | Crash, recovery and upgrade          | Guest-created sessions/settings survive supported recovery/prior-app upgrade; exact prior/new bytes and final owned process/VM cleanup are recorded.           | BLOCKED        |

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

A changed artifact receives new identities and affected retests. CF-1 includes
no Pi 1.3.0 comparison run; #219 requires its own plan after the production
baseline is established.
Signing/notarization, runtime release/pin, SDK adoption and managed activation
retain their existing gates.

## Resume

Arrange an observable interactive guest session and supported test login, and
select exact prior-app bytes for upgrade. Repeat the blocked rows on the
verified production runtime. Do not advance to CF-2 or close #217 yet.

The harness can prepare a fresh run from preserved verified archives:

```bash
python3 scripts/qualify-client-baseline.py prepare \
  --archive .artifacts/pr-merge-20261021/golden-fix-apple/candidate.zip \
  --runtime-archive .artifacts/client-baseline/inputs/pi-darwin-arm64-v0.85.1-psmfd.1.tar.gz
python3 scripts/qualify-client-baseline.py guest --run <printed-run-directory> \
  --graphics --hold-seconds 1800
```

The second command owns the CI controller lock until it removes its unique
clone. It refuses another running VM. Only verified QA inputs are shared
read-only under `/Volumes/My Shared Files/qa`; host home and clipboard are not
shared, and this clean-launch session has offline networking. Entering a
provider credential requires a separately recorded supported guest/network
setup; this offline session cannot establish authenticated acceptance.

In the guest, before optional test tools, open the verified DMG in Finder,
copy the app to Applications and launch it. Inventory preinstalled tools; if
repeating the prior tool-disabled condition, move only the guest's
`/opt/homebrew` and `/Library/Developer/CommandLineTools` to the recorded
`ft-qa-held-*` sibling paths and recheck availability. Never run these guest
preparation actions on the host. Record the actual preparation and prompts.

Keep private observations inside the printed run directory, then use `record`
with its `--case`, `--status`, `--observation`, `--evidence`, `--observer` and
`--mode` options. GUI passes require `--mode observed-gui`. `summary --run`
reports capture/readiness and always leaves maintainer acceptance explicit.
Create `finish-session` in the run directory to end observations early. The
guest is also bounded by its hold deadline and normal interruption cleanup;
`cleanup --run` recovers a recorded owned clone after an abnormal interruption.

Reproduce harness verification with:

```bash
python3 -m unittest discover -s scripts/tests -p 'test_qualify_client_baseline.py' -v
ruff check scripts/qualify-client-baseline.py scripts/tests/test_qualify_client_baseline.py
```

No baseline acceptance, production pin change or activation is claimed. See
STATUS and the latest WORK_LOG entry for the PR/CI checkpoint.
