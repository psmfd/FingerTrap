import './styles.css';
import * as api from './api';
import { PaneRegistry } from './registry';
import { StatusPanel } from './status';
import { TabBar } from './tabbar';

async function main(): Promise<void> {
  const tabbarEl = document.getElementById('tabbar');
  const panesEl = document.getElementById('panes');
  if (!tabbarEl || !panesEl) {
    throw new Error('expected #tabbar and #panes in DOM');
  }

  const registry = new PaneRegistry(panesEl, () => tabbar.render());
  const status = new StatusPanel(panesEl);
  const tabbar = new TabBar(tabbarEl, registry, [
    { label: '⎔', ariaLabel: 'Toggle status panel', onClick: () => status.toggle() },
  ]);

  await api.start();

  // One global dispatch per notification type; the registry routes by
  // sessionId. Panes never subscribe individually, so a closed pane cannot
  // leak a handler.
  api.onPtyOutput((n) => registry.handleOutput(n));
  api.onPtyExit((n) => registry.handleExit(n));
  api.onStatusSnapshot((n) => status.update(n));

  // xterm's onResize only fires from term.resize(...) — observe the shared
  // pane host and let FitAddon translate DOM size changes into cell counts.
  // Hidden panes are skipped; they refit on activation instead.
  const observer = new ResizeObserver(() => {
    registry.fitActive();
  });
  observer.observe(panesEl);

  // The startup pane is an unqualified spawn: the ADR-0013/0014 host-default
  // chain (request → settings → environment → pi) decides what it is.
  await registry.open();
}

main().catch((err: unknown) => {
  console.error(err);
});
