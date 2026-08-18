import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { PaneContent } from '../src/pane-content';
import { PaneRegistry } from '../src/registry';

// jsdom does not reliably provide requestAnimationFrame; the registry's
// nextFrame choreography needs one.
globalThis.requestAnimationFrame ??= (cb: FrameRequestCallback): number =>
  setTimeout(() => cb(performance.now()), 0) as unknown as number;

/**
 * Records every PaneContent call — the seam that catches the trap class
 * typechecking cannot: a correctly-typed registry that still bypasses the
 * pane's own close() (the hardcoded-pty/kill bug this refactor removes).
 */
class FakeContent implements PaneContent {
  readonly container = document.createElement('div');
  readonly calls: string[] = [];
  readonly messages: { text: string; kind: 'info' | 'error' }[] = [];
  openBehavior: () => Promise<void> = () => Promise.resolve();

  open(): Promise<void> {
    this.calls.push('open');
    return this.openBehavior();
  }

  resize(): void {
    this.calls.push('resize');
  }

  focus(): void {
    this.calls.push('focus');
  }

  writeSystemMessage(text: string, kind: 'info' | 'error'): void {
    this.messages.push({ text, kind });
  }

  close(): Promise<void> {
    this.calls.push('close');
    return Promise.resolve();
  }

  dispose(): void {
    this.calls.push('dispose');
  }
}

describe('PaneRegistry kind-dispatch', () => {
  let host: HTMLElement;
  let registry: PaneRegistry;
  let contents: FakeContent[];
  let onChange: () => void;

  beforeEach(() => {
    host = document.createElement('div');
    document.body.appendChild(host);
    contents = [];
    onChange = vi.fn();
    registry = new PaneRegistry(host, onChange, () => {
      const content = new FakeContent();
      contents.push(content);
      return content;
    });
  });

  it('open() resizes after a layout pass, then opens the content', async () => {
    const pane = await registry.open();

    expect(contents).toHaveLength(1);
    // resize-before-open is the load-bearing cell-measurement choreography.
    expect(contents[0].calls.indexOf('resize')).toBeLessThan(contents[0].calls.indexOf('open'));
    expect(registry.active()?.sessionId).toBe(pane.sessionId);
    expect(pane.state).toBe('running');
  });

  it('a failed open renders the error into the pane and marks it exited', async () => {
    let created: FakeContent | undefined;
    const failing = new PaneRegistry(host, onChange, () => {
      created = new FakeContent();
      created.openBehavior = () => Promise.reject(new Error('no pi executable found'));
      return created;
    });

    const pane = await failing.open();

    expect(pane.state).toBe('exited');
    expect(created!.messages).toEqual([
      { text: 'failed to spawn pane: no pi executable found', kind: 'error' },
    ]);
  });

  it('close() dispatches through the pane content, never a hardcoded wire call', async () => {
    const pane = await registry.open();

    registry.close(pane.sessionId);

    expect(contents[0].calls).toContain('close');
    expect(contents[0].calls).toContain('dispose');
    expect(registry.tabs()).toHaveLength(0);
  });

  it('closeTab() closes every pane in the tree through its content', async () => {
    await registry.open();
    await registry.split('row');

    registry.closeTab(registry.tabs()[0].id);

    expect(contents).toHaveLength(2);
    for (const content of contents) {
      expect(content.calls).toContain('close');
      expect(content.calls).toContain('dispose');
    }
    expect(registry.tabs()).toHaveLength(0);
  });

  it('split() adds a leaf and cyclePane walks the global order', async () => {
    const first = await registry.open();
    const second = await registry.split('row');

    expect(registry.active()?.sessionId).toBe(second.sessionId);
    registry.cyclePane(1);
    expect(registry.active()?.sessionId).toBe(first.sessionId);
  });

  it('handleRpcExit writes the banner and stderr tail, and badges the tab', async () => {
    const pane = await registry.open({ kind: 'pi-rpc' });

    registry.handleRpcExit({ sessionId: pane.sessionId, exitCode: 143, stderrTail: 'boom' });

    expect(pane.state).toBe('exited');
    expect(contents[0].messages).toEqual([
      { text: '[pi exited (143)]', kind: 'info' },
      { text: 'boom', kind: 'error' },
    ]);
    expect(registry.tabs()[0].exited).toBe(true);
  });

  it('handleExit keeps the dead pane visible with its banner (ADR-0013)', async () => {
    const pane = await registry.open();

    registry.handleExit({ sessionId: pane.sessionId, exitCode: 1 });

    expect(pane.state).toBe('exited');
    expect(contents[0].messages).toEqual([{ text: '[process exited (1)]', kind: 'info' }]);
    expect(registry.tabs()).toHaveLength(1);
  });

  it('fitVisible resizes every pane in the active tab uniformly', async () => {
    await registry.open();
    await registry.split('row');
    for (const content of contents) content.calls.length = 0;

    registry.fitVisible();

    for (const content of contents) {
      expect(content.calls).toContain('resize');
    }
  });
});
