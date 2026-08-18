/**
 * Host-owned composer for the native RPC pane (FT-2 slice 3b, ADR-0025
 * decision 6): pi has no draft buffer over RPC, so the draft lives here
 * and submits via prompt/steer/follow_up. No api.ts import — handlers
 * are injected, which is also the vitest seam.
 *
 * Mode is driven by the turn boundary the contract guarantees:
 * `turn_start` → streaming, `agent_settled` → idle (`turn_end` fires
 * mid-flight between queued turns and would flicker the controls).
 * While streaming, Enter defaults to the non-destructive follow-up;
 * steer (which interrupts the in-flight turn) requires its explicit
 * button or Cmd/Ctrl+Enter.
 */

export type ComposerMode = 'idle' | 'streaming';

export interface ComposerHandlers {
  onPrompt(message: string): void;
  onSteer(message: string): void;
  onFollowUp(message: string): void;
  onAbort(): void;
}

export class Composer {
  readonly container: HTMLElement;
  private readonly textarea: HTMLTextAreaElement;
  private readonly sendButton: HTMLButtonElement;
  private readonly steerButton: HTMLButtonElement;
  private readonly followUpButton: HTMLButtonElement;
  private readonly abortButton: HTMLButtonElement;
  private readonly queueEl: HTMLElement;
  private mode: ComposerMode = 'idle';

  constructor(private readonly handlers: ComposerHandlers) {
    this.container = document.createElement('div');
    this.container.className = 'rpc-composer';

    this.queueEl = document.createElement('div');
    this.queueEl.className = 'composer-queue';
    this.queueEl.hidden = true;
    this.container.appendChild(this.queueEl);

    const row = document.createElement('div');
    row.className = 'composer-row';
    this.container.appendChild(row);

    this.textarea = document.createElement('textarea');
    this.textarea.rows = 2;
    this.textarea.placeholder = 'prompt — Enter sends, Shift+Enter for a newline';
    this.textarea.addEventListener('keydown', (e) => this.onKeydown(e));
    row.appendChild(this.textarea);

    const buttons = document.createElement('div');
    buttons.className = 'composer-buttons';
    row.appendChild(buttons);

    this.sendButton = this.button(buttons, 'send', () => this.submit('prompt'));
    this.followUpButton = this.button(buttons, 'follow up', () => this.submit('followUp'));
    this.steerButton = this.button(buttons, 'steer', () => this.submit('steer'));
    this.abortButton = this.button(buttons, 'abort', () => this.handlers.onAbort());
    this.abortButton.classList.add('composer-abort');

    this.applyMode();
  }

  setMode(mode: ComposerMode): void {
    if (this.mode === mode) return;
    this.mode = mode;
    this.applyMode();
  }

  /** Queue snapshot from pi's queue_update (untrusted text — textContent only). */
  setQueue(steering: readonly string[], followUp: readonly string[]): void {
    this.queueEl.textContent = '';
    const total = steering.length + followUp.length;
    this.queueEl.hidden = total === 0;
    if (total === 0) return;
    for (const [label, messages] of [
      ['steer', steering],
      ['follow-up', followUp],
    ] as const) {
      for (const message of messages) {
        const chip = document.createElement('span');
        chip.className = 'composer-chip';
        chip.appendChild(document.createTextNode(`${label}: ${message}`));
        this.queueEl.appendChild(chip);
      }
    }
  }

  focus(): void {
    this.textarea.focus();
  }

  private onKeydown(e: KeyboardEvent): void {
    if (e.key !== 'Enter' || e.shiftKey) return;
    e.preventDefault();
    if (this.mode === 'idle') {
      this.submit('prompt');
    } else if (e.metaKey || e.ctrlKey) {
      this.submit('steer');
    } else {
      // The safe default mid-stream: queue, never interrupt.
      this.submit('followUp');
    }
  }

  private submit(action: 'prompt' | 'steer' | 'followUp'): void {
    const message = this.textarea.value.trim();
    if (!message) return;
    this.textarea.value = '';
    if (action === 'prompt') this.handlers.onPrompt(message);
    else if (action === 'steer') this.handlers.onSteer(message);
    else this.handlers.onFollowUp(message);
  }

  private applyMode(): void {
    const streaming = this.mode === 'streaming';
    this.sendButton.hidden = streaming;
    this.steerButton.hidden = !streaming;
    this.followUpButton.hidden = !streaming;
    this.abortButton.hidden = !streaming;
    this.container.classList.toggle('composer-streaming', streaming);
  }

  private button(parent: HTMLElement, label: string, onClick: () => void): HTMLButtonElement {
    const el = document.createElement('button');
    el.type = 'button';
    el.appendChild(document.createTextNode(label));
    el.addEventListener('click', onClick);
    parent.appendChild(el);
    return el;
  }
}
