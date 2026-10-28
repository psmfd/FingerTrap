# CF-1C reboot handoff — 2026-10-28

The maintainer requested an immediate session end for reboot. Resume this WIP
after reading [STATUS](STATUS.md), the latest [WORK_LOG](WORK_LOG.md) entry,
[approved CF-1 plan](client-foundation-plan.html#cf1-plan), CONTRIBUTING and
ADR-0034. Existing execution approvals persist.

## Saved state and boundaries

Saved implementation checkpoint: `977c128ed63e8bacf57dfce41af8022d745f1c27`, remotely backed up in
draft [#224](https://github.com/psmfd/FingerTrap/pull/224). The final status-only commit follows it;
read the branch head and latest tracker comment for the exact final SHA.

- Branch: `test/client-baseline-gui`; base/remote dev:
  `8e5fd0cb60748ba197c60928e3b0c4895fbb1ba8`.
- #220 is merged: 22 final-head and 19 post-merge checks passed. Those results
  qualify its committed baseline harness, not this new WIP continuation.
- Current continuation: 23 local guard tests and Ruff pass. Native VNC obtains
  a real desktop frame; the final lifetime/input fix is **not live verified**.
  Full protected CI, review/enrollment and merge are pending.
- Baseline #217 still has one mandatory input PASS and eleven BLOCKED rows.
  No actual app case passed during this continuation. No CF-2 advancement.
- Production runtime stays `v0.85.1-psmfd.1`. Pi 1.3.0 comparison is deferred in
  #219. SDK adoption, managed activation, release/pin and native authority gates
  remain intact. Further extension catalog work remains deferred.
- New open Dependabot #221 (Cargo), #222 (Actions), #223 (UI npm) were read only.
  Reconcile their current state before any future integration; don't silently
  change the frozen app/runtime baseline.

At shutdown all original VMs are stopped; no disposable baseline/CI VM and no
QA driver/CI controller remains. Do not resume an old runner drain just because
the machine rebooted. `active-console.json` points to a **cleaned** old run;
the next successful driver session replaces it.

Preserve `.local-testing/`, `LEGACY_pi_config_3579dc1.patch`,
`NEXT_SESSION_pi-fold-reassessment.md`, all ignored `.artifacts/` evidence and
the operator's sibling repositories. Don't reset, stash or remove these to
make the worktree appear clean. Private data stays out of commits.

## What changed and what failed

- `scripts/qualify-client-baseline.py`: mutually exclusive native `--vnc` and
  window `--graphics`; private per-run console log; owned stopped-VM cleanup
  now skips an invalid stop operation before deletion. The stopped-VM regression
  failed before the fix and passes after it.
- `scripts/client_baseline_vnc.py`: bounded, authenticated loopback console,
  pixel/resize limits, virtual input queue and atomic boot/first-frame capture.
  Existing host `cryptography` is loaded only for VNC authentication; it is
  not an application/runtime dependency or CI guard prerequisite.
- Six console guards plus 17 baseline guards run through `scripts/tests/`;
  repo-check discovers all `test_*.py` there.

Relevant private runs:

| Run                             | Actual outcome                                                                                                                             |
| ------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------ |
| `20261023T004000Z-26339eea6cb5` | Guest ready; deadline cleaned it before the separate probe.                                                                                |
| `20261023T132854Z-e69e67da8c60` | VNC authenticated, then framework warning/reset on missing DesktopSize capability; stopped-VM cleanup bug reproduced, fixed and recovered. |
| `20261023T133506Z-b727c20f4c58` | DesktopSize negotiation produced a real 1824×1368 PNG; wrapper exited before first input; cleanup verified.                                |

The final patch keeps a dedicated unwritten input pipe open for the `script`
PTY wrapper, closes it after exit, and keeps one authenticated VNC connection
for the observation session. `/dev/null` previously sent EOF to the PTY.
**That diagnosis/fix still needs the live lifetime/input retest.** The last
aborted launch did not create another run. Preserve the failed attempts; don't
rewrite them as app failures or passes.

Primary console behavior was checked against installed Tart 2.32.1 and its
[native VNC implementation](https://raw.githubusercontent.com/cirruslabs/tart/2.32.1/Sources/tart/VNC/FullFledgedVNC.swift).
No macOS privacy database or Gatekeeper setting was changed.

## Preserve and reverify these exact local inputs

The [reboot input inventory](evidence/client-baseline/gui-continuation/reboot-inputs.json)
records relative paths, sizes and SHA-256 digests, rechecked at handoff. These
binary copies are local only; keep `.artifacts/` across reboot.

- Current app: `.artifacts/pr-merge-20261021/golden-fix-apple/candidate.zip`;
  artifact 12385811971, source `0a287c4eb6769ee810330de6d625861f241f65ce`.
- Production Pi: `.artifacts/client-baseline/inputs/pi-darwin-arm64-v0.85.1-psmfd.1.tar.gz`;
  asset 553290482, source `e7d09c9dc5fca409f46a5476669e3c0404054d69`.
- Possible previous QA app: `.artifacts/pr-merge-20261021/apple/candidate.zip`;
  artifact 12382323858, source `42bb1b2a210acf30e17de1744257a60988398ecc`.
  Its published artifact digest was independently read and its cached archive
  rehashed, but it has **not been selected/exercised as upgrade acceptance**.
  Both candidates say app 0.1.0; this is not a published-release upgrade claim.
- Desktop PNG and all raw run logs/states remain private under their run IDs.
  VNC URLs include generated connection secrets; never print/commit them.

## One concrete resume action

First verify local/remote saved heads and current #217/PR state using the
repository-authorized `TheSemicolon` identity per command; don't switch global
GitHub authentication. Fetch/reconcile intervening changes without overwriting
private state. Check `tart --version`, `tart list --format json`, required host
Python/cryptography, the reviewed Softnet helper and the shared controller lock.

Run only the kept atomic driver, which rechecks input digests and owns cleanup:

```bash
python3 scripts/client_baseline_vnc.py \
  --archive .artifacts/pr-merge-20261021/golden-fix-apple/candidate.zip \
  --runtime-archive .artifacts/client-baseline/inputs/pi-darwin-arm64-v0.85.1-psmfd.1.tar.gz \
  --hold-seconds 1800
```

It prepares a new run, obtains first pixels, writes the private
`.artifacts/client-baseline/active-console.json`, and accepts bounded request
files inside that run. Wait for the first-frame result and inspect its image;
do not infer success from a startup log. It is offline, with only verified QA
inputs shared read-only and clipboard sharing disabled.

Then send **one** guest Go To Folder chord and inspect the response image. This
uses only workspace writes; the already-approved driver supplies virtual input
to that owned guest. No helper from `/private/tmp` is needed:

```python
import json, time, uuid
from pathlib import Path

active = json.loads(Path('.artifacts/client-baseline/active-console.json').read_text())
run = Path(active['run'])
state = json.loads((run / 'state.json').read_text())
assert state['phase'] == 'guest-ready' and state['consoleMode'] == 'native-vnc'
identifier = uuid.uuid4().hex[:12]
request = run / f'request-{identifier}.json'
temporary = request.with_suffix('.tmp')
temporary.write_text(json.dumps({'action': 'chord', 'keys': [65511, 65505, 103], 'delay': 1}))
temporary.replace(request)
response = run / f'response-{identifier}.json'
deadline = time.monotonic() + 25
while not response.exists():
    assert time.monotonic() < deadline, 'Console response deadline'
    time.sleep(0.2)
print(response.read_text())  # Contains image path/result, never the VNC URL.
```

If first frame plus input remain alive, continue clean Finder installation
before optional guest test tools, then observed keyless client cases. If they
fail, retain the new result and name the next bounded substrate correction;
don't churn through app tests on a dead console. Real supported-provider login
requires operator input through the supported guest UI and appropriate recorded
network setup; don't copy host credentials. The current driver is offline.

Create `finish-session` inside the run to stop early; the deadline also cleans
the owned guest. Inspect actual process/VM absence afterwards. Recover only
recorded owned leftovers with `qualify-client-baseline.py cleanup --run ...`.

Reproduce the completed local checks without launching a guest:

```bash
python3 -m unittest discover -s scripts/tests -p 'test_*.py' -v
ruff check scripts/qualify-client-baseline.py scripts/client_baseline_vnc.py scripts/tests
```

Before merging this WIP, finish review and the full protected matrix. Enroll
only the owned PR in existing Tart routing; preserve the pinned worker and
restricted group. No CI check or acceptance blocker was waived for reboot.

## Suggested next-session prompt

> Resume FingerTrap in /Users/pdavis/projects/_personal/FingerTrap from
> docs/migration/STATUS.md, docs/migration/HANDOFF-client-baseline-gui.md and the
> latest WORK_LOG.md entry. Verify the saved checkpoint, remote PR state and
> stopped guests, then retest the persistent native-VNC input path and continue
> approved CF-1 production-baseline validation. Keep Pi v0.85.1-psmfd.1, defer
> #219's Pi 1.3.0 comparison, preserve private evidence and all activation gates,
> and keep status/results durable. Do not treat the existing desktop frame or
> passing unit tests as app acceptance.
