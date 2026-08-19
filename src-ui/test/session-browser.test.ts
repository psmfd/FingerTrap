import { describe, expect, it } from 'vitest';
import type { SessionSummary, WorktreeRecord } from '../src/api';
import {
  buildBrowserModel,
  matchesFilter,
  planResume,
  relativeAge,
  repoKey,
} from '../src/session-browser';

function session(overrides: Partial<SessionSummary> & { sessionPath: string }): SessionSummary {
  return {
    id: overrides.sessionPath,
    cwd: '/repo',
    name: null,
    firstMessage: '',
    messageCount: 1,
    createdAt: '2026-08-18T10:00:00+00:00',
    modifiedAt: '2026-08-18T10:00:00+00:00',
    parentSessionPath: null,
    cwdMissing: false,
    reapedWorktree: false,
    originalRepo: null,
    ...overrides,
  };
}

function record(overrides: Partial<WorktreeRecord> & { sid: string }): WorktreeRecord {
  return {
    worktreePath: `/repo/.worktrees/${overrides.sid}`,
    branch: null,
    repo: '/repo',
    host: null,
    wipSha: null,
    pid: null,
    alive: false,
    shape: 'dead',
    ...overrides,
  };
}

describe('buildBrowserModel', () => {
  it('groups by repo and sorts groups and roots by recency', () => {
    const model = buildBrowserModel(
      [
        session({ sessionPath: '/s/old.jsonl', cwd: '/repo-a', modifiedAt: '2026-08-01T00:00:00+00:00' }),
        session({ sessionPath: '/s/new.jsonl', cwd: '/repo-b', modifiedAt: '2026-08-18T00:00:00+00:00' }),
        session({ sessionPath: '/s/mid.jsonl', cwd: '/repo-a', modifiedAt: '2026-08-10T00:00:00+00:00' }),
      ],
      [],
      3,
    );

    expect(model.groups.map((g) => g.repo)).toEqual(['/repo-b', '/repo-a']);
    expect(model.groups[1].roots.map((r) => r.session.sessionPath)).toEqual([
      '/s/mid.jsonl',
      '/s/old.jsonl',
    ]);
    expect(model.shownCount).toBe(3);
    expect(model.totalCount).toBe(3);
  });

  it('groups a reaped worktree session under its original repo', () => {
    const reaped = session({
      sessionPath: '/s/reaped.jsonl',
      cwd: '/repo/.worktrees/abc',
      cwdMissing: true,
      reapedWorktree: true,
      originalRepo: '/repo',
    });

    expect(repoKey(reaped)).toBe('/repo');
    const model = buildBrowserModel([reaped], [], 1);
    expect(model.groups[0].repo).toBe('/repo');
  });

  it('threads forks under their parent and flags them', () => {
    const model = buildBrowserModel(
      [
        session({ sessionPath: '/s/parent.jsonl' }),
        session({ sessionPath: '/s/child.jsonl', parentSessionPath: '/s/parent.jsonl' }),
      ],
      [],
      2,
    );

    const roots = model.groups[0].roots;
    expect(roots).toHaveLength(1);
    expect(roots[0].session.sessionPath).toBe('/s/parent.jsonl');
    expect(roots[0].children).toHaveLength(1);
    expect(roots[0].children[0].session.sessionPath).toBe('/s/child.jsonl');
    expect(roots[0].children[0].forkChild).toBe(true);
  });

  it('renders a dangling parent as a root but keeps the fork badge', () => {
    const model = buildBrowserModel(
      [session({ sessionPath: '/s/child.jsonl', parentSessionPath: '/gone/parent.jsonl' })],
      [],
      1,
    );

    const roots = model.groups[0].roots;
    expect(roots).toHaveLength(1);
    expect(roots[0].forkChild).toBe(true);
  });

  it('joins orphan records to sessions by sid and lists the orphan section', () => {
    const model = buildBrowserModel(
      [session({ sessionPath: '/s/one.jsonl', id: 'sid-1' })],
      [
        record({ sid: 'sid-1', host: 'otherhost' }),
        record({ sid: 'sid-live', alive: true, shape: 'live' }),
        record({ sid: 'sid-unrelated' }),
      ],
      1,
    );

    const node = model.groups[0].roots[0];
    expect(node.orphan?.host).toBe('otherhost');
    // Live records are neither badges nor orphan rows.
    expect(model.orphans.map((o) => o.sid)).toEqual(['sid-1', 'sid-unrelated']);
  });

  it('filters on name, first message, and cwd only', () => {
    const named = session({ sessionPath: '/s/a.jsonl', name: 'Alpha Work' });
    const first = session({ sessionPath: '/s/b.jsonl', firstMessage: 'fix the beta bug' });
    const bycwd = session({ sessionPath: '/s/c.jsonl', cwd: '/projects/gamma' });

    expect(matchesFilter(named, 'alpha')).toBe(true);
    expect(matchesFilter(first, 'BETA')).toBe(true);
    expect(matchesFilter(bycwd, 'gamma')).toBe(true);
    expect(matchesFilter(named, 'zeta')).toBe(false);

    const model = buildBrowserModel([named, first, bycwd], [], 3, 'alpha');
    expect(model.shownCount).toBe(1);
    expect(model.totalCount).toBe(3);
  });

  it('a filtered-out parent turns its children into roots', () => {
    const model = buildBrowserModel(
      [
        session({ sessionPath: '/s/parent.jsonl', name: 'parent only' }),
        session({
          sessionPath: '/s/child.jsonl',
          name: 'child match',
          parentSessionPath: '/s/parent.jsonl',
        }),
      ],
      [],
      2,
      'child match',
    );

    const roots = model.groups[0].roots;
    expect(roots).toHaveLength(1);
    expect(roots[0].session.sessionPath).toBe('/s/child.jsonl');
    expect(roots[0].forkChild).toBe(true);
  });
});

