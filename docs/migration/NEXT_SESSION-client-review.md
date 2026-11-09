# Next session — client review and evidence coverage

```text
Resume FingerTrap in /Users/pdavis/projects/_personal/FingerTrap.

Fetch origin and first read origin/docs/client-review-handoff-20261109-coverage:
CONTRIBUTING.md, docs/migration/STATUS.md, HANDOFF-client-baseline-gui.md,
issue-ledger.md and the latest WORK_LOG entry. Keep that handoff branch separate
from test/client-normal-quit.

Refresh PR #227 against dev: maintainer review, CodeRabbit reviews/threads and CI.
Handoff code head is 76c1269c6699b86456dc38aa050d2aeb4ff30543, ready/unmerged
at last snapshot unless the maintainer has since merged it. Evidence split d8c2139
and optimization-safe recipe 76c1269 preserve all 552 hashes and observations.
Summary schema v2 references two v1 manifests; read evidence/README.md and the
baseline README for the pattern and exact preservation recipe. GitHub patches
reproduce all three files; completed full CodeRabbit coverage is linked in the
handoff. Refresh the latest requested full review. The separate 10.71% docstring-
coverage warning remains for validation and maintainer decision.

Both ordered repeated-launch and fresh Quit runs already passed with cleanup.
Do not repeat completed VM tests or start a new GUI slice without cause. Fix only
validated review findings on test/client-normal-quit; run affected checks and
refresh current-head CI/CodeQL. Material probe/product changes require affected
bounded validation; docs-only edits do not require another VM run.

Maintainer owns review and merge. Do not merge or enable auto-merge. If already
merged, verify actual merge commit and dev readback before proposing next work.
Preserve private evidence/unrelated files; never reuse cleaned pointers. Five
original VMs were stopped with no owned clone/QA processes at shutdown. Baseline
one input PASS/eleven BLOCKED, Pi v0.85.1-psmfd.1, #219 Pi 1.3.0 deferred and all
acceptance/activation/backend/release gates remain. Existing CF-1 approval persists.
Update migration records during resumed work.
```
