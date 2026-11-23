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
  private readonly credentials = new Map(
    [...TOKEN_PROVIDERS].map((provider) => [provider, new CredentialControls(provider)]),
  );
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
      for (const controls of this.credentials.values()) void controls.refresh();
      api.statusRefresh().catch(() => {
        // Best-effort: the periodic poll covers a lost refresh.
      });
    } else {
      for (const controls of this.credentials.values()) controls.discardInput();
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
    const snapshots = new Map(this.snapshot?.providers.map((p) => [p.provider, p]));
    const providers = new Set([...snapshots.keys(), ...this.credentials.keys()]);
    for (const provider of providers) {
      this.root.appendChild(this.renderProvider(provider, snapshots.get(provider)));
    }
  }

  private renderProvider(provider: string, p?: api.ProviderSnapshot): HTMLElement {
    const section = el('section', 'status-provider');
    const head = el('header', 'status-head');
    head.appendChild(el('span', 'status-name', provider));
    if (p) {
      head.appendChild(el('span', `status-state status-state-${cssToken(p.state)}`, p.state));
    } else {
      const text = this.snapshot ? 'Provider status unavailable' : 'Waiting for provider status…';
      head.appendChild(el('span', 'status-state', text));
    }
    section.appendChild(head);

    if (p?.detail) {
      section.appendChild(el('p', 'status-detail', p.detail));
    }

    // Local-git surfaces are one summary line with nothing to click; say so,
    // or its plain text reads as broken links next to the linked rows (#83).
    if (provider === 'git') {
      section.appendChild(el('p', 'status-hint', 'local repository status — nothing here links to a web page'));
    }

    const controls = this.credentials.get(provider);
    if (controls) section.appendChild(controls.render(p?.state));
    if (!p) return section;

    // List order is an operator-facing choice (#85): the actionable work
    // items first (PRs, then issues), pipeline state last.
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
    if (p.runs.length > 0) {
      section.appendChild(this.renderList('CI runs', p.runs.map((r) => ({
        text: `${glyph(r.outcome)} ${r.workflowName} — ${r.displayTitle} (${r.headBranch})`,
        url: r.url,
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
}

const CREDENTIAL_STATUS = {
  configured: 'Credential stored in Keychain',
  'not-configured': 'No credential stored in Keychain',
  'store-unavailable': 'Keychain unavailable',
};

interface CredentialStatus {
  store: keyof typeof CREDENTIAL_STATUS;
  retryClear: boolean;
}

/** Credential controls retain their operation state across provider snapshots. */
class CredentialControls {
  private readonly root = el('div', 'status-token');
  private readonly input = document.createElement('input');
  private readonly save = el('button', 'status-save', 'Save') as HTMLButtonElement;
  private readonly clear = el('button', 'status-save', 'Clear') as HTMLButtonElement;
  private readonly status = el('span', 'status-token-msg', 'Checking Keychain…');
  private readonly error = el('span', 'status-token-msg');
  private stored: keyof typeof CREDENTIAL_STATUS | undefined;
  private providerNeedsToken = false;
  private busy = false;
  private clearFailed = false;
  private pendingDelivery = false;
  private mutationError = '';

  constructor(private readonly provider: string) {
    this.input.type = 'password';
    this.input.autocomplete = 'off';
    this.input.placeholder = 'Paste a fine-grained read-only PAT';
    this.input.setAttribute('aria-label', `${provider} personal access token`);
    this.save.type = this.clear.type = 'button';
    this.status.setAttribute('role', 'status');
    this.error.setAttribute('role', 'alert');
    this.root.append(this.input, this.save, this.clear, this.status, this.error);

    this.save.addEventListener('click', () => {
      const token = this.input.value;
      if (!token || this.busy) return;
      this.discardInput();
      void this.refresh({ kind: 'save', run: () => invoke('credential_save', { provider, token }) });
    });
    this.clear.addEventListener('click', () => {
      if (this.busy || (!this.retryClear && this.stored !== 'configured')) return;
      this.discardInput();
      const retryDelivery = this.pendingDelivery;
      void this.refresh({
        kind: retryDelivery ? 'retryDelivery' : 'clear',
        run: () => invoke('credential_clear', retryDelivery ? { provider, retryDelivery } : { provider }),
      });
    });
  }

  private get retryClear(): boolean {
    return this.clearFailed || this.pendingDelivery;
  }

  render(providerState?: string): HTMLElement {
    this.providerNeedsToken = providerState === 'not-configured' || providerState === 'auth-failed';
    this.updateControls();
    return this.root;
  }

  discardInput(): void {
    this.input.value = '';
  }

  async refresh(mutation?: { kind: 'save' | 'clear' | 'retryDelivery'; run: () => Promise<void> }): Promise<void> {
    if (this.busy) return;
    this.busy = true;
    this.updateControls();
    if (mutation) {
      this.mutationError = '';
      if (mutation.kind === 'save') this.clearFailed = false;
      try {
        await mutation.run();
        this.clearFailed = false;
        this.pendingDelivery = false;
      } catch {
        // The Keychain mutation may have succeeded before sidecar delivery failed.
        if (mutation.kind === 'clear') this.clearFailed = true;
        this.mutationError = mutation.kind === 'save' ? 'Save failed.' : 'Clear failed.';
      }
    }
    try {
      const result = await invoke<CredentialStatus>('credential_status', { provider: this.provider });
      this.stored =
        result.store === 'configured' || result.store === 'not-configured' ? result.store : 'store-unavailable';
      this.pendingDelivery = result.retryClear === true;
      if (this.pendingDelivery) this.clearFailed = false;
    } catch {
      this.stored = 'store-unavailable';
    } finally {
      this.busy = false;
      this.updateControls();
    }
  }

  private updateControls(): void {
    const offerSave = this.providerNeedsToken || this.stored === 'not-configured';
    this.input.hidden = this.save.hidden = !offerSave;
    if (!offerSave) this.discardInput();
    this.clear.hidden = !this.retryClear && this.stored !== 'configured';
    this.clear.textContent = this.retryClear ? 'Retry Clear' : 'Clear';
    this.input.disabled = this.save.disabled = this.clear.disabled = this.busy;
    this.status.textContent =
      this.busy || !this.stored ? 'Checking Keychain…' : CREDENTIAL_STATUS[this.stored];
    const recoveryMessage = this.retryClear ? 'Removal from the running app is unconfirmed.' : '';
    const error = [this.mutationError, recoveryMessage].filter(Boolean).join(' ');
    if (this.error.textContent !== error) this.error.textContent = error;
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
