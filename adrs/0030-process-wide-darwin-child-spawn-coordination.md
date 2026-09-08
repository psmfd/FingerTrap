# 0030 — Coordinate Darwin child spawns process-wide

- Status: Accepted
- Date: 2026-09-08

## Context and problem statement

ADR-0029 introduced a native `posix_spawn` runner for the future mcm VM
provider. Linux can create its output socket pairs atomically with
`SOCK_CLOEXEC`; Darwin cannot. On Darwin each `socketpair` therefore has a
short interval between descriptor creation and the two `fcntl(FD_CLOEXEC)`
calls. A different thread that forks during that interval can copy the mcm
socket into an unrelated child. That retained descriptor delays EOF and can
wedge bounded output draining and cleanup even after mcm exits.

Serializing only mcm launches is insufficient. The sidecar also launches PTY,
pi RPC, git, worktree-reconciliation, and login-environment children. All of
those launches share the sidecar's descriptor table. Porta.Pty is especially
important because its native `forkpty` call performs a real fork and its parent
master descriptor was not marked close-on-exec.

The coordination boundary must cover descriptor creation, close-on-exec
marking, and the operating-system spawn operation, but not child lifetime,
I/O, waiting, cancellation, or cleanup.

## Considered options

- **A — Accept the race because the window is short.** This leaves a known,
  concurrency-dependent hang in a security boundary that must be reliable
  before production VM wiring.
- **B — Give each subsystem its own spawn lock.** Separate locks do not protect
  one subsystem's descriptor window from another subsystem's fork.
- **C — Use one process-wide Darwin spawn coordinator for every sidecar child
  launch and mark the vendored PTY master close-on-exec before releasing it.**
- **D — Rewrite every launcher as a common native close-from implementation.**
  This would duplicate runtime and Porta.Pty platform machinery and is much
  larger than the defect requires.

## Decision outcome

Chosen option: **C — one process-wide Darwin child-spawn coordinator**.

The .NET sidecar owns one shared gate. On macOS, every child-launch path enters
that gate before an API can create inheritable descriptors and releases it as
soon as the API has completed the spawn. Synchronous `Process.Start`, the mcm
socketpair/`posix_spawn` sequence, and Porta.Pty's asynchronous API all use the
same gate. Child lifetime, stream draining, waits, termination, and cleanup are
outside it, so unrelated long-running children still execute concurrently.

The gate is a no-op on Linux and Windows. Linux retains ADR-0029's atomic
`SOCK_CLOEXEC` socket creation and `posix_spawn_file_actions_addclosefrom_np`
child cleanup. Windows receives no behavioral change.

The vendored Porta.Pty native shim marks the parent PTY master `FD_CLOEXEC`
before returning from `pty_spawn`. Failure to mark it is a failed spawn: the
shim closes the master, kills and reaps the new child, and reports the saved
error. This local patch is recorded in `external/Porta.Pty/UPSTREAM.md` and
must be reapplied during future vendor syncs.

All future production code in `FingerTrap.Sidecar` that creates a child process
must use the shared coordinator. A subsystem-local lock is not an acceptable
substitute because descriptor inheritance is process-wide.

A deterministic macOS regression pauses the real mcm runner after `socketpair`
and before `fcntl`, attempts a real unrelated PTY spawn, and verifies that the
PTY cannot spawn until the mcm critical section closes. It then verifies mcm
output reaches EOF while the PTY child remains alive. This tests the real
launch paths rather than only asserting that both callers invoked a mock lock.

### Consequences

- Good: unrelated Darwin children cannot inherit mcm's transient output
  descriptors.
- Good: the PTY controller cannot leak into children spawned after `forkpty`
  returns.
- Good: the gate covers existing and future managed launch paths through one
  narrow launcher.
- Good: Linux's lock-free atomic path remains unchanged.
- Bad: simultaneous macOS spawn calls serialize for the duration of their
  operating-system spawn operation.
- Bad: code that bypasses the shared launcher can reintroduce the race; launch
  path inventory and review remain required.
- Neutral: this does not authorize real mcm execution or change the production
  gates in ADR-0029.

## Refines

- [0029 — Define the VM provider and subprocess trust contract](0029-vm-provider-and-subprocess-trust-contract.md)
- [0008 — Vendor Porta.Pty](0008-vendor-porta-pty.md)
