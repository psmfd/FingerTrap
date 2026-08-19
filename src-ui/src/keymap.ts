/**
 * Configurable keymap (FT-1 slice 3, ADR-0021). Defaults ship here — the
 * chord grammar and the action vocabulary are UI concerns, since this is
 * where the key events live — and the operator overrides them per-chord via
 * the `keybindings` section of the settings file, delivered by `settings/get`
 * (the sidecar serves that map verbatim and never interprets it).
 *
 * Chord grammar: `+`-separated, case-insensitive: zero or more of
 * `mod|cmd|ctrl|alt|shift` followed by one key name (`KeyboardEvent.key`,
 * lowercased — `p`, `tab`, `escape`, …). `mod` is cmd on macOS and ctrl
 * elsewhere, so one default chord set works on every host. `cmd` matches the
 * meta key literally (Windows key off macOS — allowed, not defaulted).
 *
 * Dispatch is one window-level capture-phase keydown listener: capture runs
 * before xterm's own textarea handlers, so a matched chord stops there and
 * never reaches the terminal, and an unmatched key passes through untouched.
 * Default chords all carry a modifier xterm has no binding for, so nothing a
 * shell or pi expects is shadowed.
 */

export type ActionId =
  | 'palette.toggle'
  | 'pane.new'
  | 'pane.close'
  | 'pane.next'
  | 'pane.prev'
  | 'pane.splitRight'
  | 'pane.splitDown'
  | 'status.toggle'
  | 'sessions.toggle';

/**
 * Default chords deliberately avoid the WebView/OS-menu layer: on macOS the
 * app menu owns bare cmd+w (close window), so pane chords take shift.
 */
const DEFAULT_CHORDS: Readonly<Record<ActionId, string>> = {
  'palette.toggle': 'mod+shift+p',
  'pane.new': 'mod+shift+t',
  'pane.close': 'mod+shift+w',
  'pane.next': 'ctrl+tab',
  'pane.prev': 'ctrl+shift+tab',
  'pane.splitRight': 'mod+shift+d',
  'pane.splitDown': 'mod+shift+f',
  'status.toggle': 'mod+shift+s',
  'sessions.toggle': 'mod+shift+o',
};

const ACTION_IDS = Object.keys(DEFAULT_CHORDS) as readonly ActionId[];

interface Chord {
  meta: boolean;
  ctrl: boolean;
  alt: boolean;
  shift: boolean;
  key: string;
}

const IS_MAC = /mac/i.test(navigator.platform);

/** Parse a chord string, or null (with a warning) when it is malformed. */
function parseChord(spec: string): Chord | null {
  const parts = spec
    .toLowerCase()
    .split('+')
    .map((p) => p.trim());
  const chord: Chord = { meta: false, ctrl: false, alt: false, shift: false, key: '' };
  for (const part of parts) {
    switch (part) {
      case 'mod':
        if (IS_MAC) chord.meta = true;
        else chord.ctrl = true;
        break;
      case 'cmd':
      case 'meta':
        chord.meta = true;
        break;
      case 'ctrl':
        chord.ctrl = true;
        break;
      case 'alt':
      case 'opt':
        chord.alt = true;
        break;
      case 'shift':
        chord.shift = true;
        break;
      default:
        if (part.length === 0 || chord.key !== '') {
          return null;
        }
        chord.key = part;
    }
  }
  return chord.key === '' ? null : chord;
}

function matches(chord: Chord, e: KeyboardEvent): boolean {
  return (
    chord.meta === e.metaKey &&
    chord.ctrl === e.ctrlKey &&
    chord.alt === e.altKey &&
    chord.shift === e.shiftKey &&
    chord.key === e.key.toLowerCase()
  );
}

export class Keymap {
  private readonly bindings: { chord: Chord; action: ActionId }[] = [];
  private readonly handlers: Partial<Record<ActionId, () => void>> = {};

  /**
   * @param overrides The operator's map from `settings/get`. Unknown action
   * ids and malformed chords are warned about and skipped — the default for
   * that action stays live, so a typo degrades one binding, not the keymap.
   */
  constructor(overrides: Record<string, string> = {}) {
    for (const action of Object.keys(overrides)) {
      if (!(ACTION_IDS as readonly string[]).includes(action)) {
        console.warn(`keybindings: unknown action '${action}' ignored`);
      }
    }
    for (const action of ACTION_IDS) {
      const spec = overrides[action] ?? DEFAULT_CHORDS[action];
      const chord = parseChord(spec);
      if (chord === null) {
        console.warn(`keybindings: malformed chord '${spec}' for '${action}'; using default`);
        const fallback = parseChord(DEFAULT_CHORDS[action]);
        if (fallback) this.bindings.push({ chord: fallback, action });
        continue;
      }
      this.bindings.push({ chord, action });
    }
  }

  on(action: ActionId, handler: () => void): this {
    this.handlers[action] = handler;
    return this;
  }

  /** Install the single capture-phase listener. Call once at startup. */
  install(): void {
    window.addEventListener(
      'keydown',
      (e) => {
        for (const { chord, action } of this.bindings) {
          if (matches(chord, e)) {
            e.preventDefault();
            e.stopPropagation();
            this.handlers[action]?.();
            return;
          }
        }
      },
      { capture: true },
    );
  }
}
