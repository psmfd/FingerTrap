import { Terminal } from '@xterm/xterm';
import { FitAddon } from '@xterm/addon-fit';
import { WebglAddon } from '@xterm/addon-webgl';
import * as api from './api';

/**
 * A pane that exited stays visible (badge + scrollback) rather than
 * disappearing — ADR-0013's fail-loud posture: a pi that dies at startup
 * leaves its error where the operator is looking. Only an explicit close
 * removes it.
 */
export type PaneState = 'running' | 'exited';

export interface Pane {
  readonly sessionId: string;
  readonly title: string;
  readonly container: HTMLElement;
  readonly term: Terminal;
  readonly fit: FitAddon;
  state: PaneState;
}

function randomSessionId(): string {
  // Every Tauri WebView provides crypto; the fallback covers only the
  // (older-WebKit) case where randomUUID specifically is missing. No
  // Math.random anywhere — CodeQL js/insecure-randomness, and session ids
  // should be unguessable on principle even on a private stdio channel.
  if (typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID();
  }
  const bytes = new Uint8Array(16);
  crypto.getRandomValues(bytes);
  return `s-${Array.from(bytes, (b) => b.toString(16).padStart(2, '0')).join('')}`;
}

function bytesToBase64(bytes: Uint8Array): string {
  const CHUNK = 0x8000;
  const parts: string[] = [];
  for (let i = 0; i < bytes.length; i += CHUNK) {
    parts.push(String.fromCharCode(...bytes.subarray(i, i + CHUNK)));
  }
  return btoa(parts.join(''));
}

function base64ToBytes(b64: string): Uint8Array {
  const binary = atob(b64);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) {
    bytes[i] = binary.charCodeAt(i);
  }
  return bytes;
}

/** Wait one animation frame so cell-measurement DOM has had a layout pass. */
function nextFrame(): Promise<void> {
  return new Promise((resolve) => requestAnimationFrame(() => resolve()));
}

/**
 * Owns the mapping sessionId → pane, tab order, and focus (ADR-0021 P1). The
 * sidecar stays a session-keyed PTY host with no layout knowledge; everything
 * about *presentation* multiplicity lives here.
 */
export class PaneRegistry {
  private readonly panes: Pane[] = [];
  private readonly host: HTMLElement;
  private readonly onChange: () => void;
  private readonly encoder = new TextEncoder();
  private activeId: string | undefined;
  private nextIndex = 1;

  /**
   * @param host Element pane containers are mounted into. Inactive panes are
   * hidden, never unmounted — xterm loses scrollback and cell metrics on
   * unmount.
   * @param onChange Fired after any change a tab bar would render (open,
   * close, activate, exit).
   */
  constructor(host: HTMLElement, onChange: () => void) {
    this.host = host;
    this.onChange = onChange;
  }

  list(): readonly Pane[] {
    return this.panes;
  }

  active(): Pane | undefined {
    return this.panes.find((p) => p.sessionId === this.activeId);
  }

