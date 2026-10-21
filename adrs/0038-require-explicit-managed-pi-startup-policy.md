# 0038 — Require explicit managed Pi startup policy

- Status: Accepted
- Date: 2026-10-21
- Authorization: maintainer-approved S3-G implementation plan
- Extends: [0036](0036-resolve-managed-launches-before-process-creation.md)

## Context and problem statement

The pinned Pi executable migrates project `.pi/commands` before evaluating
project trust. Bootstrap settings precede normal argument parsing, and resource
disable flags do not prevent package resolution. Environment replacement alone
cannot make those startup paths explicit. The retained probe reproduces the
project rename using synthetic state and a digest-verified binary.

## Considered options

- Disable migrations alone: leaves other settings/resource/lifecycle paths.
- Hide project state or change cwd: changes the user's project semantics.
- Upgrade the fork: newer inspected source retains the startup ordering.
- Switch to an SDK worker: requires another backend and its own policy.
- Add an explicit executable startup policy and qualify exact artifacts.

## Decision outcome

Choose an opt-in `--startup-policy managed-v1` executable contract. Recognize
and validate it before command dispatch, cleanup, settings and migrations;
audit module initialization too. Reject unknown policy versions, duplicate
selectors and incompatible arguments before startup side effects. Preserve
the ordinary CLI behavior when the selector is absent.

Managed startup uses explicit in-memory defaults, the real project cwd,
profile-owned agent/session paths, a literal system prompt and explicitly
selected local extension files. Skip implicit project/profile migrations,
settings/auth/model/keybinding loading, package resolution/install, automatic
helper downloads, discovered extensions/resources, context/instruction files,
and SYSTEM/APPEND_SYSTEM prompts. Do not reinterpret the literal prompt as a
file path. Built-in assets needed for the interface come from the verified
artifact; they do not authorize user theme discovery or background setup.

Carry an immutable typed policy through service creation, reload and same-cwd
new sessions. Reject resource expansion and unsupported install/config/auth,
resume/import/switch/cwd operations before they read or mutate state. Support
for those operations is a later explicit contract, not an ambient fallback.
S3 remains keyless. This policy controls startup/resource discovery; it is not
an OS sandbox or a restriction on trusted extensions' arbitrary code.

FingerTrap descriptor v2 requires the `managed-v1` capability together with
an exact host-selected candidate revision and the existing complete digest
inventory. The trusted descriptor identity remains independent of its contents.
Do not infer support from a version string or a child process's claim. Reject
old/unsupported descriptors, unselected revisions and modified files before
spawn. Pass the policy selector through the shared PTY/RPC resolver.

Only policy-capable internal fixtures may relax the project `.pi` rejection.
Retain profile startup-config, credential, override and resume guards.
Production composition continues to have no managed selection provider.

### Consequences

- Good: the policy applies where discovery occurs and covers both transports.
- Good: failure of an artifact check cannot fall back to ambient discovery.
- Bad: the Pi dependency gains a reviewed policy and lifecycle test surface.
- Neutral: candidate qualification does not promote a pin or activate a product.

## Known limitations and deferred work

- [Pi #80](https://github.com/psmfd/pi/issues/80): implement and retain the
  runtime regression tests; this ADR is an accepted contract, not test evidence.
- [#201](https://github.com/psmfd/FingerTrap/issues/201): S4 stores/resume/meters
  and later child-process/tool isolation remain necessary.
- [#202](https://github.com/psmfd/FingerTrap/issues/202): review the exact
  candidate artifact and required fork patches before pin promotion. Use the
  consumer baseline for the first experiment; do not silently change bases.
- ADR-0034: SDK adoption, managed distribution/credentials/activation and
  pi_config retirement retain their separate gates.
- [Approved plan](../docs/migration/s3-discovery-resolution-plan.html): real
  process, mutation, lifecycle, protocol and platform acceptance criteria.
