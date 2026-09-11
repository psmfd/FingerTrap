# Managed Pi onboarding and installation plan (proposal)

> **Status: proposal only.** This plan is a next-session execution guide for
> [#187](https://github.com/psmfd/FingerTrap/issues/187) and
> [#186](https://github.com/psmfd/FingerTrap/issues/186). It is not accepted
> architecture, implementation authorization, or evidence that a native
> pi_config distributable exists. Do not implement the managed path until a
> reviewed ADR makes the decisions below. This planning document must not close
> either implementation issue.

**Refs:** #187, #186, [PR #184 handoff comment](https://github.com/psmfd/FingerTrap/pull/184#issuecomment-5633844432).

## Outcome and boundaries

FingerTrap should eventually offer an explicit choice between an existing
user-managed Pi installation and an opt-in, isolated FingerTrap-managed Pi
runtime. The managed mode must be a complete, immutable, verified closure of
Pi, pi_config, extensions, declared dependencies, and the runtime required to
execute them; it must not require global Node/npm.

The proposal preserves these non-negotiable boundaries:

- Do not modify `~/.pi`, global npm state, shell rc files, or host credentials.
- Use app-specific per-user storage only. `PI_CODING_AGENT_DIR` is a candidate
  supported seam, not an assumed design; validate its supported layout and
  behavior first.
- Authentication is only through Pi's supported login flow. Do not import or
  consume ambient host credentials.
- A user-managed installation and managed installation remain distinct,
  explicit, and independently supportable.
- An explicit Pi request continues to report a Pi startup failure; it never
  silently becomes a shell. `+sh` remains an explicit usable shell choice.
- Signing, notarization, and release promotion are separate gates. No candidate
  or implementation may represent those gates as complete without their own
  evidence.

## Before any implementation PR

1. Read [#187](https://github.com/psmfd/FingerTrap/issues/187),
   [#186](https://github.com/psmfd/FingerTrap/issues/186), [PR
   #184](https://github.com/psmfd/FingerTrap/pull/184), and its [handoff
   comment](https://github.com/psmfd/FingerTrap/pull/184#issuecomment-5633844432)
   live. Check their current state, linked PRs, and current upstream links; do
   not rely on copied text or a stale local pi_config clone.
2. Inspect the current worktree state and candidate files without changing or
   cleaning them. **Do not clean stale worktrees or `pi-wip` refs.** Do not
   modify global `~/.pi`.
3. Reconcile live upstream Pi and pi_config contracts: supported versions,
   packaging/distribution surfaces, extension/dependency requirements,
   installer behavior, provenance, and authentication. Record immutable input
   identifiers and source URLs. Investigate any upstream installer/distribution
   documentation dependency under #187; file a narrow downstream issue only if
   it is clearly actionable and not already tracked upstream.
4. Select exactly one scoped implementation PR only after the design/ADR is
   approved. Do not open extra PRs containing placeholder code.

## Staged implementation PR breakdown

| Stage | Proposed PR-sized result | Entry / exit evidence |
| --- | --- | --- |
| 0. Upstream contracts and provenance reconciliation | A fact record identifying current upstream Pi/pi_config contracts, immutable inputs, supported versions, distribution/provenance evidence, and gaps. | Live sources read and dated; local clone explicitly classified as stale, non-authoritative evidence; unresolved packaging dependency tracked under #187. |
| 1. ADR and design | A reviewed ADR defines user-managed/managed modes, ownership, storage, manifest, trust roots, verification, activation, rollback, update owner, authentication, and minimal environment. | Decisions and threat boundaries accepted before installer/UI implementation. No new ADR is added by this plan PR because the decision is not finalized. |
| 2. Missing-Pi onboarding UX | Startup and Pi-default split flow gives actionable onboarding: choose an existing installation or opt in to managed install, while explicit Pi failure remains visible and `+sh` works. | #186 acceptance tests pass: no-Pi launch, explicit Pi selection/error, shell selection, and Pi-default splits. |
| 3. Managed runtime/config closure | Native bounded installer stages a complete immutable runtime/config/extensions/dependencies closure in app storage, verifies it, and activates atomically with a verified prior version retained. | Architecture, provenance/signature and digest checks; traversal and symlink-escape rejection; cancellation/concurrency handling; rollback evidence. No generic shell bootstrap or `npm install latest`. |
| 4. Lifecycle, update, environment, and auth | One update owner, lifecycle/status/repair behavior, minimal managed environment, supported Pi login, and session-safe update/rollback integration. | No ambient credential import or global mutation; existing user install remains supported; stable active-session behavior proven. |
| 5. Clean guest and release gates | Independent acceptance validation in clean guests, including a guest with truly no Node/npm, plus separately governed signing/notarization/release-promotion gates. | Negative and compatibility matrix complete; candidate evidence is promoted only through the applicable release gates. |

Stages may expose a design blocker and stop; they are not a promise to ship a
particular packaging mechanism.

## Design decisions to resolve in the ADR

- Which current upstream artifacts are authoritative; which immutable IDs,
  trust roots, signature/provenance formats, and supported architectures apply.
- Whether and how pi_config is distributable as part of the managed closure;
  `PI_CODING_AGENT_DIR` semantics, ownership, layout, migration, and cleanup.
- Consent UX, installation/update ownership, manifest format and retention
  policy for active/previous versions.
- Failure, recovery, repair, downgrade, cancellation, concurrent-install, and
  offline behavior; what remains usable when managed Pi is absent or invalid.
- Exact minimal environment, executable resolution, network policy, supported
  Pi login flow, credential boundaries, telemetry/log retention, and support
  diagnostics.
- Platform/architecture support and the independent criteria for signing,
  notarization, and release promotion.

## Required safeguards and tests

The ADR and implementation slices must make these verifiable, not implicit:

- explicit trusted consent; native bounded installation; new-version staging;
  architecture, provenance/signature, and digest verification before atomic
  activation; verified previous-version rollback;
- reject wrong architecture/digest/signature, path traversal, and symlink
  escape before activation; handle cancellation and concurrent attempts;
- preserve `~/.pi`, global npm/Node state, shell rc files, and host credentials;
  use only the supported Pi login flow and a minimal managed environment;
- test a clean guest with no Pi, a truly no-Node/no-npm guest, an existing
  user-managed installation, default Pi startup/split behavior, explicit Pi
  failure, and `+sh`;
- test install/update/rollback while active sessions remain stable, then record
  repeat-instance, process-cleanup/watchdog, RPC, and Keychain coverage where
  applicable.

## Documentation map

Implementation work should update documentation only when the corresponding
facts are accepted and tested:

| Location | Intended update |
| --- | --- |
| `adrs/` and README architecture index | The approved ADR and an index entry describing the accepted managed/user-managed boundary. |
| `README.md` | User setup: choosing an existing Pi versus opt-in managed Pi, supported platforms, login, recovery, and explicit limitations. |
| `CONTRIBUTING.md` | Developer/tester setup, immutable-input refresh rules, clean-guest validation, and no-global-state safeguards. |
| `milestones.md` | Sequenced delivery and independent signing/notarization/release-promotion gates. |
| Candidate validation documentation | Candidate provenance, test matrix, limits, artifact references, and promotion evidence; candidates must be labeled as candidates. |
| Upstream docs investigation (#187) | Current upstream installer/distribution documentation, contracts, and any dependency/gap record. |

`AGENTS.md` is not in scope unless a policy change requires it. This plan adds
no ADR now: architecture is deliberately unresolved.

## PR #184 handoff — preserve, do not overstate

PR #184 is paused for this plan and **not accepted**. Its active head is
`ci/apple-silicon-candidate`; it remains a draft targeting `dev`. Treat the
following as useful partial candidate evidence, not an accepted release or
managed-install proof:

- Last check host: arm64 macOS 26.6.2 (build 25G83), Tart 2.32.1. The
  `fingertrap-release-smoke` VM was **RUNNING**; the existing `ft-macos-test`
  VM is preserved stopped.
- Base: `ghcr.io/cirruslabs/macos-sequoia-base@sha256:3f4d14a5ffb9efd3bda2ae0184fd4bc2773d924ff8b7565f958761420ec41a0c`.
  Guest: macOS 15.7.7 (build 24G720).
- Node 24.19.0 and npm 11.17.0 are present at
  `/opt/homebrew/opt/node@24/bin/{node,npm}`; Pi is **not installed**. This is
  not a no-developer-toolchain guest.
- Candidate local path:
  `/Users/pdavis/projects/_personal/FingerTrap/.local-testing/tart-pr184/candidate`.
- Artifact `10083748321`; run `34297368996`; build commit
  `579303c7d7b9834af58f40d6cfb2f0e22fdfa8ec`; branch `212255d`; outer SHA-256
  `126f235e8c2fd01dc34e068e966ade0ce3584f7929233e351fa157cb5253ed36`; both
  package hashes passed on the host.
- Operator-provided GUI observations: `+sh` executed; resize worked; closing a
  failed split left the original shell working; quit/relaunch then `+sh`
  worked. A split opened a failed Pi pane, so two working panes are unverified.
  The default-Pi error is expected for the missing dependency but does not meet
  desired clean-launch onboarding. No Gatekeeper prompt was recorded.
  Screenshots were not uploaded and are not independent automated GUI proof.
- Still untested/not accepted: real Pi, RPC, Keychain, repeat-instance race,
  process-cleanup/watchdog, upgrade acceptance, signing, notarization, and
  release promotion.

## Completion rule for this plan

This document is complete when it remains an accurate proposal and gives the
next session a safe first slice. The feature is complete only when #186 and
#187 acceptance criteria, the approved ADR, independent clean-guest evidence,
and separate release gates are satisfied.
