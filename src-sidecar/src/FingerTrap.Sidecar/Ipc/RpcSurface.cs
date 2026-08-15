using FingerTrap.Sidecar.Abstractions;
using FingerTrap.Sidecar.Settings;
using StreamJsonRpc;

namespace FingerTrap.Sidecar.Ipc;

internal sealed class RpcSurface : IDisposable
{
    private readonly IPtyService _pty;
    private readonly PaneSettings? _paneSettings;
    private readonly CredentialCache _credentials;
    private JsonRpc? _rpc;
    private bool _eventsBound;

    /// <param name="paneSettings">
    /// Persisted pane configuration (N-1, #52), or null to rely on the
    /// environment and the host default alone.
    /// </param>
    /// <param name="credentials">
    /// Receives shell-delivered provider tokens (ADR-0022); a fresh empty
    /// cache when omitted (tests).
    /// </param>
    public RpcSurface(IPtyService pty, PaneSettings? paneSettings = null, CredentialCache? credentials = null)
    {
        _pty = pty;
        _paneSettings = paneSettings;
        _credentials = credentials ?? new CredentialCache();
    }

    public void AttachRpc(JsonRpc rpc)
    {
        _rpc = rpc;
        if (_eventsBound)
        {
            return;
        }

        _pty.Output += OnPtyOutput;
        _pty.Exited += OnPtyExit;
        _eventsBound = true;
    }

    public void Dispose()
    {
        if (_eventsBound)
        {
            _pty.Output -= OnPtyOutput;
            _pty.Exited -= OnPtyExit;
            _eventsBound = false;
        }
    }

#pragma warning disable CA1822 // RPC targets must be instance methods (StreamJsonRpc.AddLocalRpcTarget)
    public Task<string> PingAsync(string message) =>
        Task.FromResult($"pong: {message}");
#pragma warning restore CA1822

    [JsonRpcMethod("pty/spawn")]
    public async Task<PtySpawnResult> PtySpawnAsync(PtySpawnRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var kind = PaneKinds.Parse(request.Kind, _paneSettings);
        var options = new PtySpawnOptions(request.Shell, request.Cwd, request.Cols, request.Rows, request.Env, kind);
        var pid = await _pty.SpawnAsync(request.SessionId, options, cancellationToken).ConfigureAwait(false);
        return new PtySpawnResult(pid);
    }

    [JsonRpcMethod("pty/write")]
    public async Task PtyWriteAsync(PtyWriteRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var bytes = Convert.FromBase64String(request.DataBase64);
        await _pty.WriteAsync(request.SessionId, bytes, cancellationToken).ConfigureAwait(false);
    }

    [JsonRpcMethod("pty/resize")]
    public Task PtyResizeAsync(PtyResizeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        _pty.Resize(request.SessionId, request.Cols, request.Rows);
        return Task.CompletedTask;
    }

    [JsonRpcMethod("pty/kill")]
    public Task PtyKillAsync(PtyKillRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        _pty.Close(request.SessionId);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Shell-originated notification (ADR-0022) — void on purpose: no
    /// response frame may exist, since stdout is relayed to the WebView.
    /// Also deliberately outside check.sh's rpc-pairing count, which counts
    /// Task-returning methods; see the note there.
    /// </summary>
    [JsonRpcMethod("credentials/set")]
    public void CredentialsSet(CredentialsSetNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        // Never log any part of this notification — the token is a secret
        // and the provider name adjacent to a failure is a correlation gift.
        _credentials.Set(notification.Provider, notification.Token);
    }

    private void OnPtyOutput(object? sender, PtyOutputEventArgs e)
    {
        var rpc = _rpc;
        if (rpc is null)
        {
            return;
        }

        var payload = new PtyOutputNotification(e.SessionId, Convert.ToBase64String(e.Data.Span));
        _ = rpc.NotifyAsync("pty/output", payload);
    }

    private void OnPtyExit(object? sender, PtyExitEventArgs e)
    {
        var rpc = _rpc;
        if (rpc is null)
        {
            return;
        }

        var payload = new PtyExitNotification(e.SessionId, e.ExitCode);
        _ = rpc.NotifyAsync("pty/exit", payload);
    }
}