  /**
   * Open a new pane and make it active.
   *
   * `kind` is omitted for a default pane so the sidecar's host-default chain
   * (request → settings → environment → pi; ADR-0013/0014) stays
   * authoritative — there is no way to ask for pi *harder*, so no caller here
   * ever sends `kind: "pi"`. The title assumes the standard host default;
   * settings-aware titles arrive with slice 3's `settings/get`.
   */
  async open(kind?: 'shell'): Promise<Pane> {
    const sessionId = randomSessionId();
    const container = document.createElement('div');
    container.className = 'pane';
    this.host.appendChild(container);

    const term = new Terminal({
      fontFamily: 'ui-monospace, SFMono-Regular, Menlo, monospace',
      fontSize: 13,
      theme: { background: '#000000' },
      cursorBlink: true,
      allowProposedApi: true,
    });
    const fit = new FitAddon();
    term.loadAddon(fit);
    term.open(container);

    // The DOM renderer (xterm's default) leaves stale rows on screen after
    // full-screen erases (#78) — our display:none pane toggling stresses its
    // known repaint gaps. Prefer the WebGL renderer; on context loss or
    // WebGL-less environments fall back to the DOM renderer by disposing the
    // addon rather than freezing the pane. Addon lifetime is owned by the
    // terminal: term.dispose() disposes loaded addons.
    try {
      const webgl = new WebglAddon();
      webgl.onContextLoss(() => {
        webgl.dispose();
      });
      term.loadAddon(webgl);
    } catch {
      // WebGL unavailable — the DOM renderer stays in effect.
    }

    const pane: Pane = {
      sessionId,
      title: `${kind ?? 'pi'} ${this.nextIndex++}`,
      container,
      term,
      fit,
      state: 'running',
    };
    this.panes.push(pane);
    this.setActive(pane);

    // First fit() only after a layout pass — before it, cell measurement
    // divides by near-zero and reports thousands of cols, which the sidecar
    // would set as the real pty winsize.
    await nextFrame();
    fit.fit();

    try {
      await api.ptySpawn({ sessionId, kind, cols: term.cols, rows: term.rows });
    } catch (err) {
      // Rendered into the terminal the spawn failed to fill — where the
      // operator is already looking. A missing pi arrives here as an
      // actionable message from the sidecar (PiNotFoundException), rather
      // than as a shell that quietly is not pi.
      term.write(`\r\n\x1b[31mfailed to spawn pane: ${(err as Error).message}\x1b[0m\r\n`);
      pane.state = 'exited';
      this.onChange();
      return pane;
    }

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

    this.onChange();
    return pane;
  }

  activate(sessionId: string): void {
    const pane = this.panes.find((p) => p.sessionId === sessionId);
    if (!pane) return;
    this.setActive(pane);
    this.onChange();
  }

  /**
   * Close a pane: kill its process (idempotent sidecar-side, so a racing
   * self-exit is fine), then remove it. Unlike a self-exit — which leaves a
   * dead tab — closing is an explicit operator action, so removal is
   * immediate.
   */
  close(sessionId: string): void {
    const idx = this.panes.findIndex((p) => p.sessionId === sessionId);
    if (idx === -1) return;
    const pane = this.panes[idx];

    api.ptyKill({ sessionId }).catch(() => {
      // Best-effort: the pane is going away either way, and the sidecar's
      // kill is idempotent. A transport-level failure here surfaces on the
      // next interaction with a live pane.
    });

    pane.term.dispose();
    pane.container.remove();
    this.panes.splice(idx, 1);

    if (this.activeId === sessionId) {
      const neighbor = this.panes[Math.min(idx, this.panes.length - 1)];
      this.activeId = undefined;
      if (neighbor) {
        this.setActive(neighbor);
      }
    }
    this.onChange();
  }

  /** Route a pty/output notification to its pane, if it still exists. */
  handleOutput(n: api.PtyOutputNotification): void {
    const pane = this.panes.find((p) => p.sessionId === n.sessionId);
    pane?.term.write(base64ToBytes(n.dataBase64));
  }

  /** A self-exit marks the tab dead but keeps it (and its scrollback). */
  handleExit(n: api.PtyExitNotification): void {
    const pane = this.panes.find((p) => p.sessionId === n.sessionId);
    if (!pane) return;
    pane.term.write(`\r\n\x1b[33m[process exited (${n.exitCode})]\x1b[0m\r\n`);
    pane.state = 'exited';
    this.onChange();
  }

  /** Refit the active pane; hidden panes refit on activation instead. */
  fitActive(): void {
    this.active()?.fit.fit();
  }

  private setActive(pane: Pane): void {
    if (this.activeId === pane.sessionId) return;
    for (const p of this.panes) {
      p.container.classList.toggle('active', p === pane);
    }
    this.activeId = pane.sessionId;
    // A container shown after display:none needs the same layout-pass-first
    // fit as a fresh mount, for the same divide-by-near-zero reason.
    void nextFrame().then(() => {
      pane.fit.fit();
      pane.term.focus();
    });
  }
}
