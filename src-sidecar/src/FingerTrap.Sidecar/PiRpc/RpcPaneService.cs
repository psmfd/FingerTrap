using System.Collections.Concurrent;
using FingerTrap.Sidecar.Executables;
using FingerTrap.Sidecar.Settings;

namespace FingerTrap.Sidecar.PiRpc;

/// <summary>
/// The single consumer a <see cref="RpcPaneService"/> publishes into —
/// deliberately an interface with one target rather than a C# multicast
/// event: the pump must <em>await</em> each publish (ordering,
/// backpressure), and awaiting a multicast <c>Func&lt;…,Task&gt;</c>
/// observes only the last subscriber's task, a silent-drop trap the
/// single-target shape makes unrepresentable.
/// </summary>
internal interface IRpcPaneSink
{
    /// <summary>One relayed pi event, ceiling-guarded, verbatim otherwise.</summary>
    public Task PublishEventAsync(string sessionId, string? eventType, string json, bool truncated);

    /// <summary>
    /// The pane's child is gone. <paramref name="stderrTail"/> is
    /// operator-diagnostic text from an untrusted-ish source (extension
    /// logs reroute to stderr) — ADR-0022 applies: the UI renders it via
    /// textContent, never markup.
    /// </summary>
    public Task PublishExitAsync(string sessionId, int exitCode, string stderrTail);
}

/// <summary>Spawn parameters for one native RPC pane.</summary>
/// <param name="Cwd">Working directory for the pi child; null inherits.</param>
/// <param name="SessionPath">
/// Session to resume via spawn-time <c>--session</c> — selection is a
/// spawn-time CLI concern per docs/rpc-contract.md, so the field exists on
/// the wire from slice 2 even though the session browser (slice 5) is its
/// first setter. Null starts a fresh session.
/// </param>
/// <param name="Env">Additive environment overrides; see
/// <see cref="PiRpcClientOptions.EnvironmentOverrides"/>.</param>
/// <param name="RequestedPath">Explicit pi executable override.</param>
internal sealed record RpcPaneSpawnOptions(
    string? Cwd,
    string? SessionPath,
    IReadOnlyDictionary<string, string>? Env,
    string? RequestedPath = null);

