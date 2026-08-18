/**
 * Keyed status/widget strips for the native RPC pane (FT-2 slice 4): the
 * fire-and-forget half of the extension UI channel that persists on
 * screen. `setStatus` and `setWidget` are replace-in-place by key —
 * standing background readouts (repo-dash's workflow rows, a guard's
 * state line), not transcript entries. Widgets place above or below the
 * composer per pi's `widgetPlacement` (default aboveEditor, matching the
 * wire default); status chips live in the below strip, footer-like.
 * `undefined` content removes the key. All text is extension-controlled
 * untrusted content — text nodes only (ADR-0022).
 */

export type WidgetPlacement = 'aboveEditor' | 'belowEditor';

interface Entry {
  root: HTMLElement;
  text: Text;
  placement: WidgetPlacement;
}

export class ExtensionStrip {
  /** Mounts directly above the composer. */
  readonly above: HTMLElement;
  /** Mounts directly below the composer. */
  readonly below: HTMLElement;
  private readonly statuses = new Map<string, Entry>();
  private readonly widgets = new Map<string, Entry>();

  constructor() {
    this.above = document.createElement('div');
    this.above.className = 'extension-strip strip-above';
    this.above.hidden = true;
    this.below = document.createElement('div');
    this.below.className = 'extension-strip strip-below';
    this.below.hidden = true;
  }

  setStatus(key: string, text: string | undefined): void {
    this.upsert(this.statuses, key, text, 'belowEditor', 'strip-status');
  }

  setWidget(key: string, lines: readonly string[] | undefined, placement: WidgetPlacement): void {
    this.upsert(
      this.widgets,
      key,
      lines === undefined ? undefined : lines.join('\n'),
      placement,
      'strip-widget',
    );
  }

  private upsert(
    entries: Map<string, Entry>,
    key: string,
    text: string | undefined,
    placement: WidgetPlacement,
    className: string,
  ): void {
    const existing = entries.get(key);
    if (text === undefined) {
      if (existing !== undefined) {
        existing.root.remove();
        entries.delete(key);
      }
      this.syncVisibility();
      return;
    }

    if (existing !== undefined && existing.placement === placement) {
      existing.text.data = text;
      return;
    }

    // New key, or an existing one that switched sides: (re)mount.
    existing?.root.remove();
    const root = document.createElement(className === 'strip-widget' ? 'pre' : 'span');
    root.className = className;
    const node = document.createTextNode(text);
    root.appendChild(node);
    this.host(placement).appendChild(root);
    entries.set(key, { root, text: node, placement });
    this.syncVisibility();
  }

  private host(placement: WidgetPlacement): HTMLElement {
    return placement === 'belowEditor' ? this.below : this.above;
  }

  private syncVisibility(): void {
    this.above.hidden = this.above.childElementCount === 0;
    this.below.hidden = this.below.childElementCount === 0;
  }
}
