/**
 * Header strip for the native RPC pane (FT-2 slice 3b): model picker,
 * thinking-level picker, context meter. Pure DOM + injected handlers (no
 * api.ts import — the vitest seam). All values originate in relayed pi
 * responses and are untrusted: every label lands via textContent/text
 * nodes, and the meter bar width comes only from a clamped number.
 */

export interface ModelChoice {
  provider: string;
  id: string;
  name: string;
}

export interface ContextUsage {
  percent: number | null;
  tokens: number | null;
  contextWindow: number | null;
}

export interface RpcHeaderHandlers {
  onSetModel(provider: string, modelId: string): void;
  onSetThinkingLevel(level: string): void;
}

/** Clamp for the meter's CSS width — never a string interpolation source. */
export function clampPercent(value: number | null): number | null {
  if (value === null || !Number.isFinite(value)) return null;
  return Math.min(100, Math.max(0, value));
}

export class RpcHeader {
  readonly container: HTMLElement;
  private readonly modelSelect: HTMLSelectElement;
  private readonly thinkingSelect: HTMLSelectElement;
  private readonly meterBar: HTMLElement;
  private readonly meterLabel: Text;
  private models: ModelChoice[] = [];

  constructor(private readonly handlers: RpcHeaderHandlers) {
    this.container = document.createElement('div');
    this.container.className = 'rpc-header';

    this.modelSelect = document.createElement('select');
    this.modelSelect.className = 'header-model';
    this.modelSelect.addEventListener('change', () => {
      const model = this.models[this.modelSelect.selectedIndex];
      if (model) this.handlers.onSetModel(model.provider, model.id);
    });
    this.container.appendChild(this.modelSelect);

    this.thinkingSelect = document.createElement('select');
    this.thinkingSelect.className = 'header-thinking';
    this.thinkingSelect.addEventListener('change', () => {
      if (this.thinkingSelect.value) this.handlers.onSetThinkingLevel(this.thinkingSelect.value);
    });
    this.container.appendChild(this.thinkingSelect);

    const meter = document.createElement('div');
    meter.className = 'header-meter';
    this.meterBar = document.createElement('div');
    this.meterBar.className = 'header-meter-bar';
    meter.appendChild(this.meterBar);
    const label = document.createElement('span');
    label.className = 'header-meter-label';
    this.meterLabel = document.createTextNode('');
    label.appendChild(this.meterLabel);
    meter.appendChild(label);
    this.container.appendChild(meter);
  }

  /** Available models; option labels via textContent only. */
  setModels(models: readonly ModelChoice[]): void {
    this.models = [...models];
    this.modelSelect.textContent = '';
    for (const model of this.models) {
      const option = document.createElement('option');
      option.appendChild(document.createTextNode(model.name));
      this.modelSelect.appendChild(option);
    }
  }

  /** Marks the active model by id (no-op when unknown). */
  setActiveModel(modelId: string | null): void {
    const index = this.models.findIndex((m) => m.id === modelId);
    if (index >= 0) this.modelSelect.selectedIndex = index;
  }

  setThinkingLevels(levels: readonly string[]): void {
    this.thinkingSelect.textContent = '';
    for (const level of levels) {
      const option = document.createElement('option');
      option.value = level;
      option.appendChild(document.createTextNode(level));
      this.thinkingSelect.appendChild(option);
    }
  }

  setActiveThinkingLevel(level: string | null): void {
    if (level !== null && [...this.thinkingSelect.options].some((o) => o.value === level)) {
      this.thinkingSelect.value = level;
    }
  }

  setContextUsage(usage: ContextUsage | null): void {
    const percent = clampPercent(usage?.percent ?? null);
    if (percent === null) {
      this.meterBar.style.width = '0%';
      this.meterLabel.data = '';
      return;
    }
    this.meterBar.style.width = `${percent}%`;
    const tokens = usage?.tokens;
    const window = usage?.contextWindow;
    const detail =
      typeof tokens === 'number' && typeof window === 'number'
        ? ` (${tokens.toLocaleString()} / ${window.toLocaleString()})`
        : '';
    this.meterLabel.data = `${Math.round(percent)}%${detail}`;
  }
}
