# CF-1C session handoff — 2026-11-07

## Latest continuation: CF-1C-CAPTURE-1

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
rejection makes the new guard fail; unchanged production source passes. Source
and evidence are locally verified; commit/remote/CI identities will follow.

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

## Current resume prompt

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
