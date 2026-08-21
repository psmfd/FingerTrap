import {
  createMessageConnection,
  type Disposable,
  type MessageConnection,
  NotificationType1,
  RequestType0,
  RequestType1,
} from 'vscode-jsonrpc/browser';
import { TauriMessageReader, TauriMessageWriter } from './transport';

let connection: MessageConnection | undefined;

export async function start(): Promise<void> {
  if (connection) return;
  const reader = new TauriMessageReader();
  const writer = new TauriMessageWriter();
  await reader.start();
  connection = createMessageConnection(reader, writer);
  connection.listen();
}

function require_(): MessageConnection {
  if (!connection) {
    throw new Error('api.start() must be awaited before invoking RPC methods');
  }
  return connection;
}

const PingMethod = new RequestType1<string, string, void>('ping');

export async function ping(message: string): Promise<string> {
  return require_().sendRequest(PingMethod, message);
}

/** What a pane is, as opposed to which binary happens to run in it. */
export type PaneKind = 'shell' | 'pi';

export interface PtySpawnRequest {
  sessionId: string;
  /**
   * Explicit executable path, overriding resolution for whichever `kind` is in
   * force. Named `shell` for wire compatibility with the pre-FT-0 contract; it
   * overrides a pi pane's executable just as much as a shell pane's.
   */
  shell?: string;
  cwd?: string;
  cols: number;
  rows: number;
  env?: Record<string, string>;
  /**
   * Omit to take the host default, which the sidecar resolves (`pi`, unless
   * `FINGERTRAP_PANE_KIND` says otherwise). The default lives sidecar-side
   * because a WebView cannot read process environment. An unrecognised value
   * is rejected rather than silently defaulted.
   */
  kind?: PaneKind;
  /**
   * Session to resume (`--session` on the pi command line) — PTY-pane
   * resume from the session browser (FT-2 slice 5, ADR-0026). Only
   * meaningful for pi panes; interactive pi owns the missing-cwd fallback
   * prompt.
   */
  sessionPath?: string;
}

export interface PtySpawnResult {
  pid: number;
}

export interface PtyWriteRequest {
  sessionId: string;
  dataBase64: string;
}

export interface PtyResizeRequest {
  sessionId: string;
  cols: number;
  rows: number;
}

/**
 * Ends a session's process and releases its PTY (ADR-0021). Idempotent: a
 * session that already exited — or never existed — is success, so the
 * close-tab path never races the process's own exit.
 */
export interface PtyKillRequest {
  sessionId: string;
}

export interface PtyOutputNotification {
  sessionId: string;
  dataBase64: string;
}

export interface PtyExitNotification {
  sessionId: string;
  exitCode: number;
}

/**
 * Native RPC pane surface (FT-2 slice 2, ADR-0025). `sessionPath` resumes a
 * session at spawn time — selection is spawn-time-only in pi's protocol; the
 * session browser (slice 5) is its first setter.
 */
export interface RpcSpawnRequest {
  sessionId: string;
  cwd?: string;
  sessionPath?: string;
  env?: Record<string, string>;
}

export interface RpcKillRequest {
  sessionId: string;
}

export interface RpcPromptRequest {
  sessionId: string;
  message: string;
  /**
   * Required by pi when prompting mid-stream: 'steer' interrupts,
   * 'followUp' queues (pi's exact casing). Omit when idle.
   */
  streamingBehavior?: 'steer' | 'followUp';
}

/** The prompt ack — asynchronous relative to the prompt's own events. */
export interface RpcPromptResult {
  success: boolean;
  error?: string | null;
}

/** Session-scoped command with a message payload (steer / follow_up). */
export interface RpcMessageRequest {
  sessionId: string;
  message: string;
}

/** Session-scoped command with no further parameters. */
export interface RpcSessionRequest {
  sessionId: string;
}

export interface RpcSetModelRequest {
  sessionId: string;
  provider: string;
  modelId: string;
}

export interface RpcSetThinkingLevelRequest {
  sessionId: string;
  level: string;
}

/**
 * Answers one interactive extension_ui_request dialog —
 * select/confirm/input/editor (FT-2 slice 4). Exactly one of
 * `value`/`confirmed`/`cancelled` is set: pi's response union is
 * discriminated by key. `requestId` echoes the request event's own `id`.
 * One-way on the pi wire — pi sends no ack and silently drops an
 * unknown/expired id, so a resolved promise means "delivered to the
 * child's stdin", never "pi applied it".
 */
