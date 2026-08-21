import { beforeEach, describe, expect, it, vi } from 'vitest';

// jsdom does not reliably provide requestAnimationFrame; markDirty's
// rAF-coalesced flush needs one.
globalThis.requestAnimationFrame ??= (cb: FrameRequestCallback): number =>
  setTimeout(() => cb(performance.now()), 0) as unknown as number;

vi.mock('../src/api', () => ({
  rpcExtensionUiResponse: vi.fn(() => Promise.resolve()),
  rpcSpawn: vi.fn(() => Promise.resolve({ piVersion: '0.84.2', capabilities: [] })),
  rpcKill: vi.fn(() => Promise.resolve()),
  rpcPrompt: vi.fn(() => Promise.resolve({ success: true })),
  rpcSteer: vi.fn(() => Promise.resolve({ success: true })),
  rpcFollowUp: vi.fn(() => Promise.resolve({ success: true })),
  rpcAbort: vi.fn(() => Promise.resolve({ success: true })),
  rpcGetState: vi.fn(() => Promise.resolve({ success: false })),
  rpcGetMessages: vi.fn(() => Promise.resolve({ success: false })),
  rpcGetSessionStats: vi.fn(() => Promise.resolve({ success: false })),
  rpcGetAvailableModels: vi.fn(() => Promise.resolve({ success: false })),
  rpcGetAvailableThinkingLevels: vi.fn(() => Promise.resolve({ success: false })),
  rpcSetModel: vi.fn(() => Promise.resolve({ success: true })),
  rpcSetThinkingLevel: vi.fn(() => Promise.resolve({ success: true })),
}));

import * as api from '../src/api';
import { RpcPaneContent } from '../src/pane-content';

function event(eventType: string, payload: Record<string, unknown>, truncated = false) {
  return { sessionId: 's1', eventType, event: payload, truncated };
}

