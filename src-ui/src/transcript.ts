/**
 * Transcript reducer (FT-2 slice 3, ADR-0025): folds relayed pi RPC events
 * into an ordered, keyed block list. Pure data — no DOM imports; the DOM
 * projection lives in transcript-view.ts. Wire shapes follow
 * docs/rpc-contract.md as verified against the pinned pi source:
 * `message_update` is delta-only (no cumulative message, no `partial`
 * snapshot), so provisional assistant content is accumulated here by
 * `contentIndex` and replaced wholesale by `message_end`'s authoritative
 * message. Every user-role message is echoed by pi as
 * `message_start`/`message_end` — the transcript renders from that echo,
 * never from a local optimistic append (single source of truth for
 * ordering, and queued steer/follow-ups appear only when delivered).
 *
 * Tolerance posture: event-name drift is a known bug class here, so an
 * unknown event type reduces to a visible marker block, never a throw.
 */

export type BlockId = string;

export type ToolStatus = 'running' | 'done' | 'error';

/**
 * `warning` exists for extension `notify` events (notifyType has three
 * values on the wire); host-originated messages stay info/error.
 */
export type SystemSeverity = 'info' | 'warning' | 'error';

export type Block =
  | { kind: 'user'; id: BlockId; text: string }
  | { kind: 'assistant-text'; id: BlockId; text: string; provisional: boolean }
  | { kind: 'thinking'; id: BlockId; text: string; provisional: boolean }
  | {
      kind: 'tool-call';
      id: BlockId;
      toolName: string;
      status: ToolStatus;
      argsText: string;
      resultText: string;
    }
  | { kind: 'system'; id: BlockId; text: string; severity: SystemSeverity }
  | { kind: 'unknown-event'; id: BlockId; eventType: string; count: number };

export interface TranscriptState {
  blocks: Block[];
  byId: Map<BlockId, number>;
  /**
   * Open streaming assistant message: contentIndex → block id. Non-null
   * between a role-assistant `message_start` and its `message_end`.
   * An orphan delta with no open message synthesizes one (transport
   * hiccups render slightly early instead of losing tokens).
   */
  streamingByIndex: Map<number, BlockId> | null;
  /** Tool blocks are keyed by pi's toolCallId, not contentIndex. */
  toolBlocks: Map<string, BlockId>;
  nextId: number;
}

export function createTranscriptState(): TranscriptState {
  return {
    blocks: [],
    byId: new Map(),
    streamingByIndex: null,
    toolBlocks: new Map(),
    nextId: 1,
  };
}

/** Event types that are protocol-normal but produce no transcript block. */
const SILENT_EVENT_TYPES = new Set([
  'agent_start',
  'agent_end',
  'agent_settled',
  'turn_start',
  'turn_end',
  'queue_update',
  'entry_appended',
  'session_info_changed',
  'thinking_level_changed',
  'bash_execution_update',
  'summarization_retry_scheduled',
  'summarization_retry_attempt_start',
  'summarization_retry_finished',
  'extension_ui_request',
  // Stray `type:"response"` lines are the documented demux fall-through
  // for unknown/late/duplicate response ids — protocol noise, not events.
  'response',
]);

/**
 * Folds one relayed notification into the state. Returns the ids of
 * blocks created or mutated — the dirty set the view flushes on the next
 * animation frame. Mutates `state` in place on purpose: no framework
 * diffs by reference here, and a token stream should not reallocate the
 * block array per delta.
 */
export function reduceTranscript(
  state: TranscriptState,
  notification: { eventType?: string | null; event: unknown; truncated: boolean },
): BlockId[] {
  const event = asRecord(notification.event);

  if (notification.truncated) {
    // The sidecar replaced an oversized event with a small marker; the
    // original payload is gone. Render the fact, do not interpret.
    const bytes = typeof event?.originalBytes === 'number' ? ` (${event.originalBytes} bytes)` : '';
    return [
      appendSystemBlock(
        state,
        `[event truncated: ${notification.eventType ?? 'unknown'}${bytes}]`,
        'info',
      ),
    ];
  }

  const type = notification.eventType ?? (typeof event?.type === 'string' ? event.type : null);
  if (type === null || event === null) {
    return [appendUnknownBlock(state, '(untyped)')];
  }

  switch (type) {
    case 'message_start':
      return onMessageStart(state, asRecord(event.message));
    case 'message_update':
      return onMessageUpdate(state, asRecord(event.assistantMessageEvent));
    case 'message_end':
      return onMessageEnd(state, asRecord(event.message));
    case 'tool_execution_start':
      return upsertToolBlock(state, event, { status: 'running' });
    case 'tool_execution_update':
      return onToolUpdate(state, event);
    case 'tool_execution_end':
      return onToolEnd(state, event);
    case 'compaction_start':
      return [appendSystemBlock(state, '[compacting…]', 'info')];
    case 'compaction_end':
      return [
        appendSystemBlock(
          state,
          event.aborted === true ? '[compaction aborted]' : '[compaction complete]',
          'info',
        ),
      ];
    case 'auto_retry_start':
      return [
        appendSystemBlock(
          state,
          `[retrying (${asText(event.attempt)}/${asText(event.maxAttempts)}): ${asText(event.errorMessage)}]`,
          'info',
        ),
      ];
    case 'auto_retry_end':
      return event.success === true
        ? []
        : [appendSystemBlock(state, `[retry failed: ${asText(event.finalError)}]`, 'error')];
    case 'extension_error':
      return [appendSystemBlock(state, `[extension error: ${asText(event.error)}]`, 'error')];
    default:
      if (SILENT_EVENT_TYPES.has(type)) return [];
      return [appendUnknownBlock(state, type)];
  }
}

