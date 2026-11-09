# CF-1C session handoff — 2026-11-08

## Ordered #227 validation — repeated launch complete, Quit rerun next

Maintainer selected add repeated-launch validation, then rerun bounded Quit on the
updated branch before readiness. #227 stays draft/unmerged; maintainer handles
review/optional CodeRabbit scan and the merge. No merge authorization inferred.

Source `1b50acc62bbe304711b70d634e93d5872206850d` is locally committed (not yet
remotely pushed). Fresh run `20261109T014125Z-992ae8ee8730` verifies unchanged
window 83, app 1014 -> sidecar 1021 -> proven shell 1071 and unrelated control 969
across one normal LaunchServices reopen after Finder foreground activation. All
PID/start-time/executable/parent identities agree. Fresh second UI proof retains
shell PID, run sentinel and 39x126 geometry. The first native transition frame
still shows Finder above the app; subsequent read-only window inventory reports
window 83 key/frontmost and independent frames show FingerTrap frontmost before
second input. No duplicate app/sidecar in the independent inventories.

Automatic completion after 21 actions, owner exit 0, no error events, independent
owned cleanup verified. Five originals stopped and no clone/QA processes remain.
[Repeat-launch evidence](evidence/client-baseline/repeat-launch-observation.json)
pins 552 private-file hashes. All 77 kept guards, Ruff/repo/docs checks pass.
Baseline remains one input PASS/eleven BLOCKED; all runtime/provider/activation/
backend/release/acceptance gates unchanged, Pi v0.85.1-psmfd.1, #219 deferred.

Next: commit these evidence/docs changes only, confirm probe source blobs unchanged,
and run existing bounded Quit in a second fresh guest on that updated branch head.
Keep its 1500s observer/360s reserved scenario/1800s hold and automatic completion.
No readiness transition until that rerun, final-head CI/CodeQL and evidence checks
complete. Final documentation-only updates will identify exact tested commits.

## CF-1C-AUTOMATION-5 — bounded normal GUI Quit observed, 2026-11-08

PR #224 merged into dev as `91bf7c53774e8559c486d3b59ad71be7b43e7f1b` after
all fourteen required CI jobs and CodeQL passed. Follow-up branch
`test/client-normal-quit` adds a fixed shell-configured Quit scenario and optional
successful-dispatch completion hook to the existing atomic queue; no product edits.

Run `20261108T152238Z-db6bbf2949e6`: pinned guest-only GUI Bridge grants verify;
live main-window admission yields window 82 twice. One compact UI shell proof at
50ms cadence independently verifies decoded output, PID 1063, 39x126 PTY and
run-bound sentinel. Final independent process inventory binds app 1006 -> sidecar
1013 -> shell 1063, plus unrelated adopted `/bin/sleep` control 961 with recorded
start time, executable and parent. One normal foreground Cmd+Q targets window 82.
The first post-Quit read finds every recorded app descendant gone while control
961 retains its complete identity. Native RFB and Bridge images show the app
window gone. No external app/sidecar/shell kill or reparenting accepted as proof;
no claim about the internal graceful-versus-watchdog shutdown path.

**Automatic completion passed:** successful dispatch finishes after 21 actions,
without a final operator request. Owner exits 0; no timeout or error events.
Owned cleanup plus independent atomic cleanup verify; five originals stopped,
no clone/QA/tool processes remain. The unrelated control survives Quit and is
removed only with the owned VM. Existing 1500s observer/1800s hold bounds retained,
360s reserved for scenario admission, max three read-only post-Quit observations.

