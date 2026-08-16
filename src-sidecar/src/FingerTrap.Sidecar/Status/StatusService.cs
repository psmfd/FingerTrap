using FingerTrap.Sidecar.Abstractions;

namespace FingerTrap.Sidecar.Status;

/// <summary>
/// Owns the poll loop over every <see cref="IStatusProvider"/> and publishes
/// merged snapshots (ADR-0022): snapshot-replace, debounced — the UI always
/// receives the full current state, so a dropped notification costs nothing
/// and a noisy repo cannot flood the WebView with per-item updates.
/// </summary>
internal sealed class StatusService : IAsyncDisposable
{
    /// <summary>Steady-state poll cadence. ~3 REST calls per tick keeps a
    /// worst case near 180 requests/hour against a 5000/hour budget; the
    /// deferred ETag store (issue tracked in the PR) reduces it further.</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(60);

    /// <summary>Floor between publishes, so refresh spam collapses.</summary>
    private static readonly TimeSpan PublishFloor = TimeSpan.FromSeconds(2);

    private readonly IReadOnlyList<IStatusProvider> _providers;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly SemaphoreSlim _fetchGate = new(1, 1);
    private Task? _loop;
    private DateTimeOffset _lastPublish = DateTimeOffset.MinValue;

    public StatusService(IReadOnlyList<IStatusProvider> providers)
    {
        _providers = providers;
    }

    /// <summary>Fired with the full merged snapshot after each poll.</summary>
    public event EventHandler<IReadOnlyList<ProviderSnapshot>>? SnapshotReady;

    public void Start()
    {
        _loop ??= Task.Run(async () =>
        {
            while (!_shutdown.IsCancellationRequested)
            {
                await FetchAndPublishAsync().ConfigureAwait(false);
                try
                {
                    await Task.Delay(PollInterval, _shutdown.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        });
    }

    /// <summary>Manual poll (status/refresh). Fire-and-forget by design —
    /// the answer arrives as the next status/snapshot notification.</summary>
    public void RefreshNow()
    {
        _ = Task.Run(FetchAndPublishAsync);
    }

    private async Task FetchAndPublishAsync()
    {
        // One fetch at a time; a refresh landing mid-poll waits its turn
        // rather than double-hitting the APIs.
        await _fetchGate.WaitAsync().ConfigureAwait(false);
        try
        {
            var wait = _lastPublish + PublishFloor - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(wait, _shutdown.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            var snapshots = new List<ProviderSnapshot>(_providers.Count);
            foreach (var provider in _providers)
            {
                try
                {
                    snapshots.Add(await provider.FetchAsync(_shutdown.Token).ConfigureAwait(false));
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception e)
                {
                    // Providers promise state-not-throw for expected cases;
                    // this is the belt for the unexpected ones. Sanitized:
                    // exception text can echo remote content.
                    snapshots.Add(ProviderSnapshot.Empty(
                        provider.Name,
                        ProviderStates.Error,
                        StatusText.Sanitize($"provider crashed: {e.Message}", 200)));
                }
            }

            _lastPublish = DateTimeOffset.UtcNow;
            SnapshotReady?.Invoke(this, snapshots);
        }
        finally
        {
            _fetchGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _shutdown.CancelAsync().ConfigureAwait(false);
        if (_loop is not null)
        {
            try
            {
                await _loop.ConfigureAwait(false);
            }
            catch
            {
                // Shutdown path — the loop's own error handling already ran.
            }
        }

        _shutdown.Dispose();
        _fetchGate.Dispose();
        foreach (var provider in _providers)
        {
            (provider as IDisposable)?.Dispose();
        }
    }
}
