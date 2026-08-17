/**
 * Command palette (FT-1 slice 3, ADR-0021 U1): hand-rolled overlay — a
 * filter input over a command list, plus a free-text input mode for commands
 * that need one argument (the "new pane in directory…" path, #75). No
 * framework, textContent-only DOM, same sink discipline as status.ts.
 *
 * The palette renders only its own static command titles today, but every
 * text node still goes through the textContent factory: if a future command
 * title ever carries provider-sourced text, this layer must not be the one
 * that starts interpreting markup.
 */

export interface Command {
  title: string;
  run: (palette: Palette) => void;
}

type Mode = { kind: 'commands' } | { kind: 'input'; placeholder: string; submit: (value: string) => void };

export class Palette {
  private readonly root: HTMLElement;
  private readonly input: HTMLInputElement;
  private readonly list: HTMLElement;
  private readonly commands: readonly Command[];
  private readonly onHide: () => void;
  private mode: Mode = { kind: 'commands' };
  private filtered: readonly Command[] = [];
  private selected = 0;
  private visible = false;

  constructor(host: HTMLElement, commands: readonly Command[], onHide: () => void = () => {}) {
    this.commands = commands;
    this.onHide = onHide;

    this.root = el('div', 'palette-overlay');
    this.root.hidden = true;
    const box = el('div', 'palette-box');
    this.input = document.createElement('input');
    this.input.className = 'palette-input';
    this.input.autocomplete = 'off';
    this.input.spellcheck = false;
    box.appendChild(this.input);
    this.list = el('ul', 'palette-list');
    box.appendChild(this.list);
    this.root.appendChild(box);
    host.appendChild(this.root);

    // Clicking the dimmed backdrop (not the box) dismisses, like Escape.
    this.root.addEventListener('mousedown', (e) => {
      if (e.target === this.root) this.hide();
    });
    this.input.addEventListener('input', () => {
      if (this.mode.kind === 'commands') this.applyFilter();
    });
    this.input.addEventListener('keydown', (e) => this.onKey(e));
  }

  toggle(): void {
    if (this.visible) this.hide();
    else this.show();
  }

  /**
   * Switch to free-text argument entry. Used by commands that need one value
   * (a directory path); Enter submits, Escape dismisses the whole palette.
   */
  promptInput(placeholder: string, submit: (value: string) => void): void {
    this.mode = { kind: 'input', placeholder, submit };
    this.input.value = '';
    this.input.placeholder = placeholder;
    this.list.replaceChildren();
    if (!this.visible) this.show();
    this.input.focus();
  }

  private show(): void {
    this.mode = { kind: 'commands' };
    this.visible = true;
    this.root.hidden = false;
    this.input.value = '';
    this.input.placeholder = 'type a command…';
    this.applyFilter();
    this.input.focus();
  }

  private hide(): void {
    if (!this.visible) return;
    this.visible = false;
    this.root.hidden = true;
    this.onHide();
  }

  private applyFilter(): void {
    const needle = this.input.value.trim().toLowerCase();
    this.filtered = needle === ''
      ? this.commands
      : this.commands.filter((c) => c.title.toLowerCase().includes(needle));
    this.selected = 0;
    this.renderList();
  }

  private renderList(): void {
    this.list.replaceChildren();
    if (this.filtered.length === 0) {
      this.list.appendChild(el('li', 'palette-none', 'no matching command'));
      return;
    }
    this.filtered.forEach((command, i) => {
      const item = el('li', 'palette-item', command.title);
      item.classList.toggle('selected', i === this.selected);
      item.addEventListener('click', () => this.runCommand(command));
      this.list.appendChild(item);
    });
  }

  private runCommand(command: Command): void {
    // Run before hiding: a command may immediately re-enter input mode
    // (promptInput), and hide() must not clobber that.
    const modeBefore = this.mode;
    command.run(this);
    if (this.mode === modeBefore) this.hide();
  }

  private onKey(e: KeyboardEvent): void {
    if (e.key === 'Escape') {
      e.preventDefault();
      this.hide();
      return;
    }
    if (this.mode.kind === 'input') {
      if (e.key === 'Enter') {
        e.preventDefault();
        const value = this.input.value.trim();
        const submit = this.mode.submit;
        this.hide();
        if (value !== '') submit(value);
      }
      return;
    }
    if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
      e.preventDefault();
      if (this.filtered.length === 0) return;
      const delta = e.key === 'ArrowDown' ? 1 : -1;
      this.selected = (this.selected + delta + this.filtered.length) % this.filtered.length;
      this.renderList();
      return;
    }
    if (e.key === 'Enter') {
      e.preventDefault();
      const command = this.filtered[this.selected];
      if (command) this.runCommand(command);
    }
  }
}

/** textContent-only element factory — the palette's single DOM sink. */
function el(tag: string, className: string, text?: string): HTMLElement {
  const node = document.createElement(tag);
  node.className = className;
  if (text !== undefined) {
    node.textContent = text;
  }
  return node;
}
