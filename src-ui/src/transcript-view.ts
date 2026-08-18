import type { Block, BlockId, TranscriptState } from './transcript';

/**
 * DOM projection of the transcript (FT-2 slice 3): one keyed element per
 * block, mutated in place. Streaming text lives in a single Text node per
 * block whose .data is resynced on flush — no node churn at token rate,
 * and message_end's authoritative replacement lands in the same node the
 * deltas grew, so nothing flickers or jumps. Every event-derived string
 * reaches the DOM through createTextNode/.data only (ADR-0022: untrusted
 * content must never parse as markup).
 */
export class TranscriptView {
  private readonly els = new Map<BlockId, BlockEls>();
  private pinnedToBottom = true;

  constructor(private readonly scrollEl: HTMLElement) {
    // Capture scroll intent when it changes, not at flush time — flush
    // runs after DOM mutations already grew scrollHeight, which would
    // misread the user's pre-mutation position.
    scrollEl.addEventListener(
      'scroll',
      () => {
        this.pinnedToBottom = shouldStayPinned(
          scrollEl.scrollTop,
          scrollEl.scrollHeight,
          scrollEl.clientHeight,
        );
      },
      { passive: true },
    );
  }

  /** Apply the dirty set from one or more reducer calls, then re-pin. */
  apply(state: TranscriptState, dirtyIds: Iterable<BlockId>): void {
    for (const id of dirtyIds) {
      const index = state.byId.get(id);
      if (index === undefined) continue;
      const block = state.blocks[index];
      const existing = this.els.get(id);
      if (existing === undefined) {
        const els = createBlockEls(block);
        this.els.set(id, els);
        this.scrollEl.appendChild(els.root);
      } else {
        updateBlockEls(existing, block);
      }
    }
    if (this.pinnedToBottom) {
      this.scrollEl.scrollTop = this.scrollEl.scrollHeight;
    }
  }

  /** The operator just acted (submitted a prompt): snap back to the tail. */
  forcePin(): void {
    this.pinnedToBottom = true;
    this.scrollEl.scrollTop = this.scrollEl.scrollHeight;
  }
}

/**
 * Pin arithmetic, pure for testability — jsdom computes no real layout,
 * so the numbers are exercised with fabricated geometry and the on-screen
 * feel stays operator smoke (the repo's stated posture for pixel work).
 * Slop absorbs sub-pixel/zoom rounding that would spuriously un-pin.
 */
export function shouldStayPinned(
  scrollTop: number,
  scrollHeight: number,
  clientHeight: number,
  slopPx = 4,
): boolean {
  return scrollHeight - scrollTop - clientHeight <= slopPx;
}

interface BlockEls {
  root: HTMLElement;
  /** The one growing text node for the block's primary content. */
  text: Text;
  /** tool-call only: header line and args text nodes. */
  toolHead?: Text;
  toolArgs?: Text;
}

function createBlockEls(block: Block): BlockEls {
  const root = document.createElement('div');
  root.className = `t-block t-${block.kind}`;

  switch (block.kind) {
    case 'user': {
      const label = document.createElement('div');
      label.className = 't-label';
      label.appendChild(document.createTextNode('you'));
      root.appendChild(label);
      const text = appendPre(root, 't-body', block.text);
      return { root, text };
    }
    case 'assistant-text': {
      const text = appendPre(root, 't-body', block.text);
      return { root, text };
    }
    case 'thinking': {
      // <details> gives native collapse; the summary is static text, the
      // body is the growing node. Collapsed by default.
      const details = document.createElement('details');
      const summary = document.createElement('summary');
      summary.appendChild(document.createTextNode('thinking'));
      details.appendChild(summary);
      const body = document.createElement('pre');
      body.className = 't-body';
      const text = document.createTextNode(block.text);
      body.appendChild(text);
      details.appendChild(body);
      root.appendChild(details);
      return { root, text };
    }
    case 'tool-call': {
      root.dataset.status = block.status;
      const head = document.createElement('div');
      head.className = 't-tool-head';
      const toolHead = document.createTextNode(toolHeadText(block));
      head.appendChild(toolHead);
      root.appendChild(head);

      const details = document.createElement('details');
      const summary = document.createElement('summary');
      summary.appendChild(document.createTextNode('args'));
      details.appendChild(summary);
      const argsPre = document.createElement('pre');
      argsPre.className = 't-body';
      const toolArgs = document.createTextNode(block.argsText);
      argsPre.appendChild(toolArgs);
      details.appendChild(argsPre);
      root.appendChild(details);

      const text = appendPre(root, 't-tool-result', block.resultText);
      return { root, text, toolHead, toolArgs };
    }
    case 'system': {
      root.classList.add(block.severity === 'error' ? 't-error' : 't-info');
      const text = document.createTextNode(block.text);
      root.appendChild(text);
      return { root, text };
    }
    case 'unknown-event': {
      const text = document.createTextNode(unknownText(block));
      root.appendChild(text);
      return { root, text };
    }
  }
}

function updateBlockEls(els: BlockEls, block: Block): void {
  switch (block.kind) {
    case 'user':
    case 'assistant-text':
    case 'thinking':
    case 'system':
      if (els.text.data !== block.text) els.text.data = block.text;
      break;
    case 'tool-call':
      els.root.dataset.status = block.status;
      if (els.toolHead) els.toolHead.data = toolHeadText(block);
      if (els.toolArgs && els.toolArgs.data !== block.argsText) els.toolArgs.data = block.argsText;
      if (els.text.data !== block.resultText) els.text.data = block.resultText;
      break;
    case 'unknown-event':
      els.text.data = unknownText(block);
      break;
  }
}

function toolHeadText(block: Extract<Block, { kind: 'tool-call' }>): string {
  const status =
    block.status === 'running' ? 'running…' : block.status === 'error' ? 'failed' : 'done';
  return `⚙ ${block.toolName} — ${status}`;
}

function unknownText(block: Extract<Block, { kind: 'unknown-event' }>): string {
  const suffix = block.count > 1 ? ` ×${block.count}` : '';
  return `[unrecognized event: ${block.eventType}${suffix}]`;
}

/** Appends <pre class=...> with one text node; returns that text node. */
function appendPre(root: HTMLElement, className: string, initial: string): Text {
  const pre = document.createElement('pre');
  pre.className = className;
  const text = document.createTextNode(initial);
  pre.appendChild(text);
  root.appendChild(pre);
  return text;
}
