import {
  AbstractMessageReader,
  AbstractMessageWriter,
  type DataCallback,
  type Disposable,
  type Message,
} from 'vscode-jsonrpc/browser';
import { invoke, Channel } from '@tauri-apps/api/core';

const CR = 0x0d;
const LF = 0x0a;

/**
 * Ceiling on a frame's declared Content-Length (ADR-0022). Must match the
 * sidecar reader's (`FrameCeilingStream.DefaultMaxFrameBytes`) — lockstep
 * pair; change both together. A breach kills the reader rather than
 * allocating whatever the header claims.
 */
const MAX_FRAME_BYTES = 4 * 1024 * 1024;

function parseContentLength(headers: string): number {
  for (const line of headers.split('\r\n')) {
    const match = /^Content-Length:\s*(\d+)$/i.exec(line);
    if (match) return Number.parseInt(match[1], 10);
  }
  throw new Error('Content-Length header missing');
}

export class TauriMessageReader extends AbstractMessageReader {
  private callback: DataCallback | undefined;
  private buffer = new Uint8Array(0);
  // Set on a frame-ceiling breach: the stream is unrecoverable at that point
  // (we cannot skip a frame we refuse to buffer), so all further input is
  // dropped — connection-fatal, no resync heuristics (ADR-0002/0022).
  private dead = false;
  // Messages parsed before listen() registers its callback are queued
  // here and flushed in listen(). Without this, any pty/output
  // notification (or response) that arrives between `await
  // reader.start()` and `connection.listen()` in api.ts would be
  // silently dropped by `this.callback?.(message)`. See issue #18.
  private pending: Message[] = [];
  private readonly channel: Channel<number[]>;

  constructor() {
    super();
    this.channel = new Channel<number[]>();
    this.channel.onmessage = (bytes: number[]) => this.feed(new Uint8Array(bytes));
  }

  async start(): Promise<void> {
    await invoke<void>('subscribe_sidecar_output', { channel: this.channel });
  }

  listen(callback: DataCallback): Disposable {
    this.callback = callback;
    // Flush anything that arrived during the startup gap. Snapshot and
    // clear first so a re-entrant callback that triggers another feed()
    // can't see a half-drained queue.
    const queued = this.pending;
    this.pending = [];
    for (const message of queued) callback(message);
    return {
      dispose: () => {
        this.callback = undefined;
      },
    };
  }

  private feed(data: Uint8Array): void {
    if (this.dead) return;
    const merged = new Uint8Array(this.buffer.length + data.length);
    merged.set(this.buffer);
    merged.set(data, this.buffer.length);
    this.buffer = merged;
    this.drain();
  }

  private drain(): void {
    while (this.buffer.length > 0) {
      const headerEnd = this.findHeaderEnd();
      if (headerEnd < 0) return;

      const headerText = new TextDecoder('ascii').decode(this.buffer.subarray(0, headerEnd));
      let contentLength: number;
      try {
        contentLength = parseContentLength(headerText);
      } catch (err) {
        this.fireError(err as Error);
        return;
      }

      if (contentLength > MAX_FRAME_BYTES) {
        this.dead = true;
        this.buffer = new Uint8Array(0);
        this.fireError(
          new Error(`rpc frame declared Content-Length ${contentLength} exceeds the ${MAX_FRAME_BYTES}-byte ceiling (ADR-0022)`),
        );
        return;
      }

      const totalLength = headerEnd + 4 + contentLength;
      if (this.buffer.length < totalLength) return;

      const bodyBytes = this.buffer.subarray(headerEnd + 4, totalLength);
      const bodyText = new TextDecoder('utf-8').decode(bodyBytes);
      this.buffer = this.buffer.subarray(totalLength);

      let message: Message;
      try {
        message = JSON.parse(bodyText) as Message;
      } catch (err) {
        this.fireError(err as Error);
        continue;
      }

      if (this.callback) {
        this.callback(message);
      } else {
        this.pending.push(message);
      }
    }
  }

  private findHeaderEnd(): number {
    for (let i = 0; i + 3 < this.buffer.length; i++) {
      if (
        this.buffer[i] === CR &&
        this.buffer[i + 1] === LF &&
        this.buffer[i + 2] === CR &&
        this.buffer[i + 3] === LF
      ) {
        return i;
      }
    }
    return -1;
  }
}

export class TauriMessageWriter extends AbstractMessageWriter {
  async write(msg: Message): Promise<void> {
    const body = JSON.stringify(msg);
    const bodyBytes = new TextEncoder().encode(body);
    const header = `Content-Length: ${bodyBytes.length}\r\n\r\n`;
    const headerBytes = new TextEncoder().encode(header);
    const frame = new Uint8Array(headerBytes.length + bodyBytes.length);
    frame.set(headerBytes);
    frame.set(bodyBytes, headerBytes.length);
    await invoke<void>('sidecar_write', { payload: Array.from(frame) });
  }

  end(): void {}
}