/// <summary>
/// Session-keyed owner of <see cref="PiRpcClient"/> instances for native
/// RPC panes (ADR-0025 decisions 1 and 3, FT-2 slice 2) — the RPC sibling
/// of <see cref="Pty.PtyService"/>. The relay is thin: one sequential pump
/// task per session reads the client's event channel and <em>awaits</em>
/// each publish into the attached <see cref="IRpcPaneSink"/>. Ordering is
/// structural (one pump, awaited publishes) rather than an undocumented
/// property of the transport — StreamJsonRpc guarantees write integrity,
/// not order — and a slow UI consumer stalls the pump, the client's
/// bounded channel, the read loop, and finally the pi child's own stdout
/// writes: the backpressure chain the slice-1 supervisor built stays
/// intact through the last hop.
/// </summary>
internal sealed class RpcPaneService : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, PaneEntry> _sessions = new(StringComparer.Ordinal);
    private readonly PiSettings? _piSettings;
    private volatile IRpcPaneSink? _sink;
    private bool _disposed;

    /// <param name="piSettings">Same injected-settings pattern as
    /// <see cref="Pty.PtyService"/>: read once, in Program.cs.</param>
    public RpcPaneService(PiSettings? piSettings = null)
    {
        _piSettings = piSettings;
    }

    /// <summary>
    /// Wires the single publish target — the RPC-pane analog of
    /// <c>RpcSurface.AttachRpc</c>. Pass null on surface teardown so a
    /// disposed sink can never be invoked into.
    /// </summary>
    public void AttachSink(IRpcPaneSink? sink)
    {
        _sink = sink;
    }

    /// <summary>
    /// Resolves pi (shared chain — <see cref="PiExecutableResolver"/>),
    /// spawns <c>pi --mode rpc</c> for the session, and starts its pump.
    /// </summary>
    public async Task SpawnAsync(string sessionId, RpcPaneSpawnOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(options);
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        var executablePath = PiExecutableResolver.ResolvePi(options.RequestedPath, _piSettings);
        var arguments = new List<string> { "--mode", "rpc" };
        if (!string.IsNullOrEmpty(options.SessionPath))
        {
            arguments.Add("--session");
            arguments.Add(options.SessionPath);
        }

        var client = PiRpcClient.Start(new PiRpcClientOptions
        {
            ExecutablePath = executablePath,
            Arguments = arguments,
            WorkingDirectory = options.Cwd,
            EnvironmentOverrides = options.Env,
        });

        var entry = new PaneEntry(client);
        if (!_sessions.TryAdd(sessionId, entry))
        {
            await client.DisposeAsync().ConfigureAwait(false);
            throw new InvalidOperationException($"rpc pane '{sessionId}' is already active");
        }

        entry.PumpTask = Task.Run(() => PumpEventsAsync(sessionId, client), CancellationToken.None);
        entry.ExitTask = Task.Run(() => PumpExitAsync(sessionId, client), CancellationToken.None);
    }

    /// <summary>
    /// Thin passthrough of a <c>prompt</c> command; returns the ack
    /// response. Queue modeling and <c>agent_settled</c> composition are
    /// the slice-3 composer's job, not the relay's.
    /// </summary>
    public Task<PiRpcResponse> SendPromptAsync(string sessionId, string parametersJson, CancellationToken cancellationToken)
    {
        return GetEntry(sessionId).Client.SendAsync("prompt", parametersJson, cancellationToken);
    }

    /// <summary>
    /// Ends a pane's child via the full shutdown ladder. Idempotent like
    /// <c>pty/kill</c>: an unknown or already-dead session is success, so
    /// close-pane never races the child's own exit.
    /// </summary>
    public async Task KillAsync(string sessionId, CancellationToken cancellationToken)
    {
        if (_sessions.TryGetValue(sessionId, out var entry))
        {
            await entry.Client.ShutdownAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        var clients = _sessions.Values.Select(e => e.Client).ToArray();
        _sessions.Clear();

        // Parallel on purpose: each client's shutdown ladder self-bounds at
        // ~10 s worst case, so a sequential loop would cost 10 s × N.
        await Task.WhenAll(clients.Select(DisposeOneAsync)).ConfigureAwait(false);
    }

    private static async Task DisposeOneAsync(PiRpcClient client)
    {
        try
        {
            await client.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Best-effort teardown during process shutdown; one wedged child
            // must not block the others or the process exit.
        }
    }

    private PaneEntry GetEntry(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var entry))
        {
            throw new InvalidOperationException($"rpc pane '{sessionId}' is not active");
        }

        return entry;
    }

    private async Task PumpEventsAsync(string sessionId, PiRpcClient client)
    {
        try
        {
            await foreach (var piEvent in client.Events.ReadAllAsync().ConfigureAwait(false))
            {
                var sink = _sink;
                if (sink is null)
                {
                    // No UI attached (startup race) — drop rather than queue:
                    // the channel is the only buffer this design allows.
                    continue;
                }

                var (json, truncated) = RpcEventGuard.EnforceCeiling(piEvent.Json, piEvent.Type);
                await sink.PublishEventAsync(sessionId, piEvent.Type, json, truncated).ConfigureAwait(false);
            }
        }
        catch (Exception)
        {
            // The UI/transport is gone; the process is exiting anyway
            // (Program.cs awaits rpc.Completion). Stop relaying rather than
            // fault an unobserved background task.
        }
    }

    private async Task PumpExitAsync(string sessionId, PiRpcClient client)
    {
        var fault = await client.Exited.ConfigureAwait(false);
        _sessions.TryRemove(sessionId, out _);

        var sink = _sink;
        if (sink is not null)
        {
            try
            {
                await sink.PublishExitAsync(sessionId, fault.ExitCode, fault.StderrTail).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Best-effort; likely already tearing down.
            }
        }

        await DisposeOneAsync(client).ConfigureAwait(false);
    }

    private sealed class PaneEntry
    {
        public PaneEntry(PiRpcClient client)
        {
            Client = client;
        }

        public PiRpcClient Client { get; }

        public Task? PumpTask { get; set; }

        public Task? ExitTask { get; set; }
    }
}