[Quit evidence and 472 private-file hashes](evidence/client-baseline/normal-quit-observation.json)
retain process identities, UI proof, raw frames/readbacks and cleanup. Original
1,031 layout and 652 review-correction hashes reverify unchanged. All 73 kept
guards pass; bypassing termination attribution or automatic completion fails the
new guards and restored source passes. Ruff/format and repository checks pass.
Source checkpoint `2969e5e0133a2d95bcbf650c1e1ceeeae54ce04d` is committed
and remotely backed up on `test/client-normal-quit`, [draft PR #227](https://github.com/psmfd/FingerTrap/pull/227)
against dev. Current-head CI/CodeQL status is tracked in the PR checks; require
both green before maintainer merge review. Unrelated local files preserved.

This is a bounded observation in the supported guest shell-default fixture, with
paced input and pinned automation installed. The final desktop still contains
an unrelated Peekaboo Screen Recording permission dialog despite successful
Bridge capture/grant readbacks; unattended enrollment is not claimed resolved.
No pristine Finder-install, live Pi/RPC pane cleanup, forced fallback or full
acceptance case is inferred. Baseline stays one input PASS/eleven BLOCKED pending
review; Pi v0.85.1-psmfd.1, #219 deferred, and every acceptance/provider/activation/
backend/release gate unchanged. Never reuse the cleaned console pointer.

**Next concrete slice: CF-1C-AUTOMATION-6.** Before a fresh boot, predeclare a
bounded repeated normal LaunchServices launch against an already observed app.
Verify same app/sidecar/proven shell and visible window identity, no duplicate
process tree, fresh focus/input proof, and automatic owned cleanup. Keep hidden/
minimized/startup/quit-race variants separate; no Pi/provider/credential changes.

Reproduce with restrictive umask/no-clobber and private stdout/stderr:

```sh
python3 -m unittest discover -s scripts/tests -p 'test_*.py'
python3 scripts/client_baseline_quit_probe.py \
  --archive .artifacts/pr-merge-20261021/golden-fix-apple/candidate.zip \
  --runtime-archive .artifacts/client-baseline/inputs/pi-darwin-arm64-v0.85.1-psmfd.1.tar.gz \
  > .artifacts/client-baseline/quit-probe-NEW-UNIQUE.log 2>&1
```

Terminal 1 runs the observer; Terminal 2 reads the explicit fresh run path from
that private log, then `<fresh-run>/quit-observation/session.json`. At each waiting
`nextIndex`, write a new no-clobber `request-NN.json` (two digits). Complete normal
permission enrollment using freshly observed native coordinates. Queue only
`{"action":"quit-probe"}` once at the current index; successful verification
finishes automatically. No final finish request and no cleaned pointer reuse.

## CF-1C-REVIEW-1 — corrections implemented and locally/live verified

Failed native-console requests now close/discard their socket; the next request
revalidates ownership and creates a fresh Console. Failure receipts stay intact;
no input is replayed. New regression uses text input followed by capture, verifies
old-socket closure before replacement and exactly one text dispatch. Reviewed
original source fails it (one connection rather than two); corrected source passes.
All 67 kept guards, baseline Ruff/format, repository and Markdown/Prettier checks
pass. Secret scanning passes with zero findings after authorized report-directory
access; the initial sandbox report-write failure is not a passing scan.

Fresh run `20261107T214721Z-76b7d2bd30f0` verifies normal guest-only Bridge
enrollment, stable main window 82 and all four compact 50ms shell proofs. App 973
-> sidecar 980 -> original shell 1030; sibling 1311 is independently verified and
closed. Original PID/sentinel survive 39x126 -> resized 34x108 -> sibling 34x53 ->
original 34x108. Fresh native/Bridge images show decoded output and final prompt.
All proof reads completed at attempt 00: partial reads and connection failure
recovery are regression-tested, not live fault-injected. Supported guest-only
shell-default fixture and all prior typing limitations remain explicit.

**Execution limitation retained:** layout result completes, then the 1500s observer
deadline expires awaiting final finish. Owner exits 1 with TimeoutError; the later
finish helper rejects the already-cleaned run without queuing input. This is not
an overall successful execution or app Quit proof. Exception-triggered owned
cleanup and independent atomic cleanup both verify. Five original VMs stopped,
no disposable clone or QA/tool processes remain; never reuse the cleaned pointer.
[Evidence summary and 652 private-file hashes](evidence/client-baseline/review-corrections.json)
preserve the actual result and timeout. Original private evidence is unchanged.

Source `74f9d3b08d4f86e9e71ee339e94c2534992396c3` is committed and remotely
backed up on ready PR #224 against dev. All fourteen jobs in
[CI run 42420784750](https://github.com/psmfd/FingerTrap/actions/runs/42420784750)
pass; title/gitflow pass. All six review threads are resolved (five addressed in
this follow-up). All five analyses and the aggregate in
[CodeQL run 42420782243](https://github.com/psmfd/FingerTrap/actions/runs/42420782243)
pass, including Rust. The temporary neutral aggregate cleared when Rust completed.
No merge performed or authorized.
Baseline stays one input PASS/eleven BLOCKED; Pi v0.85.1-psmfd.1, #219 deferred,
and all acceptance/provider/activation/backend/release gates remain intact.
Next after merge review: CF-1C-AUTOMATION-5 normal GUI Quit/descendant termination
with unrelated guest process survival control, predeclared before fresh boot.

## CF-1C-REVIEW-1 — approved merge corrections

Review checkpoint `287caafada5d5851593b79121066a9c78fa90833` adds latest-pair
window admission and bounded missing/partial proof readbacks without input replay.
All fourteen current CI jobs and CodeQL pass. Remaining correction: discard failed
native-console connections and reconnect before later requests. New regression
fails original source (one connection instead of two), then passes the fix; all
67 kept guards pass locally. Documentation accuracy/formatting and one fresh owned
layout confirmation are in progress. No historical evidence rewritten or baseline
promoted; Pi and all existing gates unchanged. Merge is not authorized.

For a fresh layout reproduction, Terminal 1 runs the observer with stdout/stderr
redirected privately. Terminal 2 reads the explicit fresh run path from that log,
then `<fresh-run>/layout-observation/session.json`. Only when `phase=waiting`,
write a new `request-NN.json` using `nextIndex` (two digits), restrictive umask and
no-clobber. Complete observed normal permission enrollment, queue the fixed
`{"action":"layout-probe"}`, wait for the index to advance, then queue
`{"action":"finish"}`. Never reuse a previous run or cleaned console pointer.

## CF-1C-AUTOMATION-4/4B — resize/split/sibling-close observed

Pinned candidate and Peekaboo GUI Bridge unchanged. Disclosed guest-only supported
ADR-0014 fixture sets version 1 pane.defaultKind=shell before the single app launch;
this makes the app's normal split action create a shell sibling rather than Pi.
No host/product/Pi/profile/provider/keychain/activation/backend/release changes.

**Original failure preserved:** run `20261107T200710Z-adb15f1b0c25` verifies fixture
and live admission, but its 254-character first proof at 50ms cadence returns
INTERACTION_FAILED/indeterminate/response_lost with a native timeout. Independent
frames show partial input; no first proof verifies. It stops before resize/split/
close without blind replay, and owned cleanup verifies. The outer 30s cap did not
predict the Bridge response budget. Do not claim the application failed layout.

**Bounded positive comparison:** fresh run `20261107T203154Z-15a8da01a49a` retains
50ms cadence and all checks but uses compact 144/127/127/127-character proofs,
bound to the run nonce, phase and four absent-before-input guest files. One UI
command/Return per phase; proof bytes and native/Bridge pixels independently agree.

| Phase                    | Shell PID | PTY rows × columns | Original sentinel         |
| ------------------------ | --------- | ------------------ | ------------------------- |
| Before resize            | 1044      | 39 × 126           | retained                  |
| 900×600 logical window   | 1044      | 34 × 108           | retained                  |
| New right sibling        | 1328      | 34 × 53            | absent, independent shell |
| After sibling-only close | 1044      | 34 × 108           | retained                  |

Fresh AX confirms exact 900×600 main window, two visible Terminal input areas
after Cmd+Shift+D, then one after Cmd+Shift+W. Process reads prove app PID 987 ->
sidecar 994 -> both shells before close; afterward only original 1044 remains,
sibling 1328 absent. Original PID/unexported sentinel and PTY dimensions survive;
no external kill or whole-tab/window close. Independent images retain decoded
markers, split geometry and final original prompt. Explicit finish after 21 queue
actions. Both guests cleaned; five originals stopped, no clone/QA/tool processes.

[Comparison and 1,031 private-file hashes](evidence/client-baseline/shell-layout-comparison.json)
retain failure, fixture, exact proofs, images/process review and cleanup. All 62
kept guards/Ruff/repo checks pass. Intentional unproven-sibling close, replacement/
sentinel/geometry acceptance and overlong-command regressions fail kept guards;
restored source passes. Source checkpoint
`cc86814ce59e54d6816d7b1130427fb3b39e2ae0` is committed and remotely backed up on
`test/client-baseline-gui`, open draft #224 against `dev`. All fourteen jobs in
[CI run 42413472484](https://github.com/psmfd/FingerTrap/actions/runs/42413472484)
pass; title/gitflow checks also pass. All 1,031 private-file hashes reverify and
secret scanning reports zero findings. Five original VMs remain stopped, no
QA/clone processes remain, and runner registrations are zero; macOS CI was hosted.
Draft #224 and issues #216/#217 record the results. Full CodeQL/review and
maintainer acceptance remain pending. Private evidence/unrelated files/cleaned
pointer preserved. No baseline row promoted pending review: one input
PASS/eleven BLOCKED. These are shell-configured, paced, bounded observations;
default-cadence delimiter loss and longer-input response budget remain limitations.
Pi v0.85.1-psmfd.1, comparison #219 deferred and every existing gate unchanged.

**Next concrete slice: CF-1C-AUTOMATION-5.** Record a bounded normal GUI quit check
before a fresh boot: capture exact app/sidecar/shell descendants and an independent
unrelated guest fixture process, invoke supported GUI Quit once, observe only
owned descendants terminate while unrelated fixture stays. No external kill
accepted as normal shutdown proof. Preserve all existing acceptance gates.

```sh
python3 -m unittest discover -s scripts/tests -p 'test_*.py'
python3 scripts/client_baseline_layout_probe.py \
  --archive .artifacts/pr-merge-20261021/golden-fix-apple/candidate.zip \
  --runtime-archive .artifacts/client-baseline/inputs/pi-darwin-arm64-v0.85.1-psmfd.1.tar.gz \
  > .artifacts/client-baseline/layout-probe-NEW-UNIQUE.log 2>&1
```

Use restrictive umask/no-clobber, fresh printed run, normal Bridge permission
enrollment, fixed layout-probe queue action and explicit finish. Bounds 1500s
observer/1800s hold/40 bootstrap actions, finite four-proof/one-resize/one-split/
one-close scenario, 30s guest/60s install/45s native probe. Never select cleaned
active-console.json. No automatic row promotion, runtime adoption or publishing.

## CF-1C-AUTOMATION-3/3B — bounded shell input/output verified

Two fresh owned runs used the same pinned candidate, production runtime inputs
and signed Peekaboo v4.9.8 GUI Bridge. Corrected main-window admission now passes
in both actual live callbacks: two uncached snapshots of the same visible main
window. A unique fresh New shell tab AX target creates one shell tab; fresh AX
confirms a focused Terminal input AXTextArea in that exact window before input.

**Original failure retained:** `20261107T181834Z-f41358dcd0f5` at default cadence
shows `printf%s` and zsh command-not-found in independent native/Bridge frames,
versus intended `printf %s`. The tee-created proof file is empty. Exact-byte guard
rejects it despite the CLI's accepted/unverified dispatch. Exception-triggered
owned cleanup verifies. At least the first delimiter was lost; root cause is
unassigned between automation delivery and the client input path. No blind replay
or product-fix claim. This is real failed-world evidence for the proof guard.

**Bounded positive comparison:** `20261107T182716Z-cc826ea8ca2a` changes foreground
typing cadence to 50ms per character. One fixed encoded-marker printf/decode/tee
command and its single Return produce exactly the expected run-bound guest file.
Independent RFB and Bridge images visibly show decoded marker output followed by
new shell prompt; the marker does not occur in the echoed encoded command. CLI
still truthfully reports dispatched_unverified; --accept-dispatched is not proof.
Measured CLI execution 0.912s default versus 8.153s paced; no general throughput
or arbitrary rapid-typing claim. Per-run markers differ to bind each trial.

Independent process readback confirms app PID 917 -> sidecar 924 -> /bin/zsh 1105
at the expected installed paths. Original exited pi 1 tab remains present. No
Pi/profile/provider/keychain/host clipboard configuration or credentials used.
Both attempts deliver input only once; subsequent checks are read-only. Positive
run explicitly finishes after 21 queue actions. Both owned guests cleaned;
independent inventory: five original stopped VMs, no disposable clone/QA/tool
processes. Raw evidence private; unrelated files and cleaned pointer preserved.

[Sanitized comparison and 828 private-file hashes](evidence/client-baseline/shell-input-comparison.json)
retain both runs, requested commands, exact proof bytes, dispatch outcomes,
independent image/process review and cleanup. All 56 kept guards/Ruff/repo checks
pass. Intentional stale/ambiguous-target and echoed-command/foreign-run proof
regressions fail; restored code passes. Source `ea433e6f8a1792a7e314b2415387269a17a57994` is committed and remotely
backed up in open draft #224 against dev. Source CI
[42402534267](https://github.com/psmfd/FingerTrap/actions/runs/42402534267)
passes all fourteen jobs; title/gitflow pass. Full CodeQL/review and maintainer/
app acceptance remain pending. All 56 guards and 828 private hashes pass locally;
secret scan zero. Zero runner registrations, hosted macOS CI/no local drain.
Following documentation-only checkpoint skips CI and leaves tested source intact. No baseline row promoted:
one input PASS/eleven BLOCKED. This is partial CF1-03 input/output evidence;
resize/split/sibling-close remain untested. Pi v0.85.1-psmfd.1, comparison #219
deferred and all activation/backend/release/provider/maintainer gates unchanged.

**Next concrete action: CF-1C-AUTOMATION-4.** Record a bounded shell resize/split/
sibling-close scenario before a fresh boot, including exact dimensions/process
ancestry and independent marker outputs before/after. Use the observed 50ms
cadence as a limited QA condition; default-cadence delimiter loss remains open
in the baseline tracking. Never treat a CLI receipt or encoded command echo as
execution. Do not promote CF1-03 until its full case is independently observed.

```sh
python3 -m unittest discover -s scripts/tests -p 'test_*.py'
python3 scripts/client_baseline_shell_probe.py \
  --archive .artifacts/pr-merge-20261021/golden-fix-apple/candidate.zip \
  --runtime-archive .artifacts/client-baseline/inputs/pi-darwin-arm64-v0.85.1-psmfd.1.tar.gz \
  > .artifacts/client-baseline/shell-probe-NEW-UNIQUE.log 2>&1
```

Use restrictive umask/no-clobber, fresh explicit printed run and normal guest
permission enrollment. Fixed queue action shell-probe runs one client launch and
single tab/command/Return with mandatory readbacks; finish explicitly. Bounds
remain 1200s observer/1500s guest hold/40 queue actions/30s guest commands/60s
install verification/45s native probes. Never reuse cleaned active-console.json.

## CF-1C-AUTOMATION-2 — packaged-client window observed

Run `20261107T175616Z-5064033904d3` installed the pinned candidate inside a fresh
owned guest, verified bundle ID dev.fingertrap.app and all three native binary
hashes, then launched exactly once through normal LaunchServices. No Gatekeeper
bypass, quarantine removal, re-signing or host/global install. Peekaboo GUI Bridge
grants came through the same normal guest UI enrollment/relaunch as the pilot.

**Observed:** native RFB and independent Bridge screenshots show FingerTrap's
visible window and controls. Three distinct uncached AX snapshots, each at least
five seconds apart, expose Toggle session browser and Toggle status panel in
window 80 (1024x686 logical bounds). Process readback independently records app
PID 933 and child sidecar PID 940 at the verified /Applications bundle paths.
The default Pi pane visibly reports an actionable missing-executable error. This
slice did not install/configure Pi or send app command/provider/keychain input;
do not claim terminal transport, provider acceptance or clean Finder installation.

**Guard correction preserved:** original live result is blocked because its
window guard counted an unnamed off-screen helper (85) as well as the visible
FingerTrap main window (80). The actual inventory is partial and keeps its warnings.
Corrected classification binds exactly one observed visible titled main window
to fresh AX; it does not assert a complete global window count. The original
blocked result remains untouched. Corrected guard passes offline replay of all
three retained live observations/screens/clocks after cleanup; no second launch,
no fabricated live callback success. Acquisition commands unchanged; final
corrected live callback was not re-executed. A kept sanitized real-inventory
fixture rejects restoration of the old count-all-windows guard. An intentional
cached/empty/wrong-app UI acceptance regression also fails the kept guard.

[Sanitized result and 350 private-file hashes](evidence/client-baseline/client-launch-observation.json)
retain exact identities, original failure, classification mode, process/image
review and cleanup. All 51 kept guards/Ruff/repo/docs/link/hash checks pass; secret scan zero.
Source `237afcdc2a5afcebcf3194200e2933be0dc43bac` is committed and remotely backed
up in open draft #224 against dev. CI
[42399603531](https://github.com/psmfd/FingerTrap/actions/runs/42399603531)
passes all fourteen jobs; title/gitflow pass. Full CodeQL/review and maintainer/
app acceptance remain open. Zero runner registrations, hosted macOS jobs; no local
drain. Following documentation-only checkpoint skips CI; tested source unchanged. Raw images/JSON/logs
remain private; unrelated files and cleaned active-console pointer preserved.
Explicit finish after 21 queue actions; independent cleanup shows five original
stopped VMs, no clone/QA/Peekaboo/Tart/Softnet processes. Baseline unchanged:
one input PASS/eleven BLOCKED, Pi v0.85.1-psmfd.1, comparison #219 deferred,
all provider/activation/backend/release and maintainer acceptance gates intact.

**Next concrete slice: CF-1C-AUTOMATION-3.** Record a bounded first shell-pane
input/output observation before boot: reuse corrected main-window admission in
a fresh guest, select New shell tab through observed AX, deliver one synthetic
printf probe, then observe exact output independently. No provider/runtime setup,
no baseline row promotion without its full case evidence. This next slice also
exercises the corrected classifier in the live callback. First checkpoint and
refresh current-source CI. Reproduce current acquisition with:

```sh
python3 scripts/client_baseline_client_observe.py \
  --archive .artifacts/pr-merge-20261021/golden-fix-apple/candidate.zip \
  --runtime-archive .artifacts/client-baseline/inputs/pi-darwin-arm64-v0.85.1-psmfd.1.tar.gz \
  > .artifacts/client-baseline/client-observe-NEW-UNIQUE.log 2>&1
python3 scripts/client_baseline_client_observe.py \
  --review-run .artifacts/client-baseline/20261107T175616Z-5064033904d3
```

Use restrictive umask/no-clobber and a fresh explicit run for acquisition. Existing
1200s observer/1500s guest hold/40-action/30s guest-command/45s native-probe bounds
remain; fixed install verification cap 60s. Queue client-observe only after Bridge
permission enrollment; finish explicitly. Review mode only reads a cleaned owned
run, verifies retained image digest and timestamps, and never starts a guest.

## CF-1C-AUTOMATION-1/1B — Peekaboo pilot completed locally

Pinned Peekaboo v4.9.8, source `70dbc4183f390a5a72d5dc01d8a77eb672217070`,
Developer ID team `FWJYW4S8P8`, app `boo.peekaboo.mac`, matching ARM64 CLI and
shipped Swift compatibility library were installed only inside disposable guests.
[Input provenance](evidence/client-baseline/peekaboo-pilot-inputs.json) and
[results with 779 private-file hashes](evidence/client-baseline/peekaboo-pilot-result.json)
retain both attempts. No host/global installation or runtime dependency added.

First run `20261107T171635Z-8d74599c3c32` safely aborted on type exit 1:
INTERACTION_FAILED, indeterminate/may_have_dispatched, retry unsafe. Independent
RFB and Bridge frames nevertheless show the exact synthetic path. The correction
preserves that error and resolves it once with a mandatory fresh Finder AX read;
no input replay. Guard regressions for accepting local capture and skipping the
fresh field assertion both fail; restored code passes all 46 kept guards/Ruff.

Continuation `20261107T172734Z-0f7178a10a0d` completed the fixed scenario and
explicit finish after 21 queue actions. Normal signed app Screen Recording and
Accessibility enrollment, macOS Quit & Reopen/Allow, then normal app Quit and
interactive relaunch yield all three canonical GUI Bridge grants. Full-window
Bridge screenshots agree with independent native RFB frames. Semantic Go menu
selection opens Finder Go to Folder; fresh AX sees PathTextField. Type returns
the retained indeterminate error, then uncached AX and both image sources confirm
`/tmp/ft-input-probe`. Escape visibly closes the dialog; Peekaboo foreground
`cmd+shift+g` reopens it, with fresh AX and independent frames confirming an empty
path field. CLI dispatch status alone is never acceptance. Native RFB Meta+q did
not quit; that older chord path is not claimed fixed. Follow-up RFB timeouts remain
possible; first native frames plus immediate Bridge screen/clock are retained.

Both owned guests cleaned; independent inventory verifies five original stopped
VMs and no disposable clone/QA/Peekaboo/Tart/Softnet processes. Initial blocked
captures and restart gaps retain errors plus independent guest clocks. Raw frames,
JSON, signing output and logs remain private; unrelated files and cleaned pointer
preserved. Tool footprint is explicit, not a pristine tool-free consumer claim.
No FingerTrap app validation occurred. Baseline remains one input PASS/eleven
BLOCKED, Pi v0.85.1-psmfd.1, #219 deferred and all existing gates unchanged.

**Decision:** this bounded Finder workflow has usable scripted input and real
window capture through the signed GUI Bridge. Production adoption, unattended
permission enrollment, and broad app reliability are not established. Keep fresh
AX and image assertions around every mutation, including indeterminate outcomes.
Implementation `5fa59ffe8ae86a117d9a8ce7b8359dd6ae884490` is committed,
secret-scanned (zero findings), remotely backed up and CI-verified in open
draft #224 against dev. CI [42397076555](https://github.com/psmfd/FingerTrap/actions/runs/42397076555)
passes all fourteen jobs; title/gitflow pass. Full CodeQL/review and app acceptance
remain pending. All macOS jobs hosted, no local drain; zero runner registrations.
Following documentation checkpoints skip CI and do not change the tested source.

**Concrete next action:** checkpoint/review this pilot, then name CF-1C-AUTOMATION-2
for a bounded packaged-client launch/observation-only slice using the pinned GUI
Bridge. Record exact app window/ready-state assertions and private evidence before
boot; retain existing app acceptance/provider and activation/backend/release gates.
Do not promote any app row from this Finder pilot. Reproduction:

```sh
python3 -m unittest discover -s scripts/tests -p 'test_*.py'
python3 scripts/client_baseline_peekaboo.py \
  --archive .artifacts/pr-merge-20261021/golden-fix-apple/candidate.zip \
  --runtime-archive .artifacts/client-baseline/inputs/pi-darwin-arm64-v0.85.1-psmfd.1.tar.gz \
  > .artifacts/client-baseline/peekaboo-pilot-NEW-UNIQUE.log 2>&1
```

Use a new private log with restrictive umask/no-clobber. The process owns boot,
1200s observation, 1500s guest hold, at most 40 bootstrap queue actions, fixed
scenario, immediate per-step readbacks and cleanup. Select only the newly printed
explicit run, never active-console.json. Queue fixed actions pilot-status,
pilot-permissions-ui and pilot-scenario in that run's peekaboo-pilot/request-NN.json;
normal native click/chord/capture handles supported UI enrollment. Finish explicitly
with action finish. No arbitrary commands, operator data, models or MCP/cloud.

## Historical continuation: CF-1C-CAPTURE-3

The read-only retry `20261107T160656Z-104c57df8692` completed diagnostic,
independent readback, an RFB capture and explicit normal finish/cleanup. Original
failed run `20261107T160333Z-036e67bd3539` is preserved: codesign exit 1 stopped
the first implementation before logs, without retaining signing output. The kept
correction now preserves nonzero signing status/output and continues diagnostics;
all 38 guards pass, including that real-failure regression.

**Observed privacy failure:** TCC attributes capture to resolved executable
`/opt/homebrew/Cellar/tart-guest-agent/0.10.0/bin/tart-guest-agent` (type 1),
then explicitly reports failure to match an existing Screen Recording code
requirement (check status -67050) and returns denied. The 12-second stream
returned 28,069 bytes, below the 256 KiB cap, with four code-requirement failures.
The binary digest matches the CAPTURE-2 agent. Signing display shows identifier
`a.out`, linker-signed ad hoc metadata, no bound Info.plist/team, and exit 1 for
the designated-requirement readout; do not flatten this into an entirely unsigned
binary claim. This establishes an actual permission/code-requirement failure
in the fresh unmodified guest. Why the prior UI grant failed and whether UI
re-enrollment fixes it remain unproven. No permission/signing/database/logging
configuration, binary or host setting changed in CAPTURE-3.

RFB still shows the recording prompt; independent guest screenshot omits it.
The RFB follow-up capture timed out after its initial frame; its independent
screen/clock completed. No input/app qualification advanced. Both owned guests
cleaned and independent inventory verified five original stopped VMs, no clone
or QA/probe/Tart/Softnet/controller process. Raw signing/TCC logs stay private.
[Sanitized attribution and 33 private-file hashes](evidence/client-baseline/gui-continuation/privacy-attribution.json)
preserve both attempts, selected identity, short denial excerpts and cleanup.
No app row changes: one input PASS/eleven BLOCKED. Pi v0.85.1-psmfd.1, deferred
comparison #219 and all activation/backend/release gates remain intact.

**Next bounded action: CF-1C-CAPTURE-4.** Record a supported exact-entry guest
permission re-enrollment experiment. Preserve both guest-agent entries and collect
TCC decisions after the normal UI grant in that same guest. If the requirement
mismatch persists, remove/re-add only the identified agent entry through normal
Privacy & Security UI using the exact resolved executable, authenticate with the
public fixture account, then verify TCC decisions and actual window captures.
No direct TCC database edits, host changes or binary re-signing. Re-enrollment is
proposed, not implemented/proven. Keep immediate readbacks, private output and
owned cleanup; input/app qualification stays gated until reliable capture.

Queue action `{"action":"diagnose-agent"}` accepts no other fields. It
revalidates the selected service/executable, retains codesign status/output,
then runs a fixed 12s guest-only TCC stream around one capture. Guest log read
uses the image's existing noninteractive sudo; no logging configuration changes.
Returned log data is capped at 256 KiB; raw content remains private. A negative
signing readout is diagnostic evidence, not permission success. Default atomic
comparison unchanged; permission-session bounds remain 720s/960s/40 actions.

## Historical continuation: CF-1C-CAPTURE-2

Run `20261107T153328Z-e16d52dd729f` finished explicitly after nine actions:
eight RFB probes plus the normal RPC-agent restart. Initial and all nine immediate
independent guest screen/clock readbacks completed. Inventory before capture
identified `gui/501/org.cirruslabs.tart-guest-agent`, PID 423, executable
`/opt/homebrew/bin/tart-guest-agent`; executable/plist program digests match.
The disk-resize daemon is separately inventoried and excluded from selection.
Supported guest privacy UI/public fixture authentication enabled the executable
recording toggle while another guest-agent entry was already enabled.

Identity was revalidated before `launchctl kickstart -k`. Its RPC request hit
the 10s command timeout, but reconnection and fresh process/service inventory
proved PID **423 → 843** with the same service, plist and executable identity.
Immediate post-restart screen/clock succeeded. RFB after restart still shows
the permission prompt over Settings; independent screenshots still omit windows.
The restart is verified and is not sufficient to fix capture in this run.
Only probe 00's follow-up frame timed out; the other seven follow-up RFB captures
succeeded. No input qualification or app case advanced. Driver exit 0 is not a
capture/input pass. Normal owned cleanup verified, controller events empty;
five original VMs stopped/preserved, no clone or QA/probe/Tart/Softnet/drain process.

[Sanitized agent restart evidence](evidence/client-baseline/gui-continuation/agent-restart-comparison.json)
retains exact service/PID/digests, nine actions, ten independent readbacks and
86 private-file hashes. Raw inventory/frames/logs remain private. All 36 kept
guards, Ruff and repository checks pass. Red proof: isolated removal of the
pre-restart PID comparison fails the kept guard; unchanged production source
passes. CF-1C-CAPTURE-2 implementation/evidence checkpoint
`3db39be45bfe320089ca91491d529cd369767782` is committed, secret-scanned (zero
findings) and remotely backed up in open draft #224 against dev. Independent
GitHub readback confirms the head. CI run
[42387709718](https://github.com/psmfd/FingerTrap/actions/runs/42387709718) passes
all fourteen platform/repository jobs. Title/gitflow and four CodeQL analyses
pass; Rust analysis remains in progress, aggregate CodeQL neutral and CodeRabbit
skipped at readback. Full CodeQL/review remain pending. All four macOS lanes are
hosted; no local jobs queued or drain started. Runner registrations are zero. This subsequent documentation-only checkpoint records the source
identity with CI skipped; read git/draft head for its own SHA. No activation or
acceptance. Next remains CF-1C-CAPTURE-3 as described above.

**Next bounded action: CF-1C-CAPTURE-3.** Collect the identified guest agent's
code-signing identity and narrowly scoped macOS privacy (TCC) decision logs around
one capture in a fresh owned atomic session. Correlate the actual requesting
client with the two visible permission entries before deciding any supported
exact-entry UI re-enrollment. Do not repeat grant/restart as an assumed fix.
No TCC database edits, permission bypass, host changes or app validation. Retain
immediate readbacks, private output and cleanup. Baseline stays one input PASS /
eleven BLOCKED, Pi v0.85.1-psmfd.1, deferred #219 and all existing gates.

The existing `--permission-session` command below now inventories the guest
agent before its first independent capture. Queue action
`{"action":"restart-agent"}` accepts no additional fields. It rechecks the
baseline service/PID/binary, normally restarts only that unique RPC service,
then requires a changed PID and matching service/executable before readback.
The reconnect budget is at most 90s within the 720s observation deadline.
No caller command, target, path or endpoint is accepted for restart.

Component-role references: [Tart guest-agent README](https://github.com/cirruslabs/tart-guest-agent#readme)
and [image launch-agent template](https://github.com/cirruslabs/macos-image-templates/blob/master/data/tart-guest-agent.plist).
Runtime selection uses the actual guest inventory, not a guessed template label.

## Historical continuation: CF-1C-CAPTURE-1

Run `20261107T143813Z-90064f4d9802` completed sixteen probes with immediate
independent screen/clock readbacks. The supported guest privacy panel showed two
guest-agent entries: executable disabled, another entry enabled. Normal guest
administrator authentication using the documented public base-image fixture
account enabled the executable toggle. No host/operator credential was used.
Native RFB proves the toggle change and captures Settings/Finder windows; no
restart instruction was shown. The recording prompt later recurred, and all
independent guest screenshots continued to omit windows. Permission activation
or duplicate identity remains unresolved; enabling the toggle alone is not a fix.

Pointer Go To Folder attempts did not establish a visible dialog. Focused chord
comparison and synthetic path verification were not completed; no app case ran.
Capture-only probes 07/12 timed out after their initial RFB frame; every probe
still received its immediate independent readback. Observer hit its 720s deadline
while waiting for the next action, exited 1 and recorded TimeoutError. Owned
cleanup succeeded and independently verified five original stopped VMs, no clone
or QA/probe/Tart/Softnet/drain process. A supporting service inventory command
failed on an absent optional directory; stdout was not retained, so no active
service identity is claimed. Follow-up validation safely refused the cleaned run.

[Sanitized permission evidence](evidence/client-baseline/gui-continuation/permission-comparison.json)
records sixteen probes, 148 private-file hashes and exact timeout/cleanup outcome.
All 31 guards, Ruff and repository checks pass. Isolated removal of symlink
rejection makes the new guard fail; unchanged production source passes. CF-1C-CAPTURE-1 implementation/evidence checkpoint
`ddb4ef1a043bf5615f86e143c95ad3e4926ca584` is committed, secret-scanned (zero
findings) and remotely backed up in open draft #224 against dev. Independent
GitHub readback confirms the head. CI run
[42385049680](https://github.com/psmfd/FingerTrap/actions/runs/42385049680) now
passes all fourteen platform/repository jobs. Title/gitflow pass; full
CodeQL/review remain pending. All four macOS
jobs use hosted macos-15 runners; no local jobs queued, no local drain launched.
GitHub runner registrations are zero. This subsequent documentation-only
checkpoint records the implementation identity with CI skipped; read git/draft
head for its own SHA. No activation, app validation or baseline acceptance.
Next remains CF-1C-CAPTURE-2 as described above.

**Next bounded action: CF-1C-CAPTURE-2.** Before triggering capture in a fresh
owned guest, inventory the active guest-agent executable and launch service.
After the supported UI grant, restart that identified guest service normally,
within the same bounded controller execution; verify reconnection and actual
window capture. Preserve duplicate-entry evidence. Keep immediate per-input
readbacks, private output and cleanup. No TCC database edits, host permission
changes or app validation. Pointer/chord/synthetic text comparison follows only
a reliable capture result. Pi v0.85.1-psmfd.1, deferred #219 and all gates remain.

Reproduce the bounded permission session with the existing input archives:

```bash
python3 scripts/client_baseline_compare.py --permission-session \
  --archive .artifacts/pr-merge-20261021/golden-fix-apple/candidate.zip \
  --runtime-archive .artifacts/client-baseline/inputs/pi-darwin-arm64-v0.85.1-psmfd.1.tar.gz \
  > .artifacts/client-baseline/capture-UNIQUE.log 2>&1
```

The observer creates `atomic-comparison/session.json` in the new owned run.
While `phase=waiting`, atomically create `request-NN.json` for `nextIndex`, with
only `action=capture`, `click` (integer x/y), `chord` (bounded keys), `text`
(the fixed synthetic `/tmp/ft-input-probe` string), or `finish`. Inspect native
`step-NN/govnc-*.png` and `guest-screen.jpg` after the next index appears.
No command execution or endpoint fields are accepted in queue requests. Key
input remains trusted guest UI input; it can authenticate the public fixture
account and is not an authorization or credential-isolation boundary.
The same execution owns boot, probes, every immediate readback and cleanup.
720s observation, 960s hold, 40 actions; expiration fails and cleans up. No
request to the already-cleaned run or old active-console pointer is valid.
Record CF-1C-CAPTURE-2 before adding the fixed inventory/restart operations.

## Historical continuation: CF-1C-GO-1

Run `20261107T141221Z-dbb92f6fb20e` completed normally. Fresh native first frame
matches the coordinate reference. RFB shows the Go dropdown opening, then closing
after Escape: pointer response, limited Escape delivery and changed RFB frames
are observed. All three post-input RFB captures succeeded, including the chord;
Meta+Shift+g produced no Go To Folder dialog. The earlier timeout did not recur
in this run; that does not establish a fix or usable chord/text delivery.

RFB also reveals a **Screen Recording permission prompt for tart-guest-agent**,
behind the dropdown and persisting after Escape/chord. Immediate independent
guest screen/clock readbacks completed after every step, but those screenshots
omit both the dropdown and permission prompt. They show only the menu highlight
change, so they cannot corroborate window contents. Permission/focus interference
is a hypothesis; no root cause or app defect is established. No app case ran.
[Sanitized evidence and 29 private-file hashes](evidence/client-baseline/gui-continuation/go-menu-comparison.json)
retain exact observations; raw frames and logs remain private.

Cleanup verified: controller events empty, five original VMs stopped/preserved,
owned clone absent, no QA/probe/Tart/Softnet/CI-drain process. The stale active
pointer was never used. Wrapper source unchanged except the named pointer step
and existing test expectation; all 28 guards, Ruff and repository checks pass.
CF-1C-GO-1 implementation/evidence checkpoint `1ca324f6d85779a1081680e05eed5f5292d2f3a8` is committed,
secret-scanned (zero findings) and remotely backed up in open draft #224 against
`dev`. GitHub independently confirms the branch head. All 28 guards, Ruff,
repository structural/lock/RPC checks, Markdown/Prettier, local links, 29 evidence
digests and pinned-probe provenance pass. New CI run
[42381113002](https://github.com/psmfd/FingerTrap/actions/runs/42381113002) has
completed successfully: all fourteen platform/repository jobs pass. Title/gitflow
and four CodeQL language analyses pass; Rust analysis remains in progress and the
CodeQL aggregate is neutral at readback. CodeRabbit skipped. Full CodeQL/review
remain pending; no activation or acceptance. This subsequent documentation-only
checkpoint records that identity with CI skipped; read git/draft head for its SHA.
Do not transfer implementation CI to the documentation head. Next remains
CF-1C-CAPTURE-1 as described above.

**One concrete next action: CF-1C-CAPTURE-1**, a separately recorded bounded atomic
permission/focus observation in a fresh owned guest. Inspect the guest-only
Screen Recording prompt using native RFB pixels and supported guest privacy UI,
then compare pointer-selected Go To Folder with keyboard input. Verify independent
screens actually include visible windows before app validation. Keep immediate
per-input screen/clock, private redirected output, explicit ownership and cleanup.
No host permission changes or TCC bypass. Do not rerun a cleaned guest.

The historical session-end instructions below describe checkpoint 219daae;
its CI is now refreshed: 24 successful checks, CodeRabbit skipped. Review and
baseline acceptance remain open. Pi v0.85.1-psmfd.1, deferred #219, one input
PASS/eleven BLOCKED and all activation/backend/release gates remain intact.

## Historical session end

Session ended at the maintainer's request; no further guest starts. The bounded
atomic diagnostic wrapper is implemented and live-verified. Read
[STATUS](STATUS.md), the latest
[WORK_LOG](WORK_LOG.md) entry, CONTRIBUTING, the
[approved CF-1 plan](client-foundation-plan.html#cf1-plan) and ADR-0034.
Execution approval persists for CF-1 against production Pi only.

## Saved branch and scope

Pre-wrap status head `3a71165aec179d376b91729d1a3c10ca7ed9af4b` is remotely
backed up. A final session-end documentation checkpoint follows it; verify local
and draft #224 head next session. Some hosted checks were still running at
wrap-up; full CI/review remains pending.

Latest implementation/evidence checkpoint `fe7ec1471cae5751700bcc77a61e8b48fbe977d7` is committed,
secret-scanned and remotely backed up in draft #224. A subsequent status-only
commit records it; read git/draft head for the current SHA. The older checkpoints
below describe the starting handoff. No protected CI or acceptance is claimed.

- Branch `test/client-baseline-gui`, draft [#224](https://github.com/psmfd/FingerTrap/pull/224)
  against `dev`. Handoff evidence checkpoint `2cdf67406416f4d0cce3a5023af4bde7be66fd8b`
  is committed, secret-scanned and backed up. A status-only commit follows it;
  read local/remote head for the final handoff SHA.
- Remote dev last verified `8e5fd0cb60748ba197c60928e3b0c4895fbb1ba8` (#220).
  Use per-command `TheSemicolon` GitHub credentials; do not switch global auth.
- Production Pi remains `v0.85.1-psmfd.1`. Pi 1.3.0 comparison stays deferred in
  [#219](https://github.com/psmfd/FingerTrap/issues/219). No CF-2, managed activation,
  SDK/backend change, runtime pin/release, or acceptance gate is waived.
- [#217](https://github.com/psmfd/FingerTrap/issues/217) remains open: one mandatory
  artifact/input PASS, eleven app rows BLOCKED. No app case ran in this session.
  Supported provider login/network setup and prior-app upgrade acceptance remain
  outstanding. A possible prior QA app was verified but never exercised.
- Twenty-three existing guards and Ruff passed before the isolated comparison.
  No product/runtime source or dependency changed. Full protected CI, review,
  enrollment and merge of this WIP remain pending; do not infer them from #220.

## Actual results and blocker

1. Run `20261028T183947Z-e59ca2bacfab` captured a first frame then recorded
   BrokenPipeError. The sequence points to the next flushed status print to
   a closed tool-output pipe; the exact stack is unavailable. Redirect driver
   stdout/stderr to a private log in every future launch.
2. Redirected run `20261028T184851Z-755e4f85b29e` stayed alive over fifteen
   minutes. Desktop and pointer-driven Finder menus were observed. Original,
   Super, uppercase and paced keyboard chords never established Go To Folder;
   some frame requests timed out. An independent guest screenshot showed desktop.
   See [sanitized live retest](evidence/client-baseline/gui-continuation/live-retest.json).
3. Independent run `20261028T194636Z-ceb986018619`, probe
   `independent-8e000f6c323c`, used `github.com/mitchellh/go-vnc` pinned to
   `v0.0.0-20150629162542-723ed9867aed`. It authenticated/captured a real
   2048x1536 desktop, then timed out on every frame after Escape and paced
   Meta+Shift+g/G. Its exit code 0 does **not** mean input passed.
   See [comparison results and source hashes](evidence/client-baseline/gui-continuation/rfb-comparison.json).
4. The bounded guest deadline cleaned it before a separately approved guest
   screenshot step executed. Next-day readback correctly rejected the cleaned
   run. No post-input independent observation exists for that comparison.

The root cause remains unknown. A second implementation reproduces the timeout;
there is no established app defect, GPU failure, or proven keyboard delivery.
The console lifetime fix is verified; usable input and frame freshness are not.
Do not churn through app acceptance cases until those observations work.

## Shutdown and private evidence

Reverified at session end 2026-11-07: five original VMs stopped/preserved; no disposable clone or
QA driver/controller/comparison/Tart/Softnet process remains. Latest state is
`cleaned`, `cleanupVerified=true`, with no controller events. The old active-console
pointer still refers to a cleaned October run and must not receive another request.
No guest was launched after the wrap-up request.

Preserve `.local-testing/`, `LEGACY_pi_config_3579dc1.patch`,
`NEXT_SESSION_pi-fold-reassessment.md`, all ignored `.artifacts/`, and sibling
repositories. Do not reset, stash, or delete them. Raw frames, logs, queue files,
connection secrets and run states remain private. Never print `vm.log` or VNC URLs.

Private comparison resources remain in `.artifacts/client-baseline/rfb-compare/`:
`main.go`, `probe.py`, `go.mod`, `go.sum`, `rfb-probe`, `provenance.json`,
`modcache/` and `buildcache/`. Exact sanitized source/module text fixtures live
beside `rfb-comparison.json` as `rfb-probe.*.txt`; restore only missing files,
never overwrite private changes. The Python wrapper expects that exact private
comparison directory and validates active run/loopback/ownership before input.

The [reboot input inventory](evidence/client-baseline/gui-continuation/reboot-inputs.json)
records exact original archive sizes/hashes; they were reverified this session:

- Current QA app `.artifacts/pr-merge-20261021/golden-fix-apple/candidate.zip`,
  artifact 12385811971, source `0a287c4eb6769ee810330de6d625861f241f65ce`.
- Production runtime `.artifacts/client-baseline/inputs/pi-darwin-arm64-v0.85.1-psmfd.1.tar.gz`,
  asset 553290482, source `e7d09c9dc5fca409f46a5476669e3c0404054d69`.
- Possible prior QA app `.artifacts/pr-merge-20261021/apple/candidate.zip`,
  artifact 12382323858, source `42bb1b2a210acf30e17de1744257a60988398ecc`.
  Both candidates say app 0.1.0; this is not a published-release upgrade claim.

## Current atomic runner and one concrete resume action

`scripts/client_baseline_compare.py` now composes boot, first pixels, independent
paced pointer/keyboard probes, immediate guest screen/clock and verified cleanup.
It receives the new run directly and never reads or writes active-console.json.
Its private probe binary/source/module hashes must match the saved comparison
fixture; private files are not restored or overwritten automatically.

Run `20261107T131951Z-0c27fad0d0fb` completed screen/clock readback after every
step. Independent RFB captured pointer and Escape frames; Meta+Shift+g still
produced a follow-up timeout. Guest screenshots show Apple icon highlighting
then desktop; no Go To Folder dialog. Usable keyboard delivery is unproven.
See [atomic evidence](evidence/client-baseline/gui-continuation/atomic-comparison.json).
No app case ran. Cleanup verified five original stopped VMs, no owned clone and
no QA/controller/Tart/Softnet process. The old active pointer stays stale.

**Next:** use a fresh bounded atomic session to compare selecting Finder's Go
menu through paced pointer input at native PNG coordinates with keyboard input.
Keep immediate independent screen/clock after each step. Establish usable input
and frame freshness before app cases; do not infer either from exit code 0 or a
clock readback alone. Record any changed diagnostic steps before the next run.

The complete wrapper invocation requires Tart filesystem access outside the
sandbox. Redirect stdout/stderr to a new private log (do not overwrite evidence):

```bash
python3 scripts/client_baseline_compare.py \
  --archive .artifacts/pr-merge-20261021/golden-fix-apple/candidate.zip \
  --runtime-archive .artifacts/client-baseline/inputs/pi-darwin-arm64-v0.85.1-psmfd.1.tar.gz \
  > .artifacts/client-baseline/atomic-UNIQUE.log 2>&1
```

The observer has a 240-second total budget, each RFB probe a 45-second process
cap, each guest command at most 30 seconds bounded by the remaining observation
time, and the guest a 480-second hold. Screens transfer in 64 KiB chunks.
The driver's finally signals owned finish and waits for the controller's cleanup;
the wrapper independently rechecks VM absence and stopped original preservation.
Never boot and observe through separately approved delayed calls.

Validate without a guest:

```bash
python3 -m unittest discover -s scripts/tests -p 'test_*.py' -v
ruff check scripts/qualify-client-baseline.py scripts/client_baseline_vnc.py scripts/client_baseline_compare.py scripts/tests
```

All 28 guards, Ruff and repository checks pass locally. Dropping the driver
observer call fails the new regression; restoring passes. The timeout test kills
a real sleeping child. No new protected CI success or app acceptance is claimed.

## Historical next-session prompt

> Resume FingerTrap in /Users/pdavis/projects/_personal/FingerTrap. Read
> CONTRIBUTING.md, docs/migration/STATUS.md,
> docs/migration/HANDOFF-client-baseline-gui.md and the latest WORK_LOG entry.
> Verify branch test/client-baseline-gui and open draft #224 against dev; the
> final session-end checkpoint follows 3a71165 (implementation fe7ec14). Re-read
> current head and CI; full protected CI/review was pending at handoff.
> The bounded atomic wrapper scripts/client_baseline_compare.py is implemented
> and live-verified. Run 20261107T131951Z-0c27fad0d0fb completed immediate guest
> screen/clock after every probe and verified cleanup; Meta+Shift+g still caused
> a follow-up RFB timeout, and usable keyboard delivery remains unproven.
> Continue CF-1C with a named bounded atomic pointer-selected Finder Go menu
> versus keyboard comparison using observed native PNG coordinates. Record the
> slice before changing diagnostic steps, retain immediate screen/clock after
> each input and owned cleanup in one execution, with private redirected logs.
> Preserve all private evidence and unrelated files; never reuse the cleaned
> active-console pointer. Five original VMs were stopped, with no owned clone
> or QA processes at shutdown. No app cases until usable input and fresh frames
> are observed. Keep Pi v0.85.1-psmfd.1, defer #219's Pi 1.3.0 comparison, and
> preserve all activation/backend/release gates. Execution approval persists.

## Historical Go-menu resume prompt

> Resume FingerTrap on test/client-baseline-gui, draft #224 against dev. Read
> STATUS, this handoff and the latest WORK_LOG entry; verify exact local/remote
> head and CI. CF-1C-GO-1 run 20261107T141221Z-dbb92f6fb20e completed with verified
> cleanup. Pointer (520,24) opened Finder Go, Escape closed it, and all RFB
> captures succeeded; Meta+Shift+g produced no Go To Folder dialog. RFB shows
> a tart-guest-agent Screen Recording permission prompt, while independent
> guest screenshots omit both prompt and dropdown. Do not treat them as full
> window-content verification. Next: record CF-1C-CAPTURE-1, a bounded fresh
> atomic guest permission/focus observation through supported guest UI, followed
> by pointer Go To Folder versus keyboard. Retain immediate screen/clock after
> every input, private redirected logs and owned cleanup. No TCC bypass or host
> permission changes. Never use the stale active pointer; no app validation
> until usable input and reliable fresh window capture are observed. Preserve
> private evidence/unrelated files, Pi v0.85.1-psmfd.1, deferred #219 and all gates.
> Existing CF-1 execution approval persists.

## Historical permission resume prompt

> Resume FingerTrap, branch test/client-baseline-gui and draft #224 against dev.
> Read STATUS, this handoff and latest WORK_LOG; verify exact head/CI.
> CF-1C-CAPTURE-1 run 20261107T143813Z-90064f4d9802 enabled the executable
> guest-agent recording toggle through supported guest UI/authentication, but
> the prompt recurred and independent screenshots still omit windows. Sixteen
> probes/readbacks completed; 720s observation deadline expired and cleanup
> verified five original stopped VMs, no clone/QA processes. Raw evidence private.
> Next: name/record CF-1C-CAPTURE-2, inventory the actual agent executable/service
> before capture, then normal guest service restart after UI grant within the
> same bounded atomic execution; verify reconnection and full-window capture.
> No TCC bypass, host permission changes or app validation. Preserve private
> evidence/stale pointer, Pi v0.85.1-psmfd.1, deferred #219, one input PASS/eleven
> BLOCKED and all activation/backend/release gates. CF-1 approval persists.

## Historical restart resume prompt

> Resume FingerTrap, branch test/client-baseline-gui, open draft #224 against dev.
> Read STATUS, this handoff and latest WORK_LOG; verify exact head/CI.
> CF-1C-CAPTURE-2 run 20261107T153328Z-e16d52dd729f identified the actual RPC
> service and verified normal restart PID 423 to 843 with matching identity.
> Supported guest UI grant plus restart did not resolve capture: RFB still
> shows the recording prompt/Settings while independent screenshots omit windows.
> Nine actions/ten readbacks completed, explicit finish/normal cleanup verified,
> five original stopped VMs, no clone or QA processes. Private evidence preserved.
> Next: record CF-1C-CAPTURE-3, collect guest-agent signing identity and narrowly
> scoped TCC capture-decision logs, map the actual client to the two permission
> entries before choosing supported exact-entry UI re-enrollment. No TCC database
> edits, permission bypass, host changes or app validation. Keep the bounded
> atomic execution, immediate readbacks, private output and cleanup. Preserve
> stale active pointer, Pi v0.85.1-psmfd.1, deferred #219, one input PASS/eleven
> BLOCKED and all activation/backend/release gates. CF-1 approval persists.

## Current resume prompt

> Resume FingerTrap on test/client-baseline-gui, draft #224 against dev. Read
> STATUS, this handoff and latest WORK_LOG; verify exact head/CI. CAPTURE-2 verified
> restart PID 423 to 843 but capture stayed blocked. CAPTURE-3 retry
> 20261107T160656Z-104c57df8692 identifies actual TCC code-requirement mismatch
> for resolved /opt/homebrew/Cellar/tart-guest-agent/0.10.0/bin/tart-guest-agent,
> check -67050, denied. Signing readout shows a.out/linker ad hoc metadata and
> exit 1; do not call it wholly unsigned. Initial failed run preserved; corrected
> retry and both cleanup receipts retained. Five originals stopped, no clone/QA
> processes, private logs preserved. Next: record CF-1C-CAPTURE-4 supported
> exact-entry UI re-enrollment, with post-grant TCC logs and actual window-capture
> verification in the same bounded owned guest. Proposed, not implemented/proven.
> No direct TCC edits, host changes, binary re-signing or app validation. Preserve
> private evidence/stale pointer, Pi v0.85.1-psmfd.1, deferred #219, one input PASS/
> eleven BLOCKED and all gates. Existing CF-1 execution approval persists.

## Automation evaluation steering

The maintainer asks about an open-source scripted UI driver. Proposed next
planning action: CF-1C-AUTOMATION-1, evaluate a pinned Peekaboo CLI + signed GUI
Bridge in the disposable guest using deterministic native commands/JSON outputs.
Its permission documentation addresses wallpaper-only background captures.
Verify exact released capabilities, supported permissions and actual Finder
input/window capture before adoption. No installation/adoption has occurred.
Keep the existing Tart ownership/deadline/evidence/cleanup wrapper, offline
operation and all existing gates. See latest STATUS/WORK_LOG for alternatives
and bounded pilot acceptance. Direct exact-entry re-enrollment remains proposed.

## Attribution checkpoint and automation steering

CF-1C-CAPTURE-3 implementation/evidence checkpoint
`8876c5839ed6536aec879ccdba11c36163278fd7` is committed, secret-scanned (zero
findings) and remotely backed up in open draft #224 against dev. All 38 guards,
Ruff, repository checks, fixed shell syntax, Markdown/Prettier, local file links
and 33 private-file digests pass. Implementation CI
[42392072357](https://github.com/psmfd/FingerTrap/actions/runs/42392072357) passes
all fourteen jobs; title/gitflow pass. Full CodeQL/review remain pending.
All macOS jobs hosted; no local drain needed, zero runner registrations. Final
inventory: five original stopped VMs, no disposable clone or QA processes.
This following documentation-only checkpoint records that source identity with
CI skipped; read git/draft head for its own SHA. Automation tooling is proposed,
not installed/adopted. Latest steering: evaluate pinned Peekaboo CLI + GUI Bridge
before additional custom UI orchestration; retain existing ownership/cleanup and
all acceptance/activation gates.
