# CF-1 packaged-client baseline evidence

## Repeated-launch evidence schema

`repeat-launch-observation.json` schema version 2 retains the observation,
run/tested identities, limitations and independent review unchanged. Its former
inline `privateEvidence` mapping is now stored in two version-1 manifests:
[part 1](repeat-launch-private-evidence-1.json) and
[part 2](repeat-launch-private-evidence-2.json), with 276 complete entries each.
`privateEvidenceManifests` lists paths relative to the summary, entry counts and
byte SHA-256 values. Each manifest contains `schemaVersion`, `runId` and the
`privateEvidence` mapping. `privateEvidenceProvenance` identifies the original
committed summary, its byte SHA-256 and the total 552 entries. Original SHA-256:
`0d6a946906cb9f574013a788ed2db41c27c7ea37426a8e6f63c1c5994b3b57fe`.

Consumer migration: version 1/unversioned records have inline `privateEvidence`;
for this version-2 record, load and verify both referenced manifests before using
their disjoint union. No existing repository script consumes this observation
mapping (repository search at conversion); existing summary links remain valid.
Other evidence schemas stay unchanged. See [evidence guidance](../README.md) for
the preferred pattern when large mappings obstruct review. Do not infer complete
GitHub or CodeRabbit review from file sizes alone.

Reproduce the preservation check from the repository root without opening private
evidence or re-running a VM:

```bash
python3 - <<'PY'
import hashlib
import json
from pathlib import Path
import subprocess

if not __debug__:
    raise RuntimeError("Run verification without -O or PYTHONOPTIMIZE")

def unique_pairs(pairs):
    result = {}
    for key, value in pairs:
        assert key not in result, f"duplicate JSON key: {key}"
        result[key] = value
    return result

def load(raw):
    return json.loads(raw, object_pairs_hook=unique_pairs)

p = Path("docs/migration/evidence/client-baseline/repeat-launch-observation.json")
summary = load(p.read_bytes())
assert summary["schemaVersion"] == 2
provenance = summary["privateEvidenceProvenance"]
original = subprocess.check_output([
    "git", "show", provenance["originalCommit"] + ":" + provenance["originalPath"]
])
assert hashlib.sha256(original).hexdigest() == provenance["originalSha256"]
old = load(original)
combined = {}
assert len(summary["privateEvidenceManifests"]) == 2
for ref in summary["privateEvidenceManifests"]:
    raw = (p.parent / ref["path"]).read_bytes()
    assert hashlib.sha256(raw).hexdigest() == ref["sha256"]
    manifest = load(raw)
    assert manifest["schemaVersion"] == 1
    assert manifest["runId"] == summary["runId"]
    entries = manifest["privateEvidence"]
    assert len(entries) == ref["entryCount"] == 276
    assert not combined.keys() & entries.keys(), "overlapping manifests"
    combined.update(entries)
assert len(combined) == provenance["entryCount"] == 552
assert combined == old.pop("privateEvidence")
added = {"schemaVersion", "privateEvidenceManifests", "privateEvidenceProvenance"}
assert {k: v for k, v in summary.items() if k not in added} == old
print("PASS: 552 exact entries; unchanged observation and review")
PY
```

## Observations

Selected #227 order completed: [repeated launch](repeat-launch-observation.json)
on 1b50acc, then [fresh Quit rerun](normal-quit-branch-rerun.json) on a3955ec.
Same probe code/tree; 552 + 472 private hashes, both owners exit 0 and cleanup
verify. All 77 guards pass. Readiness is for maintainer review, never merge or
baseline promotion; permission-dialog limitation and all gates remain explicit.

[Repeated normal launch](repeat-launch-observation.json) verifies stable process,
window and proven shell identities with fresh second input, automatic completion
and cleanup. 552 private-file hashes; the selected fresh Quit rerun follows on
the same branch before #227 readiness. No baseline promotion.

[Normal GUI Quit observation](normal-quit-observation.json) verifies recorded
app/sidecar/shell termination while an unrelated guest process survives, then
automatic completion and owned cleanup with exit 0. 472 private-file hashes;
73 kept guards. Shell-default fixture and automation permission-dialog limitation
remain explicit; no baseline promotion or full acceptance claim.

[Review corrections](review-corrections.json) retain the 67-guard recovery fix,
fresh layout proofs, observer deadline after result completion and verified owned
cleanup, with 652 private-file hashes. No baseline promotion.

Latest CF-1C-AUTOMATION-4/4B observes shell resize, right split and sibling-only
close under a disclosed supported guest shell-default fixture and 50ms cadence.
Original PID/sentinel and PTY dimensions survive; sibling termination independently
verified. A longer typing-timeout attempt is retained alongside compact-command
success in [layout comparison](shell-layout-comparison.json). Baseline remains
one input PASS/eleven BLOCKED pending review; all existing gates remain intact.

