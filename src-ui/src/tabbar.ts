import type { PaneRegistry, TabInfo } from './registry';

/**
 * The persistent tab strip (ADR-0021). Presentation only: reads the
 * registry's tab projections, renders one tab per workspace plus the
 * new-tab actions, and forwards clicks back to the registry. Rebuilt
 * wholesale on each change — at tab-strip scale, reconciliation is not
 * worth owning.
 */
export interface TabBarAction {
  label: string;
  ariaLabel: string;
  onClick: () => void;
}

export class TabBar {
  private readonly host: HTMLElement;
  private readonly registry: PaneRegistry;
  private readonly extras: readonly TabBarAction[];

  constructor(host: HTMLElement, registry: PaneRegistry, extras: readonly TabBarAction[] = []) {
    this.host = host;
    this.registry = registry;
    this.extras = extras;
  }

  render(): void {
    this.host.replaceChildren();

    for (const tab of this.registry.tabs()) {
      this.host.appendChild(this.renderTab(tab));
    }

    // Default new tab is an unqualified spawn — the host-default chain
    // decides what it is. The explicit action stays shell-only: the tab bar
    // offers the default and its opposite; the full kind/cwd choice — and
    // splits — are the palette's job (slices 3+4, #75, ADR-0024).
    this.host.appendChild(
      this.renderAction('+', 'New tab', () => void this.registry.open()),
    );
    this.host.appendChild(
      this.renderAction('+sh', 'New shell tab', () => void this.registry.open({ kind: 'shell' })),
    );
    for (const extra of this.extras) {
      this.host.appendChild(this.renderAction(extra.label, extra.ariaLabel, extra.onClick));
    }
  }

  private renderTab(info: TabInfo): HTMLElement {
    const tab = document.createElement('div');
    tab.className = 'tab';
    tab.classList.toggle('active', info.active);
    tab.classList.toggle('exited', info.exited);
    tab.addEventListener('click', () => this.registry.activateTab(info.id));

    const title = document.createElement('span');
    title.className = 'tab-title';
    title.textContent = info.exited ? `${info.title} · exited` : info.title;
    tab.appendChild(title);

    const close = document.createElement('button');
    close.className = 'tab-close';
    close.type = 'button';
    close.textContent = '×';
    close.setAttribute('aria-label', `Close ${info.title}`);
    close.addEventListener('click', (e) => {
      e.stopPropagation();
      this.registry.closeTab(info.id);
    });
    tab.appendChild(close);

    return tab;
  }

  private renderAction(label: string, ariaLabel: string, onClick: () => void): HTMLElement {
    const button = document.createElement('button');
    button.className = 'tab-new';
    button.type = 'button';
    button.textContent = label;
    button.setAttribute('aria-label', ariaLabel);
    button.addEventListener('click', onClick);
    return button;
  }
}