describe('RpcPaneContent extension UI routing', () => {
  let pane: RpcPaneContent;

  const overlayRoot = (): HTMLElement =>
    pane.container.querySelector<HTMLElement>('.ui-request-overlay')!;
  const overlayBody = (): HTMLElement =>
    pane.container.querySelector<HTMLElement>('.ui-request-body')!;

  beforeEach(() => {
    vi.clearAllMocks();
    pane = new RpcPaneContent('s1');
    document.body.appendChild(pane.container);
  });

  it('routes an interactive request to the overlay and answers on resolve', async () => {
    pane.appendEvent(
      event('extension_ui_request', {
        type: 'extension_ui_request',
        id: 'ui_1',
        method: 'confirm',
        title: 'guard: proceed?',
        message: 'really?',
      }),
    );

    expect(overlayRoot().hidden).toBe(false);
    overlayBody().dispatchEvent(new KeyboardEvent('keydown', { key: 'y', bubbles: true }));

    expect(api.rpcExtensionUiResponse).toHaveBeenCalledWith({
      sessionId: 's1',
      requestId: 'ui_1',
      confirmed: true,
    });
    // The decision survives as a transcript audit line after the modal
    // closes (the flush is rAF-coalesced, hence the wait).
    await vi.waitFor(() => {
      expect(pane.container.querySelector('.rpc-output')!.textContent).toContain(
        '[ui: confirm "guard: proceed?" → confirmed]',
      );
    });
    expect(overlayRoot().hidden).toBe(true);
  });

  it('auto-cancels a truncated interactive request via the marker identity', () => {
    pane.appendEvent(
      event(
        'extension_ui_request',
        {
          type: 'rpc_event_truncated',
          originalType: 'extension_ui_request',
          originalBytes: 9999999,
          originalId: 'ui_9',
          originalMethod: 'editor',
        },
        true,
      ),
    );

    expect(api.rpcExtensionUiResponse).toHaveBeenCalledWith({
      sessionId: 's1',
      requestId: 'ui_9',
      cancelled: true,
    });
    expect(overlayRoot().hidden).toBe(true);
  });

  it('auto-cancels a malformed interactive request that still carries an id', () => {
    pane.appendEvent(
      event('extension_ui_request', {
        type: 'extension_ui_request',
        id: 'ui_2',
        method: 'select',
        title: 'pick',
        options: [],
      }),
    );

    expect(api.rpcExtensionUiResponse).toHaveBeenCalledWith({
      sessionId: 's1',
      requestId: 'ui_2',
      cancelled: true,
    });
    expect(overlayRoot().hidden).toBe(true);
  });

  it('notify renders a system block with the wire severity', async () => {
    pane.appendEvent(
      event('extension_ui_request', {
        type: 'extension_ui_request',
        id: 'ui_3',
        method: 'notify',
        message: 'heads <b>up</b>',
        notifyType: 'warning',
      }),
    );

    await vi.waitFor(() => {
      expect(pane.container.querySelector('.t-system.t-warning')).not.toBeNull();
    });
    const block = pane.container.querySelector('.t-system.t-warning')!;
    expect(block.textContent).toBe('heads <b>up</b>');
    expect(block.querySelector('b')).toBeNull();
  });

  it('set_editor_text replaces the composer draft', () => {
    const draft = pane.container.querySelector<HTMLTextAreaElement>('.rpc-composer textarea')!;
    draft.value = 'typing…';

    pane.appendEvent(
      event('extension_ui_request', {
        type: 'extension_ui_request',
        id: 'ui_4',
        method: 'set_editor_text',
        text: 'pushed text',
      }),
    );

    expect(draft.value).toBe('pushed text');
  });

  it('setTitle invokes the onTitle callback for live tab rename (#133)', () => {
    const onTitle = vi.fn();
    pane.onTitle = onTitle;

    pane.appendEvent(
      event('extension_ui_request', {
        type: 'extension_ui_request',
        id: 'ui_t',
        method: 'setTitle',
        title: 'my session',
      }),
    );

    expect(onTitle).toHaveBeenCalledWith('my session');
  });

  it('setStatus and setWidget land in the keyed strips', () => {
    pane.appendEvent(
      event('extension_ui_request', {
        type: 'extension_ui_request',
        id: 'ui_5',
        method: 'setStatus',
        statusKey: 'guard',
        statusText: 'armed',
      }),
    );
    pane.appendEvent(
      event('extension_ui_request', {
        type: 'extension_ui_request',
        id: 'ui_6',
        method: 'setWidget',
        widgetKey: 'dash',
        widgetLines: ['ci: green'],
        widgetPlacement: 'aboveEditor',
      }),
    );

    expect(pane.container.querySelector('.strip-below .strip-status')!.textContent).toBe('armed');
    expect(pane.container.querySelector('.strip-above .strip-widget')!.textContent).toBe(
      'ci: green',
    );
  });

  it('dispose drops a pending dialog without answering it', () => {
    pane.appendEvent(
      event('extension_ui_request', {
        type: 'extension_ui_request',
        id: 'ui_7',
        method: 'confirm',
        title: 'pending',
        message: '',
      }),
    );
    expect(overlayRoot().hidden).toBe(false);

    pane.dispose();

    expect(overlayRoot().hidden).toBe(true);
    expect(api.rpcExtensionUiResponse).not.toHaveBeenCalled();
  });
});

describe('RpcPaneContent session resume (FT-2 slice 5)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('threads sessionPath into rpcSpawn and seeds history from getMessages', async () => {
    vi.mocked(api.rpcGetMessages).mockResolvedValue({
      success: true,
      data: {
        messages: [
          { role: 'user', content: 'earlier question' },
          { role: 'assistant', content: [{ type: 'text', text: 'earlier answer' }] },
        ],
      },
    } as never);

    const pane = new RpcPaneContent('s1');
    document.body.appendChild(pane.container);
    await pane.open({ sessionPath: '/s/one.jsonl' });
    // seed() runs unawaited by design; flush the promise chain + the
    // rAF-coalesced view flush (shimmed onto setTimeout above).
    await new Promise((resolve) => setTimeout(resolve, 5));

    expect(api.rpcSpawn).toHaveBeenCalledWith({
      sessionId: 's1',
      cwd: undefined,
      sessionPath: '/s/one.jsonl',
    });
    expect(api.rpcGetMessages).toHaveBeenCalledWith({ sessionId: 's1' });
    // The transcript flush is rAF-coalesced — wait for it like the audit-line
    // test above does.
    await vi.waitFor(() => {
      expect(pane.container.textContent).toContain('earlier question');
      expect(pane.container.textContent).toContain('earlier answer');
    });
  });

  it('a fresh (non-resume) open never fetches history', async () => {
    const pane = new RpcPaneContent('s2');
    document.body.appendChild(pane.container);
    await pane.open({});
    await new Promise((resolve) => setTimeout(resolve, 5));

    expect(api.rpcGetMessages).not.toHaveBeenCalled();
  });
});
