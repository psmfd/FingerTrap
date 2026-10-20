# 0034 — Own the agent runtime in FingerTrap and retire pi_config

- Status: Accepted
- Date: 2026-10-20
- Approval: Maintainer approval, 2026-10-20
- Partially supersedes: [ADR-0012](0012-home-phase-resequencing.md)'s external
  governance ownership and [ADR-0025](0025-ft2-rpc-supervisor-native-pane.md)'s
  allocation of policy ownership to pi_config
- Retained: ADR-0001's process boundaries, ADR-0025's executable supervisor,
  ADR-0028's session-browser contract, and existing approval restrictions
- Implementation tracking: [migration inventory and issue ledger](../docs/migration/README.md)

## Context and problem statement

The maintainer has directed that future pi_config functionality be developed
in FingerTrap and that pi_config cease being the active development home.
FingerTrap should own an integrated terminal and agent experience: runtime
configuration, extensions, orchestration, expertise, native controls, and
installation. This decision records the approved migration design;
it does not implement the move or select an SDK backend for production.

The previous Home plan assigned policy, agents, rules, extensions, and approval
semantics to pi_config; FingerTrap supplied native presentation and process
hosting. That split created cross-repository
dependencies for features whose intended home is this application.

The migration has three separate decisions:

1. Where source, planning, tests, and releases are owned.
2. Whether managed native sessions use the Pi executable or a Pi SDK worker.
3. Which trusted component may authorize an operation.

Changing the first does not automatically settle the other two.

### Evidence baseline

Read on 2026-10-20:

