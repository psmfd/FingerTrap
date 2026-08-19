import { Terminal } from '@xterm/xterm';
import { FitAddon } from '@xterm/addon-fit';
import { WebglAddon } from '@xterm/addon-webgl';
import * as api from './api';
import {
  appendSystemBlock,
  createTranscriptState,
  reduceTranscript,
  type BlockId,
  type SystemSeverity,
  type TranscriptState,
} from './transcript';
import { TranscriptView } from './transcript-view';
import { Composer } from './composer';
import { RpcHeader, type ContextUsage, type ModelChoice } from './rpc-header';
import { ExtensionStrip } from './extension-strip';
import { UiRequestOverlay } from './ui-request-overlay';
import {
  auditLine,
  isInteractiveMethod,
  parseUiDialogRequest,
  type UiDialogOutcome,
} from './ui-requests';

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
   * error into the pane. `sessionPath` resumes a stored pi session
   * (session browser, FT-2 slice 5) — spawn-time-only in pi's protocol.
   */
  open(opts: { cwd?: string; sessionPath?: string }): Promise<void>;

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

  async open(opts: { cwd?: string; sessionPath?: string }): Promise<void> {
    await api.ptySpawn({
      sessionId: this.sessionId,
      kind: this.kind,
      cwd: opts.cwd,
      cols: this.term.cols,
      rows: this.term.rows,
      sessionPath: opts.sessionPath,
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
 * The native RPC pane (FT-2 slice 3): header strip (model/thinking
 * pickers, context meter) over a structured transcript fed by the pure
 * reducer in transcript.ts, over the host-owned composer
 * (prompt/steer/follow-up/abort — ADR-0025 decision 6). Everything
 * relayed is untrusted content: rendering is text nodes only, never
 * markup (ADR-0022), and DOM flushes are rAF-coalesced so token-rate
 * notification bursts cost one layout pass per frame.
 */
export class RpcPaneContent implements PaneContent {
  readonly container: HTMLElement;
  private readonly output: HTMLElement;
  private readonly sessionId: string;
  private readonly state: TranscriptState = createTranscriptState();
  private readonly view: TranscriptView;
  private readonly dirty = new Set<BlockId>();
  private readonly composer: Composer;
  private readonly header: RpcHeader;
  private readonly strip: ExtensionStrip;
  private readonly overlay: UiRequestOverlay;
  private flushScheduled = false;

  constructor(sessionId: string) {
    this.sessionId = sessionId;
    this.container = document.createElement('div');
    this.container.className = 'pane rpc-pane';

    this.header = new RpcHeader({
      onSetModel: (provider, modelId) =>
        this.sendControl(api.rpcSetModel({ sessionId, provider, modelId }), 'set model'),
      onSetThinkingLevel: (level) =>
        this.sendControl(api.rpcSetThinkingLevel({ sessionId, level }), 'set thinking level'),
    });
    this.container.appendChild(this.header.container);

    this.output = document.createElement('div');
    this.output.className = 'rpc-output';
    this.container.appendChild(this.output);
    this.view = new TranscriptView(this.output);

    this.composer = new Composer({
      onPrompt: (message) => {
        this.view.forcePin();
        this.sendControl(api.rpcPrompt({ sessionId, message }), 'prompt');
      },
      onSteer: (message) => {
        this.view.forcePin();
        this.sendControl(api.rpcSteer({ sessionId, message }), 'steer');
      },
      onFollowUp: (message) => {
        this.view.forcePin();
        this.sendControl(api.rpcFollowUp({ sessionId, message }), 'follow-up');
      },
      onAbort: () => this.sendControl(api.rpcAbort({ sessionId }), 'abort'),
    });
    this.strip = new ExtensionStrip();
    this.container.appendChild(this.strip.above);
    this.container.appendChild(this.composer.container);
    this.container.appendChild(this.strip.below);

    this.overlay = new UiRequestOverlay(this.container, {
      onOutcome: (request, outcome) => {
        // The audit line is the durable record of the decision (a guard
        // confirm is security-relevant); the modal itself is ephemeral.
        this.writeSystemMessage(`[ui: ${auditLine(request, outcome)}]`, 'info');
        this.sendUiResponse(request.id, outcome);
      },
      onIdle: () => this.composer.focus(),
    });
  }

  async open(opts: { cwd?: string; sessionPath?: string }): Promise<void> {
    await api.rpcSpawn({
      sessionId: this.sessionId,
      cwd: opts.cwd,
      sessionPath: opts.sessionPath,
    });
    // Seed the header/composer from session state; failures degrade the
    // chrome, not the pane, so this does not gate open().
    void this.seed(opts.sessionPath !== undefined);
  }

  resize(): void {
    // Plain CSS flex layout — nothing to measure. Deliberately cheap: the
    // ResizeObserver path calls this at animation-frame rate during
    // gutter drags.
  }

  focus(): void {
    this.composer.focus();
  }

  /** One relayed event: transcript fold + control-plane wiring. */
  appendEvent(n: api.RpcEventNotification): void {
    try {
      this.markDirty(reduceTranscript(this.state, n));
      this.handleControlEvent(n);
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
    // A dialog answered into a dying session would be silently dropped by
    // pi anyway — drop the queue instead of leaving a stale modal up.
    this.overlay.clearAll();
    await api.rpcKill({ sessionId: this.sessionId });
  }

  dispose(): void {
    this.overlay.clearAll();
    this.container.remove();
  }

  /**
   * Composer/header mode and readouts, driven by the same event stream
   * the transcript folds. `agent_settled` is the sole idle boundary —
   * `turn_end` fires between queued turns and would flicker the controls.
   */
  private handleControlEvent(n: api.RpcEventNotification): void {
    const event = asRecord(n.event);
    switch (n.eventType) {
      case 'turn_start':
        this.composer.setMode('streaming');
        break;
      case 'agent_settled':
        this.composer.setMode('idle');
        void this.refreshStats();
        break;
      case 'queue_update':
        this.composer.setQueue(asStringArray(event?.steering), asStringArray(event?.followUp));
        break;
      case 'thinking_level_changed':
        if (typeof event?.level === 'string') this.header.setActiveThinkingLevel(event.level);
        break;
      case 'session_info_changed':
        void this.refreshState();
        break;
      case 'extension_ui_request':
        this.handleUiRequest(n, event);
        break;
      default:
        break;
    }
  }

  /**
   * The extension UI channel (FT-2 slice 4). Interactive kinds queue into
   * the modal overlay and MUST end in a response — an unanswered dialog
   * can hang the agent turn forever (docs/rpc-contract.md), so every
   * unrenderable path below still answers `cancelled` when an id exists.
   */
  private handleUiRequest(
    n: api.RpcEventNotification,
    event: Record<string, unknown> | null,
  ): void {
    if (n.truncated) {
      // The payload is gone; the sidecar's marker preserves originalId/
      // originalMethod for this event type so the dialog stays answerable.
      const id = typeof event?.originalId === 'string' ? event.originalId : null;
      const method = event?.originalMethod;
      if (id !== null && isInteractiveMethod(method)) {
        this.sendUiResponse(id, { cancelled: true });
        this.writeSystemMessage(`[ui: oversized ${method} request auto-cancelled]`, 'error');
      }
      return;
    }
    if (event === null) return;

    const method = event.method;
    if (isInteractiveMethod(method)) {
      const request = parseUiDialogRequest(event);
      if (request !== null) {
        this.overlay.enqueue(request);
      } else if (typeof event.id === 'string' && event.id.length > 0) {
        this.sendUiResponse(event.id, { cancelled: true });
        this.writeSystemMessage(`[ui: malformed ${method} request auto-cancelled]`, 'error');
      }
      return;
    }

    switch (method) {
      case 'notify':
        this.markDirty([
          appendSystemBlock(this.state, asText(event.message), notifySeverity(event.notifyType)),
        ]);
        break;
      case 'setTitle':
        // Live tab rename needs registry plumbing that does not exist yet
        // (#133); the transcript records the request meanwhile.
        this.writeSystemMessage(`[ui: title set to "${asText(event.title)}"]`, 'info');
        break;
      case 'set_editor_text':
        this.composer.setText(asText(event.text));
        break;
      case 'setStatus':
        if (typeof event.statusKey === 'string') {
          this.strip.setStatus(
            event.statusKey,
            typeof event.statusText === 'string' ? event.statusText : undefined,
          );
        }
        break;
      case 'setWidget':
        if (typeof event.widgetKey === 'string') {
          this.strip.setWidget(
            event.widgetKey,
            Array.isArray(event.widgetLines) ? asStringArray(event.widgetLines) : undefined,
            event.widgetPlacement === 'belowEditor' ? 'belowEditor' : 'aboveEditor',
          );
        }
        break;
      default:
        // Fire-and-forget drift renders as a note; an unknown *dialog*
        // kind cannot be safely auto-answered (we cannot know its
        // response shape), so nothing more to do here.
        this.writeSystemMessage(`[ui: unhandled request method "${asText(method)}"]`, 'info');
        break;
    }
  }

  private sendUiResponse(requestId: string, outcome: UiDialogOutcome): void {
    api
      .rpcExtensionUiResponse({ sessionId: this.sessionId, requestId, ...outcome })
      .catch((err: unknown) => {
        this.writeSystemMessage(`ui response error: ${(err as Error).message}`, 'error');
      });
  }

  /**
   * No local echo on submit: pi echoes every user message as
   * message_start/end (docs/rpc-contract.md), and that echo is the
   * transcript's single source of truth for ordering. The ack only ever
   * adds a rejection line.
   */
  private sendControl(
    call: Promise<{ success: boolean; error?: string | null }>,
    what: string,
  ): void {
    call
      .then((result) => {
        if (!result.success) {
          this.writeSystemMessage(`${what} rejected: ${result.error ?? 'unknown error'}`, 'error');
        }
      })
      .catch((err: unknown) => {
        this.writeSystemMessage(`${what} error: ${(err as Error).message}`, 'error');
      });
  }

  /**
   * Resumed-session history (FT-2 slice 5): pi replays nothing after a
   * spawn-time `--session`, so the transcript starts from get_messages —
   * the whole list; no `since` cursor exists. Each stored message folds
   * through the same reducer live events use, as a synthetic
   * message_start (user echo) or message_start+message_end pair
   * (assistant text/thinking materialize at end). Historical tool calls
   * have no tool_execution_* lifecycle to replay and render as part of
   * the assistant text only.
   */
  private async seedHistory(): Promise<void> {
    const result = await api.rpcGetMessages({ sessionId: this.sessionId });
    const messages = asRecord(result.data)?.messages;
    if (!result.success || !Array.isArray(messages)) return;
    for (const item of messages) {
      const message = asRecord(item);
      if (message === null) continue;
      const fold = (eventType: string): void => {
        this.markDirty(
          reduceTranscript(this.state, { eventType, event: { message }, truncated: false }),
        );
      };
      if (message.role === 'user') {
        fold('message_start');
      } else if (message.role === 'assistant') {
        fold('message_start');
        fold('message_end');
      }
    }
  }

  /** Header/composer seeding after spawn; each read degrades alone. */
  private async seed(resumed = false): Promise<void> {
    const { sessionId } = this;
    try {
      if (resumed) {
        await this.seedHistory();
      }
      const models = await api.rpcGetAvailableModels({ sessionId });
      const list = asRecord(models.data)?.models;
      if (models.success && Array.isArray(list)) {
        this.header.setModels(
          list.flatMap((m: unknown): ModelChoice[] => {
            const model = asRecord(m);
            if (typeof model?.id !== 'string' || typeof model.provider !== 'string') return [];
            return [
              {
                provider: model.provider,
                id: model.id,
                name: typeof model.name === 'string' ? model.name : model.id,
              },
            ];
          }),
        );
      }
      const levels = await api.rpcGetAvailableThinkingLevels({ sessionId });
      const levelList = asRecord(levels.data)?.levels;
      if (levels.success && Array.isArray(levelList)) {
        this.header.setThinkingLevels(asStringArray(levelList));
      }
      await this.refreshState();
      await this.refreshStats();
    } catch (err) {
      this.writeSystemMessage(`state readout error: ${(err as Error).message}`, 'error');
    }
  }

  private async refreshState(): Promise<void> {
    const result = await api.rpcGetState({ sessionId: this.sessionId });
    const data = asRecord(result.data);
    if (!result.success || data === null) return;
    this.composer.setMode(data.isStreaming === true ? 'streaming' : 'idle');
    const model = asRecord(data.model);
    if (typeof model?.id === 'string') this.header.setActiveModel(model.id);
    if (typeof data.thinkingLevel === 'string') {
      this.header.setActiveThinkingLevel(data.thinkingLevel);
    }
  }

  private async refreshStats(): Promise<void> {
    const result = await api.rpcGetSessionStats({ sessionId: this.sessionId });
    const usage = asRecord(asRecord(result.data)?.contextUsage);
    if (!result.success) return;
    this.header.setContextUsage(
      usage === null
        ? null
        : ({
            percent: typeof usage.percent === 'number' ? usage.percent : null,
            tokens: typeof usage.tokens === 'number' ? usage.tokens : null,
            contextWindow: typeof usage.contextWindow === 'number' ? usage.contextWindow : null,
          } satisfies ContextUsage),
    );
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

function asRecord(value: unknown): Record<string, unknown> | null {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : null;
}

function asStringArray(value: unknown): string[] {
  return Array.isArray(value)
    ? value.filter((item): item is string => typeof item === 'string')
    : [];
}

function asText(value: unknown): string {
  return typeof value === 'string' ? value : '';
}

function notifySeverity(value: unknown): SystemSeverity {
  // Unrecognized future notifyType values render as info — the
  // fail-open-on-display posture the reducer uses for unknown events.
  return value === 'warning' || value === 'error' ? value : 'info';
}
