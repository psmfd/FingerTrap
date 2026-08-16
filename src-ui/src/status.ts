import { invoke } from '@tauri-apps/api/core';
import * as api from './api';

/**
 * The status overlay (ADR-0022, slice 2): renders the latest snapshot and
 * hosts token entry. Every provider-sourced string is assigned via
 * textContent (through the DOM factory helpers below) — never innerHTML;
 * the sidecar sanitized at row construction, this layer refuses to be the
 * one that reintroduces markup interpretation (defense in depth).
 *
 * Token flow: the operator pastes a token here once; it goes straight to the
 * Rust shell via invoke('credential_save') — keychain plus sidecar delivery
 * — and never comes back. There is no credential_get anywhere.
 */
/**
 * Providers whose auth is a keychain-held PAT — mirrors the Rust shell's
 * PROVIDERS list in credentials.rs. The local-git provider takes no token;
 * rendering a PAT box for its not-configured state would be a lie.
 */
const TOKEN_PROVIDERS = new Set(['github', 'ado']);

/** One rendered row: text always, url only when the sidecar validated it. */
interface RowEntry {
  text: string;
  url?: string | null;
}

export class StatusPanel {
  private readonly root: HTMLElement;
  private snapshot: api.StatusSnapshotNotification | undefined;
  private visible = false;

  constructor(host: HTMLElement) {
    this.root = el('div', 'status-panel');
    this.root.hidden = true;
    host.appendChild(this.root);
  }

  toggle(): void {
    this.visible = !this.visible;
    this.root.hidden = !this.visible;
    if (this.visible) {
      this.render();
      api.statusRefresh().catch(() => {
        // Best-effort: the periodic poll covers a lost refresh.
      });
    }
  }

  update(snapshot: api.StatusSnapshotNotification): void {
    this.snapshot = snapshot;
    if (this.visible) {
      this.render();
    }
  }

  private render(): void {
    this.root.replaceChildren();
    const providers = this.snapshot?.providers ?? [];
    if (providers.length === 0) {
      this.root.appendChild(el('p', 'status-empty', 'waiting for the first snapshot…'));
      return;
    }
    for (const provider of providers) {
      this.root.appendChild(this.renderProvider(provider));
    }
  }

  private renderProvider(p: api.ProviderSnapshot): HTMLElement {
    const section = el('section', 'status-provider');
    const head = el('header', 'status-head');
    head.appendChild(el('span', 'status-name', p.provider));
    head.appendChild(el('span', `status-state status-state-${cssToken(p.state)}`, p.state));
    section.appendChild(head);

    if (p.detail) {
      section.appendChild(el('p', 'status-detail', p.detail));
    }

    if (TOKEN_PROVIDERS.has(p.provider) && (p.state === 'not-configured' || p.state === 'auth-failed')) {
      section.appendChild(this.renderTokenEntry(p.provider));
    }

    if (p.runs.length > 0) {
      section.appendChild(this.renderList('CI runs', p.runs.map((r) => ({
        text: `${glyph(r.outcome)} ${r.workflowName} — ${r.displayTitle} (${r.headBranch})`,
        url: r.url,
      }))));
    }
    if (p.pullRequests.length > 0) {
      section.appendChild(this.renderList('Pull requests', p.pullRequests.map((pr) => ({
        text: `#${pr.number} ${pr.isDraft ? '[draft] ' : ''}${pr.title} — ${pr.author}`,
        url: pr.url,
      }))));
    }
    if (p.issues.length > 0) {
      section.appendChild(this.renderList('Issues', p.issues.map((i) => ({
        text: `#${i.number} ${i.title} — ${i.author}`,
        url: i.url,
      }))));
    }
    return section;
  }

  private renderList(heading: string, rows: RowEntry[]): HTMLElement {
    const wrap = el('div', 'status-list');
    wrap.appendChild(el('h3', 'status-list-title', heading));
    const ul = el('ul', 'status-rows');
    for (const row of rows) {
      const li = el('li', 'status-row');
      if (row.url) {
        // Linked rows are buttons invoking the shell's validated open_url
        // command (ADR-0023) — never <a href>: the WebView must not
        // navigate, and the URL must pass the shell's second gate.
        const url = row.url;
        const button = el('button', 'status-row-link', row.text) as HTMLButtonElement;
        button.type = 'button';
        button.addEventListener('click', () => {
          invoke('open_url', { url }).catch((err: unknown) => {
            // Shell refused (off-allowlist), command missing (stale binary),
            // or opener failed. The row deliberately stays visually put, but
            // the cause must be one DevTools glance away — a swallowed
            // rejection is indistinguishable from a dead click handler (#82).
            console.warn('open_url failed', url, err);
          });
        });
        li.appendChild(button);
      } else {
        li.textContent = row.text;
      }
      ul.appendChild(li);
    }
    wrap.appendChild(ul);
    return wrap;
  }

  private renderTokenEntry(provider: string): HTMLElement {
    const form = el('div', 'status-token');
    const input = document.createElement('input');
    input.type = 'password';
    input.placeholder = `paste a fine-grained read-only PAT for ${provider}`;
    input.autocomplete = 'off';
    form.appendChild(input);

    const save = el('button', 'status-save', 'save') as HTMLButtonElement;
    save.type = 'button';
    const message = el('span', 'status-token-msg', '');
    save.addEventListener('click', () => {
      const token = input.value;
      if (!token) return;
      save.disabled = true;
      invoke('credential_save', { provider, token })
        .then(() => {
          input.value = '';
          message.textContent = 'saved to the OS keychain';
        })
        .catch((err: unknown) => {
          message.textContent = String(err);
        })
        .finally(() => {
          save.disabled = false;
        });
    });
    form.appendChild(save);
    form.appendChild(message);
    return form;
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

/** Provider state is untrusted-ish future-proof text; constrain what can
 * reach a class name. */
function cssToken(value: string): string {
  return value.replace(/[^a-z0-9-]/gi, '');
}

function glyph(outcome: string): string {
  switch (outcome) {
    case 'success':
      return '✓';
    case 'failure':
    case 'timed_out':
    case 'startup_failure':
      return '✗';
    case 'in_progress':
      return '◐';
    case 'queued':
      return '…';
    case 'cancelled':
    case 'skipped':
      return '⊘';
    default:
      return '?';
  }
}