export interface RpcExtensionUiResponseRequest {
  sessionId: string;
  requestId: string;
  value?: string;
  confirmed?: boolean;
  cancelled?: boolean;
}

/**
 * A pi command's outcome, envelope stripped sidecar-side (FT-2 slice 3b):
 * `data` is the response's `data` payload verbatim, or absent/null when
 * the command returns none. Untrusted content in every field — read
 * defensively, render text via textContent only (ADR-0022).
 */
export interface RpcCommandResult {
  success: boolean;
  error?: string | null;
  data?: unknown;
}

/**
 * One relayed pi event, verbatim (thin relay). `event` is the raw event
 * object — untrusted content in every field: render text via
 * textContent/text nodes only, never innerHTML (ADR-0022). `truncated`
 * marks an oversized event replaced sidecar-side by a small
 * `rpc_event_truncated` marker so it could not kill the shared transport.
 */
export interface RpcEventNotification {
  sessionId: string;
  eventType?: string | null;
  event: unknown;
  truncated: boolean;
}

/**
 * The pane's pi child is gone. `stderrTail` is bounded diagnostic text —
 * untrusted (extension logs reroute to stderr); textContent-only rendering.
 */
export interface RpcExitNotification {
  sessionId: string;
  exitCode: number;
  stderrTail: string;
}

/**
 * Status surfaces (ADR-0022). Free-text fields arrive already sanitized at
 * the sidecar's data boundary; render them via textContent regardless —
 * never innerHTML (defense in depth, there is no framework auto-escaping).
 * Provider `state` is a string, not a union: an unrecognized future state
 * must render as text, not fail to parse.
 */
export interface IssueRow {
  id: number;
  number: number;
  title: string;
  author: string;
  state: string;
  updatedAt: string;
  /** Present only when the sidecar validated it (ADR-0023); null renders unlinked. */
  url?: string | null;
}

export interface PrRow {
  id: number;
  number: number;
  title: string;
  author: string;
  state: string;
  isDraft: boolean;
  headBranch: string;
  updatedAt: string;
  /** Present only when the sidecar validated it (ADR-0023); null renders unlinked. */
  url?: string | null;
}

export interface RunRow {
  id: number;
  runNumber: number;
  workflowName: string;
  displayTitle: string;
  status: string;
  conclusion: string | null;
  outcome: string;
  headBranch: string;
  createdAt: string;
  /** Present only when the sidecar validated it (ADR-0023); null renders unlinked. */
  url?: string | null;
}

export interface ProviderSnapshot {
  provider: string;
  state: string;
  detail: string | null;
  issues: IssueRow[];
  pullRequests: PrRow[];
  runs: RunRow[];
}

export interface StatusSnapshotNotification {
  providers: ProviderSnapshot[];
}

/**
 * Effective settings (FT-1 slice 3, ADR-0021): the WebView cannot read the
 * settings file, so the sidecar answers with what it already resolved.
 * `keybindings` is the operator's per-chord override map served verbatim
 * (empty when unset); defaults and chord semantics live in keymap.ts.
 */
export interface SettingsGetResult {
  paneDefaultKind: string;
  keybindings: Record<string, string>;
}

/**
 * Session browser (FT-2 slice 5, ADR-0026). `sessionPath` is the only
 * functional resume key — everything else is sanitized display data. When
 * `cwdMissing` is true, RPC-pane resume is disabled (pi hard-exits on a
 * missing cwd in rpc mode); PTY-pane resume is offered with `originalRepo`
 * as the pane cwd when the session was a reaped worktree.
 */
export interface SessionSummary {
  sessionPath: string;
  id: string;
  cwd: string;
  name: string | null;
  firstMessage: string;
  messageCount: number;
  createdAt: string;
  modifiedAt: string;
  /** May dangle — treat unresolvable parents as fork-tree roots. */
  parentSessionPath: string | null;
  cwdMissing: boolean;
  reapedWorktree: boolean;
  originalRepo: string | null;
}

