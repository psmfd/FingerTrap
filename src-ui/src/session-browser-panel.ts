import * as api from './api';
import {
  buildBrowserModel,
  planResume,
  relativeAge,
  type BrowserModel,
  type SessionNode,
} from './session-browser';

/**
 * The session browser panel (FT-2 slice 5, ADR-0026): a StatusPanel-style
 * toggleable overlay — NOT a pane kind in the layout tree; it *produces*
 * pane opens. Every sidecar-sourced string renders via textContent through
 * the factory below (ADR-0022); the sidecar sanitized at construction and
 * this layer refuses to reintroduce markup interpretation.
 *
 * Resume actions delegate to the host (main.ts wires them into the
 * registry): `sessionPath` is the only functional key that leaves this
 * panel. The orphan section is read-only — reap/unlock stay pi-side
 * `/worktree` commands.
 */

export interface ResumeActions {
  /** Open a native RPC pane resuming the session. */
  resumeRpc(sessionPath: string): void;
  /** Open a PTY pi pane resuming the session, optionally in a cwd. */
  resumePty(sessionPath: string, cwd?: string): void;
}

export class SessionBrowserPanel {
  private readonly root: HTMLElement;
  private readonly actions: ResumeActions;
  private visible = false;
  private filter = '';
  private sessions: api.SessionsListResult | undefined;
  private worktrees: api.WorktreesListResult | undefined;
  private loadError: string | undefined;

  constructor(host: HTMLElement, actions: ResumeActions) {
    this.actions = actions;
    this.root = el('div', 'session-browser');
    this.root.hidden = true;
    host.appendChild(this.root);
  }

  toggle(): void {
    this.visible = !this.visible;
    this.root.hidden = !this.visible;
    if (this.visible) {
      this.render();
      void this.refresh();
    }
  }

  /** Fetch both lists; each degrades alone into an error line. */
  private async refresh(): Promise<void> {
    this.loadError = undefined;
    const [sessions, worktrees] = await Promise.allSettled([
      api.sessionsList(),
      api.worktreesList(),
    ]);
    if (sessions.status === 'fulfilled') {
      this.sessions = sessions.value;
    } else {
      this.loadError = `session scan failed: ${String(sessions.reason)}`;
    }
    if (worktrees.status === 'fulfilled') {
      this.worktrees = worktrees.value;
    } else {
      // Orphan surfacing is auxiliary; sessions still render without it.
      this.loadError ??= `worktree scan failed: ${String(worktrees.reason)}`;
    }
    if (this.visible) {
      this.render();
    }
  }

  private model(): BrowserModel | undefined {
    if (this.sessions === undefined) return undefined;
    return buildBrowserModel(
      this.sessions.sessions,
      this.worktrees?.records ?? [],
      this.sessions.totalCount,
      this.filter,
    );
  }

  private render(): void {
    this.root.replaceChildren();
    this.root.appendChild(this.renderHeader());
    if (this.loadError !== undefined) {
      this.root.appendChild(el('p', 'sb-error', this.loadError));
    }

    const model = this.model();
    if (model === undefined) {
      this.root.appendChild(el('p', 'sb-empty', 'scanning sessions…'));
      return;
    }

    const count =
      model.totalCount > model.shownCount
        ? `${model.shownCount} of ${model.totalCount} sessions`
        : `${model.shownCount} sessions`;
    this.root.appendChild(el('p', 'sb-count', count));

    // Corruption is a visible fact, not a silent absence (#140): files the
    // sidecar attempted but could not parse get one quiet line; normal
    // rows are untouched.
    const skipped = this.sessions?.skippedFiles ?? 0;
    if (skipped > 0) {
      const noun = skipped === 1 ? 'file' : 'files';
      this.root.appendChild(el('p', 'sb-skipped', `${skipped} unparseable session ${noun}`));
    }

    for (const group of model.groups) {
      const section = el('section', 'sb-group');
      section.appendChild(el('h3', 'sb-repo', group.repo));
      const list = el('ul', 'sb-rows');
      for (const node of group.roots) {
        this.appendNode(list, node, 0);
      }
      section.appendChild(list);
      this.root.appendChild(section);
    }

    if (model.orphans.length > 0) {
      this.root.appendChild(this.renderOrphans(model.orphans));
    }
  }

