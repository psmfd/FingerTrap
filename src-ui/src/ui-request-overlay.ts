/**
 * Modal overlay for interactive extension-UI dialogs (FT-2 slice 4):
 * renders the head of a UiRequestQueue one dialog at a time, sibling in
 * spirit to palette.ts (overlay + box, textContent-only DOM) but its own
 * class — the interaction models diverge (a filterable command list vs
 * per-kind forms with different key contracts).
 *
 * Key contracts: Esc always answers `cancelled` (dismissing is itself an
 * answer — an unanswered dialog can hang the agent turn). confirm keeps
 * `denied` (confirmed:false) distinct from cancelled; Enter confirms,
 * N denies. select: arrows wrap, Enter or click chooses. input: Enter
 * submits the value as-is (empty included — extensions treat empty as
 * their own no-op), Esc cancels. editor: plain Enter inserts a newline
 * (multi-line body — the inverse of the composer's scheme on purpose),
 * Cmd/Ctrl+Enter submits. The backdrop deliberately does not
 * click-to-dismiss: these are must-answer dialogs, and Esc is the
 * explicit cancel.
 *
 * Every rendered string (title, message, options, placeholder, prefill)
 * is extension-controlled untrusted text — text nodes only (ADR-0022).
 */

import { UiRequestQueue, type UiDialogOutcome, type UiDialogRequest } from './ui-requests';

export interface UiRequestOverlayHandlers {
  /** One dialog answered; the caller sends the wire response + audit line. */
  onOutcome(request: UiDialogRequest, outcome: UiDialogOutcome): void;
  /** Queue drained and the overlay hid — the caller restores focus. */
  onIdle(): void;
}

export class UiRequestOverlay {
  readonly container: HTMLElement;
  private readonly titleText: Text;
  private readonly badge: HTMLElement;
  private readonly badgeText: Text;
  private readonly body: HTMLElement;
  private readonly queue = new UiRequestQueue();
  private selectedIndex = 0;

  constructor(
    host: HTMLElement,
    private readonly handlers: UiRequestOverlayHandlers,
  ) {
    this.container = el('div', 'ui-request-overlay');
    this.container.hidden = true;

    const box = el('div', 'ui-request-box');
    const head = el('div', 'ui-request-head');
    const title = el('div', 'ui-request-title');
    this.titleText = document.createTextNode('');
    title.appendChild(this.titleText);
    head.appendChild(title);
    this.badge = el('div', 'ui-request-badge');
    this.badge.hidden = true;
    this.badgeText = document.createTextNode('');
    this.badge.appendChild(this.badgeText);
    head.appendChild(this.badge);
    box.appendChild(head);
    this.body = el('div', 'ui-request-body');
    box.appendChild(this.body);
    this.container.appendChild(box);
    host.appendChild(this.container);

    box.addEventListener('keydown', (e) => this.onKey(e));
  }

  get isOpen(): boolean {
    return !this.container.hidden;
  }

  enqueue(request: UiDialogRequest): void {
    const becameHead = this.queue.enqueue(request);
    if (becameHead) this.renderHead();
    else this.updateBadge();
  }

  /** Session exit / disposal: drop everything, answer nothing. */
  clearAll(): void {
    this.queue.clearAll();
    this.container.hidden = true;
  }

  private resolve(outcome: UiDialogOutcome): void {
    const request = this.queue.resolveHead();
    if (request === undefined) return;
    this.handlers.onOutcome(request, outcome);
    if (this.queue.head !== undefined) {
      this.renderHead();
    } else {
      this.container.hidden = true;
      this.handlers.onIdle();
    }
  }

  private renderHead(): void {
    const request = this.queue.head;
    if (request === undefined) return;
    this.container.hidden = false;
    this.titleText.data = request.title.length > 0 ? request.title : request.method;
    this.updateBadge();
    this.selectedIndex = 0;
    this.body.replaceChildren();

    switch (request.method) {
      case 'select':
        this.renderSelect(request.options);
        break;
      case 'confirm':
        this.renderConfirm(request.message);
        break;
      case 'input':
        this.renderInput(request.placeholder);
        break;
      case 'editor':
        this.renderEditor(request.prefill);
        break;
    }
  }

