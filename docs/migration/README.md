# Agent runtime migration inventory

**M0 inventory recorded on 2026-10-20.** FingerTrap owns new product planning
under [ADR-0034](../../adrs/0034-own-agent-runtime-and-retire-pi-config.md).
**M1a source import completed locally:** the token and cache meters now live
in an inactive [runtime package](../../packages/agent-runtime/README.md).
See the [import report](m1a-import.md) for provenance, tests, and limits.
Executable/RPC remains the backend; SDK adoption still requires the comparison
and successor ADR.

This directory contains:

- [Current status and resume instructions](STATUS.md) and
  [activity log](WORK_LOG.md): durable progress, checkpoints, validation and
  the next action. Start here when continuing work.
- [Approved implementation plan (HTML)](implementation-plan-200-profile.html):
  maintainer-approved sequence for #200 and #201, recorded 2026-10-20.
- [Issue ledger](issue-ledger.md): all 199 open pi_config issues, three pending
  PRs, public distribution requests, extension-mirror maintenance, and relevant
  external dependencies, mapped to successor work.
- [Source manifest](source-manifest.json): exact source commits, Git tree/blob
  identities, component counts, mirror provenance, and pending PR heads.
- [First import selection](first-import.json): the 20 exact source files for
  M1a and their destinations. Actual transformations and destination hashes
  are in [package provenance](../../packages/agent-runtime/provenance.json).

Here **M0–M6 mean the migration slices in ADR-0034**, not the older foundation
milestones in [milestones.md](../milestones.md). Stable work IDs such as
`IMPORT` and `PROFILE` refer to the successor links in the issue ledger.

## Source ownership and immutable inputs

| Input | Identity | Ownership/disposition |
| --- | --- | --- |
| FingerTrap base | `8ffeae809f7b4579d6b4f006c3f6e8414df60620` | Destination and new product planning owner |
| pi_config source | `9862f35a50f5f0d0770d90665cac1a4b8d3f0947` | Immutable import baseline; 944 tracked files partitioned into 61 inventory groups |
| Current Pi fork | `v0.85.1-psmfd.1`, `e7d09c9dc5fca409f46a5476669e3c0404054d69` | Existing consumer contract; keep during first import |
| Candidate Pi fork | `v0.87.1-psmfd.1`, `42429b94e4aca33fdbb8d094de29758b36411ba1` | Published fork release; consumer qualification remains incomplete |
| pi-workflow | `fd78b8e89a0ce571d72a22fe0204c334019f1f3e` | Separate canonical package; FingerTrap owns consumption |
| agent-expertise-api | External API contracts/issues | Separate canonical service; provider/bootstrap dependencies remain explicit |
| Public pi-config and 12 extension mirrors | Per-repository commits and provenance blobs in manifest | Generated distribution destinations; transition publisher or retire explicitly |

The source manifest partitions the entire tracked source tree, including
infrastructure and excluded material. It does **not** authorize copying every
inventoried path. Source exports must use the recorded commit and an explicit
per-slice file list. No branch tip, working-tree overlay, or whole Git history
is an acceptable substitute.

There is one future source owner: FingerTrap. Existing pi_config automation
remains operational until the relevant consumer handoff is verified; that is
a transition dependency, not permission to develop competing implementations.
Pi core, pi-workflow, and agent-expertise-api retain their separate source owners.

## Runtime component inventory

The baseline has **26 extensions plus `shared`**, totaling 436 tracked files
under `agent/extensions/` including its README and marker package file.
Counts below identify migration units, not readiness or test results.
All retained extension source targets `packages/agent-runtime/extensions/`;
each import must bring its actual shared/vendor dependency and test closure.