  private renderHeader(): HTMLElement {
    const header = el('div', 'sb-header');
    header.appendChild(el('h2', 'sb-title', 'sessions'));
    const filter = document.createElement('input');
    filter.className = 'sb-filter';
    filter.placeholder = 'filter by name, first message, or directory';
    filter.autocomplete = 'off';
    filter.spellcheck = false;
    filter.value = this.filter;
    filter.addEventListener('input', () => {
      this.filter = filter.value;
      this.render();
      // render() rebuilt the header; keep typing in the live input.
      const next = this.root.querySelector<HTMLInputElement>('.sb-filter');
      if (next) {
        next.focus();
        next.setSelectionRange(next.value.length, next.value.length);
      }
    });
    header.appendChild(filter);
    return header;
  }

  private appendNode(list: HTMLElement, node: SessionNode, depth: number): void {
    const s = node.session;
    const li = el('li', 'sb-row');
    if (depth > 0) {
      li.style.marginLeft = `${depth}rem`;
    }

    const line = el('div', 'sb-line');
    line.appendChild(el('span', 'sb-label', s.name ?? (s.firstMessage || '(no messages)')));
    line.appendChild(el('span', 'sb-age', relativeAge(s.modifiedAt, Date.now())));
    line.appendChild(el('span', 'sb-msgcount', `${s.messageCount} msg`));
    if (node.forkChild) {
      line.appendChild(el('span', 'sb-badge sb-badge-fork', 'fork'));
    }
    if (s.reapedWorktree) {
      line.appendChild(el('span', 'sb-badge sb-badge-reaped', 'reaped worktree'));
    } else if (s.cwdMissing) {
      line.appendChild(el('span', 'sb-badge sb-badge-missing', 'cwd missing'));
    }
    if (node.orphan !== undefined) {
      const host = node.orphan.host;
      line.appendChild(
        el('span', 'sb-badge sb-badge-orphan', host === null ? 'orphan' : `orphan on ${host}`),
      );
    }

    const plan = planResume(s);
    const rpc = button('sb-resume', 'resume (rpc)', () => {
      this.actions.resumeRpc(s.sessionPath);
      this.toggle();
    });
    if (!plan.rpcEnabled) {
      rpc.disabled = true;
      // title is a native tooltip and a plain-text sink; the reason string
      // is panel-authored, not sidecar content.
      rpc.title = plan.rpcDisabledReason ?? '';
    }
    line.appendChild(rpc);
    line.appendChild(
      button('sb-resume', 'resume (pty)', () => {
        this.actions.resumePty(s.sessionPath, plan.ptyCwd);
        this.toggle();
      }),
    );
    li.appendChild(line);
    li.appendChild(el('div', 'sb-cwd', s.cwd));
    list.appendChild(li);

    for (const child of node.children) {
      this.appendNode(list, child, depth + 1);
    }
  }

  private renderOrphans(orphans: readonly api.WorktreeRecord[]): HTMLElement {
    const section = el('section', 'sb-group sb-orphans');
    section.appendChild(el('h3', 'sb-repo', 'orphaned worktrees (read-only — use /worktree in pi to reap or unlock)'));
    const list = el('ul', 'sb-rows');
    for (const record of orphans) {
      const li = el('li', 'sb-row');
      const line = el('div', 'sb-line');
      line.appendChild(el('span', 'sb-label', record.branch ?? record.sid));
      line.appendChild(el('span', 'sb-badge sb-badge-orphan', record.shape));
      if (record.host !== null) {
        line.appendChild(el('span', 'sb-age', `host ${record.host}`));
      }
      if (record.wipSha !== null) {
        line.appendChild(el('span', 'sb-age', 'wip snapshot'));
      }
      li.appendChild(line);
      li.appendChild(el('div', 'sb-cwd', record.worktreePath ?? record.repo ?? ''));
      list.appendChild(li);
    }
    section.appendChild(list);
    return section;
  }
}

/** textContent-only element factory — the panel's single DOM sink. */
function el(tag: string, className: string, text?: string): HTMLElement {
  const node = document.createElement(tag);
  node.className = className;
  if (text !== undefined) {
    node.textContent = text;
  }
  return node;
}

function button(className: string, label: string, onClick: () => void): HTMLButtonElement {
  const node = document.createElement('button');
  node.type = 'button';
  node.className = className;
  node.textContent = label;
  node.addEventListener('click', onClick);
  return node;
}