  private updateBadge(): void {
    const waiting = this.queue.depth - 1;
    this.badge.hidden = waiting < 1;
    this.badgeText.data = waiting >= 1 ? `+${waiting} more` : '';
  }

  private renderSelect(options: readonly string[]): void {
    const list = el('ul', 'ui-request-options');
    list.tabIndex = -1;
    options.forEach((option, index) => {
      const item = el('li', 'ui-option', option);
      item.classList.toggle('selected', index === this.selectedIndex);
      item.addEventListener('click', () => this.resolve({ value: option }));
      list.appendChild(item);
    });
    this.body.appendChild(list);
    list.focus();
  }

  private renderConfirm(message: string): void {
    if (message.length > 0) {
      const pre = document.createElement('pre');
      pre.className = 'ui-request-message';
      pre.appendChild(document.createTextNode(message));
      this.body.appendChild(pre);
    }
    const buttons = el('div', 'ui-request-buttons');
    const yes = button(buttons, 'yes', () => this.resolve({ confirmed: true }));
    button(buttons, 'no', () => this.resolve({ confirmed: false }));
    this.body.appendChild(buttons);
    yes.focus();
  }

  private renderInput(placeholder: string): void {
    const input = document.createElement('input');
    input.className = 'ui-request-input';
    input.autocomplete = 'off';
    input.spellcheck = false;
    input.placeholder = placeholder;
    input.addEventListener('keydown', (e) => {
      if (e.key === 'Enter') {
        e.preventDefault();
        this.resolve({ value: input.value });
      }
    });
    this.body.appendChild(input);
    input.focus();
  }

  private renderEditor(prefill: string): void {
    const textarea = document.createElement('textarea');
    textarea.className = 'ui-request-editor';
    textarea.rows = 8;
    textarea.value = prefill;
    textarea.addEventListener('keydown', (e) => {
      if (e.key === 'Enter' && (e.metaKey || e.ctrlKey)) {
        e.preventDefault();
        this.resolve({ value: textarea.value });
      }
    });
    this.body.appendChild(textarea);
    const hint = el('div', 'ui-request-hint', 'Cmd/Ctrl+Enter submits — Esc cancels');
    this.body.appendChild(hint);
    textarea.focus();
  }

  private onKey(e: KeyboardEvent): void {
    const request = this.queue.head;
    if (request === undefined) return;

    if (e.key === 'Escape') {
      e.preventDefault();
      this.resolve({ cancelled: true });
      return;
    }

    if (request.method === 'select') {
      if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
        e.preventDefault();
        const delta = e.key === 'ArrowDown' ? 1 : -1;
        const count = request.options.length;
        this.selectedIndex = (this.selectedIndex + delta + count) % count;
        this.body.querySelectorAll('.ui-option').forEach((item, index) => {
          item.classList.toggle('selected', index === this.selectedIndex);
        });
      } else if (e.key === 'Enter') {
        e.preventDefault();
        const option = request.options[this.selectedIndex];
        if (option !== undefined) this.resolve({ value: option });
      }
      return;
    }

    if (request.method === 'confirm') {
      // Enter activates the focused button natively; the letter keys are
      // the fast path (no inputs exist in a confirm body to collide with).
      if (e.key === 'y' || e.key === 'Y') {
        e.preventDefault();
        this.resolve({ confirmed: true });
      } else if (e.key === 'n' || e.key === 'N') {
        e.preventDefault();
        this.resolve({ confirmed: false });
      }
    }
  }
}

/** textContent-only element factory (same shape as palette.ts's). */
function el(tag: string, className: string, text?: string): HTMLElement {
  const node = document.createElement(tag);
  node.className = className;
  if (text !== undefined) node.textContent = text;
  return node;
}

function button(parent: HTMLElement, label: string, onClick: () => void): HTMLButtonElement {
  const node = document.createElement('button');
  node.type = 'button';
  node.appendChild(document.createTextNode(label));
  node.addEventListener('click', onClick);
  parent.appendChild(node);
  return node;
}
