import { describe, expect, it } from 'vitest';
import {
  auditLine,
  isInteractiveMethod,
  parseUiDialogRequest,
  UiRequestQueue,
  type UiDialogRequest,
} from '../src/ui-requests';

const confirm = (id: string): UiDialogRequest => ({
  id,
  method: 'confirm',
  title: `t-${id}`,
  message: 'm',
});

describe('UiRequestQueue', () => {
  it('is FIFO: head is the oldest pending request', () => {
    const queue = new UiRequestQueue();

    expect(queue.enqueue(confirm('a'))).toBe(true);
    expect(queue.enqueue(confirm('b'))).toBe(false);
    expect(queue.head?.id).toBe('a');
    expect(queue.depth).toBe(2);

    expect(queue.resolveHead()?.id).toBe('a');
    expect(queue.head?.id).toBe('b');
    expect(queue.resolveHead()?.id).toBe('b');
    expect(queue.resolveHead()).toBeUndefined();
  });

  it('clearAll drops everything without yielding requests', () => {
    const queue = new UiRequestQueue();
    queue.enqueue(confirm('a'));
    queue.enqueue(confirm('b'));

    queue.clearAll();

    expect(queue.depth).toBe(0);
    expect(queue.head).toBeUndefined();
  });
});

describe('parseUiDialogRequest', () => {
  it('parses each interactive kind with defensive field reads', () => {
    expect(
      parseUiDialogRequest({ id: 'u1', method: 'select', title: 'pick', options: ['a', 7, 'b'] }),
    ).toEqual({ id: 'u1', method: 'select', title: 'pick', options: ['a', 'b'] });
    expect(parseUiDialogRequest({ id: 'u2', method: 'confirm', title: 'sure?' })).toEqual({
      id: 'u2',
      method: 'confirm',
      title: 'sure?',
      message: '',
    });
    expect(parseUiDialogRequest({ id: 'u3', method: 'input', placeholder: 'login' })).toEqual({
      id: 'u3',
      method: 'input',
      title: '',
      placeholder: 'login',
    });
    expect(parseUiDialogRequest({ id: 'u4', method: 'editor', prefill: 'x' })).toEqual({
      id: 'u4',
      method: 'editor',
      title: '',
      prefill: 'x',
    });
  });

  it('rejects unanswerable shapes: missing id, unknown method, optionless select', () => {
    expect(parseUiDialogRequest({ method: 'confirm', title: 't' })).toBeNull();
    expect(parseUiDialogRequest({ id: 'u1', method: 'custom' })).toBeNull();
    expect(parseUiDialogRequest({ id: 'u1', method: 'select', options: [] })).toBeNull();
    expect(parseUiDialogRequest({ id: 'u1', method: 'select', options: [1, 2] })).toBeNull();
  });
});

describe('isInteractiveMethod', () => {
  it('accepts the four dialog kinds and nothing else', () => {
    for (const method of ['select', 'confirm', 'input', 'editor']) {
      expect(isInteractiveMethod(method)).toBe(true);
    }
    expect(isInteractiveMethod('notify')).toBe(false);
    expect(isInteractiveMethod('set_editor_text')).toBe(false);
    expect(isInteractiveMethod(undefined)).toBe(false);
  });
});

describe('auditLine', () => {
  it('keeps denied distinct from cancelled and quotes chosen values', () => {
    const request = confirm('a');
    expect(auditLine(request, { confirmed: true })).toBe('confirm "t-a" → confirmed');
    expect(auditLine(request, { confirmed: false })).toBe('confirm "t-a" → denied');
    expect(auditLine(request, { cancelled: true })).toBe('confirm "t-a" → cancelled');

    const select: UiDialogRequest = { id: 's', method: 'select', title: 'pick', options: ['x'] };
    expect(auditLine(select, { value: 'x' })).toBe('select "pick" → "x"');
  });

  it('clips long titles and values so audit lines stay one line', () => {
    const select: UiDialogRequest = {
      id: 's',
      method: 'select',
      title: 'T'.repeat(200),
      options: ['x'],
    };
    const line = auditLine(select, { value: 'V'.repeat(200) });

    expect(line).toContain('…');
    expect(line.length).toBeLessThan(280);
  });
});
