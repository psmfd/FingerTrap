import { beforeEach, describe, expect, it, vi } from 'vitest';
import { UiRequestOverlay } from '../src/ui-request-overlay';
import type { UiDialogRequest } from '../src/ui-requests';

function key(target: Element, key: string, mods: Partial<KeyboardEventInit> = {}): void {
  target.dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true, ...mods }));
}

describe('UiRequestOverlay', () => {
  let host: HTMLElement;
  let onOutcome: ReturnType<typeof vi.fn>;
  let onIdle: ReturnType<typeof vi.fn>;
  let overlay: UiRequestOverlay;

  const show = (request: UiDialogRequest): void => overlay.enqueue(request);
  const root = (): HTMLElement => host.querySelector<HTMLElement>('.ui-request-overlay')!;
  const body = (): HTMLElement => host.querySelector<HTMLElement>('.ui-request-body')!;

  beforeEach(() => {
    host = document.createElement('div');
    document.body.appendChild(host);
    onOutcome = vi.fn();
    onIdle = vi.fn();
    overlay = new UiRequestOverlay(host, { onOutcome, onIdle });
  });

  it('select: renders options as text, arrows wrap, Enter chooses', () => {
    show({ id: 'u1', method: 'select', title: 'pick', options: ['<b>one</b>', 'two'] });

    // Untrusted option text stays text (ADR-0022).
    expect(body().querySelector('b')).toBeNull();
    const items = [...body().querySelectorAll('.ui-option')];
    expect(items.map((i) => i.textContent)).toEqual(['<b>one</b>', 'two']);
    expect(items[0]!.classList.contains('selected')).toBe(true);

    key(body(), 'ArrowDown');
    key(body(), 'ArrowDown'); // wraps back to index 0
    key(body(), 'ArrowDown');
    key(body(), 'Enter');

    expect(onOutcome).toHaveBeenCalledWith(expect.objectContaining({ id: 'u1' }), {
      value: 'two',
    });
    expect(root().hidden).toBe(true);
    expect(onIdle).toHaveBeenCalledTimes(1);
  });

  it('select: clicking an option chooses it', () => {
    show({ id: 'u1', method: 'select', title: 'pick', options: ['one', 'two'] });

    body().querySelectorAll<HTMLElement>('.ui-option')[1]!.click();

    expect(onOutcome).toHaveBeenCalledWith(expect.anything(), { value: 'two' });
  });

  it('confirm: yes/no buttons and Y/N keys; denial is confirmed:false', () => {
    show({ id: 'c1', method: 'confirm', title: 'sure?', message: 'really <i>sure</i>?' });
    expect(body().querySelector('i')).toBeNull();
    expect(body().querySelector('.ui-request-message')!.textContent).toBe('really <i>sure</i>?');

    key(body(), 'n');
    expect(onOutcome).toHaveBeenCalledWith(expect.objectContaining({ id: 'c1' }), {
      confirmed: false,
    });

    show({ id: 'c2', method: 'confirm', title: 'again?', message: '' });
    const yes = [...body().querySelectorAll('button')].find((b) => b.textContent === 'yes')!;
    yes.click();
    expect(onOutcome).toHaveBeenLastCalledWith(expect.objectContaining({ id: 'c2' }), {
      confirmed: true,
    });
  });

  it('Escape answers cancelled — a different wire shape from denial', () => {
    show({ id: 'c1', method: 'confirm', title: 'sure?', message: 'm' });

    key(body(), 'Escape');

    expect(onOutcome).toHaveBeenCalledWith(expect.objectContaining({ id: 'c1' }), {
      cancelled: true,
    });
  });

  it('input: Enter submits the value as-is, empty included', () => {
    show({ id: 'i1', method: 'input', title: 'login', placeholder: 'user' });
    const input = body().querySelector('input')!;
    expect(input.placeholder).toBe('user');

    key(input, 'Enter');
    expect(onOutcome).toHaveBeenCalledWith(expect.objectContaining({ id: 'i1' }), { value: '' });

    show({ id: 'i2', method: 'input', title: 'login', placeholder: '' });
    const second = body().querySelector('input')!;
    second.value = '  spaced  ';
    key(second, 'Enter');
    // As-is: the extension owns trimming/validation semantics.
    expect(onOutcome).toHaveBeenLastCalledWith(expect.objectContaining({ id: 'i2' }), {
      value: '  spaced  ',
    });
  });

  it('editor: plain Enter stays a newline; Cmd/Ctrl+Enter submits; prefill seeds', () => {
    show({ id: 'e1', method: 'editor', title: 'edit', prefill: 'line one' });
    const textarea = body().querySelector('textarea')!;
    expect(textarea.value).toBe('line one');

    key(textarea, 'Enter');
    expect(onOutcome).not.toHaveBeenCalled();

    textarea.value = 'line one\nline two';
    key(textarea, 'Enter', { metaKey: true });
    expect(onOutcome).toHaveBeenCalledWith(expect.objectContaining({ id: 'e1' }), {
      value: 'line one\nline two',
    });
  });

  it('queues FIFO with a badge; resolving the head reveals the next', () => {
    show({ id: 'a', method: 'confirm', title: 'first', message: '' });
    show({ id: 'b', method: 'input', title: 'second', placeholder: '' });

    const badge = host.querySelector<HTMLElement>('.ui-request-badge')!;
    expect(badge.hidden).toBe(false);
    expect(badge.textContent).toBe('+1 more');
    expect(host.querySelector('.ui-request-title')!.textContent).toBe('first');

    key(body(), 'y');

    expect(root().hidden).toBe(false);
    expect(badge.hidden).toBe(true);
    expect(host.querySelector('.ui-request-title')!.textContent).toBe('second');
    expect(onIdle).not.toHaveBeenCalled();
  });

  it('clearAll hides without answering anything', () => {
    show({ id: 'a', method: 'confirm', title: 'first', message: '' });
    show({ id: 'b', method: 'confirm', title: 'second', message: '' });

    overlay.clearAll();

    expect(root().hidden).toBe(true);
    expect(onOutcome).not.toHaveBeenCalled();
    expect(onIdle).not.toHaveBeenCalled();
  });
});