- FingerTrap `dev`: `8ffeae809f7b4579d6b4f006c3f6e8414df60620`.
  FT-0/FT-1 and FT-2 control functionality are delivered. Managed installation
  remains [#187](https://github.com/psmfd/FingerTrap/issues/187), onboarding
  remains [#186](https://github.com/psmfd/FingerTrap/issues/186), and
  [PR #188](https://github.com/psmfd/FingerTrap/pull/188) is a proposal.
- pi_config `dev`: `9862f35a50f5f0d0770d90665cac1a4b8d3f0947`.
  Its runtime pin and FingerTrap's recorded RPC contract remain
  `v0.85.1-psmfd.1`. The 12 extension mirrors identify pi_config as their source.
- Pi fork `v0.87.1-psmfd.1`, published 2026-09-29, resolves to source commit
  `42429b94e4aca33fdbb8d094de29758b36411ba1`. SDK observations below use that
  source, not the stale local Pi checkout or an unqualified latest npm version.
  Publication does not establish FingerTrap consumer compatibility.
- The consumer qualification work includes
  [pi_config#1170](https://github.com/psmfd/pi_config/pull/1170) and overlapping
  pin-update [#1172](https://github.com/psmfd/pi_config/pull/1172).
  Native expertise readiness remains blocked by
  [#1162](https://github.com/psmfd/pi_config/issues/1162); diagnostic continuation
  is preserved in [#1174](https://github.com/psmfd/pi_config/pull/1174).

These are dated observations, not instructions to merge old branches. Refresh
their state and record exact source identities before importing changes.

## Considered options

- **A — Keep pi_config authoritative and consume it from FingerTrap.**
  Preserves current boundaries but does not meet the new ownership goal.
- **B — Move selected source and planning here, retaining executable/RPC
  integration indefinitely.** Lowest initial integration cost; host-owned
  resource and lifecycle features must fit CLI/RPC seams or extend the fork.
- **C — Move ownership here, retain executable/RPC during migration, and
  evaluate an SDK worker for managed native sessions.** Separates source
  migration from backend replacement and makes a switch depend on evidence.
- **D — Replace the executable immediately with SDK integration, or rewrite
  Pi functionality in .NET.** Combines source migration, packaging, lifecycle,
  and compatibility changes before a replacement has been demonstrated.

## Decision outcome

Chosen option: **C**. FingerTrap becomes the development home. The executable
remains the working backend for the first migration slices. A small SDK-worker
evaluation determines whether its additional control justifies adopting it
for managed native sessions; SDK adoption requires a subsequent decision.

### 1. Move ownership while preserving working mechanisms

Keep the reusable TypeScript runtime in a distinct package, provisionally
`packages/agent-runtime/`, with extensions, shared modules, default agents,
rules, prompts, and their tests. This path is a proposed layout; this ADR
does not create placeholder packages.

The .NET sidecar continues to own terminal services, session supervision,
and native integration services. The Rust shell retains its established
credential and application-confirmation responsibilities. The WebView
renders untrusted output and collects operator intent.

Source ownership in FingerTrap does not make the WebView a policy authority,
nor require every migrated module to become C#. Pi core remains an external,
exactly pinned dependency. pi-workflow and agent-expertise-api remain separate
projects unless their ownership is changed by a later decision.

| Existing component | Destination or disposition |
| --- | --- |
| pi_config plans, rules, agents, prompts, and extension source | Curated runtime package and FingerTrap planning; preserve provenance and applicable tests |
| Guards, tool ownership, routing, compaction, worktree mechanics | Keep runtime enforcement while moving its source; port individual mechanisms only with equivalent behavioral evidence |
| Meter recorders and schemas | Runtime package; native read-only views remain #157 |
| Expertise client and unfinished local provider | Carry contracts and evidence; keep unsupported native/local capabilities disabled |
| Draft approval, approved bash, package-agent grants | Preserve current restrictions; native authorization requires its own design |
| 12 extension mirrors and public pi-config distribution | Decide per destination: publish from FingerTrap or retire with migration guidance; never keep two active source owners |
| pi-workflow | Pin and integrate when usable; its unfinished review workflows remain explicitly incomplete |
| Pi fork | Retain required patches and release provenance; review each patch's necessity against the selected backend |
| Legacy installers, mirror automation, update jobs | Replace or retire after their supported consumers have a migration path |
| Private notes, live settings/auth, local review artifacts | Excluded from public import; preserve privately where needed |

pi_config is private and FingerTrap is public. Import reviewed, tracked source
through an explicit allowlist, with license/notice preservation and secret
scanning. Do not import its whole Git history, operator configuration, or
ignored artifacts. Maintain a ledger of source commit/path, local modifications,
destination, tests, and issue disposition. Preserve pending fixes and diagnostic
evidence separately from the production runtime.

New product work should be planned here. The ownership handoff is per component:
record its source baseline and successor before making it read-only upstream.
Archive pi_config only after the completion criteria below; this ADR does not
archive repositories, close issues, publish packages, or change live installations.

### 2. Executable versus SDK

The [SDK documentation at the evaluated release][sdk-doc] describes a
TypeScript API hosted by Node.js or Bun. FingerTrap's backend is .NET, so SDK
adoption still needs a supervised JavaScript process and a language-independent
protocol. It does not remove IPC or replace the .NET sidecar.

| Concern | Pi executable over RPC | FingerTrap worker using Pi SDK |
| --- | --- | --- |
| Existing integration | Supervisor, event relay, extension dialogs, recovery, and RPC goldens already exist | Requires an adapter and lifecycle/compatibility evidence |
| Resource ownership | CLI settings and extension discovery; migrated path assumptions still need correction | Explicit `agentDir`, resource loader, settings/session managers, and tool selection |
| Session control | Limited to the verified wire contract and fork capabilities | Direct session state/events and lifecycle APIs, translated into the host protocol |
| Session browsing | Existing bounded .NET scan; ADR-0028 avoids extension-starting inspector children | SDK listing without creating an agent is a candidate; bounds, corruption reporting, and import side effects must be tested |
| Packaging | Pin and verify a CLI artifact plus the complete extension dependency set | Build/verify worker, SDK, JS runtime, assets, native dependencies, and required fork changes |
| Approval authority | Existing RPC confirmation is not a trusted human-presence proof | Custom UI bindings are also not a trusted human-presence proof |
| Compatibility | PTY/TUI and user-managed installations remain natural | CLI remains needed for PTY/TUI and any subprocess consumers until separately migrated |
| Maintenance | Reverify protocol and extension behavior at pin changes | Own the adapter protocol and reverify SDK, extensions, runtime, and lifecycle behavior |

The strongest SDK benefit is **explicit application control**. The
[factory][sdk-source] accepts `agentDir`, `modelRuntime`, `settingsManager`,
`sessionManager`, `resourceLoader`, tool allow/deny lists, and custom tools.
The [resource loader][loader-source] supports inline extensions and discovery
controls. This fits a managed application that owns its resources.

Those options do not make arbitrary extensions honor app storage or suppress
all ambient credential discovery. Existing shared-state, worktree, and meter
code directly constructs `~/.pi` paths. FingerTrap's `SessionStore` and
`WorktreeReconciler` do too. Audit and correct every such consumer for either
backend, including child-process environment propagation.

An SDK worker would occupy the current Pi-child position:

```text
WebView <-> Tauri shell <-> .NET sidecar
                               |
                     supervised session child
                     /                      \
             Pi CLI / RPC             SDK worker
               (current)             (evaluation)
```

Start the evaluation with one worker per live native session. Do not combine
sessions in one process before proving independence of extension module state,
environment, cwd, cancellation, and failure handling. A process boundary gives
failure isolation; it is not by itself a filesystem or credential sandbox.

Use a small versioned command/event adapter at the sidecar's session seam.
Preserve UI behavior and reuse scenario definitions; do not assume existing
Pi wire goldens validate a different protocol. Native services remain narrow
typed operations, with no generic call-any-sidecar-method tool.

The evaluated [package metadata][package-source] targets Node.js 22.19+ and
ships the SDK in the coding-agent package. It is not evidence of a separate,
dependency-free SDK distribution. The fork release publishes CLI archives;
an upstream npm package with the same base version must not be assumed to
contain the fork's security or capability patches. An SDK worker must identify
and verify its exact built source and dependency closure. Node versus Bun
remains an explicit packaging/compatibility choice.

### 3. Keep approval and platform decisions explicit

Repository relocation does not waive TUI-only approval contracts. The
[extension runner][runner-source] derives `hasUI` from a supplied UI context;
[RPC mode][rpc-source] supplies one while identifying itself as `rpc`.
An SDK host can also bind a UI and mode. Setting `mode: "tui"` merely to pass
existing checks would misrepresent the authorization boundary and is prohibited.

A native approval design must identify the trusted prompt, bind confirmation
to the exact final operation or record, reject replay and changed arguments,
and define cancellation and audit behavior. Until that design is implemented
and verified, protected operations retain their current refusal/TUI path.
This applies to #158 and the proposed approved-bash and package-agent work.
The existing requirement for credential brokering and grants before FT-3
remains in force under FingerTrap ownership.

Apple Silicon macOS remains the packaged app target. Linux ARM64 expertise
runtime/Landlock evidence does not certify macOS. Local expertise must remain
gated until a supported macOS design is verified, or be explicitly deferred
while the existing remote provider is qualified. API bootstrap still depends
on [agent-expertise-api#528](https://github.com/psmfd/agent-expertise-api/issues/528).

### 4. Sequence the migration

| Slice | Reviewable result | Exit evidence |
| --- | --- | --- |
| M0 — Ownership and inventory | Migration design accepted; record component/issue dispositions and immutable inputs | One authoritative owner per component; pending work and private evidence accounted for |
| M1 — Curated source import | Runtime package with preserved contracts, notices, and applicable tests | Source/provenance review; no live config copied; focused existing tests pass |
| M2 — Isolated managed profile | Shared app-storage contract, explicit executable selection/environment, PTY/RPC launch and resume | No writes to `~/.pi`, global npm, or shell rc files; no ambient credential import; session/worktree/meter paths agree |
| M3 — SDK evaluation | One-session worker prototype against the same selected fork revision as the executable comparator | Pass the gate below or record a no-go; neither outcome blocks ownership migration |
| M4 — Managed distribution | Complete verified closure, onboarding, supported login, one updater, atomic activation and rollback | #186/#187 criteria and clean Apple Silicon guest with no global Node/npm |
| M5 — Native capabilities | Transcript rendering, dashboards, and separately designed approvals, expertise, workflows, and tool hosting | Feature-specific contracts and platform evidence; no inherited readiness claims |
| M6 — Retirement | Replacement publishing ownership or explicit mirror retirement; archived pi_config with migration pointers | FingerTrap builds/tests/releases independently; recovery and old-user migration documented |

Reconcile #187 and draft #188 with the selected runtime before installer work.
Signing, notarization, and release promotion remain separate acceptance gates.
SSH/SFTP, settings, and VM-host work retain their Native-track dispositions.

### SDK adoption gate

The prototype must demonstrate a useful SDK-specific gain, such as controlled
resource discovery or session metadata access without agent startup. Merely
reimplementing current RPC commands is insufficient justification.

- Use the same exact qualified Pi source for both backends; account for every
  required fork patch, artifact, license, and native dependency.
- Verify streaming, settled/queue semantics, steer/follow-up/abort, model
  changes, compaction, resume/fork, cwd replacement, and subscription rebinding.
- Exercise real migrated extensions, protected tool ownership, worktree
  isolation, and subagent spawning; an SDK parent does not migrate CLI children.
- Prove per-profile path and credential isolation, extension UI cancellation,
  and refusal of protected approvals without a trusted native design.
- Prove startup failure, deadlines, bounded output/backpressure, process death,
  shutdown hooks, crash recovery, and cleanup through the packaged worker.
- If proposing SDK session listing, preserve metadata-only projection,
  resource bounds, skipped-file visibility, and zero extension startup. Keep
  ADR-0028's single source until an explicitly accepted replacement passes.
- Measure cold start, resident memory, package size, and parallel-pane behavior
  against executable/RPC on Apple Silicon. Record results without assuming an
  SDK performance advantage.
- Verify clean-guest installation, supported provider login, offline startup
  behavior, session-safe updates, and rollback. Do not silently switch an
  active session between backends.

Record the outcome in a successor ADR. Keep executable/RPC for user-managed
and PTY sessions even if the SDK becomes the managed native-session backend.

### Consequences

- Good: source, tests, and product planning converge in the application that
  consumes them; migration is not held behind the whole pi_config backlog.
- Good: existing runtime mechanisms and executable behavior remain usable
  while an SDK alternative earns adoption through evidence.
- Bad: FingerTrap inherits runtime packaging, policy maintenance, and any
  retained extension-publishing obligations.
- Bad: a temporary second backend needs comparison tests and a recorded
  disposition; an inconclusive prototype must not become permanent support.
- Neutral: native approval and macOS expertise work remain real projects;
  neither repository ownership nor SDK APIs complete them.

## Known limitations and deferred work

- No runtime source is imported by this ADR. The detailed migration ledger
  and successor implementation issues are M0 work.
- Existing pi_config qualification failures remain open evidence; moving
  code does not clear them. Preserve useful fixes from #1170 independently
  of duplicate pin updates, and diagnostic evidence from #1174.
- The SDK comparison is a source/documentation assessment, not a packaged
  runtime experiment. Production SDK adoption and JS-runtime selection are
  deliberately undecided.
- This decision does not complete local expertise, pi-workflow review
  definitions, #157/#158, FT-3, or release signing/notarization.

[sdk-doc]: https://github.com/psmfd/pi/blob/42429b94e4aca33fdbb8d094de29758b36411ba1/packages/coding-agent/docs/sdk.md
[sdk-source]: https://github.com/psmfd/pi/blob/42429b94e4aca33fdbb8d094de29758b36411ba1/packages/coding-agent/src/core/sdk.ts
[loader-source]: https://github.com/psmfd/pi/blob/42429b94e4aca33fdbb8d094de29758b36411ba1/packages/coding-agent/src/core/resource-loader.ts
[package-source]: https://github.com/psmfd/pi/blob/42429b94e4aca33fdbb8d094de29758b36411ba1/packages/coding-agent/package.json
[runner-source]: https://github.com/psmfd/pi/blob/42429b94e4aca33fdbb8d094de29758b36411ba1/packages/coding-agent/src/core/extensions/runner.ts
[rpc-source]: https://github.com/psmfd/pi/blob/42429b94e4aca33fdbb8d094de29758b36411ba1/packages/coding-agent/src/modes/rpc/rpc-mode.ts
