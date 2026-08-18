/**
 * Pending interactive extension-UI dialogs (FT-2 slice 4, ADR-0025): pure
 * data + parsing, no DOM — the overlay in ui-request-overlay.ts renders
 * `head`, everything else waits in FIFO order. FIFO is the correctness
 * property that matters: pi allows concurrently pending dialogs (a plain
 * id-keyed map server-side, no ordering), and every queued request is
 * eventually surfaced as long as the operator answers each in turn —
 * which they must, because an unanswered dialog can hang the agent turn
 * forever (docs/rpc-contract.md: pi has no reliable backstop).
 */

export type UiDialogRequest =
  | { id: string; method: 'select'; title: string; options: string[] }
  | { id: string; method: 'confirm'; title: string; message: string }
  | { id: string; method: 'input'; title: string; placeholder: string }
  | { id: string; method: 'editor'; title: string; prefill: string };

export type UiDialogMethod = UiDialogRequest['method'];

/** pi's key-discriminated extension_ui_response payload union. */
export type UiDialogOutcome = { value: string } | { confirmed: boolean } | { cancelled: true };

const INTERACTIVE_METHODS: ReadonlySet<string> = new Set(['select', 'confirm', 'input', 'editor']);

/** Whether a wire `method` names a dialog that expects a response. */
export function isInteractiveMethod(method: unknown): method is UiDialogMethod {
  return typeof method === 'string' && INTERACTIVE_METHODS.has(method);
}

/**
 * Defensive read of one interactive extension_ui_request event. Returns
 * null when the request cannot be rendered meaningfully (no id, unknown
 * method, a select with no usable options) — the caller must then answer
 * `cancelled` itself if an id exists, per the always-answer rule.
 */
export function parseUiDialogRequest(event: Record<string, unknown>): UiDialogRequest | null {
  if (typeof event.id !== 'string' || event.id.length === 0) return null;
  const id = event.id;
  const title = typeof event.title === 'string' ? event.title : '';
  switch (event.method) {
    case 'select': {
      const options = Array.isArray(event.options)
        ? event.options.filter((option): option is string => typeof option === 'string')
        : [];
      return options.length > 0 ? { id, method: 'select', title, options } : null;
    }
    case 'confirm':
      return {
        id,
        method: 'confirm',
        title,
        message: typeof event.message === 'string' ? event.message : '',
      };
    case 'input':
      return {
        id,
        method: 'input',
        title,
        placeholder: typeof event.placeholder === 'string' ? event.placeholder : '',
      };
    case 'editor':
      return {
        id,
        method: 'editor',
        title,
        prefill: typeof event.prefill === 'string' ? event.prefill : '',
      };
    default:
      return null;
  }
}

/**
 * Transcript audit line for a resolved dialog — guard decisions
 * (confirm/deny on a security prompt) must survive in the durable pane
 * record after the ephemeral modal closes. All inputs are untrusted or
 * operator text; the caller renders the line via textContent only.
 */
export function auditLine(request: UiDialogRequest, outcome: UiDialogOutcome): string {
  const subject = `${request.method} "${clip(request.title)}"`;
  if ('cancelled' in outcome) return `${subject} → cancelled`;
  if ('confirmed' in outcome) return `${subject} → ${outcome.confirmed ? 'confirmed' : 'denied'}`;
  return `${subject} → "${clip(outcome.value)}"`;
}

function clip(text: string, max = 120): string {
  return text.length <= max ? text : `${text.slice(0, max)}…`;
}

/** FIFO of pending dialogs; index 0 is what the overlay shows. */
export class UiRequestQueue {
  private queue: UiDialogRequest[] = [];

  get head(): UiDialogRequest | undefined {
    return this.queue[0];
  }

  get depth(): number {
    return this.queue.length;
  }

  /** Returns true when the request became the visible head. */
  enqueue(request: UiDialogRequest): boolean {
    this.queue.push(request);
    return this.queue.length === 1;
  }

  /** Pops and returns the head — the just-answered request. */
  resolveHead(): UiDialogRequest | undefined {
    return this.queue.shift();
  }

  /**
   * Drops every pending request without answering — session exit and pane
   * disposal only, where a response would target a dead child anyway.
   */
  clearAll(): void {
    this.queue = [];
  }
}
