import { beforeEach, describe, expect, it, vi } from 'vitest';
import { Composer, type ComposerHandlers } from '../src/composer';

function pressEnter(el: HTMLTextAreaElement, mods: Partial<KeyboardEventInit> = {}): void {
  el.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, ...mods }));
}

function visibleButtons(container: HTMLElement): string[] {
  return [...container.querySelectorAll('button')]
    .filter((b) => !b.hidden)
    .map((b) => b.textContent ?? '');
}

describe('Composer', () => {
  let handlers: { [K in keyof ComposerHandlers]: ReturnType<typeof vi.fn> };
  let composer: Composer;
  let textarea: HTMLTextAreaElement;

  beforeEach(() => {
    handlers = {
      onPrompt: vi.fn(),
      onSteer: vi.fn(),
      onFollowUp: vi.fn(),
      onAbort: vi.fn(),
    };
    composer = new Composer(handlers);
    document.body.appendChild(composer.container);
    textarea = composer.container.querySelector('textarea')!;
  });

  it('Enter submits a prompt when idle and clears the draft', () => {
    textarea.value = '  hello pi  ';
    pressEnter(textarea);

    expect(handlers.onPrompt).toHaveBeenCalledWith('hello pi');
    expect(textarea.value).toBe('');
  });

  it('Shift+Enter never submits (newline stays default behavior)', () => {
    textarea.value = 'line one';
    pressEnter(textarea, { shiftKey: true });

    expect(handlers.onPrompt).not.toHaveBeenCalled();
    expect(handlers.onFollowUp).not.toHaveBeenCalled();
  });

  it('an empty draft is never submitted', () => {
    textarea.value = '   ';
    pressEnter(textarea);

    expect(handlers.onPrompt).not.toHaveBeenCalled();
  });

  it('while streaming, Enter queues a follow-up (the non-destructive default)', () => {
    composer.setMode('streaming');
    textarea.value = 'also do this';
    pressEnter(textarea);

    expect(handlers.onFollowUp).toHaveBeenCalledWith('also do this');
    expect(handlers.onPrompt).not.toHaveBeenCalled();
    expect(handlers.onSteer).not.toHaveBeenCalled();
  });

  it('while streaming, Cmd/Ctrl+Enter steers (interrupt is explicit)', () => {
    composer.setMode('streaming');
    textarea.value = 'stop, change course';
    pressEnter(textarea, { metaKey: true });

    expect(handlers.onSteer).toHaveBeenCalledWith('stop, change course');
    expect(handlers.onFollowUp).not.toHaveBeenCalled();
  });

  it('shows send when idle; steer/follow up/abort only while streaming', () => {
    expect(visibleButtons(composer.container)).toEqual(['send']);

    composer.setMode('streaming');
    expect(visibleButtons(composer.container)).toEqual(['follow up', 'steer', 'abort']);

    composer.setMode('idle');
    expect(visibleButtons(composer.container)).toEqual(['send']);
  });

  it('abort button invokes the handler without touching the draft', () => {
    composer.setMode('streaming');
    textarea.value = 'keep me';
    const abort = [...composer.container.querySelectorAll('button')].find(
      (b) => b.textContent === 'abort',
    )!;
    abort.click();

    expect(handlers.onAbort).toHaveBeenCalledTimes(1);
    expect(textarea.value).toBe('keep me');
  });

  it('setText replaces the whole draft and stashes the clobbered one', () => {
    textarea.value = 'my careful draft';
    composer.setText('extension pushed this');

    expect(textarea.value).toBe('extension pushed this');
    const restore = composer.container.querySelector<HTMLButtonElement>('.composer-restore')!;
    expect(restore.hidden).toBe(false);

    restore.click();
    expect(textarea.value).toBe('my careful draft');
    expect(restore.hidden).toBe(true);
  });

  it('setText over an empty draft offers no restore', () => {
    composer.setText('pushed');

    expect(textarea.value).toBe('pushed');
    expect(composer.container.querySelector<HTMLButtonElement>('.composer-restore')!.hidden).toBe(
      true,
    );
  });

  it('renders queue chips as text and hides the row when empty', () => {
    composer.setQueue(['<b>steer me</b>'], ['later']);
    const queue = composer.container.querySelector<HTMLElement>('.composer-queue')!;

    expect(queue.hidden).toBe(false);
    expect(queue.querySelectorAll('.composer-chip')).toHaveLength(2);
    // Untrusted queue text stays text (ADR-0022).
    expect(queue.querySelector('b')).toBeNull();
    expect(queue.textContent).toContain('steer: <b>steer me</b>');
    expect(queue.textContent).toContain('follow-up: later');

    composer.setQueue([], []);
    expect(queue.hidden).toBe(true);
  });
});
