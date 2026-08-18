import { beforeEach, describe, expect, it } from 'vitest';
import { ExtensionStrip } from '../src/extension-strip';

describe('ExtensionStrip', () => {
  let strip: ExtensionStrip;

  beforeEach(() => {
    strip = new ExtensionStrip();
    document.body.appendChild(strip.above);
    document.body.appendChild(strip.below);
  });

  it('status chips are keyed replace-in-place and live below the composer', () => {
    strip.setStatus('guard', 'watching');
    strip.setStatus('guard', 'armed <b>now</b>');

    const chips = strip.below.querySelectorAll('.strip-status');
    expect(chips).toHaveLength(1);
    // Untrusted status text stays text (ADR-0022).
    expect(strip.below.querySelector('b')).toBeNull();
    expect(chips[0]!.textContent).toBe('armed <b>now</b>');
    expect(strip.below.hidden).toBe(false);
    expect(strip.above.hidden).toBe(true);
  });

  it('undefined content removes the key and hides an emptied strip', () => {
    strip.setStatus('a', 'one');
    strip.setStatus('b', 'two');

    strip.setStatus('a', undefined);
    expect(strip.below.querySelectorAll('.strip-status')).toHaveLength(1);

    strip.setStatus('b', undefined);
    expect(strip.below.hidden).toBe(true);
  });

  it('widgets join lines, honor placement, and remount on a placement switch', () => {
    strip.setWidget('dash', ['row one', 'row two'], 'aboveEditor');

    const widget = strip.above.querySelector('.strip-widget')!;
    expect(widget.textContent).toBe('row one\nrow two');
    expect(strip.above.hidden).toBe(false);

    strip.setWidget('dash', ['moved'], 'belowEditor');
    expect(strip.above.querySelector('.strip-widget')).toBeNull();
    expect(strip.above.hidden).toBe(true);
    expect(strip.below.querySelector('.strip-widget')!.textContent).toBe('moved');

    strip.setWidget('dash', undefined, 'belowEditor');
    expect(strip.below.hidden).toBe(true);
  });
});