/** `totalCount` can exceed `sessions.length` — the sidecar deep-parses only
 * the most recently modified N files, so the UI shows "N of totalCount".
 * `skippedFiles` counts files the scan attempted but could not parse
 * (unreadable / no valid header) — rendered so corruption is a visible
 * fact, not a silent absence (#140, ADR-0028); cap-excluded files are
 * unattempted and never counted there. */
export interface SessionsListResult {
  sessions: SessionSummary[];
  totalCount: number;
  skippedFiles: number;
}

/**
 * One reconciled per-session worktree (read-only surfacing — reap/unlock
 * stay pi-side `/worktree` commands). `host` is display-only: a recorded
 * pid is only meaningful on the host that recorded it (pi_config#1019).
 */
export interface WorktreeRecord {
  sid: string;
  worktreePath: string | null;
  branch: string | null;
  repo: string | null;
  host: string | null;
  wipSha: string | null;
  pid: number | null;
  alive: boolean;
  shape: 'live' | 'dead' | 'gone' | 'stray';
}

export interface WorktreesListResult {
  records: WorktreeRecord[];
}

const PtySpawnMethod = new RequestType1<PtySpawnRequest, PtySpawnResult, void>('pty/spawn');
const PtyWriteMethod = new RequestType1<PtyWriteRequest, void, void>('pty/write');
const PtyResizeMethod = new RequestType1<PtyResizeRequest, void, void>('pty/resize');
const PtyKillMethod = new RequestType1<PtyKillRequest, void, void>('pty/kill');
const RpcSpawnMethod = new RequestType1<RpcSpawnRequest, void, void>('rpc/spawn');
const RpcKillMethod = new RequestType1<RpcKillRequest, void, void>('rpc/kill');
const RpcPromptMethod = new RequestType1<RpcPromptRequest, RpcPromptResult, void>('rpc/prompt');
const RpcSteerMethod = new RequestType1<RpcMessageRequest, RpcCommandResult, void>('rpc/steer');
const RpcFollowUpMethod = new RequestType1<RpcMessageRequest, RpcCommandResult, void>(
  'rpc/followUp',
);
const RpcAbortMethod = new RequestType1<RpcSessionRequest, RpcCommandResult, void>('rpc/abort');
const RpcGetStateMethod = new RequestType1<RpcSessionRequest, RpcCommandResult, void>(
  'rpc/getState',
);
const RpcGetMessagesMethod = new RequestType1<RpcSessionRequest, RpcCommandResult, void>(
  'rpc/getMessages',
);
const RpcGetSessionStatsMethod = new RequestType1<RpcSessionRequest, RpcCommandResult, void>(
  'rpc/getSessionStats',
);
const RpcGetAvailableModelsMethod = new RequestType1<RpcSessionRequest, RpcCommandResult, void>(
  'rpc/getAvailableModels',
);
const RpcGetAvailableThinkingLevelsMethod = new RequestType1<
  RpcSessionRequest,
  RpcCommandResult,
  void
>('rpc/getAvailableThinkingLevels');
const RpcSetModelMethod = new RequestType1<RpcSetModelRequest, RpcCommandResult, void>(
  'rpc/setModel',
);
const RpcSetThinkingLevelMethod = new RequestType1<
  RpcSetThinkingLevelRequest,
  RpcCommandResult,
  void
>('rpc/setThinkingLevel');
const RpcExtensionUiResponseMethod = new RequestType1<RpcExtensionUiResponseRequest, void, void>(
  'rpc/extensionUiResponse',
);
const StatusRefreshMethod = new RequestType0<void, void>('status/refresh');
const SettingsGetMethod = new RequestType0<SettingsGetResult, void>('settings/get');
const SessionsListMethod = new RequestType0<SessionsListResult, void>('sessions/list');
const WorktreesListMethod = new RequestType0<WorktreesListResult, void>('worktrees/list');
const StatusSnapshotNotif = new NotificationType1<StatusSnapshotNotification>('status/snapshot');
const PtyOutputNotif = new NotificationType1<PtyOutputNotification>('pty/output');
const PtyExitNotif = new NotificationType1<PtyExitNotification>('pty/exit');
const RpcEventNotif = new NotificationType1<RpcEventNotification>('rpc/event');
const RpcExitNotif = new NotificationType1<RpcExitNotification>('rpc/exit');

export async function ptySpawn(request: PtySpawnRequest): Promise<PtySpawnResult> {
  return require_().sendRequest(PtySpawnMethod, request);
}

