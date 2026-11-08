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
Implemented and locally/live verified; scoped commit, remote backup, draft PR,
current-head CI/CodeQL and review remain next. Unrelated local files preserved.

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

## CF-1C-AUTOMATION-5 — approved normal GUI Quit slice

PR #224 squash-merged into dev as `91bf7c53774e8559c486d3b59ad71be7b43e7f1b`;
all fourteen final-head CI jobs and CodeQL passed before merge. Current branch
`test/client-normal-quit` starts at that merge. Existing CF-1 approval persists.

Before implementation/boot: reuse pinned artifacts, signed guest GUI Bridge,
supported guest-only shell-default fixture, latest-pair app admission and one
compact paced UI shell proof. Create an unrelated guest-only `/bin/sleep 1200`
control outside app ancestry. Independently bind exact app/sidecar/shell ancestry
and all app descendants plus control PID/start-time/executable identity. Invoke
normal foreground Cmd+Q once against the admitted exact window. Never externally
kill app/sidecar/shell as shutdown proof. At most three read-only post-Quit process
observations must show all recorded descendants gone and unchanged unrelated
control alive. Native/Bridge frames and independent guest clock follow every
probe, including failures. Whole owned VM cleanup happens afterward.

Bounds: existing 1500s observer/1800s guest hold, 40 enrollment queue actions,
reserve 360s before starting fixed scenario (enrollment must finish by 1140s),
30s guest commands/60s install/45s native probes. A successful fixed quit dispatch
automatically finishes the queue; no final operator request. Failed verification
must never mark completion. stdout/stderr private, restrictive umask/no-clobber;
no cleaned pointer, host installs, TCC database edits, operator/provider credentials or product edits.

Validate ancestry/control identity, survivor/reparented-child refusal, one Quit
mutation and automatic completion regressions, full kept suite/Ruff/repo/docs/
links/hash/secrets, then one fresh owned live comparison and cleanup. Baseline
one PASS/eleven BLOCKED, Pi v0.85.1-psmfd.1, #219 deferred; every application
acceptance/provider/activation/backend/release gate remains unchanged.
Next: implement fixed probe and its regression guards before boot.

## CF-1C-REVIEW-1 — maintainer merge approval and required head checks

Maintainer approved merging PR #224 into dev after the correction review.
Correction source `74f9d3b08d4f86e9e71ee339e94c2534992396c3` passes all fourteen
CI jobs and all CodeQL analyses; documentation checkpoint
`e80d638296dde1e4b348c852c3bd2d3dd95c20c4` passes CodeQL. Its skip-ci marker
left required workflow checks absent at the current PR head, so merge protection
blocks it. This scoped documentation update deliberately runs normal CI. Do not
bypass protection, synthesize check statuses or alter any gates. Once current-head
required checks and security pass, execute the approved squash merge and verify
PR merged/base dev plus the resulting commit. Record that actual merge identity
in the next migration status update. No new guest execution is needed.

Baseline stays one input PASS/eleven BLOCKED; Pi v0.85.1-psmfd.1, #219 deferred
and all application acceptance/provider/activation/backend/release gates unchanged.
Next implementation slice remains predeclared normal GUI Quit/descendant checks;
this approval authorizes merge, not baseline promotion or activation.

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

## CF-1C-REVIEW-1 — approved merge corrections in progress

Starting from reviewed `287caafada5d5851593b79121066a9c78fa90833` on
`test/client-baseline-gui`, ready PR #224 against dev. Approved scope: discard a
failed native-console connection, reconnect before the next request with ownership
revalidation, preserve failure receipts and never replay input. Add a regression
that fails on the original code. Correct stale checkpoint/queue instructions and
formatting. Validate all kept guards/Ruff/repo/docs, then one fresh bounded owned
layout confirmation with immediate independent screens/clocks and private output.
Boot/probes/cleanup remain one owner execution; never use a cleaned pointer.
Historical evidence stays historical; baseline one PASS/eleven BLOCKED, pinned
Pi v0.85.1-psmfd.1, #219 deferred and all gates unchanged. No merge authorized.

<!-- coderabbit-ai-generated:v1 -->

<!-- coderabbit-ai-generated-section:v1:3efc2f95650102ec7b57380d929d099a933b5df5d5a316fb810c5a721b8e40f5 -->

# Runtime migration: current status

<!-- coderabbit-ai-generated:v1 -->

<!-- coderabbit-ai-generated-section:v1:0d93b6d920a3ac68fd578d86c0d7d53b7e1e5fc78489812ca92b2f68bebafc66 -->

CF-1C-AUTOMATION-4 review follow-up implemented and locally verified: all 66
Python guards, repository checks and focused Ruff correctness checks pass. New
guards reject the original implementation. Review fixes are committed in
`287caafada5d5851593b79121066a9c78fa90833`; all fourteen CI jobs and current
CodeQL checks pass. CF-1C-REVIEW-1 below tracks remaining merge corrections.

<!-- /coderabbit-ai-generated:v1 -->

<!-- /coderabbit-ai-generated:v1 -->

Updated: 2026-11-07. Resumed CF-1C under the existing execution approval.

Latest steering: shell-fixture layout/sibling-close checkpoint backed up; all fourteen CI jobs pass. Timeout retained; normal GUI quit is next.

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

## CF-1C-AUTOMATION-4 — approved bounded resize/split/sibling-close

Start 0cb0be6ef2d0748ff89b0cae790424bc2b39219d, branch test/client-baseline-gui,
open draft #224 against dev. Prior source ea433e6 CI fourteen jobs green. Before
implementation: dedicated fixed layout scenario reuses current atomic owner,
pinned candidate/runtime/Peekaboo GUI Bridge, supported permission enrollment,
corrected live window admission and proven 50ms input cadence. No product edits.

Source inspection shows split uses configured default kind rather than inheriting
shell. Explicit guest-only supported ADR-0014 settings fixture before first app
launch: {version:1,pane:{defaultKind:"shell"}} at ~/Library/Application Support/
fingertrap/settings.json. Require absent file/no overwrite. This is a disclosed
shell-configured test condition, not a pristine default-Pi installation. No Pi
executable/profile/provider/keychain or activation/backend/release setting changes.

One initial shell; four fixed paced UI command/Return deliveries generate distinct
run-bound guest proof files only through the interactive shell. Each proof records
decoded marker, $$ PID and /bin/stty size. Check absence before input and exact
marker/parseable positive PID/rows/columns after, with independent native RFB and
Bridge pixels/clock after every probe. No blind retry of a mutation.

Predeclared sequence/assertions: original proof; resize exact main window to
900x600 logical pixels, fresh AX confirms geometry and same shell PID with smaller
rows/columns; Cmd+Shift+D once splits right; fresh AX exposes two terminal input
areas and sibling proof has distinct shell PID/narrower columns. Independently
verify app->sidecar->both shells ancestry. Cmd+Shift+W once closes focused sibling
only; fresh AX returns to one terminal, sibling PID disappears, original stays.
Final original proof has same PID and restored post-resize PTY dimensions. At most
three read-only process observations for asynchronous sibling exit. No external
kill, no closing whole tab/window or unrelated processes. Clean entire owned guest
at explicit finish; never use cleaned active-console pointer.

Bounds reviewed before boot: 1500s observer/1800s guest hold (extra fixed layout
probes), 40 bootstrap queue actions, finite four-proof/one-resize/one-split/one-close
scenario, 30s guest commands/60s install verification/45s native probes. stdout/
stderr redirected privately. Validate target/PID/geometry/proof regression guards,
kept suite/Ruff/repo/docs/hash/secrets, then one live owned run and cleanup.
Baseline stays one input PASS/eleven BLOCKED pending review of complete evidence;
default-cadence loss remains unresolved. Pi v0.85.1-psmfd.1, #219 deferred and all
acceptance/activation/backend/release/provider gates intact. Next: implement and
validate fixed scenario before fresh boot.

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

## CF-1C-AUTOMATION-3 — approved bounded shell-pane input/output

Start e8ff56b24ed7ad67a742cbb0f8bd913f56d0b44e, branch test/client-baseline-gui,
draft #224 against dev. Prior implementation 237afcd CI fourteen jobs green.
Before changes: add a fixed shell-probe queue action in a dedicated QA module,
reuse the existing client install/one-launch, corrected live admission, pinned
Peekaboo GUI Bridge and atomic Tart/VNC lifetime. No product/runtime changes.

Fresh guest and normal permission enrollment. Require corrected live stable
main-window admission. Select exactly one New shell tab AX button from a fresh
explicit snapshot, click it once, then observe the newly added shell tab/terminal
receiver. Deliver one fixed synthetic shell command through Peekaboo UI input.
The command base64-decodes a run-bound marker and tees it to a single owned guest
/tmp/ft-cf1-shell-proof file. Verify that file absent before UI input; only the
interactive shell may create it. Independent guest read must exactly match the
expected marker; decoded marker must be visible in fresh terminal evidence, not
merely echoed as part of the typed command. No remote command writes the proof.

Every probe/mutation gets independent native RFB plus Bridge screen/clock. No
blind replay after an indeterminate input. Observe exact current receiver/value
before deciding to send Return; otherwise stop with evidence. At most one tab
creation, one command delivery and one Return; bounded read-only observations
may repeat. Record app/sidecar/shell ancestry independently. Do not close siblings,
install/configure Pi, access provider/keychain/host clipboard or use operator data.
Bounds: 1200s observer, 1500s guest hold, 40 queue actions, 30s guest-command,
60s fixed install verification, 45s native probe. Private stdout/stderr; explicit
finish/owned cleanup in the same execution; never use cleaned active pointer.

