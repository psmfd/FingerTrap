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

export interface PtyOutputNotification {
  sessionId: string;
  dataBase64: string;
}

export interface PtyExitNotification {
  sessionId: string;
  exitCode: number;
}

const PtySpawnMethod = new RequestType1<PtySpawnRequest, PtySpawnResult, void>('pty/spawn');
const PtyWriteMethod = new RequestType1<PtyWriteRequest, void, void>('pty/write');
const PtyResizeMethod = new RequestType1<PtyResizeRequest, void, void>('pty/resize');
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

export function onPtyOutput(handler: (n: PtyOutputNotification) => void): Disposable {
  return require_().onNotification(PtyOutputNotif, handler);
}

export function onPtyExit(handler: (n: PtyExitNotification) => void): Disposable {
  return require_().onNotification(PtyExitNotif, handler);
}
