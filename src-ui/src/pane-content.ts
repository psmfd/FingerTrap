import { Terminal } from '@xterm/xterm';
import { FitAddon } from '@xterm/addon-fit';
import { WebglAddon } from '@xterm/addon-webgl';
import * as api from './api';
import {
  appendSystemBlock,
  createTranscriptState,
  reduceTranscript,
  type BlockId,
  type TranscriptState,
} from './transcript';
import { TranscriptView } from './transcript-view';

/**
 * What the registry, layout tree, and tab chrome need from a pane's content,
 * regardless of what renders inside the container (ADR-0025 decision 4).
 * Capability-neutral names on purpose — never `term`/`fit` — so nothing
 * outside an implementation can assume xterm.
 */
export interface PaneContent {
  readonly container: HTMLElement;

  /**
   * Start the pane's backing process/session. The registry calls this only
   * after a layout pass and a resize() — the cell-measurement choreography
   * PTY content depends on. Throws on failure; the registry renders the
   * error into the pane.
   */
  open(opts: { cwd?: string }): Promise<void>;

  /**
   * Called after every layout pass that could have changed on-screen size:
   * mount, tab activation, split/close reflow, gutter drag, ResizeObserver.
   * Never conditionally skipped by callers — the nextFrame-then-resize
   * choreography is load-bearing for PTY cell measurement and runs
   * uniformly for every pane kind. RPC content's implementation is a cheap
   * no-op (plain CSS flex layout).
   */
  resize(): void;

  /** Move input focus into this pane's interactive surface. */
  focus(): void;

  /**
   * Render an out-of-band operator-facing message into the pane's own
   * stream — spawn/attach failure, exit banner. Plain text + severity;
   * implementations translate (ANSI for xterm, styled text node for the
   * RPC pane). Never markup: ADR-0022's textContent-only rule applies.
   */
  writeSystemMessage(text: string, kind: 'info' | 'error'): void;

  /**
   * End the underlying process/session — idempotent, kind-dispatched: PTY
   * content calls pty/kill, RPC content calls rpc/kill (a different wire
   * method entirely; the registry calling pty/kill unconditionally would
   * silently leak the pi RPC child).
   */
  close(): Promise<void>;

  /** Tear down local resources only; never talks to the sidecar. */
  dispose(): void;
}

/** What an open()/split() caller can request. */
export type OpenKind = api.PaneKind | 'pi-rpc';

function bytesToBase64(bytes: Uint8Array): string {
  const CHUNK = 0x8000;
  const parts: string[] = [];
  for (let i = 0; i < bytes.length; i += CHUNK) {
    parts.push(String.fromCharCode(...bytes.subarray(i, i + CHUNK)));
  }
  return btoa(parts.join(''));
}

/**
 * The xterm-backed PTY pane — all xterm/FitAddon/WebGL knowledge lives
 * here; the registry no longer imports any of it.
 */
export class PtyPaneContent implements PaneContent {
  readonly container: HTMLElement;
  private readonly term: Terminal;
  private readonly fit: FitAddon;
  private readonly sessionId: string;
  private readonly kind: api.PaneKind | undefined;
  private readonly encoder = new TextEncoder();

  constructor(sessionId: string, kind?: api.PaneKind) {
    this.sessionId = sessionId;
    this.kind = kind;
    this.container = document.createElement('div');
    this.container.className = 'pane';

    this.term = new Terminal({
      fontFamily: 'ui-monospace, SFMono-Regular, Menlo, monospace',
      fontSize: 13,
      theme: { background: '#000000' },
      cursorBlink: true,
      allowProposedApi: true,
    });
    this.fit = new FitAddon();
    this.term.loadAddon(this.fit);
    this.term.open(this.container);

    // The DOM renderer (xterm's default) leaves stale rows on screen after
    // full-screen erases (#78) — our display:none tab toggling stresses its
    // known repaint gaps. Prefer the WebGL renderer; on context loss or
    // WebGL-less environments fall back to the DOM renderer by disposing the
    // addon rather than freezing the pane. Addon lifetime is owned by the
    // terminal: term.dispose() disposes loaded addons.
    try {
      const webgl = new WebglAddon();
      webgl.onContextLoss(() => {
        webgl.dispose();
      });
      this.term.loadAddon(webgl);
    } catch {
      // WebGL unavailable — the DOM renderer stays in effect.
    }
  }

  async open(opts: { cwd?: string }): Promise<void> {
    await api.ptySpawn({
      sessionId: this.sessionId,
      kind: this.kind,
      cwd: opts.cwd,
      cols: this.term.cols,
      rows: this.term.rows,
    });

    // Wired only after a successful spawn — a pane whose spawn failed
    // deliberately ignores typing instead of erroring on every keystroke.
    const { sessionId, term } = this;
    term.onData((data) => {
      api
        .ptyWrite({ sessionId, dataBase64: bytesToBase64(this.encoder.encode(data)) })
        .catch((err: unknown) => {
          term.write(`\r\n\x1b[31m[ptyWrite error] ${(err as Error).message}\x1b[0m\r\n`);
        });
    });

    // The sidecar coalesces resize requests over 50 ms (ADR-0006); no UI
    // debounce.
    term.onResize(({ cols, rows }) => {
      api.ptyResize({ sessionId, cols, rows }).catch((err: unknown) => {
        term.write(`\r\n\x1b[31m[ptyResize error] ${(err as Error).message}\x1b[0m\r\n`);
      });
    });
  }

