import { beforeEach, describe, expect, it, vi } from 'vitest';
import { invoke } from '@tauri-apps/api/core';
import type { ProviderSnapshot, StatusSnapshotNotification } from '../src/api';
import { StatusPanel } from '../src/status';

vi.mock('@tauri-apps/api/core', () => ({ invoke: vi.fn() }));
vi.mock('../src/api', () => ({ statusRefresh: vi.fn().mockResolvedValue(undefined) }));

function snapshot(state: ProviderSnapshot['state']): StatusSnapshotNotification {
  return {
    providers: ['github', 'ado', 'git'].map((provider) => ({
      provider,
      state,
      detail: null,
      pullRequests: [],
      issues: [],
      runs: [],
    })),
  };
}

describe('Status panel Keychain controls', () => {
  let host: HTMLElement;
  let panel: StatusPanel;
  const shell = vi.mocked(invoke);

  beforeEach(() => {
    shell.mockReset();
    host = document.createElement('div');
    panel = new StatusPanel(host);
  });

  function card(provider: string): HTMLElement {
    return [...host.querySelectorAll<HTMLElement>('section')].find(
      (section) => section.querySelector('.status-name')?.textContent === provider,
    )!;
  }

  function button(provider: string, label: string): HTMLButtonElement {
    return [...card(provider).querySelectorAll('button')].find(
      (element) => element.textContent === label,
    )!;
  }

  function status(provider: string): string | null | undefined {
    return card(provider).querySelector('[role=status]')?.textContent;
  }

  it('reads both provider cards and clears the selected credential while offline', async () => {
    const stored = new Set(['github', 'ado']);
    shell.mockImplementation(async (command, args) => {
      const provider = (args as { provider: string }).provider;
      if (command === 'credential_clear') {
        stored.delete(provider);
        return;
      }
      return stored.has(provider) ? 'configured' : 'not-configured';
    });
    panel.update(snapshot('error'));
    panel.toggle();
    expect(status('github')).toBe('Checking Keychain…');
    expect(button('github', 'Clear').disabled).toBe(true);
    await vi.waitFor(() => expect(status('ado')).toBe('Credential stored in Keychain'));
    expect(status('github')).toBe('Credential stored in Keychain');
    expect(card('git').querySelector('.status-token')).toBeNull();

    button('ado', 'Clear').click();
    await vi.waitFor(() => expect(status('ado')).toBe('No credential stored in Keychain'));
    expect(shell).toHaveBeenCalledWith('credential_clear', { provider: 'ado' });
    expect(button('ado', 'Clear').hidden).toBe(true);
    expect(status('github')).toBe('Credential stored in Keychain');
  });

  it('masks and clears input and serializes save through a snapshot and reopen', async () => {
    let finishSave!: () => void;
    let stored = false;
    shell.mockImplementation(async (command) => {
      if (command === 'credential_save') {
        await new Promise<void>((resolve) => {
          finishSave = resolve;
        });
        stored = true;
        return;
      }
      return stored ? 'configured' : 'not-configured';
    });
    panel.update(snapshot('not-configured'));
    panel.toggle();
    await vi.waitFor(() => expect(status('github')).toBe('No credential stored in Keychain'));
    const field = card('github').querySelector('input')!;
    expect(field.type).toBe('password');
    field.value = 'synthetic-test-value';
    button('github', 'Save').click();
    expect(field.value).toBe('');
    expect(shell).toHaveBeenCalledWith('credential_save', {
      provider: 'github',
      token: 'synthetic-test-value',
    });

    panel.update(snapshot('auth-failed'));
    panel.toggle();
    panel.toggle();
    expect(button('github', 'Save').disabled).toBe(true);
    expect(
      shell.mock.calls.filter(
        ([command, args]) =>
          command === 'credential_status' && (args as { provider: string }).provider === 'github',
      ),
    ).toHaveLength(1);
    finishSave();
    await vi.waitFor(() => expect(status('github')).toBe('Credential stored in Keychain'));
    expect(button('github', 'Clear').disabled).toBe(false);
  });

  it('reports mutation failures separately from the durable result', async () => {
    let stored = false;
    shell.mockImplementation(async (command) => {
      if (command === 'credential_save' || command === 'credential_clear') {
        stored = command === 'credential_save';
        throw new Error('private delivery details');
      }
      return stored ? 'configured' : 'not-configured';
    });
    panel.update(snapshot('not-configured'));
    panel.toggle();
    await vi.waitFor(() => expect(status('github')).toBe('No credential stored in Keychain'));
    card('github').querySelector('input')!.value = 'synthetic-test-value';
    button('github', 'Save').click();
    await vi.waitFor(() => expect(status('github')).toBe('Credential stored in Keychain'));
    expect(card('github').querySelector('[role=alert]')?.textContent).toBe('Save failed');

    button('github', 'Clear').click();
    await vi.waitFor(() => expect(status('github')).toBe('No credential stored in Keychain'));
    expect(card('github').querySelector('[role=alert]')?.textContent).toBe('Clear failed');
    expect(host.textContent).not.toContain('private delivery details');
  });

  it('distinguishes a failed or unavailable Keychain read from an absent credential', async () => {
    shell
      .mockResolvedValueOnce('store-unavailable')
      .mockRejectedValueOnce(new Error('unavailable'));
    panel.update(snapshot('not-configured'));
    panel.toggle();
    await vi.waitFor(() => expect(status('ado')).toBe('Keychain unavailable'));
    expect(status('github')).toBe('Keychain unavailable');
    expect(button('github', 'Clear').hidden).toBe(true);
    expect(button('ado', 'Clear').hidden).toBe(true);
  });
});
