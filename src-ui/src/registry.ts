import { Terminal } from '@xterm/xterm';
import { FitAddon } from '@xterm/addon-fit';
import { WebglAddon } from '@xterm/addon-webgl';
import * as api from './api';
import { type LayoutNode, type SplitDir, leaf, leaves, removeLeaf, renderLayout, splitLeaf } from './layout';

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

/** What an open()/split() caller can choose (FT-1 slice 3, #75). */
export interface OpenOptions {
  /**
   * Explicit pane kind. Omitted means the sidecar's host-default chain
   * (request → settings → environment → pi; ADR-0013/0014) decides —
   * including for the palette's plain "new pane". An explicit value is an
   * operator choice from the palette and deliberately overrides that chain;
   * `'pi'` is now sendable for exactly that reason.
   */
  kind?: api.PaneKind;
  /** Working directory, resolved and validated sidecar-side; a bad path
   * fails the spawn loudly into the pane. */
  cwd?: string;
}

/** What the tab bar renders — presentation projection of a tab. */
export interface TabInfo {
  readonly id: string;
  readonly title: string;
  readonly active: boolean;
  /** Every pane in the tab has exited (a lone dead pane keeps its tab). */
  readonly exited: boolean;
}

/**
 * A tab is a workspace (ADR-0024): one layout tree of panes plus a focused
 * pane. The root element hosts the rendered tree and display-toggles like
 * panes themselves did pre-splits — mounted forever, xterm never unmounts.
 */
interface Tab {
  readonly id: string;
  readonly root: HTMLElement;
  tree: LayoutNode;
  activePaneId: string;
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
 * Owns pane lifecycle, the tab list with each tab's layout tree, and focus
 * (ADR-0021 P1, ADR-0024). The sidecar stays a session-keyed PTY host with
 * no layout knowledge; everything about *presentation* multiplicity lives
 * here.
 */
export class PaneRegistry {
  private readonly panes = new Map<string, Pane>();
  private readonly tabList: Tab[] = [];
  private readonly host: HTMLElement;
  private readonly onChange: () => void;
  private readonly encoder = new TextEncoder();
  private activeTabId: string | undefined;
  private nextIndex = 1;
  private nextTabId = 1;

  /**
   * The kind an unqualified spawn actually gets, from `settings/get` —
   * display-only (tab titles). Starts at the host default and is corrected
   * at startup before the first pane's title renders.
   */
  defaultKind: api.PaneKind = 'pi';

  /**
   * @param host Element tab roots are mounted into. Inactive tabs are
   * hidden, never unmounted — xterm loses scrollback and cell metrics on
   * unmount.
   * @param onChange Fired after any change a tab bar would render (open,
   * close, activate, exit, split).
   */
  constructor(host: HTMLElement, onChange: () => void) {
    this.host = host;
    this.onChange = onChange;
  }

  tabs(): readonly TabInfo[] {
    return this.tabList.map((tab) => {
      const ids = leaves(tab.tree);
      const activePane = this.panes.get(tab.activePaneId);
      const title = activePane?.title ?? '';
      return {
        id: tab.id,
        title: ids.length > 1 ? `${title} +${ids.length - 1}` : title,
        active: tab.id === this.activeTabId,
        exited: ids.every((id) => this.panes.get(id)?.state === 'exited'),
      };
    });
  }

  /** The focused pane of the active tab. */
  active(): Pane | undefined {
    const tab = this.activeTab();
    return tab ? this.panes.get(tab.activePaneId) : undefined;
  }

  /** Open a new tab with one pane and make it active. */
  async open(opts: OpenOptions = {}): Promise<Pane> {
    const root = document.createElement('div');
    root.className = 'tab-root';
    this.host.appendChild(root);

    const pane = this.createPane(opts);
    const tab: Tab = {
      id: `t-${this.nextTabId++}`,
      root,
      tree: leaf(pane.sessionId),
      activePaneId: pane.sessionId,
    };
    this.tabList.push(tab);
    this.renderTab(tab);
    this.activateTab(tab.id);

    await this.spawnInto(pane, opts);
    return pane;
  }

