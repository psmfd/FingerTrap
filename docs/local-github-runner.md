# Local GitHub Actions runner

The macOS pilot runs tests and Apple Silicon packaging in one disposable Tart
VM on the workstation. GitHub schedules the job and stores its logs/artifacts.
The runner is not a persistent workstation process. See [ADR-0037](../adrs/0037-use-disposable-tart-github-runners.md).

## Current status

Preparation started 2026-10-20. The unregistered build template is prepared and
shut down. The reviewed privileged Softnet helper is installed. Routine CI
still uses hosted runners. The pilot must pass before changing that routing
or any required checks.

Observed host: Tart 2.32.1, Softnet 0.19.0, 18 CPUs, 128 GiB RAM, approximately
122 GiB available disk at setup. Dedicated base `ft-actions-base-20261020` was
cloned without automatic pruning from:

```text
ghcr.io/cirruslabs/macos-sequoia-base@sha256:3f4d14a5ffb9efd3bda2ae0184fd4bc2773d924ff8b7565f958761420ec41a0c
```

Guest: macOS 15.7.7 / 24G720, ARM64, command-line developer tools installed.
The template includes Homebrew Bash; `scripts/check.sh` requires `mapfile`,
which the system Bash 3.2 lacks. The workflow selects Homebrew Bash explicitly.
VM allocation: 8 CPUs, 16 GiB memory, 80 GB sparse disk. Existing acceptance
VMs are preserved. The base image's older unregistered runner is unused.
The dedicated FingerTrap runner is version 2.338.0; its official archive SHA-256
is pinned in `scripts/ci/prepare-tart-runner.sh`. No credentials belong in the
template, and a job clone must never be promoted back to it.

## One-time preparation

The host bootstrap requires `tart`, `gh`, Python 3, and the repository-authorized
GitHub identity. It does not switch the global gh account. Current pilot scripts
default to `TheSemicolon`; `--github-user` can select another authorized identity.

The reviewed base-preparation script runs inside the clean guest:

```sh
tart exec -i ft-actions-base-20261020 /bin/bash -s < scripts/ci/prepare-tart-runner.sh
tart exec ft-actions-base-20261020 sudo -n /sbin/shutdown -h now
```

Wait for the `tart run` process to exit before cloning. The guest transport may
disconnect during shutdown. Do not use `tart stop` to save template changes:
an abrupt stop can lose recent guest writes. Verify prerequisites in a new clone
before registering it; the controller now checks Bash as well as runner state.

Preparation deliberately refuses to overwrite an existing FingerTrap runner
directory. On failure inspect the guest; do not delete or reuse credential
files blindly.

Softnet initially failed with “root privileges are required to run and
passwordless sudo was not available.” Its upstream design needs a privileged
network helper. The retained installer copies the exact locally reviewed
Softnet 0.19.0 binary into a root-owned directory with mode 4755. It changes no
sudoers policy and does not change Homebrew's binary permissions:

```sh
sudo /bin/bash scripts/ci/install-softnet-helper.sh
```

The installer checksum is specific to this workstation's ARM64 binary.
The launcher verifies its digest, ownership and permissions. Replacing it with
an updated version requires reviewing and updating both checksums.
The root-owned directory is
`/Library/PrivilegedHelperTools/fingertrap-softnet`; the launcher alone prepends
it to PATH. Softnet drops privileges after network initialization.

Create/read back the dedicated GitHub runner group:

```sh
python3 scripts/ci/tart-runner.py configure
python3 scripts/ci/tart-runner.py check
```

The trusted branch and workflow must exist on GitHub first; the API rejects
nonexistent refs. Publish the reviewed pilot branch before `configure`.

This creates `fingertrap-tart-pilot` only if absent. It never broadens an existing
group. Allowed repository: `psmfd/FingerTrap` only. Allowed workflow:
`.github/workflows/local-macos.yml@refs/heads/ci/tart-runner-pilot` only.
The host's organization administration token stays on the host. The guest
receives only a disposable JIT registration over stdin, after network checks.

## Run one job

