# Agent runtime issue migration ledger

Snapshot: 2026-10-20. Governing decision: [ADR-0034](../../adrs/0034-own-agent-runtime-and-retire-pi-config.md).
See the [component inventory and sequence](README.md) and [immutable inputs](source-manifest.json).

This is a planning disposition, not a bulk GitHub transfer or closure. Source issues
and PRs remain open. Links into private repositories remain access-controlled;
raw issue bodies, logs, and private notes are not republished here. Each of the
**199 open pi_config issues** appears once below. The three open PRs are
accounted for separately. A disposition does not assert a bug is fixed.

## Successor work

| ID                                                                            | FingerTrap owner                                                            | Start condition                                                                                                                                                                                                                     |
| ----------------------------------------------------------------------------- | --------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [CLIENT-FOUNDATION / #216](https://github.com/psmfd/FingerTrap/issues/216)    | Packaged client, completed profiles and supervised JavaScript/Pi SDK worker | Current sequence; [plan](client-foundation-plan.html). Further catalog import/review waits for foundation exit. Existing #201/#202/#186/#187 retain implementation ownership.                                                       |
| [CLIENT-BASELINE / #217](https://github.com/psmfd/FingerTrap/issues/217)      | Item-1 clean Apple Silicon packaged-client baseline                         | Approved execution; input verification PASS and eleven GUI/provider/upgrade cases BLOCKED. Captured, not accepted; [worksheet/evidence](evidence/client-baseline/README.md).                                                        |
| [CLIENT-PI130-COMPARE / #219](https://github.com/psmfd/FingerTrap/issues/219) | Separate Pi 1.3.0 client comparison                                         | Deferred; outside CF-1. Use the accepted production-baseline evidence from #217 and prepare a separate reviewed plan before execution.                                                                                              |
| [IMPORT / #200](https://github.com/psmfd/FingerTrap/issues/200)               | Curated runtime source and regression tests                                 | [M1a imported locally, inactive](m1a-import.md); remaining groups need reviewed dependency/test closures                                                                                                                            |
| [PROFILE / #201](https://github.com/psmfd/FingerTrap/issues/201)              | Isolated managed runtime profile                                            | Before managed activation; design can proceed alongside M1                                                                                                                                                                          |
| [BACKEND / #202](https://github.com/psmfd/FingerTrap/issues/202)              | Pi qualification and SDK evaluation                                         | Qualify one fork revision, then run the ADR-0034 comparison after profile isolation                                                                                                                                                 |
| [LATEST-PI / #210](https://github.com/psmfd/FingerTrap/issues/210)            | Latest stable Pi upgrade and validation, requested 2026-10-21               | Pi 1.3.0 source-integrated and exact-artifact qualified; [evidence](evidence/pi-next/README.md). Remaining example dependency disposition and separate exact release/pin transaction stay open; #216 is the active client sequence. |
| [AUTHORITY / #203](https://github.com/psmfd/FingerTrap/issues/203)            | Native authorization and guarded tool ownership                             | Design first; native approval and FT-3 remain blocked                                                                                                                                                                               |
| [EXPERTISE / #204](https://github.com/psmfd/FingerTrap/issues/204)            | Expertise provider and mutation qualification                               | Preserve evidence now; qualify remote/macOS provider before activation                                                                                                                                                              |
| [WORKFLOW / #205](https://github.com/psmfd/FingerTrap/issues/205)             | pi-workflow integration                                                     | External runtime and publication gates first                                                                                                                                                                                        |
| [RETIRE / #206](https://github.com/psmfd/FingerTrap/issues/206)               | Distribution handoff and pi_config retirement                               | Plan per-target handoff now; archive only after M6 gates                                                                                                                                                                            |
| [DASHBOARDS / #157](https://github.com/psmfd/FingerTrap/issues/157)           | Existing FingerTrap #157                                                    | Existing read-only panel scope; do not duplicate #157                                                                                                                                                                               |
| [INSTALL / #187](https://github.com/psmfd/FingerTrap/issues/187)              | Existing FingerTrap #186/#187                                               | Existing #186/#187; reconcile draft #188 with ADR-0034                                                                                                                                                                              |

`carry`: implement or revalidate in the successor when its component moves.
`blocked`: preserve requirements; the named approval/platform/dependency gate is unmet.
`replace`: preserve useful acceptance criteria but supersede the old implementation/queue.
`defer`: retain the idea without scheduling or making it a migration prerequisite.
`retain-external`: track the dependency here while implementation stays upstream.

## pi_config open issues

### Runtime defects and dispatch restrictions

**carry → [IMPORT / #200](https://github.com/psmfd/FingerTrap/issues/200)** (18 issues). Reproduce and fix when importing the affected component, before enabling it. Treat #1112/#152 as overlapping dispatch validation requirements of #1169; preserve #280's pending patch separately.

[#1169](https://github.com/psmfd/pi_config/issues/1169), [#1151](https://github.com/psmfd/pi_config/issues/1151), [#1148](https://github.com/psmfd/pi_config/issues/1148), [#1142](https://github.com/psmfd/pi_config/issues/1142), [#1141](https://github.com/psmfd/pi_config/issues/1141), [#1128](https://github.com/psmfd/pi_config/issues/1128), [#1112](https://github.com/psmfd/pi_config/issues/1112), [#1103](https://github.com/psmfd/pi_config/issues/1103),
[#1020](https://github.com/psmfd/pi_config/issues/1020), [#1019](https://github.com/psmfd/pi_config/issues/1019), [#1018](https://github.com/psmfd/pi_config/issues/1018), [#977](https://github.com/psmfd/pi_config/issues/977), [#896](https://github.com/psmfd/pi_config/issues/896), [#890](https://github.com/psmfd/pi_config/issues/890), [#862](https://github.com/psmfd/pi_config/issues/862), [#861](https://github.com/psmfd/pi_config/issues/861),
[#280](https://github.com/psmfd/pi_config/issues/280), [#152](https://github.com/psmfd/pi_config/issues/152).

### Hermetic build and regression harness

**carry → [IMPORT / #200](https://github.com/psmfd/FingerTrap/issues/200)** (12 issues). Replace home-cache dependency installation with a committed package manifest/lock. Carry behavioral tests while removing operator settings, model services, hooks, and worktree discovery from test inputs.

[#1137](https://github.com/psmfd/pi_config/issues/1137), [#1099](https://github.com/psmfd/pi_config/issues/1099), [#1097](https://github.com/psmfd/pi_config/issues/1097), [#1091](https://github.com/psmfd/pi_config/issues/1091), [#1090](https://github.com/psmfd/pi_config/issues/1090), [#1042](https://github.com/psmfd/pi_config/issues/1042), [#973](https://github.com/psmfd/pi_config/issues/973), [#957](https://github.com/psmfd/pi_config/issues/957),
[#947](https://github.com/psmfd/pi_config/issues/947), [#905](https://github.com/psmfd/pi_config/issues/905), [#884](https://github.com/psmfd/pi_config/issues/884), [#284](https://github.com/psmfd/pi_config/issues/284).

### Runtime documentation and redistribution review

**carry → [IMPORT / #200](https://github.com/psmfd/FingerTrap/issues/200)** (7 issues). Rewrite relevant contracts for FingerTrap and preserve limitations and notices. Skills require a separate content/rights review; tracked status alone is insufficient permission to publish.

[#1161](https://github.com/psmfd/pi_config/issues/1161), [#1031](https://github.com/psmfd/pi_config/issues/1031), [#991](https://github.com/psmfd/pi_config/issues/991), [#978](https://github.com/psmfd/pi_config/issues/978), [#761](https://github.com/psmfd/pi_config/issues/761), [#508](https://github.com/psmfd/pi_config/issues/508), [#133](https://github.com/psmfd/pi_config/issues/133).

### Current runtime qualification

**carry → [BACKEND / #202](https://github.com/psmfd/FingerTrap/issues/202)** (7 issues). Use #1167 as the current consumer-qualification record. #705/#1138 describe one workaround retirement: verify behavior before removal. Preserve fork/API compatibility checks; SDK adoption remains gated.

[#1167](https://github.com/psmfd/pi_config/issues/1167), [#1138](https://github.com/psmfd/pi_config/issues/1138), [#705](https://github.com/psmfd/pi_config/issues/705), [#955](https://github.com/psmfd/pi_config/issues/955), [#954](https://github.com/psmfd/pi_config/issues/954), [#578](https://github.com/psmfd/pi_config/issues/578), [#577](https://github.com/psmfd/pi_config/issues/577).

### Upstream release notification queue

**replace → [BACKEND / #202](https://github.com/psmfd/FingerTrap/issues/202)** (11 issues). Retain the release inbox, including newer upstream 1.x notices; replace individual bump tasks with one qualified fork/runtime selection. Neither a notification nor an npm version qualifies a FingerTrap runtime.

[#1180](https://github.com/psmfd/pi_config/issues/1180), [#1179](https://github.com/psmfd/pi_config/issues/1179), [#1178](https://github.com/psmfd/pi_config/issues/1178), [#1177](https://github.com/psmfd/pi_config/issues/1177), [#1176](https://github.com/psmfd/pi_config/issues/1176), [#1175](https://github.com/psmfd/pi_config/issues/1175), [#1173](https://github.com/psmfd/pi_config/issues/1173), [#1171](https://github.com/psmfd/pi_config/issues/1171),
[#1164](https://github.com/psmfd/pi_config/issues/1164), [#1163](https://github.com/psmfd/pi_config/issues/1163), [#1051](https://github.com/psmfd/pi_config/issues/1051).

### Managed storage and duplicate discovery

**carry → [PROFILE / #201](https://github.com/psmfd/FingerTrap/issues/201)** (1 issues). Unify profile roots and prevent duplicate global/local extension activation. Cover sidecar, CLI/SDK resources, children, worktrees, sessions, and meters.

[#548](https://github.com/psmfd/pi_config/issues/548).

### Authorization and tool-host design

**blocked → [AUTHORITY / #203](https://github.com/psmfd/FingerTrap/issues/203)** (21 issues). Carry the requirements, with native approval redesign and credential/grant gates. Keep the existing TUI/refusal contract. Source migration does not permit model-controlled confirmation or relabeling an SDK session as TUI.

[#1121](https://github.com/psmfd/pi_config/issues/1121), [#1120](https://github.com/psmfd/pi_config/issues/1120), [#1119](https://github.com/psmfd/pi_config/issues/1119), [#1118](https://github.com/psmfd/pi_config/issues/1118), [#1117](https://github.com/psmfd/pi_config/issues/1117), [#1116](https://github.com/psmfd/pi_config/issues/1116), [#1115](https://github.com/psmfd/pi_config/issues/1115), [#1114](https://github.com/psmfd/pi_config/issues/1114),
[#1113](https://github.com/psmfd/pi_config/issues/1113), [#1111](https://github.com/psmfd/pi_config/issues/1111), [#1032](https://github.com/psmfd/pi_config/issues/1032), [#1024](https://github.com/psmfd/pi_config/issues/1024), [#956](https://github.com/psmfd/pi_config/issues/956), [#950](https://github.com/psmfd/pi_config/issues/950), [#946](https://github.com/psmfd/pi_config/issues/946), [#945](https://github.com/psmfd/pi_config/issues/945),
[#919](https://github.com/psmfd/pi_config/issues/919), [#918](https://github.com/psmfd/pi_config/issues/918), [#917](https://github.com/psmfd/pi_config/issues/917), [#915](https://github.com/psmfd/pi_config/issues/915), [#913](https://github.com/psmfd/pi_config/issues/913).

### Confinement and tool-result hardening

**blocked → [AUTHORITY / #203](https://github.com/psmfd/FingerTrap/issues/203)** (9 issues). Preserve existing defenses; qualify platform-specific replacements and exact tool ownership. Linux confinement evidence does not qualify Apple Silicon macOS.

[#1122](https://github.com/psmfd/pi_config/issues/1122), [#1060](https://github.com/psmfd/pi_config/issues/1060), [#709](https://github.com/psmfd/pi_config/issues/709), [#708](https://github.com/psmfd/pi_config/issues/708), [#707](https://github.com/psmfd/pi_config/issues/707), [#507](https://github.com/psmfd/pi_config/issues/507), [#505](https://github.com/psmfd/pi_config/issues/505), [#457](https://github.com/psmfd/pi_config/issues/457),
[#456](https://github.com/psmfd/pi_config/issues/456).

### Expertise readiness and reproducibility

**blocked → [EXPERTISE / #204](https://github.com/psmfd/FingerTrap/issues/204)** (14 issues). Preserve failing evidence and installer/approval tests. Reassess under the supported platform and API contract; do not import diagnostic artifacts as a ready local provider.

[#1162](https://github.com/psmfd/pi_config/issues/1162), [#1160](https://github.com/psmfd/pi_config/issues/1160), [#1136](https://github.com/psmfd/pi_config/issues/1136), [#1135](https://github.com/psmfd/pi_config/issues/1135), [#1134](https://github.com/psmfd/pi_config/issues/1134), [#1132](https://github.com/psmfd/pi_config/issues/1132), [#1131](https://github.com/psmfd/pi_config/issues/1131), [#1108](https://github.com/psmfd/pi_config/issues/1108),
[#1107](https://github.com/psmfd/pi_config/issues/1107), [#1106](https://github.com/psmfd/pi_config/issues/1106), [#693](https://github.com/psmfd/pi_config/issues/693), [#654](https://github.com/psmfd/pi_config/issues/654), [#621](https://github.com/psmfd/pi_config/issues/621), [#604](https://github.com/psmfd/pi_config/issues/604).

### Workflow and review integration

**blocked → [WORKFLOW / #205](https://github.com/psmfd/FingerTrap/issues/205)** (1 issues). Consume the separately owned pi-workflow project after its scope, executor, commands, review definitions, and publication gates are met.

[#782](https://github.com/psmfd/pi_config/issues/782).

### Publishing and release ownership

**carry → [RETIRE / #206](https://github.com/psmfd/FingerTrap/issues/206)** (19 issues). Consolidate release and mirror obligations under FingerTrap. Preserve existing consumers until each destination has an owner, verified package closure, and migration path. Proposed new mirrors require an explicit need.

[#1023](https://github.com/psmfd/pi_config/issues/1023), [#968](https://github.com/psmfd/pi_config/issues/968), [#920](https://github.com/psmfd/pi_config/issues/920), [#910](https://github.com/psmfd/pi_config/issues/910), [#860](https://github.com/psmfd/pi_config/issues/860), [#836](https://github.com/psmfd/pi_config/issues/836), [#737](https://github.com/psmfd/pi_config/issues/737), [#717](https://github.com/psmfd/pi_config/issues/717),
[#646](https://github.com/psmfd/pi_config/issues/646), [#634](https://github.com/psmfd/pi_config/issues/634), [#626](https://github.com/psmfd/pi_config/issues/626), [#522](https://github.com/psmfd/pi_config/issues/522), [#474](https://github.com/psmfd/pi_config/issues/474), [#473](https://github.com/psmfd/pi_config/issues/473), [#469](https://github.com/psmfd/pi_config/issues/469), [#444](https://github.com/psmfd/pi_config/issues/444),
[#157](https://github.com/psmfd/pi_config/issues/157), [#156](https://github.com/psmfd/pi_config/issues/156), [#99](https://github.com/psmfd/pi_config/issues/99).

### Legacy installer implementation

**replace → [INSTALL / #187](https://github.com/psmfd/FingerTrap/issues/187)** (5 issues). Carry useful fetch/verification failure cases into #187; replace setup/update scripts with the managed installation contract instead of importing a second updater.

[#1165](https://github.com/psmfd/pi_config/issues/1165), [#628](https://github.com/psmfd/pi_config/issues/628), [#197](https://github.com/psmfd/pi_config/issues/197), [#166](https://github.com/psmfd/pi_config/issues/166), [#109](https://github.com/psmfd/pi_config/issues/109).

### Superseded planning/layout maintenance

**replace → [RETIRE / #206](https://github.com/psmfd/FingerTrap/issues/206)** (5 issues). ADR-0034 and this ledger become the plan of record. Preserve historical ADRs privately; old layout/frontmatter corrections do not gate migration.

[#969](https://github.com/psmfd/pi_config/issues/969), [#835](https://github.com/psmfd/pi_config/issues/835), [#662](https://github.com/psmfd/pi_config/issues/662), [#214](https://github.com/psmfd/pi_config/issues/214), [#212](https://github.com/psmfd/pi_config/issues/212).

### Wrapper and model qualification

**defer → [IMPORT / #200](https://github.com/psmfd/FingerTrap/issues/200)** (18 issues). Retain the evaluation backlog. Import only selected wrappers after their relevant quality and injection tests pass; do not activate all inherited agents or add a new expert by default.

[#1087](https://github.com/psmfd/pi_config/issues/1087), [#1086](https://github.com/psmfd/pi_config/issues/1086), [#1085](https://github.com/psmfd/pi_config/issues/1085), [#1084](https://github.com/psmfd/pi_config/issues/1084), [#1083](https://github.com/psmfd/pi_config/issues/1083), [#1082](https://github.com/psmfd/pi_config/issues/1082), [#1081](https://github.com/psmfd/pi_config/issues/1081), [#1080](https://github.com/psmfd/pi_config/issues/1080),
[#1079](https://github.com/psmfd/pi_config/issues/1079), [#1078](https://github.com/psmfd/pi_config/issues/1078), [#1077](https://github.com/psmfd/pi_config/issues/1077), [#1076](https://github.com/psmfd/pi_config/issues/1076), [#1075](https://github.com/psmfd/pi_config/issues/1075), [#1074](https://github.com/psmfd/pi_config/issues/1074), [#1073](https://github.com/psmfd/pi_config/issues/1073), [#1066](https://github.com/psmfd/pi_config/issues/1066),
[#1062](https://github.com/psmfd/pi_config/issues/1062), [#530](https://github.com/psmfd/pi_config/issues/530).

### Routing and context experiments

**defer → [IMPORT / #200](https://github.com/psmfd/FingerTrap/issues/200)** (26 issues). Keep existing behavior during relocation. Schedule new tuning/features separately; existing readiness defects remain gates and are not waived by this deferral.

[#1144](https://github.com/psmfd/pi_config/issues/1144), [#1140](https://github.com/psmfd/pi_config/issues/1140), [#1089](https://github.com/psmfd/pi_config/issues/1089), [#1030](https://github.com/psmfd/pi_config/issues/1030), [#908](https://github.com/psmfd/pi_config/issues/908), [#904](https://github.com/psmfd/pi_config/issues/904), [#900](https://github.com/psmfd/pi_config/issues/900), [#889](https://github.com/psmfd/pi_config/issues/889),
[#870](https://github.com/psmfd/pi_config/issues/870), [#869](https://github.com/psmfd/pi_config/issues/869), [#865](https://github.com/psmfd/pi_config/issues/865), [#844](https://github.com/psmfd/pi_config/issues/844), [#839](https://github.com/psmfd/pi_config/issues/839), [#775](https://github.com/psmfd/pi_config/issues/775), [#772](https://github.com/psmfd/pi_config/issues/772), [#584](https://github.com/psmfd/pi_config/issues/584),
[#546](https://github.com/psmfd/pi_config/issues/546), [#541](https://github.com/psmfd/pi_config/issues/541), [#443](https://github.com/psmfd/pi_config/issues/443), [#442](https://github.com/psmfd/pi_config/issues/442), [#366](https://github.com/psmfd/pi_config/issues/366), [#365](https://github.com/psmfd/pi_config/issues/365), [#354](https://github.com/psmfd/pi_config/issues/354), [#344](https://github.com/psmfd/pi_config/issues/344),
[#335](https://github.com/psmfd/pi_config/issues/335), [#226](https://github.com/psmfd/pi_config/issues/226).

### Optional observability and session features

**defer → [DASHBOARDS / #157](https://github.com/psmfd/FingerTrap/issues/157)** (8 issues). Retain ideas for later native work; #157 still delivers read-only existing meter schemas. Review reports and handoff features are separate implementation slices.

[#1034](https://github.com/psmfd/pi_config/issues/1034), [#773](https://github.com/psmfd/pi_config/issues/773), [#674](https://github.com/psmfd/pi_config/issues/674), [#293](https://github.com/psmfd/pi_config/issues/293), [#292](https://github.com/psmfd/pi_config/issues/292), [#291](https://github.com/psmfd/pi_config/issues/291), [#290](https://github.com/psmfd/pi_config/issues/290), [#289](https://github.com/psmfd/pi_config/issues/289).

### Additional runtime capabilities

**defer → [IMPORT / #200](https://github.com/psmfd/FingerTrap/issues/200)** (6 issues). Retain scoped follow-ups without making them prerequisites for moving existing source. Reassess demand and contracts before implementation.

[#780](https://github.com/psmfd/pi_config/issues/780), [#675](https://github.com/psmfd/pi_config/issues/675), [#882](https://github.com/psmfd/pi_config/issues/882), [#238](https://github.com/psmfd/pi_config/issues/238), [#223](https://github.com/psmfd/pi_config/issues/223), [#96](https://github.com/psmfd/pi_config/issues/96).

### External Pi/provider proposals

**retain-external → [BACKEND / #202](https://github.com/psmfd/FingerTrap/issues/202)** (8 issues). Track integrations here; core fork changes and provider fixes stay with their canonical owners. Verify upstream status when activated rather than treating old proposals as delivered.

[#1001](https://github.com/psmfd/pi_config/issues/1001), [#982](https://github.com/psmfd/pi_config/issues/982), [#944](https://github.com/psmfd/pi_config/issues/944), [#892](https://github.com/psmfd/pi_config/issues/892), [#678](https://github.com/psmfd/pi_config/issues/678), [#368](https://github.com/psmfd/pi_config/issues/368), [#211](https://github.com/psmfd/pi_config/issues/211), [#210](https://github.com/psmfd/pi_config/issues/210).

### Alternative distribution research

**defer → [RETIRE / #206](https://github.com/psmfd/FingerTrap/issues/206)** (3 issues). Keep research references; OCI/WSL/hosted sandbox delivery is outside the current Apple Silicon release target.

[#194](https://github.com/psmfd/pi_config/issues/194), [#139](https://github.com/psmfd/pi_config/issues/139), [#130](https://github.com/psmfd/pi_config/issues/130).

## Pending changes and evidence

The PR heads below are alternatives to the source baseline, never implicit
members of the import. Full head/base IDs are in `source-manifest.json`.

| Input                                                                       | State at snapshot                                                | Required disposition                                                                                                                                                                                                                                           |
| --------------------------------------------------------------------------- | ---------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [pi_config PR #1170](https://github.com/psmfd/pi_config/pull/1170)          | Draft; head `090d3eea4af3f59bdd768e73bcb2ebdae25468a7`; 16 files | IMPORT carries the tool-declaration/shadowing fix and patch-audit correction; BACKEND owns the coupled version bump. Full consumer validation remains failed. Reapply and test the fix independently; do not merge the whole branch as qualification evidence. |
| [pi_config PR #1172](https://github.com/psmfd/pi_config/pull/1172)          | Open; head `ea85cda4a23b2425eee04b5eaf0fd55558b1ead4`; 6 files   | BACKEND compares duplicate pin/checksum changes with #1170; one qualified version selection and one set of pins.                                                                                                                                               |
| [pi_config PR #1174](https://github.com/psmfd/pi_config/pull/1174)          | Draft; head `0b2170cf1f74ce395b113da981d38412f5bdc425`; 93 files | EXPERTISE preserves diagnostic and reproducibility work. Separate the useful harness from generated/private evidence and unrelated file churn; no production readiness claim.                                                                                  |
| Local `LEGACY_pi_config_3579dc1.patch`                                      | Untracked evidence; SHA-256 in manifest                          | IMPORT reconciles #280 against the current serial subagent code, preserves error/abort behavior and updates provenance. No blind cherry-pick.                                                                                                                  |
| Dirty `pi_config/notes/session-handoff.md`, local handoffs/review artifacts | Working-tree/private evidence                                    | Preserve in place. They are excluded from immutable-source exports and public documentation imports.                                                                                                                                                           |

## Public config distribution

All five open [pi-config](https://github.com/psmfd/pi-config/issues) issues are
requests on a generated distribution, not a second source of truth.

| Source                                                                                               | Successor                                                          | Disposition                                                                                                                      |
| ---------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------ | -------------------------------------------------------------------------------------------------------------------------------- |
| [#1](https://github.com/psmfd/pi-config/issues/1)                                                    | [AUTHORITY / #203](https://github.com/psmfd/FingerTrap/issues/203) | Carry idempotent work-item creation into the typed client/credential design.                                                     |
| [#2](https://github.com/psmfd/pi-config/issues/2), [#3](https://github.com/psmfd/pi-config/issues/3) | [IMPORT / #200](https://github.com/psmfd/FingerTrap/issues/200)    | Defer Lima endpoint/payload configuration until that provider is selected; no ambient operator configuration import.             |
| [#4](https://github.com/psmfd/pi-config/issues/4), [#5](https://github.com/psmfd/pi-config/issues/5) | [EXPERTISE / #204](https://github.com/psmfd/FingerTrap/issues/204) | Carry local-provider and synchronization requirements; do not equate existing Linux experiments with a supported macOS provider. |

## Extension mirrors

All 12 source mirrors move to RETIRE for publishing decisions and BACKEND for
the coordinated Pi compatibility bump. Dependency PRs remain with the consuming
package until it is retired; this ledger does not waive dependency/security fixes.
Each mirror's snapshot commit, provenance blob, and source commit is recorded in
`source-manifest.json`. Different provenance SHAs reflect different component
updates; they are not independent source owners.

| Repository                                                                      | Open issue disposition                                                                                                    | Open PRs at snapshot                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                          |
| ------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [pi-artifact-handoff](https://github.com/psmfd/pi-artifact-handoff)             | None                                                                                                                      | [#8](https://github.com/psmfd/pi-artifact-handoff/pull/8), [#18](https://github.com/psmfd/pi-artifact-handoff/pull/18), [#20](https://github.com/psmfd/pi-artifact-handoff/pull/20), [#42](https://github.com/psmfd/pi-artifact-handoff/pull/42), [#45](https://github.com/psmfd/pi-artifact-handoff/pull/45), [#46](https://github.com/psmfd/pi-artifact-handoff/pull/46), [#47](https://github.com/psmfd/pi-artifact-handoff/pull/47), [#48](https://github.com/psmfd/pi-artifact-handoff/pull/48)                                                          |
| [pi-auto-router](https://github.com/psmfd/pi-auto-router)                       | None                                                                                                                      | [#1](https://github.com/psmfd/pi-auto-router/pull/1), [#11](https://github.com/psmfd/pi-auto-router/pull/11), [#15](https://github.com/psmfd/pi-auto-router/pull/15), [#25](https://github.com/psmfd/pi-auto-router/pull/25), [#26](https://github.com/psmfd/pi-auto-router/pull/26), [#27](https://github.com/psmfd/pi-auto-router/pull/27), [#28](https://github.com/psmfd/pi-auto-router/pull/28), [#29](https://github.com/psmfd/pi-auto-router/pull/29)                                                                                                  |
| [pi-bash-destructive-guard](https://github.com/psmfd/pi-bash-destructive-guard) | None                                                                                                                      | [#5](https://github.com/psmfd/pi-bash-destructive-guard/pull/5), [#16](https://github.com/psmfd/pi-bash-destructive-guard/pull/16), [#18](https://github.com/psmfd/pi-bash-destructive-guard/pull/18), [#37](https://github.com/psmfd/pi-bash-destructive-guard/pull/37), [#38](https://github.com/psmfd/pi-bash-destructive-guard/pull/38), [#39](https://github.com/psmfd/pi-bash-destructive-guard/pull/39), [#40](https://github.com/psmfd/pi-bash-destructive-guard/pull/40), [#41](https://github.com/psmfd/pi-bash-destructive-guard/pull/41)          |
| [pi-cache-meter](https://github.com/psmfd/pi-cache-meter)                       | None                                                                                                                      | [#7](https://github.com/psmfd/pi-cache-meter/pull/7), [#14](https://github.com/psmfd/pi-cache-meter/pull/14), [#16](https://github.com/psmfd/pi-cache-meter/pull/16), [#33](https://github.com/psmfd/pi-cache-meter/pull/33), [#34](https://github.com/psmfd/pi-cache-meter/pull/34), [#35](https://github.com/psmfd/pi-cache-meter/pull/35), [#36](https://github.com/psmfd/pi-cache-meter/pull/36), [#37](https://github.com/psmfd/pi-cache-meter/pull/37)                                                                                                  |
| [pi-compaction-optimizer](https://github.com/psmfd/pi-compaction-optimizer)     | None                                                                                                                      | [#1](https://github.com/psmfd/pi-compaction-optimizer/pull/1), [#6](https://github.com/psmfd/pi-compaction-optimizer/pull/6), [#8](https://github.com/psmfd/pi-compaction-optimizer/pull/8), [#25](https://github.com/psmfd/pi-compaction-optimizer/pull/25), [#26](https://github.com/psmfd/pi-compaction-optimizer/pull/26), [#27](https://github.com/psmfd/pi-compaction-optimizer/pull/27), [#28](https://github.com/psmfd/pi-compaction-optimizer/pull/28), [#29](https://github.com/psmfd/pi-compaction-optimizer/pull/29)                              |
| [pi-context-manager](https://github.com/psmfd/pi-context-manager)               | None                                                                                                                      | [#9](https://github.com/psmfd/pi-context-manager/pull/9), [#11](https://github.com/psmfd/pi-context-manager/pull/11), [#28](https://github.com/psmfd/pi-context-manager/pull/28), [#29](https://github.com/psmfd/pi-context-manager/pull/29), [#30](https://github.com/psmfd/pi-context-manager/pull/30), [#31](https://github.com/psmfd/pi-context-manager/pull/31), [#32](https://github.com/psmfd/pi-context-manager/pull/32)                                                                                                                              |
| [pi-expertise-client](https://github.com/psmfd/pi-expertise-client)             | None                                                                                                                      | [#1](https://github.com/psmfd/pi-expertise-client/pull/1), [#5](https://github.com/psmfd/pi-expertise-client/pull/5), [#8](https://github.com/psmfd/pi-expertise-client/pull/8), [#29](https://github.com/psmfd/pi-expertise-client/pull/29), [#30](https://github.com/psmfd/pi-expertise-client/pull/30), [#31](https://github.com/psmfd/pi-expertise-client/pull/31), [#32](https://github.com/psmfd/pi-expertise-client/pull/32), [#33](https://github.com/psmfd/pi-expertise-client/pull/33), [#34](https://github.com/psmfd/pi-expertise-client/pull/34) |
| [pi-gh-identity-guard](https://github.com/psmfd/pi-gh-identity-guard)           | [#10](https://github.com/psmfd/pi-gh-identity-guard/issues/10) → PROFILE: untracked per-repository identity configuration | [#1](https://github.com/psmfd/pi-gh-identity-guard/pull/1), [#6](https://github.com/psmfd/pi-gh-identity-guard/pull/6), [#8](https://github.com/psmfd/pi-gh-identity-guard/pull/8), [#26](https://github.com/psmfd/pi-gh-identity-guard/pull/26), [#27](https://github.com/psmfd/pi-gh-identity-guard/pull/27), [#28](https://github.com/psmfd/pi-gh-identity-guard/pull/28), [#29](https://github.com/psmfd/pi-gh-identity-guard/pull/29), [#30](https://github.com/psmfd/pi-gh-identity-guard/pull/30)                                                      |
| [pi-indexing](https://github.com/psmfd/pi-indexing)                             | None                                                                                                                      | [#1](https://github.com/psmfd/pi-indexing/pull/1), [#11](https://github.com/psmfd/pi-indexing/pull/11), [#13](https://github.com/psmfd/pi-indexing/pull/13), [#27](https://github.com/psmfd/pi-indexing/pull/27), [#28](https://github.com/psmfd/pi-indexing/pull/28), [#29](https://github.com/psmfd/pi-indexing/pull/29), [#30](https://github.com/psmfd/pi-indexing/pull/30), [#31](https://github.com/psmfd/pi-indexing/pull/31)                                                                                                                          |
| [pi-secrets-guard](https://github.com/psmfd/pi-secrets-guard)                   | None                                                                                                                      | [#5](https://github.com/psmfd/pi-secrets-guard/pull/5), [#14](https://github.com/psmfd/pi-secrets-guard/pull/14), [#16](https://github.com/psmfd/pi-secrets-guard/pull/16), [#35](https://github.com/psmfd/pi-secrets-guard/pull/35), [#36](https://github.com/psmfd/pi-secrets-guard/pull/36), [#37](https://github.com/psmfd/pi-secrets-guard/pull/37), [#38](https://github.com/psmfd/pi-secrets-guard/pull/38), [#39](https://github.com/psmfd/pi-secrets-guard/pull/39)                                                                                  |
| [pi-token-meter](https://github.com/psmfd/pi-token-meter)                       | None                                                                                                                      | [#1](https://github.com/psmfd/pi-token-meter/pull/1), [#8](https://github.com/psmfd/pi-token-meter/pull/8), [#9](https://github.com/psmfd/pi-token-meter/pull/9), [#24](https://github.com/psmfd/pi-token-meter/pull/24), [#25](https://github.com/psmfd/pi-token-meter/pull/25), [#26](https://github.com/psmfd/pi-token-meter/pull/26), [#27](https://github.com/psmfd/pi-token-meter/pull/27), [#28](https://github.com/psmfd/pi-token-meter/pull/28)                                                                                                      |
| [pi-web-fetch](https://github.com/psmfd/pi-web-fetch)                           | None                                                                                                                      | [#8](https://github.com/psmfd/pi-web-fetch/pull/8), [#17](https://github.com/psmfd/pi-web-fetch/pull/17), [#19](https://github.com/psmfd/pi-web-fetch/pull/19), [#41](https://github.com/psmfd/pi-web-fetch/pull/41), [#42](https://github.com/psmfd/pi-web-fetch/pull/42), [#43](https://github.com/psmfd/pi-web-fetch/pull/43), [#44](https://github.com/psmfd/pi-web-fetch/pull/44), [#45](https://github.com/psmfd/pi-web-fetch/pull/45)                                                                                                                  |

## Separately owned dependencies

| Source | Disposition |
| --- | --- | --- |
| pi-workflow [#1](https://github.com/psmfd/pi-workflow/issues/1), [#2](https://github.com/psmfd/pi-workflow/issues/2), [#3](https://github.com/psmfd/pi-workflow/issues/3), [#4](https://github.com/psmfd/pi-workflow/issues/4), [#5](https://github.com/psmfd/pi-workflow/issues/5) | Retain with pi-workflow; reconcile older umbrella statuses against the implemented foundation before closure. Integration wording changes from pi_config to FingerTrap. |
| pi-workflow [#12](https://github.com/psmfd/pi-workflow/issues/12), [#13](https://github.com/psmfd/pi-workflow/issues/13), [#14](https://github.com/psmfd/pi-workflow/issues/14), [#15](https://github.com/psmfd/pi-workflow/issues/15), [#16](https://github.com/psmfd/pi-workflow/issues/16), [#17](https://github.com/psmfd/pi-workflow/issues/17), [#18](https://github.com/psmfd/pi-workflow/issues/18), [#19](https://github.com/psmfd/pi-workflow/issues/19), [#25](https://github.com/psmfd/pi-workflow/issues/25), [#35](https://github.com/psmfd/pi-workflow/issues/35) | Retain external scope/executor/commands/review/acceptance/publication work. WORKFLOW owns FingerTrap consumption; no usable review extension is claimed yet. |
| pi-workflow [#24](https://github.com/psmfd/pi-workflow/issues/24) | Unrelated test issue; retain there, no migration action. |
| pi-workflow PRs [#44](https://github.com/psmfd/pi-workflow/pull/44), [#45](https://github.com/psmfd/pi-workflow/pull/45), [#46](https://github.com/psmfd/pi-workflow/pull/46), [#47](https://github.com/psmfd/pi-workflow/pull/47), [#48](https://github.com/psmfd/pi-workflow/pull/48) | Dependency maintenance stays with the canonical package. |
| agent-expertise-api [#528](https://github.com/psmfd/agent-expertise-api/issues/528), [#342](https://github.com/psmfd/agent-expertise-api/issues/342) | Retain external bootstrap/export and downsync requirements; EXPERTISE tracks them as dependencies. This is not an audit of the entire API backlog. |
| Pi core/fork | Keep source and releases in psmfd/pi. BACKEND tracks required patches and exact consumer qualification; new upstream notices do not change the selected runtime. |

## Existing FingerTrap work

Reuse [#186](https://github.com/psmfd/FingerTrap/issues/186) and
[#187](https://github.com/psmfd/FingerTrap/issues/187), reconciling draft
[#188](https://github.com/psmfd/FingerTrap/pull/188) before installer implementation.
Reuse [#157](https://github.com/psmfd/FingerTrap/issues/157) for dashboards and
[#158](https://github.com/psmfd/FingerTrap/issues/158) for the expertise panel.
[#151](https://github.com/psmfd/FingerTrap/issues/151) stays behind ADR-0028's
bounded metadata-only contract and the SDK evaluation; an SDK API is not itself
proof of side-effect-free browsing. [#120](https://github.com/psmfd/FingerTrap/issues/120)
is an older FT-2 tracker: its open state does not undo the delivered control scope.

The other open FingerTrap issues and dependency PRs retain their existing owners
and Native/release/security scope. Transcript Markdown rendering is still
unimplemented and needs a separately scoped UI issue when scheduled. Migration
does not complete packaging/signing, N-1, VM-host dependencies, or FT-3.

## Handoff and closure rule

New product work starts in FingerTrap. Before closing a source issue, link the
successor and record whether its behavior was implemented, superseded, or
explicitly declined; do not close a failing test merely because source moved.
Before disabling a mirror's source sync, establish its replacement publisher
or retirement instructions. Archive pi_config only after the ADR-0034 M6
build/test/release/recovery and consumer-migration gates pass.
