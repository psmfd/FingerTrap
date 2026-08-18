import { Terminal } from '@xterm/xterm';
import { FitAddon } from '@xterm/addon-fit';
import { WebglAddon } from '@xterm/addon-webgl';
import * as api from './api';

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
      api.ptyWrite({ sessionId, dataBase64: bytesToBase64(this.encoder.encode(data)) }).catch((err: unknown) => {
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
 * The native RPC pane's walking skeleton (FT-2 slice 2): renders raw
 * relayed pi events as text lines, with a provisional single-line prompt
 * input so streaming is demonstrable — slice 3's composer replaces it.
 * Everything relayed is untrusted content: rendering is text nodes only,
 * never markup (ADR-0022), and DOM appends are rAF-coalesced so
 * token-rate notification bursts cost one layout pass per frame.
 */
export class RpcPaneContent implements PaneContent {
  readonly container: HTMLElement;
  private readonly output: HTMLElement;
  private readonly input: HTMLInputElement;
  private readonly sessionId: string;
  private pending: { text: string; className?: string }[] = [];
  private flushScheduled = false;

  constructor(sessionId: string) {
    this.sessionId = sessionId;
    this.container = document.createElement('div');
    this.container.className = 'pane rpc-pane';

    this.output = document.createElement('div');
    this.output.className = 'rpc-output';
    this.container.appendChild(this.output);

    const composer = document.createElement('form');
    composer.className = 'rpc-composer';
    this.input = document.createElement('input');
    this.input.type = 'text';
    this.input.placeholder = 'prompt (provisional — slice 3 composer replaces this)';
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

  /** One relayed event, appended as its raw JSON line. */
  appendEvent(n: api.RpcEventNotification): void {
    this.enqueue({ text: JSON.stringify(n.event) });
  }

  writeSystemMessage(text: string, kind: 'info' | 'error'): void {
    this.enqueue({ text, className: kind === 'error' ? 'rpc-system-error' : 'rpc-system-info' });
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
    this.enqueue({ text: `> ${message}`, className: 'rpc-system-info' });
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

  private enqueue(line: { text: string; className?: string }): void {
    this.pending.push(line);
    if (this.flushScheduled) return;
    this.flushScheduled = true;
    requestAnimationFrame(() => {
      this.flushScheduled = false;
      this.flush();
    });
  }

  private flush(): void {
    const lines = this.pending;
    this.pending = [];
    for (const line of lines) {
      // Text nodes only — a text node never parses HTML, so untrusted
      // relayed content cannot become markup (ADR-0022).
      const el = document.createElement('div');
      if (line.className) el.className = line.className;
      el.appendChild(document.createTextNode(line.text));
      this.output.appendChild(el);
    }
    this.output.scrollTop = this.output.scrollHeight;
  }
}