Push the reviewed pilot source to the dedicated `ci/tart-runner-pilot` branch
without opening a PR. The new workflow's branch-specific push trigger queues
only the local pilot; ordinary CI's push triggers are `dev`/`main`. Inspect
actual run routing before claiming that no other workflows ran.

```sh
python3 scripts/ci/tart-runner.py run
```

Keep this process and the workstation awake. One invocation handles at most
one job; there is no background daemon or autoscaler. The workflow timeout is
60 minutes; the controller bounds boot to three minutes and runner life to
70 minutes. It verifies external GitHub HTTPS and that an actively listening
host gateway endpoint times out before registering a runner.

The launcher mounts no host directories, disables audio/clipboard, and blocks
private/link-local networks plus host IPv4 addresses through Softnet. The guest
alone uses public DNS resolvers (1.1.1.1 and 8.8.8.8), since blocking the
gateway also blocks its DNS proxy. Softnet 0.19.0 drops outgoing IPv6 frames.
The workflow checks out only its own trusted ref, with read-only contents
permissions and no retained Git credentials. It runs runtime-package, UI,
sidecar and Rust checks, then builds and smokes the packaged app. Artifacts
remain unsigned; the clean GUI acceptance checklist still applies.

## Recovery and evidence

Private logs and atomic phase records live under `.artifacts/tart-runner/`
(ignored by git). Inspect `state.json`, `vm.log`, `runner.log` and the diagnostic
archive after a failure; logs can contain job data and must not be committed.
Normal cleanup stops/deletes only the generated job VM and removes its JIT
runner registration. It preserves the base and unrelated VMs.

After power loss or SIGKILL, cleanup cannot run. Read the exact generated VM
name and runner ID from `state.json`, inspect `tart list` and GitHub's runner
list, then stop/delete that clone and remove that registration. Do not run a
wildcard VM cleanup. An incomplete cleanup is a failed operation, not success.

## Approved macOS cutover

The maintainer approved moving macOS jobs first and retaining Windows/Linux on
GitHub-hosted runners. GitHub rejected direct PR merge refs and test-merge SHAs
in runner-group policies. A reusable worker pinned by full commit SHA provides
the supported boundary. Required macOS check names gain `/ validate`; change
their protection mappings only after the equivalent checks pass.

`.github/tart-policy.json` explicitly enrolls migration PR #207. `cutover`
verifies its source is this repository, then permits the exact reusable worker
revision in group 5. The worker's immutable routing decision selects local
capacity for enrolled PRs, dev/main CI and dev candidate dispatches.
`workerRevision` must name the published full SHA used by workflow callers.
The controller writes the PR list to `FINGERTRAP_TART_PRS` and records the
applied workflow list in `FINGERTRAP_TART_WORKFLOWS`, then reads both back.
These repository variables require administration permissions to change.

```sh
python3 scripts/ci/tart-runner.py cutover
python3 scripts/ci/tart-runner.py check
python3 scripts/ci/tart-runner.py drain --max-jobs 5
```

The cutover command accepts the known prior pilot policy, the desired policy,
or the previous policy recorded in the administration-controlled variable.
It refuses unexplained server drift. Enrollment or worker updates require a
reviewed policy change and rerunning `cutover`.
The launcher rechecks server policy before every registration.

The five jobs are sidecar, UI, runtime package, Tauri and candidate packaging.
Each receives a fresh VM, one at a time. `drain` is bounded by job count and each
runner's lifetime; it is not a permanent service. Start it for the work session.
If fewer jobs are queued, interrupt the controller to clean up the waiting VM.
Unenrolled/fork PRs keep hosted macOS coverage; they are outside this migration's
local allowlist. An enrolled job has no automatic hosted fallback.

Acceptance requires the GitHub job to complete on the recorded local runner,
checksums/build identity to name the expected source, and cleanup to remove
both VM and runner. A zero runner exit code alone is not a passing build.
Verify all five routed jobs before claiming the cutover complete.

References: [GitHub runner lifecycle](https://docs.github.com/en/actions/reference/runners/self-hosted-runners),
[workflow restrictions](https://docs.github.com/en/rest/actions/self-hosted-runner-groups),
[Softnet installation](https://github.com/cirruslabs/softnet/tree/0.19.0).
