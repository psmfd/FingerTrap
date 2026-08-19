import type { SessionSummary, WorktreeRecord } from './api';

/**
 * Session-browser view model (FT-2 slice 5, ADR-0026) — pure logic, split
 * from the DOM view (session-browser-panel.ts) the same way transcript.ts
 * is split from transcript-view.ts, so grouping, threading, filtering, and
 * badge derivation unit-test without a DOM.
 *
 * Everything in the inputs is sanitized display data from the sidecar
 * except `sessionPath`, the only functional resume key. This module never
 * invents display text from unsanitized sources — it only rearranges what
 * the sidecar already sanitized.
 */

/** One session row, threaded: children are forks whose header named this
 * session as parent. A session whose parent is not in the (filtered) list
 * renders as a root — dangling parents are expected (deleted files). */
export interface SessionNode {
  readonly session: SessionSummary;
  readonly children: readonly SessionNode[];
  /** True when this node's parent exists but was filtered out or missing —
   * i.e. the node is a fork rendered at root level. */
  readonly forkChild: boolean;
  /** The dead worktree record whose sid matches this session's id, if any —
   * the orphan-join badge. */
  readonly orphan: WorktreeRecord | undefined;
}

/** Sessions grouped by the repo they belong to: the original repo for a
 * reaped worktree, else the recorded cwd. */
export interface RepoGroup {
  readonly repo: string;
  readonly roots: readonly SessionNode[];
}

export interface BrowserModel {
  readonly groups: readonly RepoGroup[];
  /** Reconciled worktree records with a dead (or unknowable) pid — the
   * read-only orphan section. Reap/unlock stay pi-side commands. */
  readonly orphans: readonly WorktreeRecord[];
  /** Sessions rendered after filtering. */
  readonly shownCount: number;
  /** Total session files on disk (can exceed what the sidecar parsed). */
  readonly totalCount: number;
}

/** The repo a session belongs to for grouping. */
export function repoKey(session: SessionSummary): string {
  return session.reapedWorktree && session.originalRepo !== null
    ? session.originalRepo
    : session.cwd;
}

/** Case-insensitive substring filter over name, first message, and cwd —
 * the only fields the UI filters on (the sidecar never aggregates full
 * message text; see SessionStore). */
export function matchesFilter(session: SessionSummary, filter: string): boolean {
  if (filter.length === 0) return true;
  const needle = filter.toLowerCase();
  return (
    (session.name ?? '').toLowerCase().includes(needle) ||
    session.firstMessage.toLowerCase().includes(needle) ||
    session.cwd.toLowerCase().includes(needle)
  );
}

export function buildBrowserModel(
  sessions: readonly SessionSummary[],
  worktrees: readonly WorktreeRecord[],
  totalCount: number,
  filter = '',
): BrowserModel {
  const shown = sessions.filter((s) => matchesFilter(s, filter));
  const orphanBySid = new Map<string, WorktreeRecord>();
  for (const record of worktrees) {
    if (!record.alive) orphanBySid.set(record.sid, record);
  }

  // Thread forks: parentSessionPath → sessionPath, resolved among the
  // filtered set only. A filtered-out or missing parent makes its children
  // roots (flagged forkChild so the row can still show the badge).
  const byPath = new Map(shown.map((s) => [s.sessionPath, s]));
  const childrenOf = new Map<string, SessionSummary[]>();
  const roots: SessionSummary[] = [];
  for (const session of shown) {
    const parent = session.parentSessionPath;
    if (parent !== null && byPath.has(parent) && parent !== session.sessionPath) {
      const list = childrenOf.get(parent) ?? [];
      list.push(session);
      childrenOf.set(parent, list);
    } else {
      roots.push(session);
    }
  }

  // Numeric compare, not lexical: the sidecar emits ISO timestamps whose
  // offsets are uniform today, but sorting must not depend on that.
  const byModifiedDesc = (a: SessionSummary, b: SessionSummary): number =>
    (Date.parse(b.modifiedAt) || 0) - (Date.parse(a.modifiedAt) || 0);

  const toNode = (session: SessionSummary, forkChild: boolean): SessionNode => ({
    session,
    forkChild,
    orphan: orphanBySid.get(session.id),
    children: (childrenOf.get(session.sessionPath) ?? [])
      .slice()
      .sort(byModifiedDesc)
      .map((child) => toNode(child, true)),
  });

  const groups = new Map<string, SessionNode[]>();
  for (const session of roots.slice().sort(byModifiedDesc)) {
    const key = repoKey(session);
    const list = groups.get(key) ?? [];
    list.push(toNode(session, session.parentSessionPath !== null));
    groups.set(key, list);
  }

  // Groups sort by their newest root — roots are already modified-desc, so
  // the first root is the group's recency.
  const groupList: RepoGroup[] = [...groups.entries()]
    .map(([repo, groupRoots]) => ({ repo, roots: groupRoots }))
    .sort((a, b) => byModifiedDesc(a.roots[0].session, b.roots[0].session));

  return {
    groups: groupList,
    orphans: worktrees.filter((r) => !r.alive),
    shownCount: shown.length,
    totalCount,
  };
}

/**
 * Resume-action availability per ADR-0026: a missing cwd disables RPC-pane
 * resume (spawn-time `--session` in rpc mode hard-exits with no JSON
 * error); PTY-pane resume is always offered — interactive pi owns the
 * missing-cwd fallback prompt. `ptyCwd` is the pane cwd a PTY resume
 * should pass: the original repo for a reaped worktree, the recorded cwd
 * when it still exists, nothing otherwise (pi falls back to process cwd).
 */
export interface ResumePlan {
  readonly rpcEnabled: boolean;
  readonly rpcDisabledReason: string | undefined;
  readonly ptyCwd: string | undefined;
}

export function planResume(session: SessionSummary): ResumePlan {
  if (!session.cwdMissing) {
    return { rpcEnabled: true, rpcDisabledReason: undefined, ptyCwd: session.cwd };
  }
  return {
    rpcEnabled: false,
    rpcDisabledReason: session.reapedWorktree
      ? 'working directory was a reaped worktree — resume as a PTY pane instead (ADR-0026)'
      : 'working directory no longer exists — resume as a PTY pane instead (ADR-0026)',
    ptyCwd:
      session.reapedWorktree && session.originalRepo !== null ? session.originalRepo : undefined,
  };
}

/** Compact relative age ("3m", "2h", "5d") for rows; absolute dates read
 * worse in a dense list. Falls back to the raw string when unparseable. */
export function relativeAge(iso: string, nowMs: number): string {
  const then = Date.parse(iso);
  if (Number.isNaN(then)) return iso;
  const seconds = Math.max(0, Math.floor((nowMs - then) / 1000));
  if (seconds < 60) return `${seconds}s`;
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h`;
  return `${Math.floor(hours / 24)}d`;
}
