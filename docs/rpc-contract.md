# pi `--mode rpc` contract study (FT-2 gate)

**Verified against:** pi tag `v0.84.1-psmfd.1` (commit `18b98782f39a`, the version
pinned by `pi_config`), by reading
`packages/coding-agent/src/modes/rpc/{rpc-types,rpc-mode,rpc-client,jsonl}.ts`,
the type chain into `pi-agent-core`/`pi-ai`, and the RPC test suite (including
the 5868 unknown-command-id regression). This note satisfies the FT-2 gate in
`docs/milestones.md`: it enumerates the methods and events FT-2 consumes and
files the gaps as issues (listed at the end). Re-verify against the source on
every pi pin bump — event-name drift across pi versions is a previously-hit bug
class (pi_config's subagent extension once listened for a `tool_result_end`
event that pi 0.80.2 no longer emitted), and the protocol has no version
handshake (psmfd/pi#56).

A caution for future readers of the pi source: `packages/protocol` +
`packages/server` (`pi-server`) is an experimental, unused-by-the-CLI
multi-session daemon protocol (CBOR over Unix sockets). It is **not** what
`pi --mode rpc` speaks. Everything below is the JSONL stdin/stdout protocol in
`packages/coding-agent/src/modes/rpc/`.

## Wire framing

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
`extension_ui_request`. A response for an unknown/expired id is silently
dropped.

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
  round-trip and expect an `extension_ui_response`: `select`, `confirm`,
  `input`, `editor` (the first three honor an optional `timeout` after which
  pi self-resolves with a default). Fire-and-forget: `notify`, `setStatus`,
  `setWidget`, `setTitle`, `set_editor_text` — **`set_editor_text` is the hook
  a host composer must listen for** (extensions pushing text at the editor).

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
  heartbeat/health command — liveness is process-level only. There is also no
  ready signal at startup (the reference client sleeps 100 ms and checks for
  early exit); psmfd/pi#56 proposes a hello line that would fix both this and
  versioning.

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
| Session resume | `switch_session` (path known) or spawn-time `--session` | covered; reaped-cwd recovery blocked on pi#55 |
| Session list | none — `SessionManager.list*()` is library-only; sidecar parses `~/.pi/agent/sessions/--<cwd-dashes>--/<timestamp>_<id>.jsonl` directly until pi#54 lands | gap (pi#54) |
| Worktree-orphan surfacing | not RPC territory by design: read the worktree extension's per-session manifests (`~/.pi/agent/extensions/worktree/sessions/<sid>.json` — `{ v, sessionId, repo, worktreePath, branch, pid, host, createdAt, updatedAt, lastSnapshotSha }`) × `git worktree list --porcelain` lock reasons (`session:<sid> pid:<p> host:<h> started:<iso>`) × `refs/pi-wip/<sid>`; port the extension's reconcile algorithm read-only. Formats are extension-owned, not protocol-versioned — re-verify on pi_config bumps | covered (observe-from-outside) |
| Reference-into-prompt | host-owned composer + `steer`/`follow_up`/`prompt` to submit; listen for `set_editor_text` | covered natively |
| Observability dashboards | pi_config meter JSONL files, read directly — no RPC involvement | out of this note's scope |

## Filed gaps

- [psmfd/pi#54](https://github.com/psmfd/pi/issues/54) — no scriptable
  session-list surface (RPC command or `pi sessions list --json`).
- [psmfd/pi#55](https://github.com/psmfd/pi/issues/55) — `switch_session`
  lacks `cwdOverride`; `MissingSessionCwdError` unrecoverable over RPC.
- [psmfd/pi#56](https://github.com/psmfd/pi/issues/56) — no protocol version
  handshake or ready signal.
- [psmfd/pi_config#1018](https://github.com/psmfd/pi_config/issues/1018) —
  repo-dash panels crash under RPC mode (informational here; FT-2 goes
  native).