Validation: real-path guard regressions for stale/ambiguous receiver and forged
or pre-existing proof, kept suite/Ruff/repo/docs/hash/secrets checks, one bounded
live run. This is partial CF1-03 evidence, not its full resize/split/sibling case;
baseline remains one input PASS/eleven BLOCKED. Pi v0.85.1-psmfd.1, comparison
comparison #219 deferred and all activation/backend/release/provider gates intact. Next:
implement fixed scenario and evidence assertions before boot.

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

## CF-1C-AUTOMATION-2 — approved packaged-client launch/observation

Proceed authorized from f27dcd9, branch test/client-baseline-gui, open draft #224
against dev; prior source 5fa59ff CI fourteen jobs green. Before implementation:
add one dedicated fixed client-observe queue action around the existing Peekaboo
pilot lifetime. Fresh guest, same pinned app/runtime/tool artifacts and normal
GUI Bridge permission enrollment. No custom RFB/lifetime reimplementation.

Install the already verified candidate zip into guest /Applications via ditto;
verify installed bundle ID dev.fingertrap.app and all three native binary hashes
against the prepared run before one normal LaunchServices open. Record actual
Gatekeeper/prompts without disabling, re-signing or stripping quarantine. This
is tool-assisted launch observation, not a pristine Finder installation pass.
No runtime/profile/provider/keychain changes or app command input in this slice.

Predeclared observed-ready criterion: exactly one positive-size FingerTrap window,
uncached complete AX observation targeting FingerTrap with session-browser and
status-panel controls visible, two fresh observations five seconds apart, plus
private independent native RFB and Bridge screen/clock after each step. Record
app/sidecar PIDs and installed paths independently; process existence alone is
not UI readiness. At most three read-only observation attempts; launch never
replayed. If webview AX is unavailable, preserve pixels/processes and mark the
criterion blocked rather than weakening it to CLI success. Preserve all failures.

Bounds unchanged: 1200s observer, 1500s guest hold, 40 queue actions, command cap
30s (installation 60s), native probe 45s. Boot, enrollment, launch, observations,
explicit finish and verified owned cleanup in one process, stdout/stderr private.
Do not reuse cleaned pointer. Validate new guards with intentional regression,
existing suite/Ruff/repo/docs/hash/secret checks, then one bounded live run.
Baseline stays one input PASS/eleven BLOCKED; Pi v0.85.1-psmfd.1, #219 deferred,
all activation/backend/release/provider acceptance gates intact. Next: implement
fixed installed-identity and fresh-window assertions, then run the owned slice.

## CF-1C-AUTOMATION-1/1B — Peekaboo pilot verified

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

## CF-1C-AUTOMATION-1 — approved Peekaboo pilot

Maintainer approved the pilot. Start at `a0aa2655a30c220a33481b4f632d1bd4e0841a09`,
branch test/client-baseline-gui, draft #224 against dev. Select published Peekaboo
v4.9.8 (release 435831803, 2026-11-05), matching app zip asset 715044161 and
ARM64 CLI tar asset 715044399. Pin GitHub asset digests, release signing/inventory
manifests and actual embedded source identities before any guest execution.
No host/global installation or package manager, no runtime/product dependency.

Scope: a dedicated QA pilot module using the existing Tart/VNC owner and cleanup
wrapper. Minimal reviewed hooks may stage verified tool inputs into the new
read-only QA share and select a per-probe independent readback. Do not rebuild
the atomic lifetime/RFB implementation. Guest-only installation of matching
prebuilt app/CLI, normal supported Screen Recording/Accessibility permissions to
the actual signed GUI Bridge, deterministic CLI Finder/menu/dialog/text scenario,
JSON/AX/window assertions, real screenshots plus independent native RFB evidence.
No model agent, MCP/cloud credentials, host clipboard, TCC edits/bypass or binary
re-signing. No fallback capture through a background CLI accepted as a Bridge pass.
Record test-tool footprint; do not claim a pristine tool-free consumer test.

Reviewed pilot bounds before implementation: 1200s observer, 1500s guest hold,
40 native bootstrap actions and a fixed finite CLI scenario. Each guest command
at most 30s (install/verification at most 60s bounded by remaining time), native
probe cap 45s. One boot/stage/permissions/script/observations/cleanup execution
with private redirected stdout/stderr. Before Bridge permission works, preserve
capture failure plus independent guest clock without inferring window contents.
After grants, require actual full-window capture and input proof. Run identifier
passed explicitly; never select the cleaned active-console pointer.

Validation: artifact/source/signature/archive provenance, meaningful staging/
callback/ownership/Bridge-source guards and red proof, existing kept suite/Ruff/
repo/docs checks, then bounded live pilot and independent cleanup. Preserve all
private evidence and unrelated files. Pilot go/no-go stays separate from app
validation: baseline one input PASS/eleven BLOCKED, Pi v0.85.1-psmfd.1, deferred
comparison #219 and all activation/backend/release gates remain unchanged. Next: acquire and
review exact artifacts and release command docs, implement minimal QA integration.

## CF-1C-CAPTURE-3 — bounded read-only privacy-client attribution

Continue within persistent CF-1 diagnostic approval after verified restart
proved insufficient. Starting source 3db39be and documentation
`a61b2596275a4ed8dc556d19ba04122dedfab43d` are backed up in draft #224.
Before changing the slice: add a fixed diagnose-agent queue action with no
caller commands/paths/predicates. Read the selected guest executable's signing
identity/designated requirement and a 12-second narrowly filtered live macOS TCC
stream around a capture request; preserve output privately.
No permission/config/database mutation, global logging change or host operation.

Affected files: existing atomic comparison script, focused guard test and
migration records/evidence. Keep guest ownership checks, 720s observation/960s
hold/40-action budgets and 30s command caps. A guest-local privileged read of
system privacy logs may use the clean image's existing noninteractive sudo;
never request or copy host credentials. Failures/redaction are evidence, not an
excuse to alter TCC or weaken gates. No app or input qualification in this slice.
One fresh atomic session: inventory, initial capture/clock, fixed diagnostic
readout, explicit finish and verified cleanup. Validate before boot; review the
actual requesting client and denial before proposing any exact-entry UI change.
Next: implement the read-only diagnostic action and kept guard.

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

## Historical CF-1C-CAPTURE-2 — approved active-agent inventory and normal restart

Maintainer approved this next bounded action. Start at
`0cf35d21f239c7c65f5d1c82b359913cf86285ee`, branch `test/client-baseline-gui`,
open draft #224 against dev. Reuse the existing atomic permission session.
Before the first independent capture, inventory guest-agent process executable,
launch plist, domain/label and active PID. Preserve complete private inventory
and errors; optional missing directories must not discard useful output.
Only a unique active RPC-capable launch service whose PID/executable match the
inventory may be restarted. Revalidate identity immediately before restart.
Use normal guest launchctl service restart, then bounded reconnection and verify
a changed PID with the same service/executable. No guessed service, broad kill,
TCC edits, host settings change or permission bypass.

Affected files: scripts/client_baseline_compare.py and kept guard tests, plus
migration records/evidence. Add one fixed restart-agent queue action with no
caller-supplied command, service, path or endpoint. Supported guest UI grant and
public fixture authentication remain observable through RFB. Keep immediate
independent screen/clock attempts after every probe; restart records its own
bounded reconnect/readback outcome in the same owned execution. Default runner
unchanged. Retain 720s observation, 960s hold, 40 actions, 45s probe and 30s guest
command limits; restart gets at most 90s within remaining observation time.

Validate ownership, ambiguous/non-RPC/wrong-PID rejection and changed-PID
readback with meaningful regressions/red proof, then one fresh private redirected
live run. Compare actual Settings/Finder windows in both capture paths after UI
grant and restart. Only after reliable capture, qualify pointer Go To Folder,
focused keyboard chord and synthetic text. No app cases in this slice. Finish
or fail within bounds and independently verify owned cleanup. One input PASS /
eleven BLOCKED, Pi v0.85.1-psmfd.1, deferred #219 and all gates remain intact.
CF-1C-CAPTURE-2 preflight: inventory and identity-checked restart-agent operation
implemented in the existing permission session. All 36 guards and Ruff pass.
Removing pre-restart PID comparison in an isolated module fails the new guard;
production source unchanged. Pinned probe verified; local/draft head 0cf35d2
matches open draft #224 against dev. Preflight verified five original stopped
VMs and no guest driver/probe/Tart/Softnet/drain process. Live result follows.

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

## Historical CF-1C-CAPTURE-1 — approved guest permission and focus remediation

Maintainer approved the presented plan. Start at `6a1ae46f76dff2b0a84b814e6768fd0bb70f4179`
on `test/client-baseline-gui`, draft #224 against dev. Reuse the atomic driver,
pinned independent probe, immediate guest screen/clock and owned cleanup.
Affected implementation: `scripts/client_baseline_compare.py` and its kept guards.
Add an explicit permission-session option: a fresh-run-only bounded guest-input
queue permits inspecting native RFB images between UI actions while the same
boot/observer/cleanup execution stays alive. No active-console pointer access.
Each action still executes the pinned probe and immediate independent readback.

