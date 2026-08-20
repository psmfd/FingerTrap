# pi `--mode rpc` contract study (FT-2 gate)

**Verified against:** pi tag `v0.84.2-psmfd.1` (commit `6bdcb3089026`, the version
pinned by `pi_config`) — re-verified at this bump by the golden re-record diff
(the pin-bump ritual below, its first live exercise) — originally built by reading
`packages/coding-agent/src/modes/rpc/{rpc-types,rpc-mode,rpc-client,jsonl}.ts`,
the type chain into `pi-agent-core`/`pi-ai`, and the RPC test suite (including
the 5868 unknown-command-id regression). This note satisfies the FT-2 gate in
`docs/milestones.md`: it enumerates the methods and events FT-2 consumes and
files the gaps as issues (listed at the end). Re-verify against the source on
every pi pin bump — event-name drift across pi versions is a previously-hit bug
class (pi_config's subagent extension once listened for a `tool_result_end`
event that pi 0.80.2 no longer emitted). Since `v0.84.2-psmfd.1` the protocol
HAS a version handshake: the `hello` first line (psmfd/pi#56, psmfd-patch-010;
see Wire framing).

A caution for future readers of the pi source: `packages/protocol` +
`packages/server` (`pi-server`) is an experimental, unused-by-the-CLI
multi-session daemon protocol (CBOR over Unix sockets). It is **not** what
`pi --mode rpc` speaks. Everything below is the JSONL stdin/stdout protocol in
`packages/coding-agent/src/modes/rpc/`.

## Pin-bump ritual (record–replay goldens, #139)

This study is now backed by committed wire transcripts —
`src-sidecar/tests/FingerTrap.Sidecar.Tests/Goldens/data/*.golden.jsonl` —
recorded from the real pinned pi and replayed keylessly through
`PiRpcClient` on every PR (`GoldenReplayTests`). On a pin bump, do **not**
re-read the pi source as the first move. Instead:

1. Install the new pi on PATH, then re-record:
   `FT_RECORD_GOLDENS=1 dotnet test src-sidecar --filter-method "*Record_Scenario*"`
   (self-skips without the env var, without pi on PATH, and on Windows; each
   scenario records twice and must be byte-identical before it writes — see
   CONTRIBUTING for details).
2. **The working-tree diff of `Goldens/data/` IS the drift report.** An
   empty diff means the wire behaviors this suite covers are unchanged. A
   non-empty diff enumerates exactly what moved.
3. Update this study from that diff, then commit the new goldens with the
   pin bump. Go source-diving only for changes the diff surfaces but does
   not explain.

The recorder serves model turns from a local canned OpenAI-completions
endpoint (temp HOME + scenario-owned `models.json`), so recordings are
keyless, token-free, and deterministic; ids/timestamps/paths are tokenized
(`@UUID:1@`, `@TS:1@`, `@CWD:main@`, …). One scenario
(`malformed-line-parse-error`) commits fully pinned — no volatile values —
to catch response-prose drift cheaply.

## Wire framing

