import { describe, expect, it } from 'vitest';
import {
  appendSystemBlock,
  createTranscriptState,
  reduceTranscript,
  type Block,
  type TranscriptState,
} from '../src/transcript';

/** Wire-shaped notification helper (matches api.RpcEventNotification). */
function n(event: Record<string, unknown>, truncated = false) {
  return { eventType: typeof event.type === 'string' ? event.type : null, event, truncated };
}

function feed(state: TranscriptState, ...events: Record<string, unknown>[]): void {
  for (const event of events) reduceTranscript(state, n(event));
}

function kinds(state: TranscriptState): string[] {
  return state.blocks.map((b) => b.kind);
}

function only<K extends Block['kind']>(
  state: TranscriptState,
  kind: K,
): Extract<Block, { kind: K }> {
  const matches = state.blocks.filter((b) => b.kind === kind);
  expect(matches).toHaveLength(1);
  return matches[0] as Extract<Block, { kind: K }>;
}

describe('transcript reducer', () => {
  it('renders the user echo once: message_start appends, message_end is a no-op', () => {
    const state = createTranscriptState();
    const message = { role: 'user', content: 'hello pi', timestamp: 1 };
    feed(state, { type: 'message_start', message }, { type: 'message_end', message });

    expect(kinds(state)).toEqual(['user']);
    expect(only(state, 'user').text).toBe('hello pi');
  });

  it('extracts text and image markers from array-form user content', () => {
    const state = createTranscriptState();
    feed(state, {
      type: 'message_start',
      message: {
        role: 'user',
        content: [
          { type: 'text', text: 'look at this' },
          { type: 'image', data: 'xxxx', mimeType: 'image/png' },
        ],
      },
    });

    expect(only(state, 'user').text).toBe('look at this\n[image]');
  });

  it('accumulates deltas by contentIndex, then message_end replaces authoritatively', () => {
    const state = createTranscriptState();
    feed(
      state,
      { type: 'message_start', message: { role: 'assistant', content: [] } },
      { type: 'message_update', assistantMessageEvent: { type: 'text_start', contentIndex: 0 } },
      {
        type: 'message_update',
        assistantMessageEvent: { type: 'text_delta', contentIndex: 0, delta: 'hel' },
      },
      {
        type: 'message_update',
        assistantMessageEvent: { type: 'text_delta', contentIndex: 0, delta: 'lo' },
      },
    );
    expect(only(state, 'assistant-text').text).toBe('hello');
    expect(only(state, 'assistant-text').provisional).toBe(true);

    feed(state, {
      type: 'message_end',
      message: { role: 'assistant', content: [{ type: 'text', text: 'hello, corrected' }] },
    });
    expect(only(state, 'assistant-text').text).toBe('hello, corrected');
    expect(only(state, 'assistant-text').provisional).toBe(false);
    // Replacement mutated the existing block; nothing was appended.
    expect(kinds(state)).toEqual(['assistant-text']);
  });

  it('keeps interleaved thinking and text contentIndexes independent', () => {
    const state = createTranscriptState();
    feed(
      state,
      { type: 'message_start', message: { role: 'assistant', content: [] } },
      {
        type: 'message_update',
        assistantMessageEvent: { type: 'thinking_start', contentIndex: 0 },
      },
      {
        type: 'message_update',
        assistantMessageEvent: { type: 'thinking_delta', contentIndex: 0, delta: 'hmm' },
      },
      { type: 'message_update', assistantMessageEvent: { type: 'text_start', contentIndex: 1 } },
      {
        type: 'message_update',
        assistantMessageEvent: { type: 'text_delta', contentIndex: 1, delta: 'answer' },
      },
      {
        type: 'message_update',
        assistantMessageEvent: { type: 'thinking_delta', contentIndex: 0, delta: '…' },
      },
    );

    expect(kinds(state)).toEqual(['thinking', 'assistant-text']);
    expect(only(state, 'thinking').text).toBe('hmm…');
    expect(only(state, 'assistant-text').text).toBe('answer');
  });

  it('synthesizes a block for an orphan delta rather than dropping the token', () => {
    const state = createTranscriptState();
    feed(state, {
      type: 'message_update',
      assistantMessageEvent: { type: 'text_delta', contentIndex: 0, delta: 'early' },
    });

    expect(only(state, 'assistant-text').text).toBe('early');
  });

  it('folds the tool lifecycle into one block keyed by toolCallId', () => {
    const state = createTranscriptState();
    feed(
      state,
      {
        type: 'message_update',
        assistantMessageEvent: {
          type: 'toolcall_end',
          contentIndex: 0,
          toolCall: { type: 'toolCall', id: 'tc1', name: 'bash', arguments: { cmd: 'ls' } },
        },
      },
      { type: 'tool_execution_start', toolCallId: 'tc1', toolName: 'bash', args: { cmd: 'ls' } },
      {
        type: 'tool_execution_update',
        toolCallId: 'tc1',
        toolName: 'bash',
        args: {},
        partialResult: { content: [{ type: 'text', text: 'file-a' }] },
      },
    );
    const running = only(state, 'tool-call');
    expect(running.status).toBe('running');
    expect(running.toolName).toBe('bash');
    expect(running.argsText).toContain('"cmd"');
    expect(running.resultText).toBe('file-a');

    feed(state, {
      type: 'tool_execution_end',
      toolCallId: 'tc1',
      toolName: 'bash',
      result: { content: [{ type: 'text', text: 'file-a\nfile-b' }] },
      isError: false,
    });
    expect(only(state, 'tool-call').status).toBe('done');
    expect(only(state, 'tool-call').resultText).toBe('file-a\nfile-b');
  });

  it('marks a failed tool execution as error', () => {
    const state = createTranscriptState();
    feed(state, {
      type: 'tool_execution_end',
      toolCallId: 'tc9',
      toolName: 'bash',
      result: { content: [{ type: 'text', text: 'boom' }] },
      isError: true,
    });

    expect(only(state, 'tool-call').status).toBe('error');
  });

  it('ignores toolResult messages — tool blocks are execution-driven', () => {
    const state = createTranscriptState();
    const message = { role: 'toolResult', toolCallId: 'tc1', content: [], isError: false };
    feed(state, { type: 'message_start', message }, { type: 'message_end', message });

    expect(state.blocks).toHaveLength(0);
  });

  it('never throws on an unknown event type; runs coalesce into a counted marker', () => {
    const state = createTranscriptState();
    feed(
      state,
      { type: 'tool_result_end', payload: 1 },
      { type: 'tool_result_end', payload: 2 },
      { type: 'something_new' },
    );

    expect(kinds(state)).toEqual(['unknown-event', 'unknown-event']);
    expect(state.blocks[0]).toMatchObject({ eventType: 'tool_result_end', count: 2 });
    expect(state.blocks[1]).toMatchObject({ eventType: 'something_new', count: 1 });
  });

  it('stays silent on protocol-normal non-transcript events, including stray responses', () => {
    const state = createTranscriptState();
    feed(
      state,
      { type: 'agent_start' },
      { type: 'turn_start' },
      { type: 'queue_update', steering: [], followUp: [] },
      { type: 'response', command: 'get_state', success: true },
      { type: 'agent_settled' },
    );

    expect(state.blocks).toHaveLength(0);
  });

  it('renders a truncation marker for an oversized event the sidecar replaced', () => {
    const state = createTranscriptState();
    reduceTranscript(state, {
      eventType: 'tool_execution_update',
      event: {
        type: 'rpc_event_truncated',
        originalType: 'tool_execution_update',
        originalBytes: 5000000,
      },
      truncated: true,
    });

    const system = only(state, 'system');
    expect(system.text).toContain('tool_execution_update');
    expect(system.text).toContain('5000000');
  });

  it('renders compaction and retry lifecycle as system lines', () => {
    const state = createTranscriptState();
    feed(
      state,
      { type: 'compaction_start', reason: 'threshold' },
      { type: 'compaction_end', reason: 'threshold', aborted: false, willRetry: false },
      {
        type: 'auto_retry_start',
        attempt: 1,
        maxAttempts: 3,
        delayMs: 100,
        errorMessage: 'overloaded',
      },
      { type: 'auto_retry_end', success: true, attempt: 1 },
    );

    expect(kinds(state)).toEqual(['system', 'system', 'system']);
    expect(state.blocks.map((b) => (b.kind === 'system' ? b.text : ''))).toEqual([
      '[compacting…]',
      '[compaction complete]',
      '[retrying (1/3): overloaded]',
    ]);
  });

  it('appendSystemBlock joins the same ordered stream as events', () => {
    const state = createTranscriptState();
    feed(state, { type: 'message_start', message: { role: 'user', content: 'hi' } });
    appendSystemBlock(state, '[pi exited (0)]', 'info');

    expect(kinds(state)).toEqual(['user', 'system']);
  });
});