Budget reviewed before changing code: permission session gets 720s observation,
960s guest hold, at most 40 actions; retain 45s probe and 30s guest-command caps.
The default three-step comparison keeps 240s/480s. Unknown/oversized/symlinked
requests fail closed; timeout or interruption invokes owned cleanup. UI actions
only, no host commands/clipboard, TCC database writes, permission bypass or host
permission changes. Raw screenshots, request files and logs remain private.

Live sequence: trigger/read the guest-agent recording prompt, use native RFB
coordinates to open supported guest Privacy & Security settings, enable the
actual guest-agent Screen Recording entry and follow any restart requirement.
Then compare a visible window/menu between RFB and guest capture, pointer-selected
Go To Folder versus focused Finder keyboard chord, and harmless synthetic text.
Do not infer success from screenshot existence, clock progress or process exit.
Stop with a preserved blocker if the guest UI cannot be resolved within bounds.

Validation: existing guards plus meaningful queue ownership/limits/failure-readback
regressions and red-first proof, Ruff, repository/docs checks, then one private
redirected bounded live execution. Record exact observations and cleanup. No app
validation until usable input and reliable window capture are observed. Keep
one input PASS/eleven BLOCKED, Pi v0.85.1-psmfd.1, deferred #219 and all gates.
CF-1C-CAPTURE-1 implemented: explicit 720s/40-action fresh-run queue, 960s guest
hold; default comparison unchanged. All 31 guards, Ruff and repository checks
pass. Red proof: isolated removal of symlink rejection makes the new guard fail;
production source unchanged and full restored suite passes. Pinned private probe
verified; preflight had five originals stopped and no QA/Tart/Softnet/drain
process. The bounded live result and current next action follow below.

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

## Historical CF-1C-GO-1 — native-coordinate Go menu versus keyboard

