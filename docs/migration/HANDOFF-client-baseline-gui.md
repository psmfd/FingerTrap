# CF-1C session handoff — 2026-11-07

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

## Next-session prompt

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
