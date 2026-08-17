# 0024 — Splits as a per-tab binary layout tree

- Status: Accepted
- Date: 2026-08-17

## Context and problem statement

Splits are FT-1's last slice, deferred by [ADR-0021](0021-ft1-chrome-pane-registry-and-slices.md)
with a stated reason: "persisted layout (N-1's deferred half) wants splits to
exist before it freezes a schema" (#51). So this decision has two customers —
the chrome that renders splits now, and N-1-D, which will later serialize
whatever model is chosen here. The constraints in force:

- ADR-0021 U1 (no framework) and P1 (the UI owns presentation multiplicity;
  the sidecar is a session-keyed PTY host with no layout knowledge) both
  stand. Splits must not move the RPC surface.
- The slice-1 chrome equates tab and pane 1:1 — `PaneRegistry.cycle` walks
  panes as if they were tabs, and the tab bar renders one tab per pane. That
  equation dissolves the moment one tab holds two panes.
- Slice 3 shipped `keybindings` as an operator-facing map; whatever actions
  splits add must be additive to that vocabulary, and existing actions must
  not silently change meaning for a user who never splits.
- xterm panes must never unmount (scrollback and cell metrics die), and the
  WebGL renderer (#81) can in principle lose its context when a canvas is
  reparented mid-split.

## Considered options

- **T1 — Binary split tree per tab**: each tab owns
  `LayoutNode = leaf(sessionId) | split(dir, ratio, a, b)`; splitting wraps
  the focused leaf in a split node, closing promotes the sibling.
- **T2 — Flat per-tab pane list with a grid heuristic**: tabs hold `Pane[]`,
  geometry derived (1→full, 2→halves, 3→one-plus-two…).
- **T3 — Global tiling, no tabs**: one tree for the whole window, tabs
  retired (the i3 model).

## Decision outcome

Chosen option: **T1 — binary split tree per tab**.

T2 has no stable identity for "this split, at this ratio" — the geometry is a
function of count, so drag-resize and any future persisted layout have
nothing to hold onto; it un-decides exactly the schema question this ADR
exists to settle. T3 throws away the tab bar slice 1 just shipped and makes
the persistence schema *bigger* (workspaces would have to be reinvented
inside the tree). T1 is the tmux/iTerm-shaped model: minimal node vocabulary,
every operation local to one node, and the tree is plain data — N-1-D can
serialize it verbatim, which is the deferral ADR-0021 asked this slice to
repay.

Decisions bundled here, so the implementation does not improvise them:

- **Tab = workspace.** A tab owns one tree and one focused pane. The
  registry keeps global session lifecycle (spawn/kill/route-by-sessionId);
  tab/tree bookkeeping is chrome state next to it, not sidecar state. The
  RPC surface does not change.
- **Focus semantics are backward-compatible.** `pane.next`/`pane.prev`
  cycle panes within the active tab and fall through to the neighboring tab
  when the active tab has exactly one pane — byte-for-byte today's behavior
  until the first split exists. Splitting focuses the new pane. New additive
  actions: `pane.splitRight` (`mod+shift+d`) and `pane.splitDown`
  (`mod+shift+f`); both spawn an unqualified pane (the ADR-0013/0014
  host-default chain decides its kind), matching the tab bar's `+`.
- **Close collapses.** Closing a pane replaces its parent split with the
  sibling subtree; closing a tab's last pane closes the tab. A pane that
  *exits* stays in place with its scrollback (ADR-0013 fail-loud); a tab
  renders exited only when every pane in it has exited.
- **Ratio is part of the model, adjusted by drag.** Each split node renders
  a gutter; dragging updates `ratio`, clamped to 0.1–0.9, with pointer
  capture on the gutter so xterm never sees the drag's pointer stream. No
  minimum-size solver beyond the clamp — that is layout-engine
  territory this chrome does not need.
- **Persistence is explicitly out.** The tree is serializable by
  construction (`dir`/`ratio`/`sessionId` leaves), and that is this ADR's
  entire contribution to N-1-D: the shape exists and is frozen by usage, not
  by a persistence feature shipped early.

### Consequences

- Good: FT-1 completes; N-1-D has a concrete, already-exercised schema to
  persist instead of a guess.
- Good: the sidecar is untouched — the whole slice is `src-ui` chrome, and
  a user who never splits sees no behavioral change.
- Bad: split/close reparent pane containers, which can momentarily disturb
  the WebGL canvas; accepted because the #81 `onContextLoss` → DOM-renderer
  fallback already nets that failure, and containers are moved, never
  recreated.
- Bad: no directional focus navigation (`focus left/right/up/down`) — cycle
  order is tree traversal. Deferred until real use shows the cycle is
  insufficient; the tree makes directional nav addable without schema
  change.
- Neutral: `src-ui` still has no test framework; the tree operations are
  pure functions precisely so their correctness is inspectable, and the
  interactive geometry is verified by operator smoke rather than CI.
