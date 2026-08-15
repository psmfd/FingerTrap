# Security Policy

## Reporting a vulnerability

Please report vulnerabilities privately via GitHub's
[private vulnerability reporting](https://github.com/psmfd/FingerTrap/security/advisories/new)
for this repository. Do not open a public issue for a security problem.

You can expect an acknowledgement within a week. Fixes ship through the
normal release flow (`dev` → `main` promotion, semantic-release).

## Supported versions

Only the latest release on `main` is supported. There are no maintenance
branches for older versions.

## Scope notes

- The sidecar speaks JSON-RPC over stdio to the Tauri shell only; it opens no
  network sockets.
- `Porta.Pty` under `src-sidecar/external/` is a vendored fork; report issues
  in its code here, not upstream.