Starting checkpoint `219daae53e3e28cd334156c0c776f52737252944` matches local
`test/client-baseline-gui` and open draft #224 against `dev` (`8e5fd0c`).
CI run [42377205471](https://github.com/psmfd/FingerTrap/actions/runs/42377205471)
completed successfully: all fourteen jobs pass. Exact-head checks: 24 successful,
CodeRabbit skipped, no pending/failing checks. Review and acceptance remain
separate; these results apply to the saved checkpoint, not upcoming changes.

**Named bounded slice, recorded before changing diagnostics:** CF-1C-GO-1.
Reuse the implemented atomic wrapper and pinned private probe; change only the
pointer step from Apple to Finder Go at native PNG `(520, 24)`, then Escape and
the existing paced Meta+Shift+g comparator. Coordinates were observed in the
2048x1536 first frame of run `20261107T131951Z-0c27fad0d0fb` (SHA-256
`6a44b071a7d70e697b089fb563150d5c30557596de3cfe261bbd5f8a8140bcac`).
Inspect the fresh first frame and each independent guest screen for matching
layout and actual menu/dialog changes before interpreting input.

Validation: kept Python guards, Ruff and repository checks; one fresh owned
boot/probes/immediate per-input guest screen+clock/cleanup execution with both
output streams redirected to a new private log. Retain the existing 240s
observation/480s guest bounds and independent cleanup check. Never read or write
the stale active-console pointer. Initial inventory: five original stopped VMs,
no disposable clone. Raw evidence and unrelated files remain private/preserved.
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

No app validation; baseline remains one input PASS/eleven BLOCKED. Keep Pi
`v0.85.1-psmfd.1`, deferred #219 and all activation/backend/release gates.

## Historical session-end checkpoint

Implementation `fe7ec1471cae5751700bcc77a61e8b48fbe977d7` and status checkpoint
`3a71165aec179d376b91729d1a3c10ca7ed9af4b` are committed and remotely backed up
in open draft #224 on `test/client-baseline-gui` against `dev`. This wrap-up
commit follows them; read local/draft head for its identity. No further guest
starts in this session. Final inventory: five original stopped VMs, no disposable
clone and no QA/controller/probe/Tart/Softnet process. Private evidence preserved.

Hosted checks on `3a71165` include successful repo-check, lint and several platform
jobs, with other jobs still running at readback. Full protected CI/review remains
pending; re-read the final head next session. No merge or acceptance.
Use [the handoff and copyable prompt](HANDOFF-client-baseline-gui.md#next-session-prompt).
The atomic wrapper is complete; the next change is the bounded pointer-selected
Finder menu versus keyboard comparison, not reimplementation of the wrapper.

## CF-1C atomic comparison — implemented and live-verified wrapper

Resumed branch/draft #224 at `81341dd6df244fcf6d879e2ead0b6b50e2cde207`.
Implemented `scripts/client_baseline_compare.py`: explicit fresh driver-owned run,
pinned independent probe, immediate per-step guest screen/clock, 240s observation
budget, 480s guest hold, and owned cleanup with independent inventory verification.
The active-console pointer is neither read nor written; its cleaned run stays stale.
28 local guards, Ruff and repository checks pass. Dropping the observer call makes
the new regression fail; restoring it passes. No protected CI claim.

Live run `20261107T131951Z-0c27fad0d0fb` completed every independent screen/clock
readback. RFB captured after pointer and Escape; Meta+Shift+g still timed out.
Screens show Apple icon highlighting then desktop, with no Go To Folder dialog.
Usable keyboard delivery remains unproven; no app case attempted/passed. Evidence:
[atomic-comparison.json](evidence/client-baseline/gui-continuation/atomic-comparison.json).
Guest cleaned normally, events empty, cleanup verified, five original VMs stopped
and preserved, no disposable clone. Raw screens/logs remain private.

**Next action:** fresh atomic pointer-versus-keyboard comparison using observed
Finder menu/native PNG coordinates, with immediate guest screen/clock after each
input. Establish usable input and fresh frames before app validation. Keep Pi
`v0.85.1-psmfd.1`, defer #219, and retain activation/backend/release gates.
Implementation/evidence checkpoint `fe7ec1471cae5751700bcc77a61e8b48fbe977d7` is committed,
secret-scanned (zero findings) and remotely backed up in draft #224. A subsequent
status-only commit records this identity; read git/draft head for its own SHA.
Full protected CI and review remain pending; no acceptance or merge claimed.

## Historical 2026-10-29 handoff — superseded by atomic wrapper above

Branch `test/client-baseline-gui`, draft #224. Handoff evidence checkpoint
`2cdf67406416f4d0cce3a5023af4bde7be66fd8b` is committed, secret-scanned and remotely backed up.
A subsequent status-only commit records this identity; read git/draft head for
its own SHA. No protected CI or acceptance is claimed for this checkpoint.
The independent comparison is recorded in
[rfb-comparison.json](evidence/client-baseline/gui-continuation/rfb-comparison.json),
with exact probe-source text fixtures and module sums beside it. No application
or runtime source/dependency changed. Local comparison files/caches/binary and
raw evidence remain private under `.artifacts/client-baseline/`.

Run `20261028T194636Z-ceb986018619`: pinned independent go-vnc authenticated
and captured a 2048x1536 desktop. Follow-up frames after Escape and paced
Meta+Shift+g/G all timed out, matching the kept client's symptom. This does not
establish whether input arrived or identify the root cause. The guest deadline
cleaned it normally before the independent guest-screen step; next-day readback
correctly refused the cleaned run. No app case attempted or passed.

**Shutdown verified 2026-10-29:** all original VMs stopped/preserved, no disposable
clone, no QA driver/controller/comparison/Tart/Softnet process. Latest run state
is cleaned with cleanupVerified=true and no controller events. Active-console
points to that cleaned run; do not reuse it. Private evidence is preserved.

**Historical resume action (completed 2026-11-07):** implement and name a bounded atomic comparison wrapper
around the saved driver and independent probes: boot/new first frame, paced
pointer/keyboard, immediate guest screen/clock readback, then owned cleanup in
one approved tool execution. Do not repeat separated calls across approval or
session delays. Validate ownership/stale-pointer/deadline behavior before live
use. That historical action is now implemented and live-verified as recorded above. Exact existing
commands, private paths and probe limitations are in
[HANDOFF-client-baseline-gui.md](HANDOFF-client-baseline-gui.md).

Baseline remains one input PASS and eleven app BLOCKED rows; #217 stays open.
Keep production Pi `v0.85.1-psmfd.1`, #219 comparison deferred, and all activation,
backend, release and CF-2 gates intact. Provider login/network setup and actual
prior-app upgrade acceptance remain outstanding. Full protected CI/review and
merge of #224 remain pending. All 23 kept guards and Ruff were rerun successfully at wrap-up, along with
Markdown/Prettier, local links, fixture/provenance/frame hashes and whitespace.
No new protected CI success is claimed.

### Prior live result — lifetime verified, keyboard blocked

Saved branch/draft #224 match `c8abce5dd346634387f8ff01616856f8ed3a86b3`;
remote dev remains `8e5fd0cb60748ba197c60928e3b0c4895fbb1ba8`. #217 and #219
remain open. Six CodeQL checks pass on the saved head; full protected CI remains
pending. All 23 local guards and Ruff pass. Preserved input/Softnet digests match.

Post-reboot run `20261028T183947Z-e59ca2bacfab` captured its first frame then
recorded BrokenPipeError. Its code sequence points to the following flushed
stdout status print; the exact origin is inferred because the exec session
vanished. Cleanup was verified. The prior sandboxed attempt created no VM.

**Live retest:** redirected-driver run `20261028T184851Z-755e4f85b29e` stayed
alive for more than fifteen minutes with repeated requests and normal cleanup.
Desktop pixels and pointer-driven Finder View/Go menus were observed. Original,
Super, uppercase and paced diagnostic chords did not establish Go To Folder;
several follow-up captures timed out. The guest agent/OS stayed responsive and
an independent guest screenshot also showed the desktop. Keyboard input and
frame freshness remain unqualified. No app case was attempted; eleven app rows
remain BLOCKED. See [sanitized run evidence](evidence/client-baseline/gui-continuation/live-retest.json).

All original VMs are stopped/preserved, the owned clone is absent, and process
inspection confirms no QA controller/driver/Tart/Softnet process remains.
Private frames/logs/queue files are preserved. The active-console pointer now
references the cleaned retest; never send another request to it.

**One resume action:** compare input from a known RFB client against the kept
driver in one fresh owned offline guest, checking pointer press/release pacing,
keysym/modifier handling and fresh frames against guest clock/screenshot. No
alternate client was found on PATH; record the isolated probe's exact source
and dependency identity before use. Continue app installation only after
observable keyboard input works. Preserve the redirected-output launch pattern
in [the handoff](HANDOFF-client-baseline-gui.md). Production Pi remains
`v0.85.1-psmfd.1`, #219 comparison and all activation/backend gates stay deferred.
Evidence checkpoint `b9db2fe81e02c491b6775b70592374300ac5dc98` is committed,
secret-scanned (zero findings) and remotely backed up in draft #224. This
subsequent status checkpoint records its identity; read git for its own hash.
Documentation formatting/links/evidence digests and repo checks pass. Full
protected CI, usable keyboard input and all app acceptance remain pending.

## Historical reboot handoff — superseded by live continuation above

Reboot backup checkpoint `977c128ed63e8bacf57dfce41af8022d745f1c27` is committed, secret-scanned and remotely
backed up in draft [#224](https://github.com/psmfd/FingerTrap/pull/224). The following
status-only checkpoint records that identity; read `git log -1` for its own
hash. Neither this WIP nor the final input-lifetime fix is CI/live verified.

Read the [exact GUI continuation handoff](HANDOFF-client-baseline-gui.md) and
the latest WORK_LOG entry. Branch `test/client-baseline-gui` is based on verified
dev `8e5fd0cb60748ba197c60928e3b0c4895fbb1ba8` (#220: 22 head/19 merged
checks passed). The continuation is a work-in-progress checkpoint, not CI-verified
or accepted. All 23 current local guards and Ruff checks pass. All VMs are
stopped, no disposable clone or QA/CI controller remains, and private inputs
are preserved. The last interrupted launch did not create a new run.

**One resume action:** after rechecking the saved branch/remote state and host
readiness, run the atomic VNC driver, then confirm that its desktop remains
alive and responds to one guest-only Go To Folder chord. The dedicated input
pipe/persistent VNC fix is implemented but has not had this live retest.
Keep all eleven app acceptance rows BLOCKED until actually observed. Pi remains
`v0.85.1-psmfd.1`; #219 comparison stays deferred. New Dependabot #221/#222/#223
were inventoried for the next session, not reviewed or merged during shutdown.

**CF-1C — observable guest GUI, paused for reboot.** The maintainer says "Proceed"
after #220 integration. Reconcile its final 22 head/19 post-merge checks and
saved inputs, then establish observable input/frame capture in one owned clean
Tart guest. Investigate Tart's native VNC console as an alternative to the
timed-out guest AppleEvents path; preserve normal macOS privacy/Gatekeeper
behavior. Name any harness changes and their validation before implementation.
Continue keyless client cases, select and verify a prior QA app artifact, and
record provider-login inputs still needed. Production Pi remains
`v0.85.1-psmfd.1`; #219 comparison and all production activation gates stay separate.

**CF-1A/B — captured with GUI/provider blockers; acceptance open.** The
maintainer's "Proceed" approves the recorded CF-1A–F plan against production
Pi `v0.85.1-psmfd.1` only. Implemented `scripts/qualify-client-baseline.py` and
15 meaningful guard regressions in `scripts/tests/test_qualify_client_baseline.py`;
verified exact package/runtime provenance and inventoried Tart inputs, staged
only owned guests, and preserved sanitized evidence here. Wrong bytes, unsafe
archive paths, unowned cleanup and incomplete acceptance are rejected. Failure
history is retained. Continue from draft #220, preserving private state.
The second run `20261022T175149Z-c22f92748f50` records one input-verification
PASS and eleven BLOCKED cases. Exact production Pi works in the supplementary
packaged-sidecar smoke (shell echo, real Pi output and Pi termination), which
does not pass GUI rows. Finder/UI automation did not establish a usable app
window, supported test login is unavailable, and prior-app upgrade bytes are
not selected. Both owned guests are removed and original VMs preserved.
Read [inputs/results and reproduction](evidence/client-baseline/README.md).
Fifteen harness guards pass, with two red-first mutations retained. Complete
protected PR verification, then arrange an observable guest session and repeat
blocked rows. No CF-2 advance, production pin, SDK adoption, managed activation
or Pi 1.3.0 comparison is authorized by this slice.

Checkpoint `11f92741c0ee6f1779c8028e297301a7e62e81d8` is committed,
secret-scanned and remotely backed up in #220; #217 is independently read back.
Protected source head `f86413f7e7bf31a69e874e9bf88367d7d7b8c56d` passes all
22 checks; its four local macOS CI guests are cleaned. Review has zero unresolved
threads. Read the [source CI snapshot](evidence/client-baseline/source-ci.json).
The subsequent documentation checkpoint must also pass protected checks before
normal merge; final head/merge readback is recorded in #220 and #217. CI is not
baseline acceptance.

**CF-0S — baseline scope decision.** The maintainer selected the current
production runtime baseline only for CF-1/#217: `v0.85.1-psmfd.1`. Pi 1.3.0
comparison is separately tracked in deferred
[#219](https://github.com/psmfd/FingerTrap/issues/219), outside CF-1. The live
Issues #216/#217/#210 are updated and independently read back. Scope checkpoint
`674805a8dc4a26a36edc5adb6d74b266934b01b5` is committed, secret-scanned and
remotely backed up in draft [#220](https://github.com/psmfd/FingerTrap/pull/220).
Focused documentation checks pass; full protected CI and owned-PR Tart enrollment
remain pending the rest of the plan review and are required before merge.
The subsequent CF-1 execution approval is recorded above; this earlier scope
checkpoint alone did not authorize the entire implementation plan.

**CF-0 — client foundation tracking, prepared.** The maintainer selected
client acceptance, complete profile isolation and a supervised JavaScript/Pi SDK
worker as the next campaign, with extension catalog imports/review deferred until
the foundation passes. Umbrella [#216](https://github.com/psmfd/FingerTrap/issues/216)
and packaged-client baseline [#217](https://github.com/psmfd/FingerTrap/issues/217)
are created and independently read back. The
[ordered foundation and item-1 plan](client-foundation-plan.html) and
[baseline evidence contract](evidence/client-baseline/README.md) are prepared.
Current source baseline is `6fe2627d040b385fa96fdd91f7341c39214a112f`, with all
19 post-merge checks passed. Tracking checkpoint `7f75c70` is committed,
secret-scanned and remotely backed up in [#218](https://github.com/psmfd/FingerTrap/pull/218).
It merged at `538ff32df0759c2237ea685997d519df3501dd8b` after all 22 head and
19 post-merge checks passed. The subsequent CF-1 approval is recorded above.
Tracking preparation does not imply
baseline execution, a production SDK switch or runtime activation. No new
extension catalog import/review starts before the foundation exit.

**S3-M3 — final CI readback.** Late Dependabot #214 is reviewed and closed as
superseded by #211. Current source dev is
`b0292a9fb3c38f70abfec534d004c6ccf13dbac4`; all 19 post-merge checks pass.
PR #214 targets older main: all six sidecar/Tauri failures are NuGet audit NU1902
for MessagePack 2.5.302; Gitflow rejects a dependency branch targeting main.
Dev already contains MessagePack 2.5.303 and audited Vite 8.3.3. Main still needs
normal dev-to-main release promotion; closing the duplicate does not remediate
main. No audit/protection is suppressed. Read the
[exact CI snapshot](evidence/pr-merge-20261021/final-ci-readback.json) and
[#214 disposition](https://github.com/psmfd/FingerTrap/pull/214#issuecomment-6328122599).
Final tracking [#215](https://github.com/psmfd/FingerTrap/pull/215) merged at
`6fe2627d040b385fa96fdd91f7341c39214a112f` after all 22 head checks; all 19
post-merge checks pass. The new foundation campaign above is the active sequence.

**S3-M2 — golden handshake ordering follow-up.** #212 merged at `9d8b892`;
its dev run 39683025954 exposed a Linux golden-scenario ordering race (408 pass,
one failure, 12 skips). The parse response may arrive before startup hello, and
the scenario could record EOF first. A one-second delayed-hello regression proves
the failure. The scenario now awaits readiness with its existing event budget;
all 13 focused cases and 410 full-suite tests pass locally (12 opt-in skips).
Only tests/harness change; production code and golden bytes are intact. See the
[red/green evidence](evidence/golden-handshake/README.md). Protected verification
and merge state are recorded in [#213](https://github.com/psmfd/FingerTrap/pull/213).
It merged at `b0292a9fb3c38f70abfec534d004c6ccf13dbac4` after all 23 head
checks passed; all 19 post-merge checks pass. The digest-verified Apple artifact
12385811971 matches the checked source tree and passes packaged shell smoke.

Current candidate: Pi #83 at `28741c97f9baf5649e8ad3ba1c996d7bbcb5abd2`;
FingerTrap fixture source #209 at `0143c3317db5343d004bbfa1b75fd566b87c1b6c`.
Both are committed, remotely backed up and CI verified: all eight Pi and 23
FingerTrap checks pass. Read [qualification/evidence](evidence/pi-next/README.md)
and the [promotion/rollback record](evidence/pi-next/promotion-rollback.md).
Production release/pin and activation retain their separate approval gates.

**S3-M1 source integration complete.** Maintainer authorization covers source
merges and superseded drafts; release/tag/pin, SDK and managed activation retain
their separate gates. Pi #83 merged at `5f1f1dfbacb508086ed9b68563c78c3aa1456d7c`
with exactly the qualified tree. GitHub marks #82 incorporated through that
merge; its exact reference branch is restored. #81 is closed without merging
its older tree. All six Pi post-merge checks pass.

FingerTrap #209 merged at `cc3bf8855b0f748c04c44a92093a9a708381f264`
after 23 head checks; all 19 post-merge checks pass. #188 is superseded by the
accepted migration ADRs. Combined dependency #211 merged at
`260aec5067d37247b240a8641829bc520ae74bf3` after all 23 checks and artifact
verification, incorporating #199/#198/#197/#195/#190/#165, which are now closed.
All 19 checks also pass on the merged dependency commit.
The combined UI audit reports zero findings. Read the
[integration review and evidence](evidence/pr-merge-20261021/README.md).
The original backlog has zero open PRs in both repositories. This subsequent
tracking checkpoint records those results; its own CI is visible in #212.

## Authorization and scope

The maintainer approved the
[implementation plan](implementation-plan-200-profile.html) on 2026-10-20
and explicitly requested durable tracking across failures and sessions.
The approved plan covers FingerTrap [#200][import] and [#201][profile].
The initial request said #210; the plan disclosed its failed lookup and
proposed #201. The subsequent plan approval is the working authorization
for that disclosed scope. No requirements are invented for #210.

The maintainer also approved the focused S3-A through S3-F implementation
plan on 2026-10-20. Proceed within that scope without repeating approval.
Executable/RPC and Pi `v0.85.1-psmfd.1` remain the migration baseline.
Production SDK adoption, managed distribution/activation, new trusted
approval capabilities and repository retirement retain their separate gates.

The maintainer approved S3-G1 through S3-G4 and preparation of the S3-G5
qualification report on 2026-10-21. This includes the focused Pi fork patch,
FingerTrap fixture integration, companion commits/draft PRs and unpublished
qualification artifacts. Exact-pin promotion and production activation retain
their separate decisions.

The maintainer subsequently approved the maintained-base forward-port,
capability-patch limit resolution and exact-artifact requalification. Proceed
on verified fork main `42429b94e4aca33fdbb8d094de29758b36411ba1`, preserving
its newer fixes. Record a bounded patch-020 exception and actual accounting;
do not silently raise unrestricted fork limits. Exact production pin/release
and managed activation remain separately gated.

After this campaign settles, the maintainer requests a latest-Pi release
upgrade and validation campaign. Track it here now; resolve "latest" from
upstream/fork release provenance when that campaign starts, then retain the
managed policy and required fork patches and repeat protocol/artifact tests.

The maintainer subsequently selected the ten-area client foundation sequence in
issue #216: client baseline, shared contracts, completed profiles/credentials, a
supervised JavaScript/Pi SDK worker, lifecycle integration, failure/isolation
proof, complete packaging, final qualification and foundation review. Further
extension catalog import/review is deferred until that exit. The current request
authorized durable trackers and proposed item-1 plan preparation, followed by
discussion before execution. The maintainer subsequently said "Proceed" after
the production-only plan discussion, authorizing CF-1A–F execution. Later
implementation plans and existing production
release/pin, SDK adoption, native authority and managed activation gates remain
explicit; this roadmap does not mark any of them passed.

## Historical patching handoff — 2026-10-21

This historical handoff was resumed and verified on 2026-10-21. Saved branches,
remote PRs and all 426 preserved artifact entries matched. G7 corrected a test
fixture alert. The current resume action below supersedes the old restart action;
the original evidence follows for recovery.

- Repository: `/Users/pdavis/projects/_personal/FingerTrap`, branch
  `feat/managed-pi-startup`. Before this handoff, local and remote head were
  `42aab08fffc0a854f5664f11630bba68721c0e36`; this documentation-only commit
  follows it. Read `git log -1` for the handoff commit rather than resetting
  to the older checkpoint. Tested source remains `faa4242`.
- Pi branch `fix/managed-startup-maintained` is clean and remotely backed
  up at `1f37eea4bf71f95ed9a4e62674949de3a9feb8b0`. The temporary worktree
  `/private/tmp/ft-pi-maintained` is convenient, not the recovery authority.
  If patching removes it, recover the exact remote branch into a fresh
  isolated worktree; preserve the operator's sibling Pi checkout and any
  unrelated edits. Do not reuse the historical `fix/managed-startup-v1`.
- Independent stop-time checks: #209 and Pi #82 remain open drafts at the
  expected heads; #210 is open. GitHub runner registrations: zero. All Tart
  VMs are stopped; no job or qualification clone remains. No build drain or
  standalone qualification needs restarting merely because the session ended.
- Exact runtime, host, maintained comparison artifact, model-data archive
  and metadata were copied out of temporary storage. All 426 copied files
  and symlinks were checked against their sources, including the qualified
  executable hashes. Local preservation directory:
  `.artifacts/s3-discovery/patching-handoff-20261021/`.
  Its `inventory.json` SHA-256 is
  `8ae0a0357b0614696989a675f6b43ff761f80b6eb6bfe01412bd9fa90ff531a5`.
  These binary copies are ignored and local only; source and the 102 retained
  qualification evidence files are backed up through GitHub. Keep `.artifacts`
  during patching. The downloaded product package remains separately in
  `.artifacts/s3-discovery/maintained-package/`.
- Preserved artifact paths under that directory: runtime `candidate/darwin-arm64`,
  published host `host`, released comparison `baseline/pi`. Verify hashes
  before reuse. A rebuilt binary is a new artifact if its digest differs;
  previous exact-artifact results do not automatically qualify it.
- Preserve `.local-testing/`, `LEGACY_pi_config_3579dc1.patch`, and
  `NEXT_SESSION_pi-fold-reassessment.md`; they are unrelated untracked
  private state and have not been added to this handoff.

On restart, read this file and the latest WORK_LOG entries, inspect local
changes, fetch/read the companion PR heads with the repository-authorized
`TheSemicolon` identity without changing the global active account, and
reconcile any intervening changes before editing. Temporary helper scripts
under `/private/tmp` are not required recovery inputs. Recheck toolchain and
Tart readiness after patching before new builds; retain local macOS execution
and the existing hosted Windows/Linux lanes. Do not rerun completed tests
without a changed input or an outstanding concern.

Suggested restart text:

> Resume FingerTrap's Pi migration from the patching handoff in
> docs/migration/STATUS.md and the latest WORK_LOG.md entries. Verify saved
> branches and remote PR state, then continue the recorded next action within
> existing approvals. Once this campaign is settled, proceed with the latest-Pi
> upgrade and validation tracked in #210. Keep status and evidence durable.

## Resume here

**Next action: resume the saved native-VNC live-input retest after reboot.** Read
[the reboot handoff](HANDOFF-client-baseline-gui.md),
[the implementation proposal](client-foundation-plan.html#cf1-plan),
[the worksheet](evidence/client-baseline/README.md) and #217. The runtime scope
is settled: production `v0.85.1-psmfd.1` only; comparison belongs to #219.
The maintainer approved execution after the production-only scope decision.
Inputs and harness/source CI checks pass; GUI/provider acceptance remains
blocked. Verify the final #220 documentation/merge readback, then arrange an
observable interactive guest session, supported test login and exact previous
app before rerunning blocked cases. Use the worksheet's reproduction commands.
CF-2 through CF-10 remain planned; #217 stays open until maintainer acceptance.

The prior Pi source review and exact-artifact qualification are complete. #210
remains open in parallel for the separate runtime release/pin decision: review
the [promotion/rollback record](evidence/pi-next/promotion-rollback.md), resolve
the remaining Gondolin/node-forge example dependency disposition, then approve
the exact release/tag/pin transaction before publication or install.
Production remains v0.85.1-psmfd.1, with managed selection disabled. #210's first
five qualification items are complete; its production verification item stays
open. SDK/backend adoption, GUI/keychain acceptance and managed activation retain
ADR-0034's separate gates. No qualification test is pending for the named bytes.

Known blocker for a clean production dependency recommendation: two high findings
remain in the optional Gondolin example chain, with no published node-forge fix.
Core install-lock exclusion and managed non-discovery are recorded evidence,
not a blanket waiver. Do not promote without the recorded disposition decision.

- Latest stable upstream was rechecked at completion: v1.3.0,
  `71023aec1546d6d677a65669c5665c0927ce0dd0`; fork release remains
  v0.87.1-psmfd.1. Pi source `28741c97f9baf5649e8ad3ba1c996d7bbcb5abd2`
  is integrated by #83 into protected main `5f1f1dfbacb508086ed9b68563c78c3aa1456d7c`.
- Qualified ARM64 executable SHA-256:
  `901a10f67a06f04310f4d064e95f7e3e60ec523d253366731e077a239237407f`.
  Direct 18-case, published-host two-profile RPC/PTY and offline Tart matrices
  pass, including MCP canaries/rejection. Four runtime regressions and the
  host policy mutation are caught; restored source/builds pass.
- Node 22.23.0 / Bun 1.3.14 and integrity-verified npm pi-ai 1.3.0 data:
  static/full hermetic tests and actual release build pass. Source-release/npm
  model differences are retained. Hosted Linux/Bubblewrap passes.
- All 12 RPC scenarios record twice for baseline and candidate. New durationMs
  volatility failed the first recorder pass; a red-first narrow normalizer
  correction resolves it. Outbound commands/process controls match exactly;
  upstream inbound additions are reviewed. Raw pairs/drift remain; production
  0.85.1 goldens are unchanged.
- Actual C accounting is 6 patches / 23 paths / 2,021 lines (020: 682), within
  unchanged 7 / 25 / 2,200 limits. Manifest/text/both guards agree, with no
  unexplained upstream residual. Four real CodeQL findings are fixed; four
  false positives have specific proof, rationale and verified dismissal.
- FingerTrap `0143c33` passes all 23 exact-source checks. Five macOS jobs ran
  locally on Tart; Windows/Linux remain hosted. Apple artifact 12376529013
  verifies download/app/DMG digests and ARM64 binaries. Merge `5b88cdff` has
  parents `9bfc092` / `0143c33` and identical source tree. Packaged shell smoke
  passes; product Pi smoke explicitly skips because Pi is not installed.
- All five disposable CI VMs and registrations are removed. Independent
  readback: zero GitHub runners; all remaining Tart VMs stopped; base preserved.
- Source/evidence checkpoint `0143c33` is committed and remotely backed up.
  Final qualification/decision checkpoint `f8225ebc125aede6cc226fdf8bf74222ac550107`
  is secret-scanned and remotely backed up. Pi #82/#83, FingerTrap #209 and
  #210 descriptions were updated and independently read back. This subsequent
  tracking-readback checkpoint does not inherit fresh source CI.

**Recovery:** keep `.artifacts/pi-next/preserved-20261021/` across patching.
All 215 copied files match sources: runtime, host, npm model archive, upstream
source archive and Bun archive. Inventory SHA-256:
`67587ddb8e4d1d4d5755b4edc1aa0fe12dc76141fae1e2e83b6f1c6c52096ef6`.
Copies are ignored/local. Recover merged Pi commit `5f1f1df` into an isolated clone;
temporary worktree `/private/tmp/ft-pi-latest` is convenient, not authoritative.
Preserve the sibling operator checkout and three private untracked entries.
Changed executable digests require fresh qualification. Reproduction and
mutation scripts are retained with the evidence.

**Historical disposition, superseded by S3-M1:** retain Pi #82 and its qualified 0.87.1 artifact as a
reference draft, defer its production promotion, and use #83 for latest-release
review. G7 `95e95f0` is backed up in #82; all seven checks pass and alert 87 is
fixed. Runtime source remains identical to qualified `1f37eea`; its binary keeps
that source identity. The maintained report/102 evidence files remain historical.
Pi #81 / `39b729b` is the old-base experiment and must not overwrite maintained
main. S3, #200, #201 and #202 remain open.

Before future source pushes, run `python3 scripts/ci/tart-runner.py check`.
After jobs queue, run `python3 scripts/ci/tart-runner.py drain --max-jobs 5`;
use four for CI without packaging. Do not run the controller concurrently with
standalone Tart qualification. Repeat completed checks only for changed inputs
or an outstanding concern. See the [runner runbook](../local-github-runner.md).

## Work board

| ID       | Approved result                                                            | Status                                                         | Evidence / remaining work                                                                                                                                                 |
| -------- | -------------------------------------------------------------------------- | -------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| M0       | Ownership ADR, inventory and successors                                    | Committed and backed up                                        | `6a999c9`; ADR-0034 accepted; inventory and #200–#206 exist                                                                                                               |
| CF-0     | Durable client foundation tracking                                         | Prepared; checkpoint/verification recorded below               | #216 umbrella, #217 baseline, [plan](client-foundation-plan.html); tracking only                                                                                          |
| CF-1     | Packaged-client baseline                                                   | Captured with blockers; not accepted                           | [Approved plan](client-foundation-plan.html#cf1-plan), [worksheet](evidence/client-baseline/README.md); actual GUI/provider acceptance remains open                       |
| CF-2–10  | Contracts, isolated profiles, JS worker and final foundation qualification | Planned                                                        | #201/#202/#186/#187 retain implementation ownership; SDK go/no-go and foundation exit precede more catalog import/review                                                  |
| M1a      | Token/cache-meter source package                                           | Verified, backed up, inactive                                  | [Report](m1a-import.md); runtime package CI passed on all three platforms at `6a999c9`                                                                                    |
| S1       | Profile schema, compatibility and baseline                                 | Verified locally and in CI; backed up                          | `52fc6bc`; [foundation report](profile-foundation.md); complete CI and Apple Silicon package/smoke passed                                                                 |
| S2       | Reviewed import slices and test registration                               | Verified locally and in CI; backed up                          | `92137c4`; [manifest report](import-manifests.md); all platform checks and Apple Silicon package/smoke passed                                                             |
| S3       | PTY/RPC launch isolation                                                   | Foundation merged; candidate qualified; pin decision pending   | Production baseline still lacks managed policy; maintained candidate is qualified in G6; S3 is not complete                                                               |
| S3-G     | Close the pinned-Pi startup/discovery gap                                  | G1–G6 implemented, qualified and source backed up              | [Maintained report](s3-managed-startup-maintained-qualification.html); production pin/release and activation remain gated                                                 |
| S3-G6    | Preserve maintained fork fixes and requalify managed startup               | Complete; source backed up; CI verified                        | Pi `1f37eea` / FT `faa4242`; exact host/Tart/protocol/mutations pass; 7 Pi and 23 FT checks pass; dependency disposition required before production promotion             |
| Pi-next  | Upgrade to latest Pi release and validate                                  | v1.3.0 qualified and source merged; promotion pending          | Pi main `5f1f1df` / FT #209 `cc3bf88`; [evidence/decision record](evidence/pi-next/README.md); #210 stays open for release/pin decision                                   |
| S3-M1    | Review/integrate open source PRs                                           | Original backlog settled; source checks and artifacts verified | #83/#209/#211 merged; alternatives/dependency originals reconciled; [review](evidence/pr-merge-20261021/README.md)                                                        |
| CI-local | Reduce hosted builds with a local runner                                   | Merged and verified on protected dev                           | `9bfc092`; 22 post-merge checks passed, five macOS jobs local; artifact/cleanup verified; dev protection mapped; main mapping deferred to promotion; Windows/Linux hosted |
| S4       | Stores, resume and isolated meters                                         | Pending                                                        | Inject profile roots; preserve ADR-0028 and recovery behavior                                                                                                             |
| S5-A     | State/observability import wave                                            | Pending                                                        | Shared closure, prefill dependency, artifact handoff                                                                                                                      |
| S5-B     | Guards and typed tools                                                     | Pending                                                        | Enforcement tests, identity, tool ownership, vendor notices                                                                                                               |
| S5-C     | Worktree/subagent dispatch                                                 | Pending                                                        | PR #1170 fix, #280 reconciliation, child isolation                                                                                                                        |
| S5-D     | Context/presentation                                                       | Pending                                                        | Preserve tuning; gate unsupported RPC UI                                                                                                                                  |
| S5-E     | Capability-dependent source                                                | Pending                                                        | Preserve reviewed contracts/tests; #203/#204 activation gates                                                                                                             |
| S5-F     | Curated defaults                                                           | Pending                                                        | Explicit selection, rights review, restrictions and evaluations                                                                                                           |
| S6       | Integration qualification and issue reconciliation                         | Pending                                                        | Real pinned runtime, independent profiles, native/process evidence, hosted CI                                                                                             |

“Pending” means not started. Source import is not activation. A local pass
does not imply hosted CI, package installation, provider login or release
qualification. #200 and #201 remain open until their full criteria pass.

## Durable locations and recovery

- This file: authoritative current state, next action and blockers.
- [WORK_LOG.md](WORK_LOG.md): chronological changes, checks and checkpoint IDs.
- [Issue ledger](issue-ledger.md): inherited work and successor ownership.
- [Source manifest](source-manifest.json) and
  [package provenance](../../packages/agent-runtime/provenance.json):
  immutable source identities, transformations and destination digests.
- Git commits/remote branch: recoverable source checkpoints. Record actual
  push/PR/CI state here after observing it; an attempted push is not a backup.

At an interruption, preserve dirty work. Inspect the last logged command and
process state before rerunning; read diffs before assuming a step finished.
Use the pinned source identities, not a newer branch tip. Run the affected
validation after resuming an incomplete edit and update this record.

## Historical foundation checkpoints / validation state

- S3-G proposal: [HTML review document](s3-discovery-resolution-plan.html),
  41 links and 13 unique IDs checked; script syntax, 390-pixel mobile layout,
  desktop layout and section navigation passed. No remote assets. Markdown,
  structural and whitespace checks passed. These are document checks only;
  implementation approved 2026-10-21; exact-pin promotion remains gated.
- Merged migration: `docs/pi-runtime-migration`, PR #207, squash `9bfc092` on dev.
- Historical handoff branch: `docs/runtime-migration-handoff`, based on `9bfc092`.
  The remote `ci/tart-runner-pilot` branch preserves worker checkpoint
  `cf1cd50` for immutable reuse; do not delete it.
- Base: `8ffeae809f7b4579d6b4f006c3f6e8414df60620`.
- Baseline checkpoint: `6a999c9`, pushed to
  `psmfd/FingerTrap:docs/pi-runtime-migration`.
- Dependency repair: `44455f5`; S1 foundation: `52fc6bc`. Both pushed to the
  same remote branch. Source remains inactive.
- S2 import/test manifests: `92137c4`, pushed to the same remote branch.
- S1/S2 handoff: `a10f2c3`, committed and pushed. Its documentation-only
  checks are distinct from the verified S2 source checkpoint.
- S3 approval plan: `dac31ce`, committed and pushed; approved by the
  maintainer in the subsequent turn.
- S3 implementation/audit: `95424b3`, committed and pushed. PR #207 now
  describes profile/import/launch foundations and the explicit discovery gap.
- Merged review: [PR #207](https://github.com/psmfd/FingerTrap/pull/207),
  targeting `dev`; runtime activation remains disabled.
- Existing M1a local results: [recorded report](m1a-import.md).
- Checkpoint revalidation: all 20 provenance entries, lint, typecheck and
  41 meter tests passed; staged-export secret scan found no leaks.
- HTML plan: local links/anchors and JavaScript syntax verified; desktop and
  390-pixel mobile render checked; navigation works; no remote assets.
- Latest repository structural check: passed, zero errors/warnings, during
  baseline checkpoint preparation.
- S1 local: package provenance/lint/typecheck; 69 runtime tests; Release
  sidecar build zero warnings/errors; 359 sidecar tests passed, 12 skipped.
  Managed fallback mutation detected by both guard cases; restored suite green.
- Baseline CI: runtime/UI matrix, structural checks and CodeQL passed. All
  sidecar/Tauri jobs failed restore on MessagePack 2.5.302 (NU1902).
  Patched 2.5.303 plus affected locks pass local audited locked restore and
  the S1 hosted matrix.
- S1 hosted: all checks passed, including sidecar/runtime/UI/Tauri on Linux,
  Windows and macOS, CodeQL, and Apple Silicon package/smoke.
  [CI run](https://github.com/psmfd/FingerTrap/actions/runs/39443316974);
  [Apple Silicon run](https://github.com/psmfd/FingerTrap/actions/runs/39443316965).
- S2 local: complete 35-file package inventory, eight suites, 84 passing tests,
  independent offline frozen restore/validation, and omitted-suite mutation
  detected. No imported implementation changed.
- S2 hosted: all checks passed for `92137c4`, including the full platform
  matrix, CodeQL and Apple Silicon package/smoke.
  [CI run](https://github.com/psmfd/FingerTrap/actions/runs/39445278440);
  [Apple Silicon run](https://github.com/psmfd/FingerTrap/actions/runs/39445278068).
- The final status/log update changes documentation only. Its new head may
  have a fresh CI run; the source evidence above names the verified commits.
- S3 planning: standalone HTML, 42 links and 12 unique IDs checked; script
  syntax, 390-pixel mobile layout, desktop layout and current-section
  navigation passed. Markdown, structural and whitespace checks passed.
  These are document checks, not S3 implementation acceptance.
- S3 local: [report](s3-launch-foundation.md), 401 passing sidecar tests,
  12 existing skips, zero failures; four representative regressions detected
  and reverted. Published ARM64 sidecar/native companion smoke passed.
  Exact-release migration blocker reproduced and tracked in the audit.
- Local runner: `a6bdb88` pilot, `a403061` Bash correction, `cf1cd50` reusable
  worker, `e042746` routing. All backed up. Source `e042746` passed 23 checks,
  including five macOS jobs on Tart and hosted Windows/Linux/CodeQL.
  Downloaded candidate digests/source/runner identity verified; cleanup checked
  independently. Prior documentation handoff `d701bdf` was saved with CI
  skipped; its head is distinct from the verified source checkpoint. The later
  merge and protected-dev evidence are recorded below.

## Commands for the next session

```sh
git status --short --branch
git log -5 --oneline
gh run list --repo psmfd/FingerTrap --branch dev --limit 5
python3 scripts/ci/tart-runner.py check
pnpm --dir packages/agent-runtime check:provenance
pnpm --dir packages/agent-runtime test
```

Use the repository-authorized GitHub identity when checking a private source
or writing this branch; do not change the operator's global active account.
For sidecar changes, the reliable local path is audited locked restore, then
`dotnet build src-sidecar -c Release --no-restore -m:1 -p:UseSharedCompilation=false`
and `dotnet test src-sidecar -c Release --no-build --no-restore`.
Native/process checks may need the tool's filesystem/process sandbox approval.
After a new source push queues the local jobs, run
`python3 scripts/ci/tart-runner.py drain --max-jobs 5`. Do not claim a later
documentation-only or changed head has CI evidence from a different commit.

## Known gates and protected local state

- Authorized tests are limited to owned repositories, synthetic data, loopback
  fixtures and disposable environments. The maintainer clarified this scope after
  reporting additional platform safety checks. Keep defensive proof minimal;
  retain exact rejection context if a block occurs. Do not bypass safeguards.

- Exact-fork discovery and credential-source controls require verification.
  A missing control blocks managed activation; it does not authorize a pin
  bump or SDK switch.
- User-managed mode stays compatible. No automatic operator auth/settings/
  session migration; no global npm, shell-rc or operator `~/.pi` writes.
- Preserve `.local-testing/`, `LEGACY_pi_config_3579dc1.patch`,
  `NEXT_SESSION_pi-fold-reassessment.md` and pi_config's dirty local notes.
  The legacy patch is private evidence until a reviewed fix is reconstructed.
- The current `origin` uses the repository's older owner URL. GitHub confirms
  the canonical destination is public `psmfd/FingerTrap`; use the authorized
  repository account when backing up commits.

[import]: https://github.com/psmfd/FingerTrap/issues/200
[profile]: https://github.com/psmfd/FingerTrap/issues/201

## Approved foundation merge evidence

- Maintainer approved coordinated merge and protected-branch verification.
- Candidate `8196b4f`: [CI](https://github.com/psmfd/FingerTrap/actions/runs/39530284288)
  and [package](https://github.com/psmfd/FingerTrap/actions/runs/39530284279) passed;
  all 23 distinct checks passed (25 reports including repeated title/gitflow).
- Nine controller tests pass locally and in `repo-check`; wildcard-binding
  mutation fails the real socket-binding assertion. Live gateway-only canaries
  passed before all five registrations. CodeQL #8 is fixed, not dismissed.
- Merge `9bfc092` has the exact tested tree. Independent readback confirms only
  three `dev` contexts changed; `main` and the worker source ref are unchanged.
- Local snapshots and phase/evidence JSON are in `.artifacts/tart-rollout-207/`.
  Source changes are merged and backed up; protected dev validation is complete.
- Protected dev CI `39531687800` and packaging `39531859652` passed. All 22
  reported checks passed; five local job VMs/registrations cleaned up. Artifact
  `12316018036` archive SHA-256
  `ee2ec15073e50ac55173bb075fca4052be904e2ea4ef6382436840b55b7ea1d1`
  and app/DMG digests match; embedded source and runner match the merged build.
- Final handoff changes documentation only. It is saved with CI skipped on
  `docs/runtime-migration-handoff`; include it in the next implementation PR.
  #208 remains open for later main-promotion mapping and scheduling decisions.

## 2026-11-07 — open-source UI automation evaluation requested

Maintainer approves the interrupted checkpoint command and asks whether an open-
source automation solution should script interface execution. Resumed checkpoint
verification shows all CAPTURE-3 changes preserved, not yet staged/committed; no
new guest start. Research recommendation (not adoption/installation): evaluate
Peekaboo released CLI plus signed GUI Bridge inside a fresh disposable guest.
Its documented semantic element/window/menu commands and JSON results can be
driven deterministically; permissions guidance specifically covers wallpaper-
only background-session captures and prefers the GUI Bridge for these contexts.
Primary sources: [Peekaboo](https://github.com/openclaw/Peekaboo), its
[permissions guide](https://github.com/openclaw/Peekaboo/blob/main/docs/permissions.md)
and [platform support](https://github.com/openclaw/Peekaboo/blob/main/docs/platform-support.md). Released app/CLI target macOS 15+. Main-branch docs
are candidate capabilities, not proof that any chosen release/our guest works.

Compare Hammerspoon (Lua/Accessibility/CLI scripting) and Appium Mac2 (native
XCTest/WebDriver, requires Xcode). Proposed CF-1C-AUTOMATION-1: pin/review one
matching Peekaboo app+CLI release and signatures/digests; guest-only installation
and supported permissions; deterministic Finder menu/dialog/text scenario with
observable window assertions, fresh screenshots and independent RFB comparison;
owned cleanup and exact artifact/permission provenance. Keep model-agent/MCP/cloud
features out of this offline deterministic pilot. Retain current Tart ownership/
budget/evidence wrapper. No automatic adoption, host/global installation, product
dependency change, TCC bypass or app-case acceptance. Existing direct UI re-
enrollment remains a separate proposed diagnostic; evaluate tooling before more
custom UI orchestration. Pi v0.85.1-psmfd.1, deferred #219 and all gates intact.

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

AUTOMATION-1 artifact review: v4.9.8 asset/inventory/signing digests match; source
70dbc4183f390a5a72d5dc01d8a77eb672217070, Developer ID team FWJYW4S8P8. App zip
paths and six framework symlinks remain contained. Passive host extraction only,
no tool launch/installation. App identifier boo.peekaboo.mac, minimum macOS 15.0,
source stamp matches, stapled ticket observed. App/deep library and ARM64 CLI
strict signature checks pass outside the sandbox. Initial sandboxed CLI signature
check reported invalid/authority unavailable; unrestricted read-only verification
passes, so no corrupt-artifact claim. CLI requires its shipped Swift compatibility
dylib. Next: stage these pinned inputs and implement Bridge-only QA readback.

AUTOMATION-1 preflight: matching app/CLI/dylib provenance reviewed and pinned in
peekaboo-pilot-inputs.json. Dedicated guest-only pilot and minimal prepare/readback
hooks implemented locally; 43 kept guards/Ruff/repository checks pass. Intentional
Bridge-local fallback regression fails the kept guard; restored code passes.
Branch/draft head a0aa265 verified; prior implementation 8876c58 CI remains green.
Five original VMs stopped, no disposable clone. Next: one bounded live boot with
normal GUI app permission enrollment, fixed Finder scenario, independent frames/
clock and owned cleanup. Source is not yet committed or CI-verified.

AUTOMATION-1 live run 20261107T171635Z-8d74599c3c32: guest signature/source
verification succeeds and canonical GUI Bridge responds with source=bridge.
All three grants initially denied, blocked capture and immediate guest clock
preserved. Native menu item pointer attempts dismiss menu without opening UI;
Down then Return visibly selects/opens the permission checklist. This is bounded
native keyboard evidence, not Peekaboo input qualification. Normal GUI Screen
Recording Grant produces correctly attributed Peekaboo prompt. Next: finish
normal guest app permission enrollment then run the fixed Finder scenario.

AUTOMATION-1 result: run 20261107T171635Z-8d74599c3c32 aborted safely on
Peekaboo type exit 1/INTERACTION_FAILED, outcome indeterminate/may_have_dispatched,
retry unsafe. Immediate independent RFB and Bridge pixels both visibly show exact
/tmp/ft-input-probe in Finder Go to Folder. The CLI error is preserved; do not
blindly replay. GUI app Screen Recording + Accessibility normal enrollment,
macOS Quit & Reopen, supported Allow prompt, then normal app quit/relaunch yield
all three Bridge grants. Bridge full-window capture works, unlike prior agent
capture. Semantic Go menu delivery resolves to fresh AX GoToWindow/PathTextField;
used_cache=false. Escape/chord not reached. Owned cleanup verified, five originals
stopped/preserved. No app validation and baseline unchanged.

Next bounded comparison CF-1C-AUTOMATION-1B: preserve indeterminate type failure,
then resolve once with mandatory fresh Finder AX value plus independent images;
never repeat typing. Continue Escape and keyboard shortcut only if the exact
field is observed. Strengthen dialog/freshness/readback checks and guards before
one fresh pilot with identical pinned artifacts, deadlines and owned cleanup.

AUTOMATION-2 implementation prepared locally: dedicated fixed client-observe
module reuses pinned Peekaboo staging/enrollment and existing atomic owner;
installed ID/three binary hashes checked before single LaunchServices open.
Mandatory native/Bridge screen+clock follows every install/launch/window/AX/
process probe. Fifty kept guards/Ruff pass. Intentional cached/empty/wrong-app
UI acceptance regression fails kept guard; restored code passes. Prior source
CI remains green and draft/head verified. Next: bounded live run with normal
permissions, at most three read-only observations, explicit finish and cleanup.

AUTOMATION-2 run 20261107T175616Z-5064033904d3 completed three uncached AX and
native/Bridge readbacks, installed identity proof and independent process inventory;
explicit finish after 21 queue actions. App PID 933 and child sidecar 940 use the
verified installed paths. Visible window 80 exposes both required controls and
an actionable missing-Pi default pane. No Pi/profile/provider setup performed.
Original live guard blocked because it counted unnamed off-screen helper 85.
Before correction: classify exactly one observed visible titled FingerTrap main
window, bind its ID to the fresh AX target and retain partial-inventory warnings.
Do not claim complete global window inventory. Preserve original blocked result;
validate corrected classification by replay of the retained independently acquired
snapshots/screens/clocks after cleanup, not by relaunching or fabricating live
callback success. Add kept real-inventory regression fixture. Baseline unchanged.

AUTOMATION-3 delivery refinement before implementation: one compound Peekaboo
foreground type call carries the fixed printf/base64-decode/tee command plus its
single trailing Return, using --accept-dispatched only to preserve unverified
receipts without treating them as success. No separate Return after uncertain
input. Fresh AX must first identify the shell tab and focused terminal receiver.
Read-only proof-file bytes plus independent terminal pixels decide execution;
encoded command echo does not contain the decoded output marker. Never replay.

AUTOMATION-3 preflight: dedicated fixed shell-probe module implemented; reuses
corrected live client admission and existing atomic owner without reimplementation.
All 56 kept guards/Ruff pass. Intentional echoed-command/foreign-run proof
acceptance regression fails; production restored. Fresh AX snapshot/unique button,
shell tab/focused receiver and absent proof are enforced before delivery. One
compound input with Return; all subsequent probes read-only. Next: bounded live
run, independent file/pixel/ancestry review, explicit finish/verified cleanup.

AUTOMATION-3 live run 20261107T181834Z-f41358dcd0f5: corrected main-window
classifier now passes the actual live callback, two uncached snapshots of window 80. Installed identities and independent readbacks verified; guest proof absent
before UI mutation. Next fixed step is unique fresh New shell tab action and
one command/Return, followed only by proof/image/ancestry observations.

AUTOMATION-3 first run 20261107T181834Z-f41358dcd0f5 failed safely: corrected
live window admission passed, one New shell tab and focused AXTextArea observed.
Single UI command/Return dispatched unverified. Native/Bridge images show at
least the first intended delimiter lost: zsh reports command not found printf%s,
versus requested printf %s. The tee-created proof file is empty; exact-byte guard
rejects it and owned cleanup verifies. No input replay, no positive shell proof.

Next bounded atomic comparison CF-1C-AUTOMATION-3B: fresh guest, same pinned
inputs, app admission, tab selection and encoded marker command; change only
foreground typing cadence to 50ms per character (under existing 30s command cap).
One delivery/Return, observe proof and pixels, never retry input in the same guest.
Retain original failed run as real red proof of false-success prevention. Existing
CF-1/shell approval persists; no product/runtime/provider/gate changes.

AUTOMATION-4 identity refinement before implementation: first shell command also
sets an unexported run-bound FT_CF1_SURVIVOR variable in that interactive shell.
Each proof adds its sentinel value. Original proofs must retain the value; newly
spawned sibling must not inherit it. PID+ancestry+sentinel checks prevent treating
a replacement shell as the surviving original. This is guest shell state only.

AUTOMATION-4 preflight: fixed guest-shell-default fixture, four run-bound
PID/stty/sentinel proofs, exact resize and guarded split/sibling-close implemented.
All 62 kept guards/Ruff pass; guest fixture Python/JSON syntax verified. Intentional
bypass of sibling identity admission fails the kept mutation-path guard; production
restored. Commands fit 30s cap at 50ms cadence. Next: one bounded live owned run,
normal Bridge permissions, proof/image/process review and explicit finish/cleanup.

AUTOMATION-4 failed run 20261107T200710Z-adb15f1b0c25: supported shell-default
fixture and live window admission verify. First 254-character proof at 50ms
cadence returns INTERACTION_FAILED/indeterminate/response_lost, NSPOSIX timeout;
independent pixels show partial line, no proof file exists. It stops before resize,
split or close, no blind input replay, owned cleanup verified. The outer 30s cap
was insufficient to predict the Bridge response budget; preserve actual failure.

Next bounded CF-1C-AUTOMATION-4B: fresh guest, same fixture/input artifacts/50ms
cadence and assertions. Compact each proof to at most 150 characters, using the
run's existing 12-hex nonce in marker/unexported FT4 sentinel and short owned
/tmp/ft4-{b,r,s,a} paths. Preserve absolute tool paths, PID/stty/sentinel fields,
one command/Return per phase, absent-file guard and exact marker binding. No
chunked or repeated input; all other bounds/cleanup/gates unchanged. Add a kept
command-length guard that rejects the original longer pattern before boot.

AUTOMATION-4B preflight: compact proof lengths 144/127/127/127 characters; 50ms
cadence unchanged. All 62 guards/Ruff pass, including 150-character maximum;
intentional long-command regression fails. Fresh run 20261107T203154Z-15a8da01a49a
has all three canonical GUI Bridge grants and is executing the fixed scenario.
Original 254-character timeout attempt remains private and independently cleaned.
