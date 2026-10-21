# Working in FingerTrap

Read [CONTRIBUTING.md](CONTRIBUTING.md) for repository conventions, test
requirements, vendored-code provenance and branch/PR rules.

## Active runtime migration

For pi_config migration, runtime imports or managed-profile work, begin with:

1. [Current status and resume instructions](docs/migration/STATUS.md).
2. [Approved implementation plan](docs/migration/implementation-plan-200-profile.html).
3. [Activity log](docs/migration/WORK_LOG.md), starting at its latest entry.
4. [ADR-0034](adrs/0034-own-agent-runtime-and-retire-pi-config.md) and the
   affected component's inventory/provenance.

Maintain these records during the work, not just at session end:

- Before changing a slice, record its ID, intended change and validation in
  `STATUS.md`; append an entry to `WORK_LOG.md`.
- After a meaningful result or failed check, record the outcome and exact
  next action. Distinguish implemented, locally verified, committed,
  remotely backed up, CI-verified and activated.
- Checkpoint reviewed changes in scoped commits. Record commit/PR identities
  in the next status update; do not fabricate a self-referential commit hash.
- Before handoff, leave one concrete resume action, known blockers and
  commands needed to reproduce outstanding checks. Temporary logs can help,
  but durable summaries and essential fixtures belong in the repository.

The approval recorded in the status document persists across sessions.
Continue within that scope without asking for the same approval again.
Keep activation and backend gates in ADR-0034 intact. Preserve unrelated
working-tree changes and private local evidence.