  /**
   * Split the focused pane of the active tab (ADR-0024): the new pane takes
   * the b-side at ratio 0.5 and receives focus. Splitting with no tab open
   * degrades to opening one.
   */
  async split(dir: SplitDir, opts: OpenOptions = {}): Promise<Pane> {
    const tab = this.activeTab();
    if (!tab) {
      return this.open(opts);
    }

    const pane = this.createPane(opts);
    tab.tree = splitLeaf(tab.tree, tab.activePaneId, dir, pane.sessionId);
    this.renderTab(tab);
    this.focusPane(pane.sessionId);
    // The surviving pane was reparented into a half-size cell; refit it
    // after the layout pass (spawnInto only fits the new pane).
    void nextFrame().then(() => this.fitVisible());
    this.onChange();

    await this.spawnInto(pane, opts);
    return pane;
  }

  activateTab(tabId: string): void {
    const tab = this.tabList.find((t) => t.id === tabId);
    if (!tab) return;
    this.activeTabId = tab.id;
    for (const t of this.tabList) {
      t.root.classList.toggle('active', t === tab);
    }
    // A root shown after display:none needs the same layout-pass-first fit
    // as a fresh mount, for the divide-by-near-zero reason in createPane.
    void nextFrame().then(() => {
      this.fitVisible();
      this.panes.get(tab.activePaneId)?.term.focus();
    });
    this.onChange();
  }

  /** Focus a pane, activating its tab if needed. */
  focusPane(sessionId: string): void {
    const tab = this.tabList.find((t) => leaves(t.tree).includes(sessionId));
    if (!tab) return;
    tab.activePaneId = sessionId;
    this.applyFocusClasses(tab);
    if (this.activeTabId !== tab.id) {
      this.activateTab(tab.id);
      return;
    }
    void nextFrame().then(() => {
      this.panes.get(sessionId)?.term.focus();
    });
    this.onChange();
  }

  /**
   * Focus the neighboring pane in global order — tab order × tree traversal
   * (ADR-0024). Identical to the pre-splits tab cycle while every tab holds
   * one pane; crossing a tab boundary activates that tab.
   */
  cyclePane(delta: 1 | -1): void {
    const order = this.tabList.flatMap((t) => leaves(t.tree));
    if (order.length < 2) return;
    const current = this.activeTab()?.activePaneId;
    const index = current === undefined ? -1 : order.indexOf(current);
    const next = order[(index + delta + order.length) % order.length];
    this.focusPane(next);
  }

  /**
   * Close a pane: kill its process (idempotent sidecar-side, so a racing
   * self-exit is fine), then remove it and collapse its split — the sibling
   * subtree takes the parent's place; the last pane closing closes the tab.
   * Unlike a self-exit — which leaves a dead pane — closing is an explicit
   * operator action, so removal is immediate.
   */
  close(sessionId: string): void {
    const pane = this.panes.get(sessionId);
    const tab = this.tabList.find((t) => leaves(t.tree).includes(sessionId));
    if (!pane || !tab) return;

    api.ptyKill({ sessionId }).catch(() => {
      // Best-effort: the pane is going away either way, and the sidecar's
      // kill is idempotent. A transport-level failure here surfaces on the
      // next interaction with a live pane.
    });
    this.disposePane(pane);

    const collapsed = removeLeaf(tab.tree, sessionId);
    if (collapsed === null) {
      this.removeTab(tab);
      return;
    }

    tab.tree = collapsed;
    if (tab.activePaneId === sessionId) {
      tab.activePaneId = leaves(collapsed)[0];
    }
    this.renderTab(tab);
    if (tab.id === this.activeTabId) {
      void nextFrame().then(() => {
        this.fitVisible();
        this.panes.get(tab.activePaneId)?.term.focus();
      });
    }
    this.onChange();
  }

  /** Close a whole tab: every pane in its tree, then the tab itself. */
  closeTab(tabId: string): void {
    const tab = this.tabList.find((t) => t.id === tabId);
    if (!tab) return;
    for (const sessionId of leaves(tab.tree)) {
      const pane = this.panes.get(sessionId);
      if (!pane) continue;
      api.ptyKill({ sessionId }).catch(() => {
        // Same best-effort contract as close().
      });
      this.disposePane(pane);
    }
    this.removeTab(tab);
  }