| Component paths under `agent/extensions/` | Files | Destination and gate |
| --- | --- | --- |
| `token-meter`, `cache-meter` | 11 + 8 | IMPORT M1a complete, inactive. Recorder schemas and six test files retained; 41 tests pass. PROFILE replaces default home paths before managed use. |
| `prefill-meter` | 8 | Later IMPORT. It calls the Pi SDK prompt formatter at runtime; qualify that dependency and the managed storage path. |
| `shared` | 62 | Import only each slice's dependency closure, eventually the retained common library. Includes state, routing, GitHub, expertise, secret scanning, and package-agent contracts; do not flatten distinct trust boundaries into one bridge. |
| `secrets-guard`, `bash-destructive-guard`, `gh-identity-guard` | 5 + 12 + 16 | IMPORT preserves enforcement, adversarial tests, shell parsing, and identity behavior. AUTHORITY owns stronger controls; these guards are not an OS sandbox. |
| `bash-confinement` | 4 | Preserve source/evidence; AUTHORITY qualifies platform enforcement. Linux-specific guarantees cannot be enabled by a macOS path change. |
| `package-agent-broker` | 42 | Preserve proposal/grant/digest contracts and adversarial tests. AUTHORITY blocks native approval and credential-bearing dispatch until the trusted design passes. |
| `subagent` | 20 | Preserve upstream/patch provenance and serial execution contracts. Resolve malformed tool declarations, project-agent confirmation, child invocation, isolation, and crash cleanup before enabling migrated dispatch. |
| `worktree` | 16 | IMPORT retains mechanics and tests; PROFILE aligns session/manifest roots with the sidecar. Carry host-local PID and porcelain-parser defects. |
| `auto-router` | 28 | Retain routing behavior and tests. BACKEND verifies workaround retirement and fork compatibility; model/quality tuning is separate deferred work. |
| `compaction-optimizer`, `context-manager`, `payload-tuner` | 25 + 10 + 12 | Preserve hooks, settings precedence, abort behavior, schemas, and dependency closure. Do not change tuning defaults as part of relocation. |
| `artifact-handoff`, `web-fetch` | 4 + 5 | Curate portable source/tests and explicit filesystem/network policy; do not import operator allowlists. |
| `git-read`, `github-read` | 4 + 6 | Retain bounded typed operations, shared parsers, identity and output sanitation. FT-3 is not a generic sidecar-method bridge. |
| `hashline-edit` | 51 | Retain the builtin `edit` ownership contract, upstream patch manifest, and `LICENSE.upstream` from RimuruW/pi-hashline-edit. |
| `indexing` | 20 | Preserve typed search and shared state; qualify cocoindex/model dependencies, integrity pins, and supported-platform availability. |
| `repo-dash` | 15 | Preserve TUI behavior and data contracts; correct RPC-incompatible custom UI calls before activation. Existing native dashboards remain #157. |
| `plain-english` | 7 | Curate the output/style mechanism and tests; workflow mutation enforcement is separate future work. |
| `expertise-client`, `expertise-fanout-gate`, `expertise-indexer`, `expertise-mutation-boundary` | 15 + 8 + 17 + 3 | EXPERTISE preserves contracts/tests and selects a supported provider. Local mutation remains gated; RPC UI does not authorize protected changes. |
| Extension-root `README.md`, `package.json` | 2 | Replace with FingerTrap package documentation and real dependency metadata. The source package is only an ESM marker; it is not a reproducible dependency manifest. |

## Defaults, tooling, plans, and exclusions

| Source | Inventory | Disposition |
| --- | --- | --- |
| `agent/agents` | 21 wrappers | Curate useful defaults under the runtime package. Positive tools, model selection, project trust, and relevant evaluation results are activation gates. |
| `agent/skills` | 68 files across 21 skill directories | Select applicable skills and review content rights/provenance. Do not copy or activate the entire catalog merely because it is tracked. |
| `agent/rules` | 26 files | Carry requirements into FingerTrap runtime policy with supersession review. Historical parallel/serial and orchestration rules must be reconciled; these are not instructions for the migration agent. |
| `agent/prompts` | 8 prompts | Curate useful templates; review-class prompts remain compatibility candidates until pi-workflow is ready. |
| `agent/AGENTS.md`, settings/model examples | 3 files | Rewrite the runtime catalog and safe examples for managed profiles. Never use a source example as a live operator configuration. |
| `agent/vendor`, `agent/npm` | Exact trees in manifest | Select actual dependency closure: Pi, parser, scanning/indexing tools, expertise helpers and other retained consumers. Preserve versions/checksums/notices; independently qualify platform assets. |
| `scripts`, `tests` | 101 + 5 files | Bring relevant fixtures and behavioral tests with each slice. Replace home-cache installs, network-on-test runners, and source-repo discovery assumptions with package-local locked tooling. |
| `hooks` | 3 scripts | Reassess repository/developer hooks separately from runtime enforcement. Do not auto-install global hooks or change shell configuration. |
| `LICENSE` | MIT source notice | Preserve the original notice in imported code/package; retain additional vendored notices independently. |
| `adrs`, `notes`, `docs`, `security` | Exact trees in manifest | Reference evidence privately; write selected public contracts in FingerTrap. No wholesale ADR renumbering or private plan/log publication. |
| `mirror`, `.github`, installers, root setup/update/config files | Exact trees/blobs in manifest | RETIRE or INSTALL replaces ownership and automation. Import useful validation cases, not a second active updater or publisher. |
| `.pi`, `.review`, `setup.local.env.example` | Tracked templates | Exclude from runtime import; tracked status is not a public-content review. |
| Live auth/settings/model stores, sessions, caches, trust/grants, ignored logs/artifacts | Outside tracked-source selection | Excluded. No automatic copying from the operator's installation. |

