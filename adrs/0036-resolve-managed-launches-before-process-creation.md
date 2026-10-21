# 0036 — Resolve managed launches before process creation

- Status: Accepted
- Date: 2026-10-20
- Authorization: maintainer-approved S3-A through S3-F plan
- Extends: [0035](0035-version-managed-runtime-profiles.md)

## Context and problem statement

RPC inherits the sidecar environment. Porta.Pty merges it in C# and again
in the Unix child. A profile path alone cannot isolate either launch.
The pinned Pi CLI also performs project migrations before project trust:
resource-disable flags do not make arbitrary project launches isolated.

## Considered options

- Override selected variables: leaves ambient credentials and discovery.
- Change process-wide environment while launching: races concurrent profiles.
- Resolve immutable host-owned inputs, with explicit replacement at each
  process boundary and a separate production qualification gate.

## Decision outcome

Choose the third option. One Pi launch resolver serves RPC and Pi PTY panes.
Inherited environments remain the default for existing callers and shell panes.
Replacement starts empty; literal empty values remain values. Invalid names,
NULs, duplicate platform-equivalent keys and relative replacement executables
are rejected before process creation.

The host selects a profile, artifact root and descriptor with an independently
trusted SHA-256. Descriptor v1 records runtime/revision/platform/architecture,
executable, explicit extensions and the complete file/digest inventory.
Limit descriptors to 1 MiB, 8192 files and 64 extensions. Reject unknown or
duplicate fields, unlisted files, traversal, case aliases and links inside the
artifact. Verify every digest before launching. Runtime files and writable
profile state are separate. These checks are not an OS sandbox or protection
against a malicious same-user process racing filesystem replacement.

The context copies arguments and environment, resolves cwd separately and
never changes the parent environment/cwd. Managed raw executable/environment
overrides and resume paths are rejected. Credential references remain unresolved
and therefore rejected in this keyless foundation; trusted credential delivery
is separate work. Home, application-data, XDG, temp and Pi state locations
derive from the selected profile. PATH is explicit; no login-shell enrichment,
ambient provider keys, loader hooks, proxy settings or agent sockets are copied.

Unix replacement uses a distinct native export and absolute-path execve with
an explicit environment array, including an empty array. Preserve the existing
export and Darwin spawn coordination/FD_CLOEXEC behavior. Missing replacement
support fails closed. Windows replacement passes an explicit Unicode block
and bypasses inherited PATH resolution.

Production composition has no managed selection provider. Only internal host
fixtures exercise the new path; no settings, environment or public RPC bypass
exists. The managed resolver guard remains. Until the discovery blocker is
resolved, controlled fixtures reject project `.pi` state and profile startup
configuration rather than executing it. The keyless candidate argv disables
automatic resources/context, denies project trust and requests offline mode.
Its presence is not evidence of qualification.

### Consequences

- Good: both transports share selection and isolation policy.
- Good: native and managed replacements can be verified independently.
- Bad: artifact hashing adds bounded startup work; distribution must supply
  a trusted descriptor identity and complete dependency inventory.
- Neutral: inherited shell/Pi behavior remains supported; managed product
  activation remains unavailable.

## Known limitations and deferred work

- [#201](https://github.com/psmfd/FingerTrap/issues/201) /
  [#202](https://github.com/psmfd/FingerTrap/issues/202): pinned CLI project
  migrations run before trust; arbitrary-project discovery qualification is
  blocked. See [audit](../docs/migration/s3-discovery-audit.md).
- S4: profile-aware stores/resume/recovery and meter paths.
- S5: imported child dispatchers and actual tool/helper configuration policy.
- #187/#203: installed artifact activation, trusted credentials and approvals.
- S6: complete retained-runtime/platform qualification. No SDK or pin change
  is authorized by this ADR.
