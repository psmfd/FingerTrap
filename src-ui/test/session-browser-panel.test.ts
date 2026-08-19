import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { SessionSummary, SessionsListResult, WorktreesListResult } from '../src/api';

vi.mock('../src/api', () => ({
  sessionsList: vi.fn(),
  worktreesList: vi.fn(),
}));

import * as api from '../src/api';
import { SessionBrowserPanel, type ResumeActions } from '../src/session-browser-panel';

function session(overrides: Partial<SessionSummary> & { sessionPath: string }): SessionSummary {
  return {
    id: overrides.sessionPath,
    cwd: '/repo',
    name: null,
    firstMessage: 'hello there',
    messageCount: 3,
    createdAt: '2026-08-18T10:00:00+00:00',
    modifiedAt: '2026-08-18T10:00:00+00:00',
    parentSessionPath: null,
    cwdMissing: false,
    reapedWorktree: false,
    originalRepo: null,
    ...overrides,
  };
}

function stubLists(sessions: SessionSummary[], worktrees: WorktreesListResult = { records: [] }) {
  vi.mocked(api.sessionsList).mockResolvedValue({
    sessions,
    totalCount: sessions.length,
  } satisfies SessionsListResult);
  vi.mocked(api.worktreesList).mockResolvedValue(worktrees);
}

async function settled(): Promise<void> {
  // Let the refresh() promise chain flush.
  await new Promise((resolve) => setTimeout(resolve, 0));
}

describe('SessionBrowserPanel', () => {
  let host: HTMLElement;
  let actions: { resumeRpc: ReturnType<typeof vi.fn>; resumePty: ReturnType<typeof vi.fn> };
  let panel: SessionBrowserPanel;

  beforeEach(() => {
    vi.clearAllMocks();
    host = document.createElement('div');
    document.body.appendChild(host);
    actions = { resumeRpc: vi.fn(), resumePty: vi.fn() };
    panel = new SessionBrowserPanel(host, actions as unknown as ResumeActions);
  });

  it('starts hidden and fetches both lists on first toggle', async () => {
    stubLists([session({ sessionPath: '/s/a.jsonl' })]);
    const root = host.querySelector<HTMLElement>('.session-browser')!;
    expect(root.hidden).toBe(true);

    panel.toggle();
    await settled();

    expect(root.hidden).toBe(false);
    expect(api.sessionsList).toHaveBeenCalledOnce();
    expect(api.worktreesList).toHaveBeenCalledOnce();
    expect(root.textContent).toContain('hello there');
    expect(root.textContent).toContain('/repo');
  });

  it('resume (rpc) is enabled for a live cwd and hands over the sessionPath', async () => {
    stubLists([session({ sessionPath: '/s/a.jsonl' })]);
    panel.toggle();
    await settled();

    const buttons = host.querySelectorAll<HTMLButtonElement>('.sb-resume');
    expect(buttons[0].textContent).toBe('resume (rpc)');
    expect(buttons[0].disabled).toBe(false);
    buttons[0].click();

    expect(actions.resumeRpc).toHaveBeenCalledWith('/s/a.jsonl');
    // A resume closes the panel — the pane is what the operator wants next.
    expect(host.querySelector<HTMLElement>('.session-browser')!.hidden).toBe(true);
  });

  it('resume (rpc) is disabled with an ADR-0026 reason when the cwd is missing', async () => {
    stubLists([
      session({
        sessionPath: '/s/reaped.jsonl',
        cwd: '/repo/.worktrees/x',
        cwdMissing: true,
        reapedWorktree: true,
        originalRepo: '/repo',
      }),
    ]);
    panel.toggle();
    await settled();

    const buttons = host.querySelectorAll<HTMLButtonElement>('.sb-resume');
    expect(buttons[0].disabled).toBe(true);
    expect(buttons[0].title).toContain('ADR-0026');

    buttons[1].click();
    expect(actions.resumePty).toHaveBeenCalledWith('/s/reaped.jsonl', '/repo');
  });

  it('a failed session scan renders the error instead of rows', async () => {
    vi.mocked(api.sessionsList).mockRejectedValue(new Error('sidecar gone'));
    vi.mocked(api.worktreesList).mockResolvedValue({ records: [] });
    panel.toggle();
    await settled();

    expect(host.querySelector('.sb-error')?.textContent).toContain('session scan failed');
  });

  it('renders the read-only orphan section with host and shape', async () => {
    stubLists(
      [session({ sessionPath: '/s/a.jsonl', id: 'sid-1' })],
      {
        records: [
          {
            sid: 'sid-1',
            worktreePath: '/repo/.worktrees/sid-1',
            branch: 'feat/x',
            repo: '/repo',
            host: 'otherhost',
            wipSha: 'a'.repeat(40),
            pid: 42,
            alive: false,
            shape: 'dead',
          },
        ],
      },
    );
    panel.toggle();
    await settled();

    const orphans = host.querySelector<HTMLElement>('.sb-orphans')!;
    expect(orphans.textContent).toContain('feat/x');
    expect(orphans.textContent).toContain('otherhost');
    expect(orphans.textContent).toContain('dead');
    // Read-only: no resume/reap buttons in the orphan section.
    expect(orphans.querySelectorAll('button')).toHaveLength(0);
  });

  it('filter input narrows rows and shows the shown-of-total count', async () => {
    stubLists([
      session({ sessionPath: '/s/a.jsonl', name: 'alpha' }),
      session({ sessionPath: '/s/b.jsonl', name: 'beta' }),
    ]);
    panel.toggle();
    await settled();

    const filter = host.querySelector<HTMLInputElement>('.sb-filter')!;
    filter.value = 'alpha';
    filter.dispatchEvent(new Event('input', { bubbles: true }));

    const root = host.querySelector<HTMLElement>('.session-browser')!;
    expect(root.textContent).toContain('1 of 2 sessions');
    expect(root.textContent).toContain('alpha');
    expect(root.textContent).not.toContain('beta');
  });
});