The dirty source `notes/session-handoff.md` and FingerTrap's existing local
handoffs, testing directory, and legacy patch remain untouched. The manifest
records their disposition without publishing their contents. Pending PR
changes are separately identified in the ledger; none is silently overlaid
onto the baseline.

## First implementation slice: M1a source imported

The slice below is implemented in the inactive package. Its
[completion report](m1a-import.md) records the actual review and validation;
the wider source migration and activation gates remain open.

Create an initially private, independently testable
`packages/agent-runtime/` package containing **token-meter and cache-meter**.
Their 19 tracked files plus the source MIT license make the 20-file candidate
manifest. Neither extension imports `../shared`; token-meter needs TypeBox
at runtime, and their Pi extension API imports are type-only. This keeps the
first source move bounded while preserving data that #157 will consume.
Prefill-meter follows later because its formatter has a runtime SDK dependency.

1. Export the exact candidate blobs into a staging directory. Review source,
   tests, examples, documentation links, and license notices for public import;
   scan the complete staged result. Record any transformations and destination
   digests. A failed review removes or revises an item rather than expanding
   the allowlist implicitly.
2. Create a real package manifest, package-local pnpm lock, strict typecheck,
   lint, and test commands. Preserve the current API compatibility baseline;
   development typings from npm are not proof of fork runtime equivalence.
   Do not use `scripts/lib/extension-deps.sh` to install into the home cache.
3. Run all six existing test files against isolated temporary directories:
   record/schema behavior, session keys, storage, tiers, and token-meter hooks.
   Verify the test run does not consume or alter live operator state. Adapt
   harness paths explicitly and record meaningful behavior changes.
4. Preserve the existing JSONL contracts and leave the package out of runtime
   discovery/activation. No Pi pin bump, installer change, dashboard feature,
   or SDK backend switch belongs in this slice.

**Exit:** reviewed provenance and notices, complete locked dependency metadata,
passing focused tests/typecheck/lint, and a clean source/package review.
No claim of managed installation or production runtime readiness follows.

## Sequence after M1a

| Next work | Dependency and outcome |
| --- | --- |
| Remaining M1 imports | IMPORT defines a file/test/dependency closure for each group. Preserve pending security/correctness fixes before activating affected components; do not wait for every deferred feature. |
| M2 managed profile | PROFILE establishes one app-owned root contract across launch, resume, resource discovery, session browsing, worktree manifests, meters, child processes, and credential resolution. No `~/.pi`, global npm, or shell-rc mutations; no ambient credential import. |
| M3 backend comparison | BACKEND first selects one qualified fork revision for both candidates, then runs the ADR-0034 gate with real migrated extensions. Use one SDK worker per native session; retain CLI children/PTY until separately migrated. Record a go/no-go ADR. |
| M4 install/onboarding | Reconcile #186/#187 and draft #188 with this ownership decision. Verify the full artifact closure, login, atomic activation and rollback on a clean Apple Silicon guest without global Node/npm. |
| M5 product capabilities | #157 consumes stable recorder schemas; #158 remains display/intent. AUTHORITY, EXPERTISE, and WORKFLOW each satisfy their own gates. Transcript rendering remains separately scoped UI work. |
| M6 retirement | RETIRE records a publisher or explicit retirement path for every mirror, independent FingerTrap builds/tests/releases/recovery, and old-user migration. Only then archive pi_config. |

The strongest SDK case is explicit control of resources, tools, settings,
sessions, and lifecycle. It still requires a supervised JavaScript process
beside the .NET sidecar, a protocol adapter, and complete packaging. The
source move does not need that decision to be settled first.

## Maintaining this record

`source-manifest.json` is a dated snapshot, not a moving dependency lock.
Reconcile source changes deliberately and update the affected component's
successor before changing its baseline. `first-import.json` is supplemented
by the package's actual import provenance.

Inventory counts and object identities come from `git ls-tree -r -t` at the
recorded source commit. Mirror provenance/package blob IDs were matched
against each recorded mirror commit's root tree. Issue coverage was checked
against all pages of the open-issue API response with PRs counted separately.
This records planning evidence; it is not a new runtime test or security audit.
