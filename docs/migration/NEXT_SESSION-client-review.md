# Next-session prompt — client review handoff

Copy the following prompt into the next session:

```text
Resume FingerTrap in /Users/pdavis/projects/_personal/FingerTrap.

Fetch origin and start by reading the separately backed-up documentation branch
origin/docs/client-review-handoff-20261109. Read CONTRIBUTING.md,
docs/migration/STATUS.md, docs/migration/HANDOFF-client-baseline-gui.md,
docs/migration/issue-ledger.md and the latest WORK_LOG entry. Do not push this
handoff branch onto the code PR merely to synchronize the records.

Refresh PR #227's state, head, review, CodeRabbit comments/threads and CI against
dev. Handoff snapshot: ready and unmerged at
1b6f10d98ba85a59ce37f5e0e5adf35fa4cb2113; 77 guards, all 14 CI jobs and CodeQL
pass. CodeRabbit COMMENTED flags docstring coverage and incomplete bot review
of repeat-launch-observation.json; refresh and validate these items.

The requested repeated-launch validation and subsequent fresh bounded Quit rerun
both passed, in that order, with source identities and private evidence in the
handoff. Do not redo completed tests or start a new GUI slice without cause.
Address validated review changes on test/client-normal-quit and run affected
checks. Material probe/product changes require the affected bounded validation;
documentation-only changes do not require another VM run.

The maintainer owns review, optional scanning and the merge. Do not merge or
enable auto-merge. If already merged by the maintainer, verify and record its
actual merge commit and dev readback before proposing the next bounded slice.

Preserve private evidence and unrelated files; never reuse a cleaned console
pointer. Five original VMs were stopped with no owned clone/QA processes at
shutdown. Baseline remains one input PASS/eleven BLOCKED; keep Pi
v0.85.1-psmfd.1, defer #219's Pi 1.3.0 comparison and preserve every acceptance,
activation, backend and release gate. Existing CF-1 execution approval persists
within that scope. Update the migration records during resumed work.
```
