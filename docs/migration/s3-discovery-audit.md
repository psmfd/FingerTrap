# S3 pinned-runtime discovery audit

Source: Pi `e7d09c9dc5fca409f46a5476669e3c0404054d69`
(`v0.85.1-psmfd.1`). Recorded 2026-10-20. Source observations below are
distinct from executable probe results.

## Finding and disposition

**Arbitrary-project managed activation is blocked.** `main.ts` invokes
`runMigrations(cwd)` before the runtime trust decision. In `migrations.ts`,
`migrateExtensionSystem(cwd)` calls `migrateCommandsToPrompts(projectDir)`,
which renames project `.pi/commands` to `.pi/prompts`. This occurs even when
automatic resources are disabled and project trust is denied. The CLI has no
reviewed migration-disable argument in this pin.

Keep production managed selection disabled. Continue the independently
verifiable launch/environment foundation. Internal fixtures reject project
`.pi` state and profile startup configuration; they do not claim support for
arbitrary existing projects. Track the missing control under existing
[#201](https://github.com/psmfd/FingerTrap/issues/201) and
[#202](https://github.com/psmfd/FingerTrap/issues/202). A fork change, pin bump
or SDK adoption is a separate decision.

## Control matrix

| Source | Candidate control | Evidence / remaining requirement |
| --- | --- | --- |
| Global Pi paths | `PI_CODING_AGENT_DIR`, `PI_CODING_AGENT_SESSION_DIR`, explicit `--session-dir` | Config resolves these; use profile paths, not HOME defaults |
| Session selection | Explicit session dir; reject resume for S3 | Startup settings can affect selection; S4 must validate stored cwd |
| Project trust | `--no-approve` | Parsed false override; does not gate earlier migrations |
| Extensions | `--no-extensions`, exact local `--extension` | Explicit paths still load; package resolution precedes final filtering |
| Skills/prompts/themes | `--no-skills`, `--no-prompt-templates`, `--no-themes` | Loader flags exist; inspect explicit package-derived resources separately |
| AGENTS/CLAUDE files | `--no-context-files` | Loader suppresses context collection; distinct from SYSTEM prompts |
| SYSTEM / APPEND_SYSTEM | Explicit system prompt; denied project trust | Global agent-directory prompt files remain separate inputs; fixtures refuse them |
| Packages | Empty approved config, explicit local resources, `--offline` | Missing-source installation consults offline mode; flags alone do not stop resolution |
| Global/project settings | Profile agent dir; denied project trust | Startup settings are read before final runtime settings; fixtures refuse startup config |
| Auth/models | Empty controlled agent dir; no ambient keys | Auth values may execute commands through `resolve-config-value.ts`; refuse these files in S3 fixtures |
| Home/tool helpers | Replacement HOME/XDG/app-data/temp/PATH | Does not stop tools reading repository/system config; actual imported tools are S5 |
| Legacy migrations | No sufficient reviewed control | Concrete project rename before trust; blocks arbitrary-project activation |
| Reload / follow-on sessions | Same runtime policy must persist | Exact-runtime qualification remains required; launch plumbing tests cannot certify this |

## Source anchors

- [Startup ordering](https://github.com/psmfd/pi/blob/e7d09c9dc5fca409f46a5476669e3c0404054d69/packages/coding-agent/src/main.ts).
- [Migrations](https://github.com/psmfd/pi/blob/e7d09c9dc5fca409f46a5476669e3c0404054d69/packages/coding-agent/src/migrations.ts).
- [Argument parsing](https://github.com/psmfd/pi/blob/e7d09c9dc5fca409f46a5476669e3c0404054d69/packages/coding-agent/src/cli/args.ts).
- [Resource loader](https://github.com/psmfd/pi/blob/e7d09c9dc5fca409f46a5476669e3c0404054d69/packages/coding-agent/src/core/resource-loader.ts).
- [Package resolution](https://github.com/psmfd/pi/blob/e7d09c9dc5fca409f46a5476669e3c0404054d69/packages/coding-agent/src/core/package-manager.ts).
- [Credential values](https://github.com/psmfd/pi/blob/e7d09c9dc5fca409f46a5476669e3c0404054d69/packages/coding-agent/src/core/resolve-config-value.ts).

## Executable evidence

On macOS ARM64, the retained probe verified the cached release archive against
GitHub's release-asset SHA-256
`e0e0331d478af7724fa24d780a8d4abc0033c6f5ee7c0d45dddae6f922e5c4cb`
before extracting it into a disposable directory. Executable SHA-256:
`811d65137d4272b30a6b2bd9a4093218b8f94bb8f55a9db9ccf8d443c4d4b2a5`.
Reported version: `0.85.1`; RPC hello observed; clean exit 0.

Despite all discovery-disable flags, `--no-approve` and `--offline`, the
synthetic `.pi/commands/synthetic.md` moved to `.pi/prompts/synthetic.md`,
with identical contents. A separate synthetic operator settings file stayed
unchanged. No real operator home, credentials or project files were supplied.
The temporary extraction and state were removed by the probe.

```sh
python3 scripts/probe-pi-launch-isolation.py --archive /path/to/pi-darwin-arm64-v0.85.1-psmfd.1.tar.gz
```

Exit 0 means the known blocker was reproduced. It does not mean launch
qualification passed. The probe uses a bounded child lifetime and explicit
keyless environment and is retained for evaluating a separately reviewed fix.
