# FingerTrap

Personal terminal application — local and SSH terminals, SFTP file tree, status surfaces, command palette.

## Release support

The next packaged release targets **macOS on Apple Silicon**. Intel Macs are
unsupported for the foreseeable future. Linux and Windows remain in CI for
regression coverage; they are not packaged release targets for this cycle.
Repeated launches activate the existing window and preserve its sessions.

## Architecture

Three processes, one app:

1. **Tauri shell** (`src-tauri/`) — Rust. Window, WebView, sidecar lifecycle, stdio bridge. No business logic.
2. **UI** (`src-ui/`) — TypeScript + Vite + xterm.js. Terminal panes, SFTP tree, status bar, command palette.
3. **Sidecar** (`src-sidecar/`) — .NET 10. Porta.Pty, SSH.NET, Octokit, Azure DevOps SDK. Owns 95% of the logic.

IPC is JSON-RPC 2.0 over stdio with `Content-Length` framing — `StreamJsonRpc` on the .NET side, `vscode-jsonrpc` on the TS side.

On Darwin, every sidecar child launch shares a process-wide spawn coordinator so transient descriptors cannot leak across concurrent children; see `adrs/0030-process-wide-darwin-child-spawn-coordination.md`.

[ADR-0034](adrs/0034-own-agent-runtime-and-retire-pi-config.md) establishes
FingerTrap as the development home for functionality migrating from pi_config.
The accepted plan retains executable/RPC integration during migration and
evaluates a Pi SDK worker for managed native sessions. It includes the component
migration map and SDK adoption criteria. The first meter source slice is
imported into an inactive package; remaining imports and managed activation
are pending. Production SDK adoption requires a subsequent decision.
The [migration inventory and issue ledger](docs/migration/README.md) record
the immutable inputs, successor issues, and first import slice.
For ongoing work, start with the [migration status and resume guide](docs/migration/STATUS.md);
the [activity log](docs/migration/WORK_LOG.md) records checkpoints and validation.

## Repo layout

```text
adrs/        Architecture Decision Records (MADR minimal)
docs/        Project documentation (milestones, design notes)
packages/    Curated agent runtime (source imported; app activation pending)
scripts/     Repo-level scripts (check.sh, etc.)
src-sidecar/ .NET 10 sidecar (FingerTrap.sln)
src-tauri/   Tauri 2 shell (Rust crate)
src-ui/      TypeScript + Vite UI
```

## Conventions

- **Branching:** GitHub Flow. Feature branches into `dev` (squash-merge); `dev` into `main` (merge commit). `<type>/kebab-case` branch names.
- **Commits:** Conventional Commits. `<type>(<scope>): <description>`, imperative lowercase, no period.
- **Versioning:** SemVer tags from `main` only, annotated, `v` prefix. Pre-1.0 breaking changes bump MINOR.
- **ADRs:** Sequential zero-padded three digits in `adrs/`. Supersession over editing.

See `adrs/0004-repo-conventions.md` for the canonical version.

## Local development

Verify and install required tooling:

```bash
scripts/dev-setup.sh             # check only
scripts/dev-setup.sh --install   # install missing tools (.NET via dotnet-install.sh,
                                 # rustup, cargo-tauri CLI, pnpm via corepack, apt deps)
```

Tools required: .NET 10 SDK, Node.js 22.22.2+ (22.x), 24.15.0+ (24.x), or 26+;
pnpm 10 (via corepack), rustup
(stable) + cargo, the `cargo-tauri` CLI (`tauri-cli ^2.0`), and on Linux
the Tauri system libraries (`libwebkit2gtk-4.1-dev`, `libappindicator3-dev`,
`librsvg2-dev`, `patchelf`, `build-essential`). After install, add the new
PATH entries to your shell rc: `export PATH="$HOME/.dotnet:$HOME/.cargo/bin:$PATH"`
(or restart your shell) before invoking `dotnet` / `cargo`.

Node.js itself is not auto-installed — use [nvm](https://github.com/nvm-sh/nvm)
or [volta](https://volta.sh) to install Node 22 before running `--install`.

Per-component dev loops:

```bash
# Sidecar (.NET)
cd src-sidecar && dotnet restore && dotnet build && dotnet test

# UI (TypeScript)
cd src-ui && pnpm install && pnpm lint && pnpm typecheck && pnpm build

# Tauri (Rust) — requires rustup + Linux system deps on Linux
cd src-tauri && cargo fmt --check && cargo clippy -- -D warnings && cargo check

# Repo-level
scripts/check.sh

# Agent runtime (independent package; not loaded by the app yet)
cd packages/agent-runtime
pnpm install --frozen-lockfile
pnpm check:provenance && pnpm lint && pnpm typecheck && pnpm test
```

## Status

FT-0 and FT-1 are complete; FT-2's structured control is delivered. Managed
runtime installation and the remaining native features are in progress or
planned. Windows PTY support remains deferred. See [milestones](docs/milestones.md)
for the Home and Native tracks and the accepted runtime ownership migration.
