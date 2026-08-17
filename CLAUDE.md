# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

FingerTrap is a personal terminal application (local + SSH terminals, SFTP tree, status surfaces, command palette). It is a single app composed of **three processes** that communicate over JSON-RPC. Current status: **M1** — local PTY on Linux + macOS. Windows is deferred (`pty/spawn` throws `PlatformNotSupportedException`). See `docs/milestones.md` for the M0–M8 roadmap.

## Three-process architecture

The single most important thing to internalize: logic lives in the .NET sidecar, not the Rust shell or the TS UI.

1. **Tauri shell** (`src-tauri/`, Rust) — window, WebView, and sidecar lifecycle only. **No business logic.** `src-tauri/src/sidecar.rs` spawns `fingertrap-sidecar` as a child, pipes its stdout to the UI via a Tauri `Channel`, and exposes two commands: `sidecar_write` (UI→sidecar bytes) and `subscribe_sidecar_output` (registers the output channel).
2. **UI** (`src-ui/`, TypeScript + Vite + xterm.js) — terminal panes, SFTP tree, status bar, palette.
3. **Sidecar** (`src-sidecar/`, .NET 10) — owns ~95% of the logic: Pty, SSH.NET, LibGit2Sharp, Octokit, Azure DevOps SDK.

### IPC contract (read before touching message flow)

IPC is **JSON-RPC 2.0 over stdio with `Content-Length` framing** (LSP-style). `StreamJsonRpc` on .NET, `vscode-jsonrpc` on TS. The framing is hand-rolled in the UI because it must tunnel through Tauri's byte channel rather than a real socket. Key invariants:

- **The sidecar's stdout is owned by the RPC framing.** Any `Console.Write` to stdout corrupts the stream — all sidecar diagnostics go to **stderr** (ADR-0002). See the comment block at the top of `src-sidecar/src/FingerTrap.Sidecar/Program.cs`.
- **The Rust bridge uses `set_raw_out(true)`** (`sidecar.rs`). The default Tauri reader splits stdout on CR/LF, which shreds both the `Content-Length:\r\n\r\n` framing and any PTY payload containing CR/LF. Do not remove this.
- **The TS reader buffers messages that arrive before `listen()` registers its callback** (`src-ui/src/transport.ts`, `pending` queue). Removing this drops any `pty/output` notification that races the startup gap (issue #18 / commit `953858c`).
- The RPC surface is paired across three files that must stay in sync: `RpcSurface.cs` (.NET handlers + notifications), `Ipc/PtyMessages.cs` (DTOs), and `src-ui/src/api.ts` (TS request/notification types). ADR-0003 governs this pairing.
- Single-object params: the sidecar sets `UseSingleObjectParameterDeserialization = true` because `vscode-jsonrpc`'s `RequestType1<T>` sends the arg as a named `params` object. Binary PTY data crosses the wire **base64-encoded** (`dataBase64`).

Current RPC methods: `ping`, `pty/spawn`, `pty/write`, `pty/resize`; notifications `pty/output`, `pty/exit`.

## Build / lint / test

`scripts/dev-setup.sh` (add `--install` to install) verifies the toolchain: .NET 10 SDK, Node 22+, pnpm 10 (corepack), rustup + cargo, `cargo-tauri` CLI, and on Linux the Tauri system libs. Node itself is not auto-installed.

```bash
# Sidecar (.NET) — from src-sidecar/
dotnet restore && dotnet build && dotnet test
dotnet test --filter "FullyQualifiedName~PtyServiceTests"   # single test class/method

# UI (TypeScript) — from src-ui/
pnpm install && pnpm lint && pnpm typecheck && pnpm build

# Tauri (Rust) — from src-tauri/ (needs Linux system deps on Linux)
cargo fmt --check && cargo clippy -- -D warnings && cargo check

# Repo-level structural checks (ADR numbering, lock-file shape)
scripts/check.sh

# Dev loop (runs the whole app)
cd src-tauri && cargo tauri dev

# Sidecar PTY end-to-end smoke test (macOS/Linux only)
python3 scripts/smoke-pty.py
```

## Repo-specific traps

- **Lock-file RID contamination (will break CI for everyone).** Committed `packages.lock.json` files must be RID-agnostic (`"net10.0"`, never `"net10.0/osx-arm64"`). `dotnet publish -r <rid> --self-contained` rewrites your working-tree lock files with RID-composite entries; if you commit them, CI's `dotnet restore --locked-mode` fails NU1004 on all platforms. After any local `publish`, **do not `git add` the lock files** — or regenerate clean with `dotnet restore src-sidecar` (no `-r`). `hooks/check-lock-shape.sh` (run by `scripts/check.sh` and CI's `repo-check` job) is the safety net. Background: ADR-0009, CONTRIBUTING.md "Sidecar publish workflow", upstream NuGet#8287.
- **Vendored `Porta.Pty`** (`src-sidecar/external/Porta.Pty/`) is upstream code. Do not edit upstream-style files without updating the local-patches table in `UPSTREAM.md` (ADR-0008). The sidecar uses a single platform-agnostic `PtyService` backed by this library; platform branching lives inside it.
- **Production bundling** requires the sidecar's companion native lib (`libporta_pty.{dylib,so}`) staged into `src-tauri/binaries/` before `cargo tauri build`; Linux additionally needs a `patchelf --add-rpath` step so the linker finds the lib at `/usr/lib/FingerTrap/`. Full recipes in CONTRIBUTING.md "Production bundle workflow" (ADR-0010). For fast iteration without a bundle: `cargo tauri build --no-bundle`.
- **SmolVM verification** is **required** for any change to `scripts/dev-setup.sh` (paste the `PASS` summary into the PR), and recommended for `smoke-pty.py`, the `BuildPortaPtyNative` MSBuild target, or Linux bundle behavior. Recipes in CONTRIBUTING.md (ADR-0011). Note: `smoke-pty.py` is currently hardcoded to `aarch64-apple-darwin` (issue #17).

## Conventions

This repo follows the agent-framework global rules (GitHub Flow targeting `dev`, Conventional Commits, SemVer tags from `main`, ADR-required). Repo-canonical specifics live in `adrs/0004-repo-conventions.md`. ADRs use the MADR minimal template (`adrs/TEMPLATE.md`), sequential zero-padded numbering, **supersession not editing**; `scripts/check.sh` validates numbering and required sections. When changing the RPC surface, update the three paired files together (see IPC contract above).
