# 0029 — Define the VM provider and subprocess trust contract

- Status: Accepted
- Date: 2026-09-04

## Context and problem statement

Native-track N-5 ([#169](https://github.com/psmfd/FingerTrap/issues/169))
will let FingerTrap observe and eventually manage the Lima virtual machines
owned by
[mac-container-machine](https://github.com/psmfd/mac-container-machine)
(mcm). mcm must remain the single implementation of machine state and
lifecycle semantics: porting those rules into FingerTrap would duplicate its
profile, provisioning, and backend abstractions.

The integration introduces FingerTrap's first deliberate child-CLI boundary.
The .NET sidecar already owns application logic and launches bounded helper
processes, while the Tauri shell owns the sidecar lifecycle and platform
credentials. Launching mcm directly from the WebView or as another Tauri
sidecar would put business logic in the wrong process and expand Tauri's
capability surface.

Two upstream contract gaps prevent production use today:

- mcm `status --json` sources `machine.conf` and
  `machine.<name>.conf` as shell code and may invoke a sibling token helper.
  Approving only the `machine.sh` digest therefore does not approve the code
  that status can execute. A client-safe status path is tracked by
  [mcm#80](https://github.com/psmfd/mac-container-machine/issues/80).
- mcm `list --json` wraps raw `limactl list --format json` rows. Its top-level
  envelope is versioned, but each row is still Lima's compatibility surface.
  Normalized mcm-owned rows are tracked by
  [mcm#81](https://github.com/psmfd/mac-container-machine/issues/81).

The architecture and trust boundary must be fixed before code makes either
contract reachable through FingerTrap.

## Considered options

- **A — Port mcm behavior into .NET.** FingerTrap reads Lima state and
  reproduces mcm's profile, drift, provisioning, and lifecycle rules.
- **B — Launch mcm from the .NET sidecar behind a compile-time provider, after
  its consumer contracts are safe and stable.**
- **C — Register mcm as a second Tauri sidecar or launch it from the WebView.**
- **D — Treat an operator-selected `machine.sh` path and digest as the entire
  trust boundary.**

## Decision outcome

Chosen option: **B — a sidecar-owned, compile-time provider over an upstream
client-safe contract**.

Option A is rejected because it creates two owners for machine semantics and
breaks mcm's backend boundary. Option C is rejected because Tauri capabilities
do not govern grandchildren of the existing sidecar, and a second Tauri
sidecar adds packaging and lifecycle machinery without creating a stronger
boundary. Option D is rejected because the current script sources additional
shell code and resolves executables through `PATH`; one file digest does not
describe that execution closure.

### Provider boundary and first implementation slice

`IVmProvider` lives in the sidecar abstractions. The first implementation is a
macOS-only mcm provider with one read-only status operation. Its process launch
is isolated behind an injectable runner, and its JSON parsing is isolated
behind source-generated `System.Text.Json` metadata. On non-macOS platforms
the provider returns `unsupported` without attempting a spawn; on macOS,
missing configuration is the distinct `not-configured` state.

The first code slice is deliberately narrower than #169's eventual surface:
interfaces, status DTOs, parser tests, runner conformance tests, and the
trust/authorization seams. It launches only the fake-mcm fixture. Real mcm
launches require both mcm#80 and a later shell-approval slice that binds an
operator decision to the exact invocation. The foundation does **not** add
`vm/list`, endpoint resolution, JSON-RPC methods, UI, Tauri commands or
capabilities, terminal panes, lifecycle verbs, repo mappings, or destructive
actions. Those are separate slices because they introduce different trust and
interaction boundaries.

`vm/list` remains blocked on mcm#81. FingerTrap never binds a durable DTO to a
raw Lima object.

### Process-launch contract

Every mcm invocation from the sidecar must satisfy all of these invariants:

- `UseShellExecute` is false; arguments are fixed `ArgumentList` entries and
  never assembled into a command string.
- stdin is redirected and closed immediately. stdout and stderr are redirected
  and drained concurrently so neither can block the process or corrupt the
  sidecar's JSON-RPC stdout.
- the child receives an allowlisted environment, not the inherited process
  environment. Variables that alter shell startup, dynamic loading, mcm
  settings, or language runtimes are absent unless a reviewed contract
  explicitly requires one. The client-safe entrypoint invokes the exact
  canonical, digest-verified Lima executable; it must not rely on whichever
  `limactl` appears first on `PATH`. Any remaining OS commands use documented
  absolute paths under `/usr/bin` or `/bin`.
- stdout and stderr have independent byte ceilings. Exceeding either ceiling
  terminates the child process group and returns a bounded local error.
- every invocation has a deadline and cancellation terminates its dedicated
  process group. The runner sends bounded graceful and forced termination,
  waits for cleanup, verifies that no known descendants remain, and returns a
  distinct cleanup-failed outcome if it cannot confirm that result. Waiting for
  the parent alone is not proof that descendants exited. mcm's status probes
  currently have no internal timeout, so this outer boundary is load-bearing.
- exit 0 is success, exit 1 is an mcm-reported operational failure, and exit 2
  is a configuration or invocation precondition failure. Signals, timeout,
  cancellation, malformed output, and output overflow remain distinct local
  outcomes.

The fake-mcm conformance executable is the primary test seam. Tests exercise
the real runner entry path, including exact arguments, closed stdin,
environment filtering, concurrent output, overflow, timeout, cancellation,
process-tree cleanup, and every exit class.

### Trust and approval contract

The operator configures an absolute mcm client-entrypoint path and the
required Lima executable path. Resolution canonicalizes each path, rejects
unexpected symlinks and non-regular/non-executable files, and rejects
components owned by an unexpected user or writable by group/other. The
current user and root are the only accepted owners.

The approval record identifies the complete closure through four identities:
the canonical client entrypoint path plus SHA-256 digest, the canonical Lima
executable path plus SHA-256 digest, an invocation-contract revision covering
the fixed operation/argument vocabulary, and an environment-contract revision.
A change to any identity invalidates approval and requires an explicit new
operator decision. The fixed absolute `/usr/bin` and `/bin` utilities
documented by mcm#80 are trusted as part of the macOS platform and are not
individually approved; the client-safe entrypoint may not execute sibling
scripts, sourced files, or other PATH-resolved helpers.

The Tauri shell owns persistent approval material, following ADR-0022's
platform-credential boundary. A real launch requires a structured, single-use
authorization carrying a nonce, short expiry, both approved path/digest pairs,
both approved contract revisions, the fixed operation, and the validated VM
name. The sidecar consumes it once, requires an exact match to its pending
launch, and revalidates paths and digests immediately before spawn. An
unscoped Boolean approval is never sufficient. The first foundation slice
defines and tests this seam but does not add the shell/UI approval flow or
launch real mcm.

This is **operator consent and accidental-change detection**, not a sandbox
against an attacker already able to replace files as the same user. Path
validation followed by pathname launch also retains a time-of-check/time-of-use
race. Implementation must validate immediately before launch, test replacement
and symlink cases, and document the residual risk rather than claiming the
hash eliminates it.

Production status wiring remains blocked until mcm#80 supplies a path that:

1. treats configuration as allowlisted data rather than sourced code;
2. omits optional and sibling helper execution;
3. documents every executable and environment dependency;
4. invokes the exact approved Lima executable rather than resolving it through
   ambient `PATH`; and
5. preserves a versioned output schema with closed enums.

The two digest pairs, the fixed invocation, and the environment-contract
revision define FingerTrap's approved execution closure. Any additional
mutable executable dependency requires an ADR amendment and a corresponding
identity/invalidation rule before it can enter that closure.

### JSON and diagnostic boundary

mcm output is untrusted input. The runner rejects oversized output before
parsing. The parser accepts exactly one JSON document, requires `schema == 1`,
validates required fields, closed enums, numeric ranges, collection counts,
and field lengths, and rejects trailing non-whitespace. Unknown additive keys
are tolerated only within a supported schema.

Raw stdout, stderr, parser exceptions, and child command lines never cross
`RpcSurface`. Errors are mapped to bounded FingerTrap-owned diagnostics and
pass through ADR-0022's control/BiDi stripping and redaction boundary before
becoming provider state or logs.

### Consequences

- Good: mcm remains the single owner of VM semantics and any future Lima
  backend replacement.
- Good: no new Tauri capability, external binary, CSP allowance, or NuGet
  package is required for the status foundation.
- Good: the first slice proves process and parser behavior without exposing an
  unsafe production path.
- Good: raw Lima rows cannot silently become FingerTrap's public contract.
- Bad: production status wiring depends on upstream mcm#80 and a shell-approval
  slice; list support additionally depends on mcm#81.
- Bad: executable approval cannot defend against a fully compromised same-user
  account and retains a bounded pathname race.
- Neutral: lifecycle and terminal features remain in #169 but require later
  decisions for PTY interaction, native confirmation, and SSH endpoint use.

## Known limitations and deferred work

- Deferred: normalized VM listing and `vm/list`, blocked by
  [mcm#81](https://github.com/psmfd/mac-container-machine/issues/81).
- Deferred: endpoint/SSH-config consumption and VM terminal panes; they belong
  to the N-3 transport and host-key decisions already recorded in ADR-0016 and
  ADR-0017.
- Deferred: shell/UI approval plumbing, real mcm launches, and JSON-RPC
  exposure until the tested provider foundation has a production-safe upstream
  status path.
- Deferred: lifecycle and destructive verbs, repo-to-VM mappings, deep links,
  and packaging; tracked by #169 and intentionally excluded from #172.
