# 0014 — Settings file, schema versioning, and configuration precedence

- Status: Accepted
- Date: 2026-08-15

## Context and problem statement

FT-0 shipped two environment variables as **explicitly interim** configuration,
recorded in [ADR-0013](0013-pane-kinds-and-pi-resolution.md) so they would be
migrated rather than rediscovered:

| Variable | Meaning |
| --- | --- |
| `FINGERTRAP_PI` | explicit pi executable path |
| `FINGERTRAP_PANE_KIND` | override the host default pane kind |

FT-1 delivers configurable keybindings. Without a settings system it would add
a third piece of ad-hoc configuration and the debt would compound, so Native
track **N-1** was pulled forward ahead of FT-1 (issues #51, #52).

Only part of N-1 could move. The milestone bundles four things that do not
share a dependency: the settings foundation, absorbing the env vars, user
config (theme/font/profiles), and **persisted layout**. Layout cannot precede
FT-1 — there is exactly one pane today, and persisting a layout of one
non-splittable pane is not a feature. That dependency inversion is recorded in
#51; this ADR covers the foundation and the absorption.

## Considered options

For the environment variables, once settings exist:

- **A — Keep them as a lower precedence layer.** Settings win; env still works.
- **B — Retire them loudly.** Still read, but only to emit a deprecation
  warning naming the replacement key.
- **C — Remove outright.** Delete the reads.

For an unversioned settings file:

- **D — Treat a missing `version` as the current version.** Friendly to
  hand-written files.
- **E — Require `version` explicitly.** Rejects unversioned files.

## Decision outcome

**Environment variables: option A.** Precedence, highest first:

```text
pi executable:     spawn request → settings pi.path → $FINGERTRAP_PI → PATH → throw
default pane kind: spawn request → settings pane.defaultKind → $FINGERTRAP_PANE_KIND → pi
```

Option C was rejected on the project's own stated standard: #52's acceptance
forbids configuration that "quietly stops working", and a script setting
`FINGERTRAP_PANE_KIND=shell` would silently start opening pi panes. Option B is
a defensible end state but premature — environment remains the natural fit for
*ephemeral* overrides (CI, a one-off launch) where writing a file and restoring
it afterwards is the wrong shape. Nothing that works today stops working.

**Versioning: option E.** A missing `version` is an error naming the fix.
`FingerTrapSettings.Version` is deliberately `int?` with no initializer, so
*absent* is distinguishable from *equal to the current value*. A default would
make an unversioned file parse silently as v1 — which defeats the point of
versioning it, because once v2 exists that file is ambiguous: written for v1,
or hand-written against v2 by someone who omitted the key? The cost is one line
in a file the operator is already editing deliberately.

An **unsupported** version is likewise refused rather than read best-effort: a
future schema may reuse a key with different meaning, so applying it under v1
rules could enact a setting nobody intended.

### Absent versus present-and-wrong

The asymmetry mirrors ADR-0013's treatment of `FINGERTRAP_PANE_KIND`:

- **Absent file** → defaults, silently. A fresh install must not require a
  config file, and this is the common case.
- **Present but unusable** (unreadable, malformed, unversioned, wrong version,
  bad enum value) → **throws**, naming the offending file.

Silently reverting to defaults on a bad file would make a typo'd settings file
indistinguishable from a working one — the failure this project has already
rejected once.

Loading happens **once per process**, in `Program.cs`, which exits non-zero
with the message on stderr (stdout is the RPC framing, ADR-0002). A bad enum
*value* surfaces later, as an RPC error rendered into the pane, because it is
only interpreted at spawn time.

### The file location is not `~/.config` everywhere

`docs/milestones.md` specifies `<app-data>/fingertrap/settings.json`, which is
`Environment.SpecialFolder.ApplicationData`. That is **not** the same shape on
every platform, and assuming otherwise cost a debug cycle here — a settings
file written to `~/.config/fingertrap/` on macOS was silently never read:

| Platform | Resolved path |
| --- | --- |
| macOS | `~/Library/Application Support/fingertrap/settings.json` |
| Linux | `$XDG_CONFIG_HOME` or `~/.config`, then `/fingertrap/settings.json` |
| Windows | `%APPDATA%\fingertrap\settings.json` |

This is verified on-host, not inferred, and pinned by a test. It also means
[#41](https://github.com/psmfd/FingerTrap/issues/41)'s
`~/.config/fingertrap/config.json` is correct only on Linux — one more reason
that issue is stale (its repo picker does not exist either; see #51).

Any user-facing documentation of the settings location must state it per
platform or point at `SettingsLoader.ResolvePath`.

### Unknown keys are tolerated

Within a supported version, unrecognised keys are ignored, so a file written by
a newer build that still declares v1 does not break an older one. **Version is
the compatibility gate, not key presence.**

### Consequences

- Good: the FT-0 config debt is cleared before FT-1 adds to it, which was the
  entire reason for pulling N-1 forward.
- Good: verified end to end against the real sidecar, not only by unit test —
  no file spawns pi unchanged; `pane.defaultKind: "shell"` spawns zsh; an
  explicit request still outranks settings; and `"pie"` produces a loud error
  rather than a silent default.
- Good: ADR-0013's fail-loud contracts survive the move from environment to
  file — a missing pi still throws, an unrecognised kind still throws.
- Bad: three configuration layers (request, settings, environment) is more to
  hold in mind than one, and every future setting inherits the question of
  whether it needs an env equivalent. The precedence is documented in one place
  and tested, which is the mitigation.
- Bad: requiring `version` will surprise someone hand-writing their first
  settings file. The error names the exact line to add.
- Neutral: no settings **UI**. A file and a service first; an editor for two
  keys is not yet worth building. That remains N-1 scope (#51).
- Neutral: `SettingsException` is a fourth loud-failure type alongside
  `PiNotFoundException`. Both exist so an operator-fixable configuration
  problem is distinguishable from a runtime fault.