  /** Route a pty/output notification to its pane, if it still exists. */
  handleOutput(n: api.PtyOutputNotification): void {
    this.panes.get(n.sessionId)?.term.write(base64ToBytes(n.dataBase64));
  }

  /** A self-exit marks the pane dead but keeps it (and its scrollback). */
  handleExit(n: api.PtyExitNotification): void {
    const pane = this.panes.get(n.sessionId);
    if (!pane) return;
    pane.term.write(`\r\n\x1b[33m[process exited (${n.exitCode})]\x1b[0m\r\n`);
    pane.state = 'exited';
    this.onChange();
  }

  /** Refit every pane in the active tab; hidden tabs refit on activation. */
  fitVisible(): void {
    const tab = this.activeTab();
    if (!tab) return;
    for (const sessionId of leaves(tab.tree)) {
      this.panes.get(sessionId)?.fit.fit();
    }
  }

  private activeTab(): Tab | undefined {
    return this.tabList.find((t) => t.id === this.activeTabId);
  }

  private renderTab(tab: Tab): void {
    renderLayout(tab.root, tab.tree, (id) => {
      const pane = this.panes.get(id);
      if (!pane) throw new Error(`layout references unknown pane ${id}`);
      return pane.container;
    }, () => this.fitVisible());
    this.applyFocusClasses(tab);
  }

  private applyFocusClasses(tab: Tab): void {
    const ids = leaves(tab.tree);
    // A focus ring on a lone pane is noise; it earns its place at two.
    const multi = ids.length > 1;
    for (const id of ids) {
      this.panes.get(id)?.container.classList.toggle('focused', multi && id === tab.activePaneId);
    }
  }

  private removeTab(tab: Tab): void {
    const index = this.tabList.indexOf(tab);
    tab.root.remove();
    this.tabList.splice(index, 1);
    if (this.activeTabId === tab.id) {
      this.activeTabId = undefined;
      const neighbor = this.tabList[Math.min(index, this.tabList.length - 1)];
      if (neighbor) {
        this.activateTab(neighbor.id);
        return;
      }
    }
    this.onChange();
  }

  /** Build the pane (terminal, container, wiring) without mounting it —
   * the caller places the container via its tab's layout render. */
  private createPane(opts: OpenOptions): Pane {
    const sessionId = randomSessionId();
    const container = document.createElement('div');
    container.className = 'pane';

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
      term.loadAddon(webgl);
    } catch {
      // WebGL unavailable — the DOM renderer stays in effect.
    }

    const pane: Pane = {
      sessionId,
      title: `${opts.kind ?? this.defaultKind} ${this.nextIndex++}`,
      container,
      term,
      fit,
      state: 'running',
    };
    this.panes.set(sessionId, pane);
    return pane;
  }

  /** Spawn the pane's process once its container has had a layout pass. */
  private async spawnInto(pane: Pane, opts: OpenOptions): Promise<void> {
    // First fit() only after a layout pass — before it, cell measurement
    // divides by near-zero and reports thousands of cols, which the sidecar
    // would set as the real pty winsize.
    await nextFrame();
    pane.fit.fit();

    try {
      await api.ptySpawn({
        sessionId: pane.sessionId,
        kind: opts.kind,
        cwd: opts.cwd,
        cols: pane.term.cols,
        rows: pane.term.rows,
      });
    } catch (err) {
      // Rendered into the terminal the spawn failed to fill — where the
      // operator is already looking. A missing pi arrives here as an
      // actionable message from the sidecar (PiNotFoundException), rather
      // than as a shell that quietly is not pi.
      pane.term.write(`\r\n\x1b[31mfailed to spawn pane: ${(err as Error).message}\x1b[0m\r\n`);
      pane.state = 'exited';
      this.onChange();
      return;
    }

    // Wired only after a successful spawn — a pane whose spawn failed
    // deliberately ignores typing instead of erroring on every keystroke.
    const { sessionId, term } = pane;
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

  private disposePane(pane: Pane): void {
    pane.term.dispose();
    pane.container.remove();
    this.panes.delete(pane.sessionId);
  }
}
