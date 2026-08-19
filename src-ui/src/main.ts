import './styles.css';
import * as api from './api';
import { Keymap } from './keymap';
import { Palette, type Command } from './palette';
import { PaneRegistry } from './registry';
import { SessionBrowserPanel } from './session-browser-panel';
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
  // The session browser produces pane opens (FT-2 slice 5, ADR-0026); the
  // resume actions land here so the panel never touches the registry.
  const sessions = new SessionBrowserPanel(panesEl, {
    resumeRpc: (sessionPath) => void registry.open({ kind: 'pi-rpc', sessionPath }),
    resumePty: (sessionPath, cwd) => void registry.open({ kind: 'pi', sessionPath, cwd }),
  });
  const tabbar = new TabBar(tabbarEl, registry, [
    { label: '☰', ariaLabel: 'Toggle session browser', onClick: () => sessions.toggle() },
    { label: '⎔', ariaLabel: 'Toggle status panel', onClick: () => status.toggle() },
  ]);

  // The palette owns every command the keymap can also reach, plus the
  // argument-taking spawns (#75): explicit kind, and free-text cwd — the
  // sidecar resolves and validates the path, so a bad one fails loudly into
  // the pane rather than silently opening in the wrong place.
  const commands: readonly Command[] = [
    { title: 'New pi pane', run: () => void registry.open({ kind: 'pi' }) },
    { title: 'New shell pane', run: () => void registry.open({ kind: 'shell' }) },
    // FT-2 slice 2 walking skeleton (ADR-0025): raw relayed events + a
    // provisional prompt input; the PTY pi pane stays first-class.
    { title: 'New pi pane (native rpc)', run: () => void registry.open({ kind: 'pi-rpc' }) },
    {
      title: 'New pi pane in directory…',
      run: (p) =>
        p.promptInput('absolute directory path', (cwd) => void registry.open({ kind: 'pi', cwd })),
    },
    {
      title: 'New shell pane in directory…',
      run: (p) =>
        p.promptInput(
          'absolute directory path',
          (cwd) => void registry.open({ kind: 'shell', cwd }),
        ),
    },
    {
      title: 'Close pane',
      run: () => {
        const active = registry.active();
        if (active) registry.close(active.sessionId);
      },
    },
    { title: 'Split right', run: () => void registry.split('row') },
    { title: 'Split down', run: () => void registry.split('col') },
    { title: 'Next pane', run: () => registry.cyclePane(1) },
    { title: 'Previous pane', run: () => registry.cyclePane(-1) },
    { title: 'Toggle status panel', run: () => status.toggle() },
    { title: 'Toggle session browser', run: () => sessions.toggle() },
  ];
  const palette = new Palette(panesEl, commands, () => registry.active()?.content.focus());

  await api.start();

  // One global dispatch per notification type; the registry routes by
  // sessionId. Panes never subscribe individually, so a closed pane cannot
  // leak a handler.
  api.onPtyOutput((n) => registry.handleOutput(n));
  api.onPtyExit((n) => registry.handleExit(n));
  api.onRpcEvent((n) => registry.handleRpcEvent(n));
  api.onRpcExit((n) => registry.handleRpcExit(n));
  api.onStatusSnapshot((n) => status.update(n));

  // Effective settings (slice 3): keybinding overrides and the real default
  // pane kind for tab titles. A transport-level failure here degrades to
  // built-in defaults — visibly, not silently — rather than blocking panes:
  // the settings file itself cannot be the cause (a bad file already killed
  // the sidecar at startup, before any RPC).
  let overrides: Record<string, string> = {};
  try {
    const settings = await api.settingsGet();
    overrides = settings.keybindings;
    if (settings.paneDefaultKind === 'pi' || settings.paneDefaultKind === 'shell') {
      registry.defaultKind = settings.paneDefaultKind;
    }
  } catch (err) {
    console.error('settings/get failed; using default keybindings', err);
  }

  new Keymap(overrides)
    .on('palette.toggle', () => palette.toggle())
    .on('pane.new', () => void registry.open())
    .on('pane.close', () => {
      const active = registry.active();
      if (active) registry.close(active.sessionId);
    })
    .on('pane.next', () => registry.cyclePane(1))
    .on('pane.prev', () => registry.cyclePane(-1))
    .on('pane.splitRight', () => void registry.split('row'))
    .on('pane.splitDown', () => void registry.split('col'))
    .on('status.toggle', () => status.toggle())
    .on('sessions.toggle', () => sessions.toggle())
    .install();

  // xterm's onResize only fires from term.resize(...) — observe the shared
  // pane host and let FitAddon translate DOM size changes into cell counts.
  // Hidden tabs are skipped; they refit on activation instead.
  const observer = new ResizeObserver(() => {
    registry.fitVisible();
  });
  observer.observe(panesEl);

  // The startup pane is an unqualified spawn: the ADR-0013/0014 host-default
  // chain (request → settings → environment → pi) decides what it is.
  await registry.open();
}

main().catch((err: unknown) => {
  console.error(err);
});
