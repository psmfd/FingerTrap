import type { Pane, PaneRegistry } from './registry';

/**
 * The persistent tab strip (ADR-0021). Presentation only: reads the registry,
 * renders one tab per pane plus the new-tab actions, and forwards clicks back
 * to the registry. Rebuilt wholesale on each change — at tab-strip scale,
 * reconciliation is not worth owning.
 */
export class TabBar {
  private readonly host: HTMLElement;
  private readonly registry: PaneRegistry;

  constructor(host: HTMLElement, registry: PaneRegistry) {
    this.host = host;
    this.registry = registry;
  }

  render(): void {
    this.host.replaceChildren();
    const active = this.registry.active();

    for (const pane of this.registry.list()) {
      this.host.appendChild(this.renderTab(pane, pane === active));
    }

    // Default new tab is an unqualified spawn — the host-default chain
    // decides what it is. The explicit action is shell-only on purpose:
    // there is no "new pi tab", because pi cannot be asked for harder than
    // the default already asks (ADR-0021).
    this.host.appendChild(
      this.renderAction('+', 'New tab', () => void this.registry.open()),
    );
    this.host.appendChild(
      this.renderAction('+sh', 'New shell tab', () => void this.registry.open('shell')),
    );
  }

  private renderTab(pane: Pane, isActive: boolean): HTMLElement {
    const tab = document.createElement('div');
    tab.className = 'tab';
    tab.classList.toggle('active', isActive);
    tab.classList.toggle('exited', pane.state === 'exited');
    tab.addEventListener('click', () => this.registry.activate(pane.sessionId));

    const title = document.createElement('span');
    title.className = 'tab-title';
    title.textContent = pane.state === 'exited' ? `${pane.title} · exited` : pane.title;
    tab.appendChild(title);

    const close = document.createElement('button');
    close.className = 'tab-close';
    close.type = 'button';
    close.textContent = '×';
    close.setAttribute('aria-label', `Close ${pane.title}`);
    close.addEventListener('click', (e) => {
      e.stopPropagation();
      this.registry.close(pane.sessionId);
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
