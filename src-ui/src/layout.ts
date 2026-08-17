/**
 * Per-tab split layout (FT-1 splits, ADR-0024): a binary tree whose leaves
 * are pane sessionIds and whose interior nodes are row/col splits with a
 * ratio. The tree is deliberately plain data — `dir`/`ratio`/`sessionId` —
 * because N-1-D will later serialize exactly this shape; nothing DOM-bound
 * may leak into the node type.
 *
 * Tree operations are pure functions (src-ui has no test framework; being
 * trivially inspectable is their correctness story). Rendering rebuilds the
 * wrapper DOM wholesale on structural change — at layout scale
 * reconciliation is not worth owning (same call as the tab bar) — while
 * pane containers are only ever *moved* into leaf slots, never recreated:
 * xterm loses scrollback and cell metrics on unmount, and a move preserves
 * the instance (the #81 onContextLoss fallback nets the WebGL edge case).
 */

export type SplitDir = 'row' | 'col';

export type LayoutNode =
  | { kind: 'leaf'; sessionId: string }
  | { kind: 'split'; dir: SplitDir; ratio: number; a: LayoutNode; b: LayoutNode };

export function leaf(sessionId: string): LayoutNode {
  return { kind: 'leaf', sessionId };
}

/** Pane ids in traversal (a-before-b) order — the pane-cycling order. */
export function leaves(node: LayoutNode): string[] {
  return node.kind === 'leaf' ? [node.sessionId] : [...leaves(node.a), ...leaves(node.b)];
}

/**
 * Replace the target leaf with a split holding it and a new leaf. Returns
 * the same tree when the target is absent — callers pass ids they took from
 * the tree, so absence is a no-op rather than an error path.
 */
export function splitLeaf(
  node: LayoutNode, targetId: string, dir: SplitDir, newId: string,
): LayoutNode {
  if (node.kind === 'leaf') {
    return node.sessionId === targetId
      ? { kind: 'split', dir, ratio: 0.5, a: node, b: leaf(newId) }
      : node;
  }
  return { ...node, a: splitLeaf(node.a, targetId, dir, newId), b: splitLeaf(node.b, targetId, dir, newId) };
}

/**
 * Remove the target leaf; its sibling subtree replaces the parent split.
 * Null means the tree is now empty (the tab closes).
 */
export function removeLeaf(node: LayoutNode, targetId: string): LayoutNode | null {
  if (node.kind === 'leaf') {
    return node.sessionId === targetId ? null : node;
  }
  const a = removeLeaf(node.a, targetId);
  const b = removeLeaf(node.b, targetId);
  if (a === null) return b;
  if (b === null) return a;
  return a === node.a && b === node.b ? node : { ...node, a, b };
}

const RATIO_MIN = 0.1;
const RATIO_MAX = 0.9;

/**
 * Rebuild `host`'s children to mirror the tree, moving each leaf's pane
 * container into place. Gutter drags mutate the split node's `ratio` in
 * place (the tree is live chrome state) and restyle without a rebuild;
 * `onGeometryChange` fires per drag frame so visible terminals refit.
 * Pointer capture on the gutter keeps the drag's pointer stream away from
 * xterm — no overlay needed.
 */
export function renderLayout(
  host: HTMLElement,
  node: LayoutNode,
  getContainer: (sessionId: string) => HTMLElement,
  onGeometryChange: () => void,
): void {
  host.replaceChildren(build(node, getContainer, onGeometryChange));
}

function build(
  node: LayoutNode,
  getContainer: (sessionId: string) => HTMLElement,
  onGeometryChange: () => void,
): HTMLElement {
  if (node.kind === 'leaf') {
    return getContainer(node.sessionId);
  }

  const split = document.createElement('div');
  split.className = `layout-split layout-${node.dir}`;
  const cellA = cell(node.ratio);
  const cellB = cell(1 - node.ratio);
  cellA.appendChild(build(node.a, getContainer, onGeometryChange));
  cellB.appendChild(build(node.b, getContainer, onGeometryChange));
  split.append(cellA, gutter(node, split, cellA, cellB, onGeometryChange), cellB);
  return split;
}

function cell(grow: number): HTMLElement {
  const el = document.createElement('div');
  el.className = 'layout-cell';
  el.style.flexGrow = String(grow);
  return el;
}

function gutter(
  node: Extract<LayoutNode, { kind: 'split' }>,
  split: HTMLElement,
  cellA: HTMLElement,
  cellB: HTMLElement,
  onGeometryChange: () => void,
): HTMLElement {
  const el = document.createElement('div');
  el.className = `layout-gutter layout-gutter-${node.dir}`;
  el.addEventListener('pointerdown', (down) => {
    down.preventDefault();
    el.setPointerCapture(down.pointerId);
    let frame = 0;
    const onMove = (move: PointerEvent) => {
      const rect = split.getBoundingClientRect();
      const fraction = node.dir === 'row'
        ? (move.clientX - rect.left) / rect.width
        : (move.clientY - rect.top) / rect.height;
      node.ratio = Math.min(RATIO_MAX, Math.max(RATIO_MIN, fraction));
      cellA.style.flexGrow = String(node.ratio);
      cellB.style.flexGrow = String(1 - node.ratio);
      // One refit per frame — cell metrics are wrong mid-drag otherwise,
      // but per-event refits would thrash xterm's measurement pass.
      if (frame === 0) {
        frame = requestAnimationFrame(() => {
          frame = 0;
          onGeometryChange();
        });
      }
    };
    const onUp = () => {
      el.removeEventListener('pointermove', onMove);
      el.removeEventListener('pointerup', onUp);
      onGeometryChange();
    };
    el.addEventListener('pointermove', onMove);
    el.addEventListener('pointerup', onUp);
  });
  return el;
}