export async function ptyWrite(request: PtyWriteRequest): Promise<void> {
  await require_().sendRequest(PtyWriteMethod, request);
}

export async function ptyResize(request: PtyResizeRequest): Promise<void> {
  await require_().sendRequest(PtyResizeMethod, request);
}

export async function ptyKill(request: PtyKillRequest): Promise<void> {
  await require_().sendRequest(PtyKillMethod, request);
}

export function onPtyOutput(handler: (n: PtyOutputNotification) => void): Disposable {
  return require_().onNotification(PtyOutputNotif, handler);
}

export function onPtyExit(handler: (n: PtyExitNotification) => void): Disposable {
  return require_().onNotification(PtyExitNotif, handler);
}

export async function rpcSpawn(request: RpcSpawnRequest): Promise<void> {
  await require_().sendRequest(RpcSpawnMethod, request);
}

export async function rpcKill(request: RpcKillRequest): Promise<void> {
  await require_().sendRequest(RpcKillMethod, request);
}

export async function rpcPrompt(request: RpcPromptRequest): Promise<RpcPromptResult> {
  return require_().sendRequest(RpcPromptMethod, request);
}

export async function rpcSteer(request: RpcMessageRequest): Promise<RpcCommandResult> {
  return require_().sendRequest(RpcSteerMethod, request);
}

export async function rpcFollowUp(request: RpcMessageRequest): Promise<RpcCommandResult> {
  return require_().sendRequest(RpcFollowUpMethod, request);
}

export async function rpcAbort(request: RpcSessionRequest): Promise<RpcCommandResult> {
  return require_().sendRequest(RpcAbortMethod, request);
}

export async function rpcGetState(request: RpcSessionRequest): Promise<RpcCommandResult> {
  return require_().sendRequest(RpcGetStateMethod, request);
}

/** Full history of the attached session — the post-resume transcript
 * seed (FT-2 slice 5): no `since` cursor exists, so the first fetch is
 * always the whole list. */
export async function rpcGetMessages(request: RpcSessionRequest): Promise<RpcCommandResult> {
  return require_().sendRequest(RpcGetMessagesMethod, request);
}

export async function rpcGetSessionStats(request: RpcSessionRequest): Promise<RpcCommandResult> {
  return require_().sendRequest(RpcGetSessionStatsMethod, request);
}

export async function rpcGetAvailableModels(request: RpcSessionRequest): Promise<RpcCommandResult> {
  return require_().sendRequest(RpcGetAvailableModelsMethod, request);
}

export async function rpcGetAvailableThinkingLevels(
  request: RpcSessionRequest,
): Promise<RpcCommandResult> {
  return require_().sendRequest(RpcGetAvailableThinkingLevelsMethod, request);
}

export async function rpcSetModel(request: RpcSetModelRequest): Promise<RpcCommandResult> {
  return require_().sendRequest(RpcSetModelMethod, request);
}

export async function rpcSetThinkingLevel(
  request: RpcSetThinkingLevelRequest,
): Promise<RpcCommandResult> {
  return require_().sendRequest(RpcSetThinkingLevelMethod, request);
}

export async function rpcExtensionUiResponse(
  request: RpcExtensionUiResponseRequest,
): Promise<void> {
  await require_().sendRequest(RpcExtensionUiResponseMethod, request);
}

export function onRpcEvent(handler: (n: RpcEventNotification) => void): Disposable {
  return require_().onNotification(RpcEventNotif, handler);
}

export function onRpcExit(handler: (n: RpcExitNotification) => void): Disposable {
  return require_().onNotification(RpcExitNotif, handler);
}

/** Fire-and-forget by contract: the answer is the next status/snapshot. */
export async function statusRefresh(): Promise<void> {
  await require_().sendRequest(StatusRefreshMethod);
}

export function onStatusSnapshot(handler: (n: StatusSnapshotNotification) => void): Disposable {
  return require_().onNotification(StatusSnapshotNotif, handler);
}

export async function settingsGet(): Promise<SettingsGetResult> {
  return require_().sendRequest(SettingsGetMethod);
}

export async function sessionsList(): Promise<SessionsListResult> {
  return require_().sendRequest(SessionsListMethod);
}

export async function worktreesList(): Promise<WorktreesListResult> {
  return require_().sendRequest(WorktreesListMethod);
}