describe('planResume (ADR-0026)', () => {
  it('cwd present: rpc enabled, pty resumes in the recorded cwd', () => {
    const plan = planResume(session({ sessionPath: '/s/a.jsonl', cwd: '/repo' }));
    expect(plan.rpcEnabled).toBe(true);
    expect(plan.ptyCwd).toBe('/repo');
  });

  it('reaped worktree: rpc disabled with reason, pty falls back to the original repo', () => {
    const plan = planResume(
      session({
        sessionPath: '/s/a.jsonl',
        cwd: '/repo/.worktrees/x',
        cwdMissing: true,
        reapedWorktree: true,
        originalRepo: '/repo',
      }),
    );
    expect(plan.rpcEnabled).toBe(false);
    expect(plan.rpcDisabledReason).toContain('ADR-0026');
    expect(plan.ptyCwd).toBe('/repo');
  });

  it('plain missing cwd: rpc disabled, pty offered with no cwd (pi prompts)', () => {
    const plan = planResume(
      session({ sessionPath: '/s/a.jsonl', cwd: '/gone', cwdMissing: true }),
    );
    expect(plan.rpcEnabled).toBe(false);
    expect(plan.ptyCwd).toBeUndefined();
  });
});

describe('relativeAge', () => {
  const now = Date.parse('2026-08-18T12:00:00Z');

  it('formats seconds, minutes, hours, and days compactly', () => {
    expect(relativeAge('2026-08-18T11:59:30Z', now)).toBe('30s');
    expect(relativeAge('2026-08-18T11:30:00Z', now)).toBe('30m');
    expect(relativeAge('2026-08-18T03:00:00Z', now)).toBe('9h');
    expect(relativeAge('2026-08-10T12:00:00Z', now)).toBe('8d');
  });

  it('falls back to the raw string when unparseable', () => {
    expect(relativeAge('not a date', now)).toBe('not a date');
  });
});