**Hello (first stdout line, since `v0.84.2-psmfd.1` / psmfd-patch-010):**
`{"type":"hello","piVersion":"0.84.2","protocol":1,"capabilities":[
"extension_ui","queue_modes","fork","get_commands","list_sessions"]}` —
emitted before extension binding and before the stdin reader attaches. It is
the ready gate (no more sleep-and-probe; "died before hello" is a
distinguishable spawn-failure class) and the capability-discovery surface
(additions are advertised, never probed, and never bump `protocol`).
`piVersion` carries the upstream base version (`0.84.2`), not the psmfd tag.
Golden note: hello is an event-class line, so window canonicalization hoists
an id-correlated response above it inside the same inbound window — per the
canonical form, not wire order (on the wire hello is always first).
Supervisor note: `PiRpcClient` consumes hello as ready-gate/capability state
(`WaitForHelloAsync`/`Supports`) and deliberately never publishes it to the
`Events` relay, mirroring the reference client — do not "fix" the filter;
surfacing hello to the UI is a deliberate accessor (FT#150). The wire tap
still records it, which is why the goldens carry it.

- **Strict LF-only JSONL in both directions.** One JSON object per `\n`-terminated
  line. Do **not** split on other Unicode separators: U+2028/U+2029 are legal
  inside JSON string payloads and pi deliberately avoids Node `readline` for
  this reason. A trailing `\r` is stripped per line on read (CRLF-tolerant in,
  LF-only out). On stream end, an unterminated final buffer is flushed as a
  line.
- **Correlation:** every inbound command carries an optional `id?: string`,
  echoed verbatim on the matching response. The reference client generates
  `req_<n>` ids and keys a pending-request map on them, with a 30 s per-request
  timeout. Events carry no `id`.
- **Demux rule** (from the reference client): a line with `type: "response"`
  and a known pending `id` resolves that request; every other parseable line is
  an event. Unparseable lines from the child are ignored client-side.
  Verified against `rpc-client.ts` (slice-1 review): this two-way split is
  literal — a response whose `id` is unknown, already timed out, or a
  duplicate of one already resolved falls through to the **event
  listeners**, not a drop path. A conforming supervisor mirrors that
  fall-through rather than inventing a third demux bucket.
- **Error shape:** uniform —
  `{ id?, type: "response", command: string, success: false, error: string }`.
  `error` is a plain string, no structured code; preserve `command` + `error`
  together on the .NET side. A malformed inbound line yields
  `command: "parse"` with `id: undefined` and does **not** crash the process
  — on the wire the `id` key is **absent** (`JSON.stringify` drops
  `undefined`), never JSON `null`; deserialize accordingly.
  Unknown command types return `Unknown command: <type>` with the request `id`
  preserved (the 5868 regression locks this in).
- **stdout is protocol-only, guaranteed:** RPC mode reroutes any stray
  `process.stdout.write` (errant `console.log` in extensions or deps) to
  stderr; only the RPC writer touches real stdout. All diagnostics land on
  stderr — capture it for error enrichment, never parse it. pi does **not**
  bound its own stderr volume (every stray extension `console.log` lands
  there), so the supervisor must bound its capture — only the tail is ever
  useful for enrichment. Output writes are
  backpressure-aware server-side, so a slow-reading supervisor throttles the
  child rather than ballooning it.

## Command inventory

All commands are `{ id?, type, ...params }` with exactly one response, except
`prompt` (see semantics below). Commands FT-2 consumes are marked ●; the rest
are listed so the enumeration is complete against the pin.

| Command | Params → response data | FT-2 |
|---|---|---|
| `prompt` | `message`, `images?`, `streamingBehavior?: "steer"\|"followUp"` → ack only; work streams as events | ● |
| `steer` | `message`, `images?` → none (interrupt current turn) | ● |
| `follow_up` | `message`, `images?` → none (queue after current turn) | ● |
| `abort` | → none | ● |
| `get_state` | → `RpcSessionState` (model, thinkingLevel, isStreaming, isCompacting, steering/followUp modes, sessionFile, sessionId, sessionName, autoCompactionEnabled, messageCount, pendingMessageCount) | ● |
| `get_session_stats` | → `SessionStats` (per-role message counts, toolCalls, `tokens{input,output,cacheRead,cacheWrite,total}`, `cost`, `contextUsage?{tokens,contextWindow,percent}`) | ● |
| `list_sessions` | `cwd?`, `all?` → `{ sessions: [{path, id, cwd, name?, parentSessionPath?, created, modified, messageCount, firstMessage}] }` — header fields only, `allMessagesText` deliberately off the wire (since `v0.84.2-psmfd.1`, psmfd-patch-011; golden `list-sessions`) | ● |
| `get_available_models` | → `{ models: Model[] }` (contextWindow, maxTokens, per-token cost, supportedThinkingLevels, authenticated) | ● |
| `set_model` | `provider`, `modelId` → `Model` | ● |
| `cycle_model` / `set_thinking_level` / `cycle_thinking_level` / `get_available_thinking_levels` | model/thinking controls | ● |
| `new_session` | `parentSession?` → `{ cancelled }`; rebinds on success | ● |
| `switch_session` | `sessionPath` → `{ cancelled }`; rebinds on success | ● |
| `fork` | `entryId` → `{ text, cancelled }` | |
| `clone` | → `{ cancelled }` | |
| `get_fork_messages` / `get_entries` / `get_tree` / `get_messages` / `get_last_assistant_text` | session-content reads (current session only) | ● |
| `set_session_name` | `name` → none (errors on blank) | ● |
| `get_commands` | → `{ commands: RpcSlashCommand[] }` — extension commands, prompt templates, skills (`skill:` prefix) | ● |
| `bash` / `abort_bash` | host-initiated bash in session context | |
| `compact` / `set_auto_compaction` / `set_auto_retry` / `abort_retry` | compaction/retry controls | |
| `set_steering_mode` / `set_follow_up_mode` | `"all"` \| `"one-at-a-time"` queue policy | ● |
| `export_html` | `outputPath?` → `{ path }` | |

`extension_ui_response` (`{ type, id, value | confirmed | cancelled }`) is a
special stdin message, not a command: it answers a pending
`extension_ui_request`, with the payload key discriminating the shape
(`value: string` for select/input/editor, `confirmed: boolean` for confirm,
`cancelled: true` for any dialog kind). `id` is the request event's own
pi-assigned id, echoed back verbatim. pi emits **no response frame** for it —
a sender must not await one — and a response for an unknown/expired id is
silently dropped. That silent drop is also the accepted-gap answer for the
race where a host answers after pi's own per-request `timeout` already
self-resolved the dialog: the late answer vanishes with no error anywhere
(same class as the no-heartbeat gap).

## Event inventory

Events are the `AgentSessionEvent` union, written as-is except `message_update`,
which is rewritten by `json-event.ts` before it hits the wire (verified for the
slice-3 transcript): **both** the top-level cumulative `message` field **and**
the nested `assistantMessageEvent.partial` snapshot are dropped — only
`{ type: "message_update", assistantMessageEvent: { type, contentIndex?,
delta? | content? | toolCall? } }` survives. A client that wants a live
partial message must assemble it itself, keyed by `contentIndex`, seeded by
`message_start` and replaced by the authoritative `message_end`.

- **Lifecycle:** `agent_start` → per turn: `turn_start`,
  `message_start`/`message_update`*/`message_end`,
  `tool_execution_start`/`_update`/`_end`, `turn_end` → `agent_end`
  (`messages`, `willRetry`) → **`agent_settled`**. Wire trap (slice-3
  verification): `turn_start` is `{ type }` and `turn_end` is
  `{ type, message, toolResults }` — **no `turnIndex`/`timestamp` on the
  wire**. Those fields exist only on the extension-hook mirror types in
  `extensions/types.ts`, a different consumer entirely.
- **User messages are always echoed.** Every user-role message — the initial
  `prompt`, and each queued `steer`/`follow_up` when it is finally
  delivered — arrives as `message_start` + `message_end` (identical
  `role: "user"` message objects) through the normal event stream, before
  any assistant streaming. A host transcript renders user messages from
  that echo, never from a local optimistic append: the echo is the single
  source of truth for ordering, and a queued follow-up must not appear in
  the transcript until pi actually delivers it (`queue_update` covers the
  waiting state).
- **`agent_settled` is the sole turn-completion boundary.** The reference
  client's `waitForIdle`/`collectEvents` key on it, and the server itself uses
  it as the graceful-shutdown checkpoint. Treat everything between `prompt` and
  `agent_settled` as one unit of work; attach the event listener *before*
  sending the prompt (the reference `promptAndWait` does) to avoid a race.
  **Recorded (golden `follow-up-queue`): queued `steer`/`follow_up` messages
  are delivered inside the SAME agent block** — `turn_end` → `turn_start`
  with one `agent_end` carrying every message — so a block that absorbs
  queued messages still settles exactly ONCE. Do not count one settled per
  queued message.
- **Streaming deltas:** `message_update.assistantMessageEvent` carries
  `text_delta` / `thinking_delta` / `toolcall_delta` (+ start/end per content
  block) with `contentIndex`.
- **Session/system events:** `queue_update` (steering/followUp queues),
  `compaction_start`/`compaction_end`, `auto_retry_start`/`auto_retry_end`,
  `summarization_retry_*`, `entry_appended`, `session_info_changed`,
  `thinking_level_changed`, `bash_execution_update`.
- **Extension channels** (emitted by RPC mode itself, outside the session
  union): `extension_ui_request` and `extension_error`
  (`{ extensionPath, event, error }` — informational). UI request methods that
  round-trip and expect an `extension_ui_response`: `select`
  (`title, options: string[], timeout?`), `confirm` (`title, message,
  timeout?`), `input` (`title, placeholder?, timeout?`), `editor` (`title,
  prefill?` — **no timeout field exists**). The first three honor an optional
  `timeout` after which pi self-resolves with a default — but the timeout is
  the *extension author's* opt-in, and guard-style extensions deliberately
  omit it. Fire-and-forget: `notify` (`message, notifyType?:
  info|warning|error`), `setStatus` (`statusKey, statusText | undefined`),
  `setWidget` (`widgetKey, widgetLines: string[] | undefined,
  widgetPlacement?: aboveEditor|belowEditor` — RPC mode forwards string
  lines only, component factories are dropped server-side), `setTitle`,
  `set_editor_text` — **`set_editor_text` is the hook a host composer must
  listen for** (extensions pushing text at the editor; it replaces the whole
  buffer, never inserts).
- **Every dialog request must be answered — pi has no reliable backstop.**
  An unanswered `select`/`confirm`/`input` with no extension-supplied
  timeout, and any unanswered `editor`, leaves the extension's `await`
  pending forever; because the blocked hook is part of turn execution,
  `agent_settled` never fires. `abort` releases the dialog only when the
  extension threaded `ctx.signal` into the call — the bundled examples do
  not — so the only guaranteed release valve is the host answering
  (`cancelled: true` is always valid) or the supervisor's kill ladder.
  Requests can also arrive **before the first command**: extension
  `session_start` hooks fire at spawn, so a host must be consuming stdout
  from the moment the child starts (the supervisor's always-on channel
  covers this). **Since `v0.84.2-psmfd.1` (psmfd-patch-012, closing
  psmfd/pi#57) spawn-time dialogs are ANSWERABLE** — the stdin reader
  attaches before extensions bind, pre-ready `extension_ui_response` lines
  resolve immediately, and other pre-ready input replays in order once the
  command loop is up. Recorded: golden `session-start-dialog-roundtrip`.
  Under earlier pins the same dialog was unanswerable and pi exited **0**
  silently once its event loop drained (the retired golden
  `session-start-dialog-exit` pinned that death; FT#147's treat-as-fatal
  supervisor policy is obsolete from this pin on).
- **Truncation interplay (FT-2 slice 4):** an oversized
  `extension_ui_request` is replaced by the sidecar's `rpc_event_truncated`
  marker like any event, but the marker additionally preserves
  `originalId`/`originalMethod` for this type so an interactive request
  stays answerable — the host answers a truncated dialog with
  `cancelled: true` instead of letting the turn hang.

## Prompt and queueing semantics

- `prompt`'s response is **asynchronous**: it is emitted only after preflight
  (auth/model check) succeeds, decoupled from the command send, and can
  interleave with the prompt's own first streaming events. Exactly one
  response per `id`, success or preflight failure. Correlate by `id`, never by
  ordering.
- A second `prompt` while one streams is **queued, not rejected**
  (`streamingBehavior: "followUp"` or `"steer"`); its ack arrives promptly,
  its processing waits for the current turn. `steer` interrupts; `follow_up`
  queues; `set_steering_mode`/`set_follow_up_mode` pick `"all"` vs
  `"one-at-a-time"` delivery. `abort` cancels the in-flight run.
  **Recorded (golden `steer-interrupt`): "interrupt" does not mean the
  in-flight model request is aborted.** A `steer` sent mid-stream acks,
  emits `queue_update` (`steering: [...]`), and waits for the current
  model turn to finish streaming; only `abort` cancels the HTTP request
  itself. A host that steers against a stalled model stream must expect
  the steer to land only when the stream ends (or after an explicit
  `abort`).

## Session selection and process lifecycle

- **Selection is a spawn-time CLI concern:** `--session <path|id>`,
  `--session-id`, `--continue`, `--resume` (interactive TUI — not scriptable),
  `--fork`, `--no-session`, `--session-dir`. There is no "select session" RPC
  command; mid-process rebinding happens via `new_session` / `switch_session` /
  `fork` / `clone`, after which the same pipes keep working (`cancelled: true`
  means an extension vetoed and the old session stays bound).
- **One process = one attached session.** FT-2 hosts one `pi --mode rpc` child
  per live session.
- **Resume hazard:** `switch_session` asserts the session's recorded cwd still
  exists and errors with `MissingSessionCwdError` when it is gone (the reaped-
  worktree case). The underlying `cwdOverride` escape hatch is not exposed over
  RPC — psmfd/pi#55.
- **Shutdown:** closing the child's **stdin (EOF) is the clean shutdown
  trigger** (flushes output, exit 0). SIGTERM exits 143 *without* flushing
  buffered stdout; the reference client budgets SIGTERM → 1 s grace → SIGKILL.
  Extension-requested shutdown defers to the next `agent_settled` — pi never
  kills a mid-flight turn for it.
- **Supervisor obligations** (mirror `rpc-client.ts`): id-keyed pending-request
  map with per-request timeout; on child `exit`/`error` reject *all* in-flight
  requests with the exit code/signal and captured stderr; guard `send` on
  process liveness so calls fail fast instead of hanging. There is no
  heartbeat/health command — liveness is process-level only. Startup readiness
  is the `hello` line (see Wire framing; since `v0.84.2-psmfd.1`): gate on it
  with a grace-window fallback so pre-hello pins keep working — the reference
  client's old sleep-100 ms-and-probe is retired. `PiRpcClient` implements
  this as `WaitForHelloAsync`, gated at pane spawn (FT#148).

## Extensions under RPC mode

The full operator extension suite loads and runs (`bindExtensions` with
`mode: "rpc"`), with these UI capabilities as silent no-ops: working
indicators, footer/header, themes, autocomplete, editor components,
`getEditorText()` (always `""`), and `ctx.ui.custom()` (returns `undefined`
while `ctx.hasUI` stays `true` — the combination that crashes repo-dash's
panels, psmfd/pi_config#1018). Round-tripping methods (`select`, `confirm`,
`input`, `editor`) and the fire-and-forget set above work. Consequence for
FT-2: **reference-into-prompt is a native FingerTrap picker** feeding the
host-owned composer (plus a `set_editor_text` listener for extension-pushed
text) — not a reuse of the repo-dash panel.

## FT-2 feature mapping

| FT-2 feature | Serving surface | Status |
|---|---|---|
| Model/status readouts | `get_state`, `get_session_stats` (context-fill ships as a percent), `get_available_models`; incremental usage via `message_update` | covered |
| Session resume | `switch_session` (path known) or spawn-time `--session` | delivered in FT-2 slice 5 (session browser → RPC or PTY pane; ADR-0026 disables RPC resume on a missing cwd and offers the PTY fallback); reaped-cwd RPC recovery still blocked on pi#55 |
| Session list | The sidecar's bounded direct scan of `~/.pi/agent/sessions/` — **deliberately retained** per ADR-0028 (the browser's primary case is zero panes open, and `list_sessions` only runs inside a live child; a headless child would fire operator extensions). The `list_sessions` RPC command (since `v0.84.2-psmfd.1`, psmfd-patch-011) remains available to live panes via the command passthrough; migration is deferred behind FT#151's prerequisites | delivered in FT-2 slice 5 via the direct scan; #140 added the `skippedFiles` visibility (ADR-0028) |
| Worktree-orphan surfacing | not RPC territory by design: read the worktree extension's per-session manifests (`~/.pi/agent/extensions/worktree/sessions/<sid>.json` — `{ v, sessionId, repo, worktreePath, branch, pid, host, createdAt, updatedAt, lastSnapshotSha }`) × `git worktree list --porcelain` lock reasons (`session:<sid> pid:<p> host:<h> started:<iso>`) × `refs/pi-wip/<sid>`; port the extension's reconcile algorithm read-only. Formats are extension-owned, not protocol-versioned — re-verify on pi_config bumps | delivered in FT-2 slice 5 (`worktrees/list`, read-only reconcile port; reap/unlock stay pi-side) |
| Reference-into-prompt | host-owned composer + `steer`/`follow_up`/`prompt` to submit; listen for `set_editor_text` | covered natively |
| Observability dashboards | pi_config meter JSONL files, read directly — no RPC involvement | out of this note's scope |

## Filed gaps

- [psmfd/pi#54](https://github.com/psmfd/pi/issues/54) — **RESOLVED at
  `v0.84.2-psmfd.1`** (psmfd-patch-011): `list_sessions` RPC command; golden
  `list-sessions`. The session browser deliberately did NOT migrate to it —
  the scan is retained per ADR-0028 (#140 delivered the `skippedFiles`
  visibility instead); migration is deferred to FT#151 pending a
  side-effect-free spawn in the fork.
- [psmfd/pi#55](https://github.com/psmfd/pi/issues/55) — `switch_session`
  lacks `cwdOverride`; `MissingSessionCwdError` unrecoverable over RPC.
- [psmfd/pi#56](https://github.com/psmfd/pi/issues/56) — **RESOLVED at
  `v0.84.2-psmfd.1`** (psmfd-patch-010): the `hello` first line (see Wire
  framing). FT adoption delivered via #148: `PiRpcClient.WaitForHelloAsync`
  ready gate (legacy grace fallback, typed protocol refusal,
  died-before-hello spawn errors), gated in `RpcPaneService.SpawnAsync`.
- [psmfd/pi#57](https://github.com/psmfd/pi/issues/57) — **RESOLVED at
  `v0.84.2-psmfd.1`** (psmfd-patch-012): spawn-time dialogs answerable;
  golden `session-start-dialog-roundtrip` (replaces
  `session-start-dialog-exit`). FT#147's supervisor policy is obsolete.
- [psmfd/pi_config#1018](https://github.com/psmfd/pi_config/issues/1018) —
  repo-dash panels crash under RPC mode (informational here; FT-2 goes
  native).
