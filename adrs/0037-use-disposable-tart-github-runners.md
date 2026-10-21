# 0037 — Use disposable Tart GitHub runners

- Status: Accepted; macOS runners verified on protected dev and enrolled PRs
- Date: 2026-10-20
- Extends: ADR-0033; supersedes its exclusion of local runner orchestration

## Context and problem statement

The maintainer requested local GitHub Actions capacity to reduce hosted builds.
FingerTrap already uses Tart for clean Apple Silicon release acceptance. This
workstation has Tart, a cached macOS 15 image and sufficient CPU and memory.
The repository is public, so a general repository runner would expose local
infrastructure to pull-request workflows, including modified workflow files.

## Considered options

- Continue all builds on GitHub-hosted runners.
- Register a persistent runner directly on the development workstation.
- Run one GitHub job in each isolated, disposable Tart VM.

## Decision outcome

Use the third option, beginning with a manually operated macOS pilot. Keep a
clean, unregistered build template separate from release-acceptance VMs.
Clone it per job; use GitHub's just-in-time runner registration; collect logs;
stop and delete the clone after success, failure or cancellation. The controller
runs on the workstation; the GitHub runner runs only inside the clone.

Use a dedicated organization runner group limited to FingerTrap and an exact
trusted workflow/ref. Runner labels select hardware; they are not an access
control. Verify group restrictions before registration. The pilot workflow has
no pull-request trigger and does not consume an arbitrary ref input. It has a
read-only repository token and no signing, provider or operator credentials.
The host's GitHub administration token never enters the guest.

After proving the pilot, move macOS sidecar, UI, runtime-package, Tauri and
candidate packaging jobs for this migration to the same group. Preserve their
Windows/Linux lanes. The maintainer explicitly approved this scope. GitHub
rejected direct PR merge refs and test-merge SHAs in runner-group restrictions,
so macOS jobs call a reusable worker pinned by full commit SHA. The group
permits that exact worker revision. Its immutable routing decision allows
local execution for explicitly enrolled same-repository PRs, protected
dev/main pushes and dev candidate dispatches. The initial enrolled PR is #207.
Enrollment and worker revision live in `.github/tart-policy.json`; the controller
verifies the server policy and matching repository variables before registration.
The three required macOS check names gain the reusable worker's `/ validate`
suffix; update their protection mappings only after the equivalent checks pass.
Unenrolled and fork PRs retain hosted coverage. They cannot select the local
group by editing workflow labels because GitHub enforces the group's ref list.

Require Softnet networking with explicit private-network blocks, disable
clipboard and audio sharing, and mount no host directories. Verify both
external connectivity and denial of host/private-network access before a
runner receives a job. Never fall back to default networking on failure.
Softnet's privileged helper is a separate workstation installation step.

Start with one VM at a time, 8 CPUs and 16 GiB memory. macOS build/test and
Apple Silicon packaging are the intended first migration targets. Preserve
Windows/Linux coverage and existing required checks until a validated routing
change replaces them. Keep GUI acceptance on a clean acceptance VM: installing
build tools in a runner does not prove a packaged app's runtime independence.

### Consequences

- Jobs still appear in GitHub Actions with logs and artifacts, but computation
  happens locally. The workstation must be awake and the controller running.
- Fresh clones isolate job state; the base needs deliberate OS, toolchain and
  runner updates. No registered or credential-bearing guest becomes a base.
- A failed or unavailable local runner leaves local jobs queued; do not silently
  route them to hosted capacity. Keep a documented, explicit fallback.
- Workflow restrictions and network checks are activation prerequisites.
  Merely installing Tart or registering a runner is not a successful pilot.

## Known limitations and deferred work

Automated scheduling, additional PR enrollment, Linux/Windows local capacity and
signing remain follow-up work. Do not disable current required checks before
the local equivalent passes. This decision does not resolve the Pi discovery
blocker or enable managed profiles in #201.

References: [GitHub ephemeral runners](https://docs.github.com/en/actions/reference/runners/self-hosted-runners),
[runner-group restrictions](https://docs.github.com/en/rest/actions/self-hosted-runner-groups),
and [Softnet 0.19.0](https://github.com/cirruslabs/softnet/tree/0.19.0).
