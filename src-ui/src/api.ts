import {
  createMessageConnection,
  type Disposable,
  type MessageConnection,
  NotificationType1,
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

const PtySpawnMethod = new RequestType1<PtySpawnRequest, PtySpawnResult, void>('pty/spawn');
const PtyWriteMethod = new RequestType1<PtyWriteRequest, void, void>('pty/write');
const PtyResizeMethod = new RequestType1<PtyResizeRequest, void, void>('pty/resize');
const PtyKillMethod = new RequestType1<PtyKillRequest, void, void>('pty/kill');
const StatusRefreshMethod = new RequestType1<null, void, void>('status/refresh');
const SettingsGetMethod = new RequestType1<null, SettingsGetResult, void>('settings/get');
const StatusSnapshotNotif = new NotificationType1<StatusSnapshotNotification>('status/snapshot');
const PtyOutputNotif = new NotificationType1<PtyOutputNotification>('pty/output');
const PtyExitNotif = new NotificationType1<PtyExitNotification>('pty/exit');

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

/** Fire-and-forget by contract: the answer is the next status/snapshot. */
export async function statusRefresh(): Promise<void> {
  await require_().sendRequest(StatusRefreshMethod, null);
}

export function onStatusSnapshot(handler: (n: StatusSnapshotNotification) => void): Disposable {
  return require_().onNotification(StatusSnapshotNotif, handler);
}

export async function settingsGet(): Promise<SettingsGetResult> {
  return require_().sendRequest(SettingsGetMethod, null);
}