Latest CF-1C-AUTOMATION-3/3B verifies one shell command/output at 50ms typing
cadence with exact independent guest-file proof and decoded output in native/Bridge
frames. Default cadence lost at least one delimiter and correctly failed the proof
guard; both attempts/cleanup retained in [shell comparison](shell-input-comparison.json).
Corrected main-window admission now passes live. This remains partial CF1-03;
no baseline row promoted, one input PASS/eleven BLOCKED and all gates unchanged.

Latest CF-1C-AUTOMATION-2 observes a real packaged-client window through the
signed GUI Bridge, with three uncached AX snapshots and independent native/Bridge
frames. Original window-count guard blocked on an off-screen helper; corrected
classification is verified by retained-evidence replay, not a repeated launch.
See [launch observation](client-launch-observation.json). No baseline row promoted;
Pi v0.85.1-psmfd.1 and all acceptance gates remain unchanged.

Status: **baseline captured with blockers; not accepted**. Foundation [#216](https://github.com/psmfd/FingerTrap/issues/216), baseline [#217](https://github.com/psmfd/FingerTrap/issues/217).

Read the [implementation plan](../../client-foundation-plan.html#cf1-plan) and
[product acceptance checklist](../../../apple-silicon-release-validation.md).
The maintainer approved execution after selecting the production-only scope.
Individual acceptance results remain separate from harness implementation.

## GUI continuation — CF-1C-CAPTURE-3, 2026-11-07

[Privacy attribution](gui-continuation/privacy-attribution.json) preserves a
failed initial signing-readout run and corrected read-only retry. TCC explicitly
fails an existing Screen Recording code requirement for the resolved guest-agent
binary, status -67050, then denies capture. Signing display reports a.out/linker
ad hoc metadata but exits 1 for designated requirement; do not call it entirely
unsigned. Same executable digest as the [verified normal restart](gui-continuation/agent-restart-comparison.json).
This is an observed denial; exact-entry re-enrollment remains proposed/unproven.

Both guests cleaned; five originals stopped/preserved, no clone/QA processes.
No permission, signing, binary or host setting changed in CAPTURE-3. Raw logs
remain private; sanitized metadata retains 33 file hashes. All 38 guards, Ruff
and repository checks pass. Current implementation CI/review pending.
No app row changed: one input PASS, eleven BLOCKED. Next: CF-1C-CAPTURE-4 supported
exact-entry UI re-enrollment with post-grant TCC and actual window-capture checks.
[Handoff](../../HANDOFF-client-baseline-gui.md) contains commands and gates.
Previous evidence is preserved: [grant](gui-continuation/permission-comparison.json),
[Go-menu](gui-continuation/go-menu-comparison.json),
[atomic comparison](gui-continuation/atomic-comparison.json).

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

## Finder installation and launch — 2026-11-18

[Observed CF1-01 and CF1-02 outcomes](finder-install-observation.json) use the
verified #225 candidate and a fresh, owned macOS 15.7.7 Tart guest. Finder
installed and opened FingerTrap. The normal shell control opened a usable
pane and displayed an entered command's result. Cleanup verified. The stock
guest had developer tools already present. No optional tools or Pi were added,
and no Gatekeeper prompt appeared in captured frames. The candidate is unsigned
and unnotarized. Current worksheet status is two PASS and ten BLOCKED.
Earlier run snapshots below and above retain their historical statuses.

## Shell layout and explicit Pi failures — 2026-11-18

[Observed CF1-03 and CF1-04 outcomes](shell-layout-pi-failures.json) use the
same verified #225 build in a separate owned Tart guest. Native output and
independent process checks establish shell resize and sibling-only close.
Explicit missing Pi showed actionable guidance. A non-executable configured
Pi path produced only exit code 13, so CF1-04 fails the actionable diagnostic
requirement. The app did not create a fallback shell. Cleanup verified.
Current worksheet status is three PASS, one FAIL and eight BLOCKED.

## Explicit Pi path correction — 2026-11-18

[Observed CF1-04 correction](pi-path-correction-observation.json) passed on
verified Apple Silicon draft #230 bytes built by a local Tart runner. In a
separate fresh Tart guest the explicit missing-Pi request and both configured
file cases showed what to correct. No fallback shell appeared, and the
ordinary shell worked. Owner exit and independent cleanup verified. The prior
failure on PR #225 bytes remains recorded above. Worksheet evidence totals four PASS
and eight BLOCKED. CF1-04 is only verified on the unmerged candidate.

[CF1-04 retest after the Linux CI correction](pi-path-drain-retest-observation.json)
passed on verified draft #230 artifact 14442208012 in a separate fresh Tart
guest. Both formerly failing hosted Linux lanes and all four local Tart macOS
CI lanes passed. The separate package job also passed in local Tart. The
guest and runners were removed and no fallback shell was observed.

[CF1-04 CodeRabbit retest](pi-path-review-retest-observation.json) passed on
verified #230 artifact 14445949024. An inaccessible directory and an owned
mode-0401 file gave distinct actionable diagnostics without a fallback shell.
The symlink target launched and its ordinary exit 13 reached the Pi tab.
Exact-head CI passed 14/14. Local Tart guest and runner cleanup verified.

[CF1-05 real Pi PTY](real-pi-pty-observation.json) passed on integrated dev
artifact 14456220605 at `34a755a`, using Pi `v0.85.1-psmfd.1`. Packaged
`--require-pi` smoke passed. Offline Pi rendered `/hotkeys`; its TTY changed
from 39×126 to 39×61 with the same process. Closing the Pi pane removed its
process and both shells survived. Cleanup verified. Worksheet evidence is
five PASS and seven BLOCKED.

[CF1-06 native Pi RPC](native-pi-rpc-observation.json) passed on integrated
artifact 14456220605 with real Pi `v0.85.1-psmfd.1` and a guest loopback
model. Native GUI streaming, abort and recovery, confirmation, and Session
Browser resume all had independent receipt evidence. App, sidecar, Pi and
fixture exited, and both owned guests were removed. Worksheet evidence is
six PASS and six BLOCKED. CF1-07 covers authenticated provider login.

To reproduce CF1-06, use the artifact and runtime identities in the receipt
and a new owned guest. Install the full Pi distribution and set FingerTrap's
`pi.path` to its verified executable. In the guest, create `/Users/admin/.ft-cf106`
and start the [loopback model](native-rpc-fixtures/model.py) with that directory as
its argument. Use the `port` receipt and the golden recorder's `canned` provider
schema to set `~/.pi/agent/models.json` to `http://127.0.0.1:<port>/v1`. Set the
default provider and model to `canned` and `canned-model`, install the
[confirmation fixture](native-rpc-fixtures/confirm.ts) in `~/.pi/agent/extensions`,
and launch FingerTrap with `PI_OFFLINE=1`. The fixture uses a synthetic key.

Submit `cf106 stream` in the native RPC pane. Create `release-stream` in the
fixture directory only after the first chunk is visible. Submit `cf106 abort`,
wait for its first chunk, click Abort, and check the guest fixture's disconnect
receipt before `cf106 recover`. Submit `cf106 dialog` and answer **yes** in the
GUI. Verify the dialog receipt precedes the fixture's model request. Close the
pane, use Session Browser's **resume (rpc)**, and submit `cf106 resume`. Check
that the same session file gains the final marker and that the original Pi PID
was replaced. Quit the app, stop the identified fixture, and independently
verify process exit and removal of the owned clone.

## Mandatory worksheet

All rows are required for baseline acceptance. A blocked/not-run/skipped case is
never a pass. Record source/package/runtime identities for every row and retain
distinct results after any fix or artifact change.

| ID     | Case                                 | Required observation                                                                                                                                           | Current status        |
| ------ | ------------------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------- |
| CF1-01 | Artifact and guest inputs            | Archive/app/DMG/native architectures, exact supported Pi executable, source/build identities and clean guest/tool inventory verify.                            | PASS                  |
| CF1-02 | Clean installation and Finder launch | Usable app/pane before optional test tools are installed; record Gatekeeper prompts without disabling it globally.                                             | PASS                  |
| CF1-03 | Shell transport and panes            | Commands/output/resize work; split/close preserves sibling panes.                                                                                              | PASS                  |
| CF1-04 | Explicit Pi failures                 | Missing or invalid explicit Pi requests remain actionable; no silent fallback or misleading shell success.                                                     | PASS (#230 candidate) |
| CF1-05 | Real Pi PTY                          | Named runtime starts, renders, accepts input/resizes and closes; record its exact identity.                                                                    | PASS                  |
| CF1-06 | Native Pi RPC                        | Actual client prompt/stream/abort/dialog/resume behavior works; distinguish fixtures from real provider turns.                                                 | PASS (guest loopback) |
| CF1-07 | Supported provider login             | Test credential entered through the supported guest path enables an authenticated minimal turn; no credential is copied from the host or recorded.             | BLOCKED               |
| CF1-08 | Keychain lifecycle                   | Test credential save/relaunch/read/clear/relaunch behaves correctly; test values never enter evidence.                                                         | BLOCKED               |
| CF1-09 | Repeat/startup/quit launch           | Existing window/session survives repeat launch, minimized/hidden states and startup races; one app/sidecar; launch during quit preserves shutdown.             | BLOCKED               |
| CF1-10 | Normal shutdown                      | App, sidecar and recorded owned shell/Pi descendants terminate; siblings/unrelated processes remain unaffected.                                                | BLOCKED               |
| CF1-11 | Hung-sidecar watchdog                | Stop only the recorded owned sidecar; app exits within its twenty-second watchdog plus recorded scheduling allowance; independently inspect/clean descendants. | BLOCKED               |
| CF1-12 | Crash, recovery and upgrade          | Guest-created sessions/settings survive supported recovery/prior-app upgrade; exact prior/new bytes and final owned process/VM cleanup are recorded.           | BLOCKED               |

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
