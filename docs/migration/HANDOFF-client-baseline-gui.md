# CF-1C session handoff — 2026-10-29

The maintainer requested session wrap-up. Read [STATUS](STATUS.md), the latest
[WORK_LOG](WORK_LOG.md) entry, CONTRIBUTING, the
[approved CF-1 plan](client-foundation-plan.html#cf1-plan) and ADR-0034.
Execution approval persists for CF-1 against production Pi only.

## Saved branch and scope

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

Verified 2026-10-29: all original VMs stopped/preserved; no disposable clone or
QA driver/controller/comparison/Tart/Softnet process remains. Latest state is
`cleaned`, `cleanupVerified=true`, with no controller events. The active-console
pointer refers to the cleaned latest run and must not receive another request.
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

## One concrete resume action

Reverify branch/draft, tools, input hashes, original stopped VMs, Softnet
permissions/digest and controller lock. Then **name and implement a bounded
atomic comparison wrapper** around the existing driver/probe commands below:
boot a fresh owned guest, identify its new first frame, send paced pointer and
keyboard input, immediately capture guest screen and clock independently,
retain observations, and finish/verify owned cleanup in `finally`.

The wrapper has **not** been implemented. Validate fresh-run versus stale-pointer
selection, ownership refusal, observation deadline and cleanup before its live
run. Keep host home/credentials/clipboard unshared and the guest offline.
Do not spread boot and observation across separate approval/session delays.

The existing commands to compose are shown for reproduction; this is not yet
an atomic runner. The driver must run with Tart filesystem access outside the
sandbox and its output redirected privately:

```bash
driver_log=".artifacts/client-baseline/driver-$(date -u +%Y%m%dT%H%M%SZ).log"
python3 scripts/client_baseline_vnc.py \
  --archive .artifacts/pr-merge-20261021/golden-fix-apple/candidate.zip \
  --runtime-archive .artifacts/client-baseline/inputs/pi-darwin-arm64-v0.85.1-psmfd.1.tar.gz \
  --hold-seconds 1800 > "$driver_log" 2>&1
```

The isolated Go probe is already compiled. Rebuild offline if needed from its
private directory using the verified module/build cache:

```bash
cd .artifacts/client-baseline/rfb-compare
GOMODCACHE="$PWD/modcache" GOCACHE="$PWD/buildcache" GOPROXY=off \
  go build -o rfb-probe .
```

From repository root, these existing probes require the fresh active owned run:

```bash
python3 .artifacts/client-baseline/rfb-compare/probe.py probe \
  '[{"Name":"escape","Keys":[65307]},{"Name":"lowerg","Keys":[65511,65505,103]},{"Name":"upperg","Keys":[65511,65505,71]}]'
python3 .artifacts/client-baseline/rfb-compare/probe.py screen
```

`probe.py screen` captures through the guest agent, transfers the JPEG in 64 KiB
chunks and saves a guest clock readback. The old large-output disconnect is
avoided. Both probes save under a new private `independent-*` subdirectory.
The screenshot probe itself has not yet completed live in the latest run.
Use actual PNG dimensions for pointer coordinates: the image viewer scales them.
Create `finish-session` in the owned run to end early; inspect actual process/VM
absence afterwards. Recover only recorded owned leftovers with
`qualify-client-baseline.py cleanup --run ...`.

Reproduce kept guards without a guest:

```bash
python3 -m unittest discover -s scripts/tests -p 'test_*.py' -v
ruff check scripts/qualify-client-baseline.py scripts/client_baseline_vnc.py scripts/tests
```

## Next-session prompt

> Resume FingerTrap in /Users/pdavis/projects/_personal/FingerTrap. Read
> CONTRIBUTING.md, docs/migration/STATUS.md,
> docs/migration/HANDOFF-client-baseline-gui.md and the latest WORK_LOG entry.
> Verify test/client-baseline-gui and draft #224. Continue CF-1C by implementing
> the bounded atomic boot/RFB-probe/immediate guest-screen-and-clock/cleanup
> wrapper described in the handoff; the independent pinned go-vnc client has
> reproduced follow-up frame timeouts, and its post-input screen check expired.
> Preserve private evidence and don't reuse the cleaned active-console pointer.
> Continue app validation only after observed input/frame freshness work. Keep
> production Pi v0.85.1-psmfd.1 and defer Pi 1.3.0 comparison in #219.
