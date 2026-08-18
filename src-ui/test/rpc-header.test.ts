import { beforeEach, describe, expect, it, vi } from 'vitest';
import { clampPercent, RpcHeader } from '../src/rpc-header';

describe('RpcHeader', () => {
  let handlers: {
    onSetModel: ReturnType<typeof vi.fn>;
    onSetThinkingLevel: ReturnType<typeof vi.fn>;
  };
  let header: RpcHeader;

  beforeEach(() => {
    handlers = { onSetModel: vi.fn(), onSetThinkingLevel: vi.fn() };
    header = new RpcHeader(handlers);
    document.body.appendChild(header.container);
  });

  it('renders model names as text and dispatches provider+id on change', () => {
    header.setModels([
      { provider: 'anthropic', id: 'opus', name: '<img src=x> Opus' },
      { provider: 'omlx', id: 'workhorse', name: 'Workhorse' },
    ]);
    const select = header.container.querySelector<HTMLSelectElement>('.header-model')!;

    // Untrusted model names stay text (ADR-0022).
    expect(select.querySelector('img')).toBeNull();
    expect(select.options[0].textContent).toBe('<img src=x> Opus');

    select.selectedIndex = 1;
    select.dispatchEvent(new Event('change'));
    expect(handlers.onSetModel).toHaveBeenCalledWith('omlx', 'workhorse');
  });

  it('marks the active model and thinking level from state readouts', () => {
    header.setModels([
      { provider: 'a', id: 'm1', name: 'one' },
      { provider: 'a', id: 'm2', name: 'two' },
    ]);
    header.setThinkingLevels(['off', 'medium', 'high']);

    header.setActiveModel('m2');
    header.setActiveThinkingLevel('high');

    expect(header.container.querySelector<HTMLSelectElement>('.header-model')!.selectedIndex).toBe(
      1,
    );
    expect(header.container.querySelector<HTMLSelectElement>('.header-thinking')!.value).toBe(
      'high',
    );

    // Unknown values are a no-op, never a crash.
    header.setActiveModel('missing');
    header.setActiveThinkingLevel('nope');
    expect(header.container.querySelector<HTMLSelectElement>('.header-model')!.selectedIndex).toBe(
      1,
    );
  });

  it('dispatches thinking-level changes', () => {
    header.setThinkingLevels(['off', 'high']);
    const select = header.container.querySelector<HTMLSelectElement>('.header-thinking')!;
    select.value = 'high';
    select.dispatchEvent(new Event('change'));

    expect(handlers.onSetThinkingLevel).toHaveBeenCalledWith('high');
  });

  it('renders the context meter from numbers only, clamped', () => {
    header.setContextUsage({ percent: 44.6, tokens: 437500, contextWindow: 1000000 });
    const bar = header.container.querySelector<HTMLElement>('.header-meter-bar')!;
    const label = header.container.querySelector<HTMLElement>('.header-meter-label')!;

    expect(bar.style.width).toBe('44.6%');
    expect(label.textContent).toContain('45%');
    expect(label.textContent).toContain('437,500');

    header.setContextUsage({ percent: 250, tokens: null, contextWindow: null });
    expect(bar.style.width).toBe('100%');

    header.setContextUsage(null);
    expect(bar.style.width).toBe('0%');
    expect(label.textContent).toBe('');
  });
});

describe('clampPercent', () => {
  it('clamps into [0,100] and rejects non-finite values', () => {
    expect(clampPercent(50)).toBe(50);
    expect(clampPercent(-3)).toBe(0);
    expect(clampPercent(120)).toBe(100);
    expect(clampPercent(Number.NaN)).toBeNull();
    expect(clampPercent(null)).toBeNull();
  });
});
