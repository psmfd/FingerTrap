# 0035 — Version managed runtime profiles and fail closed before activation

- Status: Accepted
- Date: 2026-10-20
- Authorization: implementation of the maintainer-approved #200/#201 plan
- Extends: [0034](0034-own-agent-runtime-and-retire-pi-config.md)
- Partially supersedes: [0014](0014-settings-file-and-precedence.md)'s
  single supported settings version; version 1 remains supported

## Context and problem statement

Managed sessions must select a profile, executable and resource set explicitly.
Existing settings version 1 ignores unknown fields for forward compatibility.
Adding a managed flag to version 1 would let an older build silently launch
the operator's ambient Pi installation. The profile contract also crosses
the .NET/TypeScript boundary and needs consistent validation and path layout.

## Considered options

- Add managed selectors to settings v1: older builds ignore the new policy.
- Replace all settings and migrate operator state: unnecessary compatibility
  and data-migration risk.
- Retain v1 and require v2 for managed selection, with independently versioned
  profile definitions and shared cross-language fixtures.

## Decision outcome

Choose the third option.

### Selection and compatibility

Missing settings and existing v1 settings retain user-managed behavior.
Version 2 requires an explicit `pi.mode` of `user-managed` or `managed`.
Managed selection requires `pi.profileId` and `pi.runtimeId`, prohibits
`pi.path`, and never falls back to request/environment/PATH resolution.
Version 1 rejects a managed selector in new builds; old builds reject the
required v2 document before attempting any launch.

Do not automatically upgrade or rewrite operator settings. Merely parsing
managed selectors does not enable managed launching: until the isolated
launcher passes #201/#202 gates, the shared Pi resolver returns a clear
unavailable error before considering any explicit or ambient executable.
Shell panes remain usable. Settings examples are documentation, not an
instruction to change a live installation.

### Profile definition version 1

An app-owned definition contains exactly `version`, `id`, `runtimeId` and
`credentialRefs`. References are provider/reference-ID pairs; credentials
are resolved separately by the trusted host and never serialized into the
definition or the extension context. Reference IDs are bounded identifiers,
not paths or secret values. Duplicate providers are invalid.

Profile IDs use lowercase ASCII letters/digits/hyphens, start with a letter,
and are at most 48 characters. Runtime/reference/provider identifiers allow
lowercase ASCII letters/digits/dots/hyphens, start with a letter or digit,
and are at most 96 characters. Windows device names are rejected on every
platform. Unknown definition fields, invalid types and unsupported versions
fail rather than being repaired or silently defaulted.

The sidecar resolves a profile from an explicitly supplied absolute
FingerTrap application-data directory. Neither language discovers that root
from HOME, cwd or a fallback. Both implementations derive:

| Name | Relative to the FingerTrap application-data directory |
| --- | --- |
| Root | `profiles/<id>` |
| Agent state | `profiles/<id>/agent` |
| Sessions | `profiles/<id>/agent/sessions` |
| Extension state | `profiles/<id>/agent/extensions` |
| Worktree manifests | `profiles/<id>/agent/extensions/worktree/sessions` |
| Cache | `profiles/<id>/cache` |
| Temporary files | `profiles/<id>/tmp` |
| Child home | `profiles/<id>/home` |

Resolution is pure: it does not create directories, copy data, load resources
or authorize filesystem access. The launcher must separately check canonical
filesystem containment, permissions and symlink/reparse-point behavior when
opening/creating paths. A syntactically contained path is not that proof.
Git worktrees remain under their repository's supported `.worktrees` layout;
profile/session-qualified names and manifests are the S5-C responsibility.

### Resource and child launch contract

The selected runtime ID identifies a host-verified immutable resource manifest,
not an executable path supplied by the WebView. Before launch, resolve and
verify the executable hash, fork revision, dependency closure and explicit
resource selection. Select environment/cwd once and propagate a non-secret
profile context through every managed child. Do not mutate process-wide
environment while resolving a profile.

The exact pinned fork source exposes `PI_CODING_AGENT_DIR`,
`PI_CODING_AGENT_SESSION_DIR`, and discovery flags including
`--no-extensions`, `--no-skills`, `--no-prompt-templates`, `--no-themes` and
`--no-context-files`. These are candidate integration controls, not yet
runtime proof: settings/package resolution, explicit resource loading and
subprocess credential sources still require the S3/S6 audit and tests.

Per-repository identity overrides belong to profile-owned, untracked
configuration, keyed by canonical repository identity. Importing existing
operator identity configuration is opt-in and separate from source import.
The identity-file format and supported credential resolution remain scoped
to their integration slices (#201 / pi-gh-identity-guard#10, #187/#203).

### Consequences

- Good: old builds cannot silently interpret a managed configuration as
  legacy user-managed behavior.
- Good: common fixtures make validation and path layout disagreements visible.
- Bad: managed users need a v2 settings document; rollback requires explicit
  user-managed selection or the retained v1 configuration.
- Neutral: contract validation does not certify resource discovery, credential
  isolation, native approval authority or an OS sandbox.

## Known limitations and deferred work

- #201 S3/S4: full replacement environment, canonical-path checks, profile-aware
  session selection/recovery, child propagation and stores.
- #200 S2/S5: generalized import manifests and remaining source/test closures.
- #202/#187: actual artifact qualification, production activation and login.
- #203: protected-operation grants and trusted native approvals.

## Evidence

- [Pinned fork configuration](https://github.com/psmfd/pi/blob/e7d09c9dc5fca409f46a5476669e3c0404054d69/packages/coding-agent/src/config.ts).
- [Pinned CLI arguments](https://github.com/psmfd/pi/blob/e7d09c9dc5fca409f46a5476669e3c0404054d69/packages/coding-agent/src/cli/args.ts).
- [Pinned resource loader](https://github.com/psmfd/pi/blob/e7d09c9dc5fca409f46a5476669e3c0404054d69/packages/coding-agent/src/core/resource-loader.ts).