  resize(): void {
    this.fit.fit();
  }

  focus(): void {
    this.term.focus();
  }

  writeSystemMessage(text: string, kind: 'info' | 'error'): void {
    const color = kind === 'error' ? '31' : '33';
    this.term.write(`\r\n\x1b[${color}m${text}\x1b[0m\r\n`);
  }

  /** PTY output routing — PTY-private, not part of PaneContent. */
  write(data: Uint8Array): void {
    this.term.write(data);
  }

  async close(): Promise<void> {
    await api.ptyKill({ sessionId: this.sessionId });
  }

  dispose(): void {
    this.term.dispose();
    this.container.remove();
  }
}

/**
 * The native RPC pane (FT-2 slice 3): a structured transcript fed by the
 * pure reducer in transcript.ts and projected by TranscriptView, with a
 * provisional single-line prompt input the slice-3b composer replaces.
 * Everything relayed is untrusted content: rendering is text nodes only,
 * never markup (ADR-0022), and DOM flushes are rAF-coalesced so
 * token-rate notification bursts cost one layout pass per frame.
 */
export class RpcPaneContent implements PaneContent {
  readonly container: HTMLElement;
  private readonly output: HTMLElement;
  private readonly input: HTMLInputElement;
  private readonly sessionId: string;
  private readonly state: TranscriptState = createTranscriptState();
  private readonly view: TranscriptView;
  private readonly dirty = new Set<BlockId>();
  private flushScheduled = false;

  constructor(sessionId: string) {
    this.sessionId = sessionId;
    this.container = document.createElement('div');
    this.container.className = 'pane rpc-pane';

    this.output = document.createElement('div');
    this.output.className = 'rpc-output';
    this.container.appendChild(this.output);
    this.view = new TranscriptView(this.output);

    const composer = document.createElement('form');
    composer.className = 'rpc-composer';
    this.input = document.createElement('input');
    this.input.type = 'text';
    this.input.placeholder = 'prompt (provisional — slice 3b composer replaces this)';
    composer.appendChild(this.input);
    composer.addEventListener('submit', (e) => {
      e.preventDefault();
      this.submitPrompt();
    });
    this.container.appendChild(composer);
  }

  async open(opts: { cwd?: string }): Promise<void> {
    await api.rpcSpawn({ sessionId: this.sessionId, cwd: opts.cwd });
  }

  resize(): void {
    // Plain CSS flex layout — nothing to measure. Deliberately cheap: the
    // ResizeObserver path calls this at animation-frame rate during
    // gutter drags.
  }

  focus(): void {
    this.input.focus();
  }

  /** One relayed event, folded through the transcript reducer. */
  appendEvent(n: api.RpcEventNotification): void {
    try {
      this.markDirty(reduceTranscript(this.state, n));
    } catch (err) {
      // Backstop: one malformed event must not take down the pane's event
      // handling for the rest of the session (mirrors the sidecar's
      // ignore-unparseable-lines posture).
      this.markDirty([
        appendSystemBlock(this.state, `[event handling error: ${(err as Error).message}]`, 'error'),
      ]);
    }
  }

  writeSystemMessage(text: string, kind: 'info' | 'error'): void {
    this.markDirty([appendSystemBlock(this.state, text, kind)]);
  }

  async close(): Promise<void> {
    await api.rpcKill({ sessionId: this.sessionId });
  }

  dispose(): void {
    this.container.remove();
  }

  private submitPrompt(): void {
    const message = this.input.value.trim();
    if (!message) return;
    this.input.value = '';
    // No local echo: pi echoes every user message as message_start/end
    // (docs/rpc-contract.md), and that echo is the transcript's single
    // source of truth for ordering.
    this.view.forcePin();
    api
      .rpcPrompt({ sessionId: this.sessionId, message })
      .then((result) => {
        if (!result.success) {
          this.writeSystemMessage(`prompt rejected: ${result.error ?? 'unknown error'}`, 'error');
        }
      })
      .catch((err: unknown) => {
        this.writeSystemMessage(`rpc/prompt error: ${(err as Error).message}`, 'error');
      });
  }

  private markDirty(ids: readonly BlockId[]): void {
    for (const id of ids) this.dirty.add(id);
    if (this.dirty.size === 0 || this.flushScheduled) return;
    this.flushScheduled = true;
    requestAnimationFrame(() => {
      this.flushScheduled = false;
      const flushIds = [...this.dirty];
      this.dirty.clear();
      this.view.apply(this.state, flushIds);
    });
  }
}