/**
 * Out-of-band operator-facing line (spawn failure, exit banner). Exposed
 * so RpcPaneContent's writeSystemMessage flows through the same ordered
 * block list as the event stream.
 */
export function appendSystemBlock(
  state: TranscriptState,
  text: string,
  severity: SystemSeverity,
): BlockId {
  return appendBlock(state, (id) => ({ kind: 'system', id, text, severity }));
}

function onMessageStart(
  state: TranscriptState,
  message: Record<string, unknown> | null,
): BlockId[] {
  const role = message?.role;
  if (role === 'user') {
    return [appendBlock(state, (id) => ({ kind: 'user', id, text: userText(message) }))];
  }
  if (role === 'assistant') {
    // Opens the streaming context; the initial partial snapshot's content
    // is typically empty — blocks materialize per contentIndex as the
    // *_start deltas arrive.
    state.streamingByIndex = new Map();
    return [];
  }
  // toolResult messages duplicate tool_execution_end's payload; tool
  // blocks are driven by the tool_execution_* lifecycle instead.
  return [];
}

function onMessageUpdate(state: TranscriptState, delta: Record<string, unknown> | null): BlockId[] {
  if (delta === null || typeof delta.type !== 'string') return [];
  const contentIndex = typeof delta.contentIndex === 'number' ? delta.contentIndex : null;

  switch (delta.type) {
    case 'start':
      return [];
    case 'text_start':
    case 'thinking_start': {
      if (contentIndex === null) return [];
      const kind = delta.type === 'text_start' ? 'assistant-text' : 'thinking';
      return [openStreamingBlock(state, contentIndex, kind)];
    }
    case 'text_delta':
    case 'thinking_delta': {
      if (contentIndex === null) return [];
      const kind = delta.type === 'text_delta' ? 'assistant-text' : 'thinking';
      const id = streamingBlockAt(state, contentIndex, kind);
      const block = blockById(state, id);
      if (block?.kind === 'assistant-text' || block?.kind === 'thinking') {
        block.text += asText(delta.delta);
      }
      return [id];
    }
    case 'text_end':
    case 'thinking_end': {
      if (contentIndex === null) return [];
      const kind = delta.type === 'text_end' ? 'assistant-text' : 'thinking';
      const id = streamingBlockAt(state, contentIndex, kind);
      const block = blockById(state, id);
      if (block?.kind === 'assistant-text' || block?.kind === 'thinking') {
        block.text = asText(delta.content);
      }
      return [id];
    }
    case 'toolcall_start':
    case 'toolcall_delta':
      // The model is still generating the call's arguments; the block
      // materializes on toolcall_end when the full ToolCall exists.
      return [];
    case 'toolcall_end': {
      const toolCall = asRecord(delta.toolCall);
      if (toolCall === null || typeof toolCall.id !== 'string') return [];
      return upsertToolBlock(
        state,
        { toolCallId: toolCall.id, toolName: toolCall.name, args: toolCall.arguments },
        { status: 'running' },
      );
    }
    default:
      // done/error variants carry a full message; message_end follows and
      // is authoritative, so nothing to fold here.
      return [];
  }
}

function onMessageEnd(state: TranscriptState, message: Record<string, unknown> | null): BlockId[] {
  if (message?.role !== 'assistant') {
    // The user echo rendered on message_start; toolResult is tool-driven.
    return [];
  }

  const content = Array.isArray(message.content) ? message.content : [];
  const open = state.streamingByIndex ?? new Map<number, BlockId>();
  state.streamingByIndex = null;

  const dirty: BlockId[] = [];
  content.forEach((item: unknown, index: number) => {
    const part = asRecord(item);
    if (part === null) return;
    if (part.type === 'text' || part.type === 'thinking') {
      const kind = part.type === 'text' ? 'assistant-text' : 'thinking';
      const authoritative = asText(part.type === 'text' ? part.text : part.thinking);
      const existingId = open.get(index);
      const existing = existingId === undefined ? undefined : blockById(state, existingId);
      if (existing && (existing.kind === 'assistant-text' || existing.kind === 'thinking')) {
        existing.text = authoritative;
        existing.provisional = false;
        dirty.push(existing.id);
      } else if (authoritative.length > 0) {
        // Non-streamed (or missed-delta) content: materialize it now.
        dirty.push(
          appendBlock(state, (id) =>
            kind === 'assistant-text'
              ? { kind, id, text: authoritative, provisional: false }
              : { kind, id, text: authoritative, provisional: false },
          ),
        );
      }
    }
    // toolCall content parts were upserted from toolcall_end already.
  });
  return dirty;
}

