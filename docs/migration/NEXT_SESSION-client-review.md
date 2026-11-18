# Next session — Pi upgrade and SDK evaluation

The [active tracker and first execution plan](STATUS.md#pi-upgrade-and-sdk-evaluation-tracker--2026-11-18)
supersede the resume action in the historical review readback below.

```text
Fetch origin and read CONTRIBUTING.md, docs/migration/STATUS.md,
docs/migration/issue-ledger.md and the latest WORK_LOG entry.
Resume from the active Pi upgrade and SDK evaluation tracker in STATUS.

The maintainer agreed to keep executable/RPC for the Pi upgrade and evaluate
the SDK against the same qualified fork revision once profile isolation is ready.
The first execution plan was approved. The read-only fork delta is complete
and recommends a scoped v1.5.2 port for qualification, with v1.3.0 as fallback.
Verified #225 passes CF1-01 through CF1-03. CF1-04 passed on #230 candidate
14445949024. The maintainer merged #230 as 34a755a with the same reviewed tree.
Integrated CI is 14/14 and CodeQL passed. Packaging and the four macOS CI
jobs passed on local Tart runners. Verified integrated artifact 14456220605
and production Pi v0.85.1-psmfd.1 passed CF1-05 real Pi PTY and CF1-06 native
Pi RPC in separate Tart guests. CF1-06 used a guest loopback model and proved
stream, abort/recovery, confirmation and session resume. All owned guests
and runners were removed. Worksheet evidence is six PASS and six BLOCKED.
Next: scope CF1-07 supported provider login and its guest network and
credential-entry path.

Keep the baseline at v0.85.1-psmfd.1. #219 remains deferred until baseline
acceptance and a separate reviewed comparison plan. Preserve all release,
backend and activation gates. Both bounded live validations already passed
with cleanup. Preserve private evidence and unrelated files and never reuse
cleaned pointers. Keep the handoff separate from test/client-normal-quit.
```
