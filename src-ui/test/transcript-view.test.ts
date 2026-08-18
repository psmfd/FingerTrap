import { describe, expect, it } from 'vitest';
import { createTranscriptState, reduceTranscript, type TranscriptState } from '../src/transcript';
import { shouldStayPinned, TranscriptView } from '../src/transcript-view';

function feed(state: TranscriptState, ...events: Record<string, unknown>[]): string[] {
  const dirty: string[] = [];
  for (const event of events) {
    dirty.push(
      ...reduceTranscript(state, {
        eventType: typeof event.type === 'string' ? event.type : null,
        event,
        truncated: false,
      }),
    );
  }
  return dirty;
}

function mount(): { container: HTMLElement; view: TranscriptView; state: TranscriptState } {
  const container = document.createElement('div');
  document.body.appendChild(container);
  return { container, view: new TranscriptView(container), state: createTranscriptState() };
}

describe('TranscriptView', () => {
  it('creates one element per block, in transcript order', () => {
    const { container, view, state } = mount();
    const dirty = feed(
      state,
      { type: 'message_start', message: { role: 'user', content: 'hi' } },
      { type: 'tool_execution_start', toolCallId: 't1', toolName: 'bash', args: {} },
    );
    view.apply(state, dirty);

    expect(container.children).toHaveLength(2);
    expect(container.children[0].className).toContain('t-user');
    expect(container.children[1].className).toContain('t-tool-call');
  });

  it('renders event-derived strings as text, never markup (ADR-0022)', () => {
    const { container, view, state } = mount();
    const hostile = '<img src=x onerror="alert(1)"><script>bad()</script>';
    const dirty = feed(state, {
      type: 'message_start',
      message: { role: 'user', content: hostile },
    });
    view.apply(state, dirty);

    expect(container.querySelector('img')).toBeNull();
    expect(container.querySelector('script')).toBeNull();
    expect(container.textContent).toContain(hostile);
  });

  it('updates a streaming block in place: same element, same text node', () => {
    const { container, view, state } = mount();
    view.apply(
      state,
      feed(
        state,
        { type: 'message_start', message: { role: 'assistant', content: [] } },
        { type: 'message_update', assistantMessageEvent: { type: 'text_start', contentIndex: 0 } },
        {
          type: 'message_update',
          assistantMessageEvent: { type: 'text_delta', contentIndex: 0, delta: 'hel' },
        },
      ),
    );
    const el = container.children[0];
    const textNode = el.querySelector('pre')?.firstChild;
    expect(textNode?.textContent).toBe('hel');

    // Authoritative replacement (message_end) mutates the same node — the
    // jsdom-checkable proxy for "no flicker".
    view.apply(
      state,
      feed(state, {
        type: 'message_end',
        message: { role: 'assistant', content: [{ type: 'text', text: 'hello, final' }] },
      }),
    );
    expect(container.children[0]).toBe(el);
    expect(el.querySelector('pre')?.firstChild).toBe(textNode);
    expect(textNode?.textContent).toBe('hello, final');
    expect(container.children).toHaveLength(1);
  });

  it('collapses thinking blocks by default', () => {
    const { container, view, state } = mount();
    view.apply(
      state,
      feed(
        state,
        { type: 'message_start', message: { role: 'assistant', content: [] } },
        {
          type: 'message_update',
          assistantMessageEvent: { type: 'thinking_start', contentIndex: 0 },
        },
        {
          type: 'message_update',
          assistantMessageEvent: { type: 'thinking_delta', contentIndex: 0, delta: 'reasoning…' },
        },
      ),
    );

    const details = container.querySelector('details');
    expect(details).not.toBeNull();
    expect(details!.open).toBe(false);
    expect(details!.textContent).toContain('reasoning…');
  });

  it('reflects tool status transitions on the same element', () => {
    const { container, view, state } = mount();
    view.apply(
      state,
      feed(state, { type: 'tool_execution_start', toolCallId: 't1', toolName: 'bash', args: {} }),
    );
    const el = container.children[0] as HTMLElement;
    expect(el.dataset.status).toBe('running');
    expect(el.textContent).toContain('running…');

    view.apply(
      state,
      feed(state, {
        type: 'tool_execution_end',
        toolCallId: 't1',
        toolName: 'bash',
        result: { content: [{ type: 'text', text: 'ok' }] },
        isError: false,
      }),
    );
    expect(container.children[0]).toBe(el);
    expect(el.dataset.status).toBe('done');
    expect(el.textContent).toContain('done');
    expect(el.textContent).toContain('ok');
  });
});

describe('shouldStayPinned', () => {
  it('pins at the bottom, within slop, and unpins when scrolled up', () => {
    expect(shouldStayPinned(600, 1000, 400)).toBe(true); // exactly at bottom
    expect(shouldStayPinned(597, 1000, 400)).toBe(true); // sub-slop rounding
    expect(shouldStayPinned(500, 1000, 400)).toBe(false); // reading history
    expect(shouldStayPinned(0, 300, 400)).toBe(true); // content shorter than viewport
  });
});