function onToolUpdate(state: TranscriptState, event: Record<string, unknown>): BlockId[] {
  // partialResult is the accumulated AgentToolResult, not a delta.
  const partial = asRecord(event.partialResult);
  return upsertToolBlock(state, event, {
    status: 'running',
    resultText: partial === null ? undefined : resultContentText(partial),
  });
}

function onToolEnd(state: TranscriptState, event: Record<string, unknown>): BlockId[] {
  const result = asRecord(event.result);
  return upsertToolBlock(state, event, {
    status: event.isError === true ? 'error' : 'done',
    resultText: result === null ? '' : resultContentText(result),
  });
}

function upsertToolBlock(
  state: TranscriptState,
  event: Record<string, unknown>,
  patch: { status: ToolStatus; resultText?: string },
): BlockId[] {
  const toolCallId = typeof event.toolCallId === 'string' ? event.toolCallId : null;
  if (toolCallId === null) return [];

  const existingId = state.toolBlocks.get(toolCallId);
  const existing = existingId === undefined ? undefined : blockById(state, existingId);
  if (existing?.kind === 'tool-call') {
    existing.status = patch.status;
    if (patch.resultText !== undefined) existing.resultText = patch.resultText;
    return [existing.id];
  }

  const id = appendBlock(state, (blockId) => ({
    kind: 'tool-call',
    id: blockId,
    toolName: asText(event.toolName) || '(unknown tool)',
    status: patch.status,
    argsText: safeStringify(event.args),
    resultText: patch.resultText ?? '',
  }));
  state.toolBlocks.set(toolCallId, id);
  return [id];
}

function openStreamingBlock(
  state: TranscriptState,
  contentIndex: number,
  kind: 'assistant-text' | 'thinking',
): BlockId {
  state.streamingByIndex ??= new Map();
  const existing = state.streamingByIndex.get(contentIndex);
  if (existing !== undefined) return existing;
  const id = appendBlock(state, (blockId) =>
    kind === 'assistant-text'
      ? { kind, id: blockId, text: '', provisional: true }
      : { kind, id: blockId, text: '', provisional: true },
  );
  state.streamingByIndex.set(contentIndex, id);
  return id;
}

function streamingBlockAt(
  state: TranscriptState,
  contentIndex: number,
  kind: 'assistant-text' | 'thinking',
): BlockId {
  return state.streamingByIndex?.get(contentIndex) ?? openStreamingBlock(state, contentIndex, kind);
}

function appendUnknownBlock(state: TranscriptState, eventType: string): BlockId {
  // Coalesce runs of the same unknown type so drift renders as one
  // counted marker, not a wall of lines.
  const last = state.blocks[state.blocks.length - 1];
  if (last?.kind === 'unknown-event' && last.eventType === eventType) {
    last.count += 1;
    return last.id;
  }
  return appendBlock(state, (id) => ({ kind: 'unknown-event', id, eventType, count: 1 }));
}

function appendBlock(state: TranscriptState, build: (id: BlockId) => Block): BlockId {
  const id = `b${state.nextId++}`;
  state.byId.set(id, state.blocks.length);
  state.blocks.push(build(id));
  return id;
}

function blockById(state: TranscriptState, id: BlockId): Block | undefined {
  const index = state.byId.get(id);
  return index === undefined ? undefined : state.blocks[index];
}

function userText(message: Record<string, unknown> | null): string {
  const content = message?.content;
  if (typeof content === 'string') return content;
  if (!Array.isArray(content)) return '';
  return content
    .map((item: unknown) => {
      const part = asRecord(item);
      if (part?.type === 'text') return asText(part.text);
      if (part?.type === 'image') return '[image]';
      return '';
    })
    .filter((part) => part.length > 0)
    .join('\n');
}

/** Text items of an AgentToolResult's content array, joined. */
function resultContentText(result: Record<string, unknown>): string {
  const content = result.content;
  if (!Array.isArray(content)) return '';
  return content
    .map((item: unknown) => {
      const part = asRecord(item);
      if (part?.type === 'text') return asText(part.text);
      if (part?.type === 'image') return '[image]';
      return '';
    })
    .filter((part) => part.length > 0)
    .join('\n');
}

function asRecord(value: unknown): Record<string, unknown> | null {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : null;
}

function asText(value: unknown): string {
  return typeof value === 'string'
    ? value
    : value === undefined || value === null
      ? ''
      : String(value);
}

function safeStringify(value: unknown): string {
  try {
    return value === undefined ? '' : JSON.stringify(value, null, 1);
  } catch {
    return '[unserializable]';
  }
}
