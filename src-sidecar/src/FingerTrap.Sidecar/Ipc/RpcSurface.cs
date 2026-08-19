using System.Text.Json.Nodes;
using FingerTrap.Sidecar.Abstractions;
using FingerTrap.Sidecar.PiRpc;
using FingerTrap.Sidecar.Sessions;
using FingerTrap.Sidecar.Settings;
using FingerTrap.Sidecar.Status;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;

namespace FingerTrap.Sidecar.Ipc;

internal sealed class RpcSurface : IDisposable, IRpcPaneSink
{
    private readonly IPtyService _pty;
    private readonly PaneSettings? _paneSettings;
    private readonly CredentialCache _credentials;
    private readonly RpcPaneService? _rpcPanes;
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
    /// <param name="keybindings">
    /// Operator keybinding overrides from settings (FT-1 slice 3), served
    /// verbatim by <c>settings/get</c>; null means "none configured".
    /// </param>
    /// <param name="rpcPanes">
    /// Native RPC pane service (FT-2 slice 2), or null when the host wires
    /// no RPC panes (tests). This surface is its <see cref="IRpcPaneSink"/>.
    /// </param>
    /// <param name="sessions">
    /// Session-store scanner for the session browser (FT-2 slice 5), or
    /// null when the host wires no browser (tests).
    /// </param>
    /// <param name="worktrees">
    /// Worktree-orphan reconciler for the session browser (FT-2 slice 5),
    /// or null when the host wires no browser (tests).
    /// </param>
    public RpcSurface(
        IPtyService pty,
        PaneSettings? paneSettings = null,
        CredentialCache? credentials = null,
        StatusService? status = null,
        IReadOnlyDictionary<string, string>? keybindings = null,
        RpcPaneService? rpcPanes = null,
        SessionStore? sessions = null,
        WorktreeReconciler? worktrees = null)
    {
        _pty = pty;
        _paneSettings = paneSettings;
        _credentials = credentials ?? new CredentialCache();
        _status = status;
        _keybindings = keybindings;
        _rpcPanes = rpcPanes;
        _sessions = sessions;
        _worktrees = worktrees;
    }

    private readonly StatusService? _status;
    private readonly IReadOnlyDictionary<string, string>? _keybindings;
    private readonly SessionStore? _sessions;
    private readonly WorktreeReconciler? _worktrees;

    private static readonly IReadOnlyDictionary<string, string> NoKeybindings =
        new Dictionary<string, string>();

    public void AttachRpc(JsonRpc rpc)
    {
        _rpc = rpc;
        if (_eventsBound)
        {
            return;
        }

        _pty.Output += OnPtyOutput;
        _pty.Exited += OnPtyExit;
        if (_status is not null)
        {
            _status.SnapshotReady += OnStatusSnapshot;
        }

        _eventsBound = true;
    }

    public void Dispose()
    {
        // Symmetric with AttachSink in Program.cs: a disposed surface's
        // IRpcPaneSink implementation must never be invoked into again.
        _rpcPanes?.AttachSink(null);
        if (_eventsBound)
        {
            _pty.Output -= OnPtyOutput;
            _pty.Exited -= OnPtyExit;
            if (_status is not null)
            {
                _status.SnapshotReady -= OnStatusSnapshot;
            }

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
        var options = new PtySpawnOptions(
            request.Shell, request.Cwd, request.Cols, request.Rows, request.Env, kind, request.SessionPath);
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

    [JsonRpcMethod("rpc/spawn")]
    public async Task RpcSpawnAsync(RpcSpawnRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var options = new RpcPaneSpawnOptions(request.Cwd, request.SessionPath, request.Env);
        await RequireRpcPanes().SpawnAsync(request.SessionId, options, cancellationToken).ConfigureAwait(false);
    }

    [JsonRpcMethod("rpc/kill")]
    public async Task RpcKillAsync(RpcKillRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await RequireRpcPanes().KillAsync(request.SessionId, cancellationToken).ConfigureAwait(false);
    }

    [JsonRpcMethod("rpc/prompt")]
    public async Task<RpcPromptResult> RpcPromptAsync(RpcPromptRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        // JsonObject rather than string interpolation: the message is
        // operator free text and must arrive as one correctly-escaped JSON
        // string value.
        var parameters = new JsonObject { ["message"] = request.Message };
        if (!string.IsNullOrEmpty(request.StreamingBehavior))
        {
            parameters["streamingBehavior"] = request.StreamingBehavior;
        }

        var response = await RequireRpcPanes()
            .SendPromptAsync(request.SessionId, parameters.ToJsonString(), cancellationToken)
            .ConfigureAwait(false);
        return new RpcPromptResult(response.Success, response.Error);
    }

    // The per-command typed surface (FT-2 slice 3b, ADR-0003's
    // one-method-per-capability posture): each method is a thin wrapper
    // over RpcPaneService.SendCommandAsync, which owns the pi plumbing.

    [JsonRpcMethod("rpc/steer")]
    public Task<RpcCommandResult> RpcSteerAsync(RpcSteerRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var parameters = new JsonObject { ["message"] = request.Message }.ToJsonString();
        return SendCommandAsync(request.SessionId, "steer", parameters, cancellationToken);
    }

    [JsonRpcMethod("rpc/followUp")]
    public Task<RpcCommandResult> RpcFollowUpAsync(RpcFollowUpRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var parameters = new JsonObject { ["message"] = request.Message }.ToJsonString();
        return SendCommandAsync(request.SessionId, "follow_up", parameters, cancellationToken);
    }

    [JsonRpcMethod("rpc/abort")]
    public Task<RpcCommandResult> RpcAbortAsync(RpcSessionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SendCommandAsync(request.SessionId, "abort", null, cancellationToken);
    }

    [JsonRpcMethod("rpc/getState")]
    public Task<RpcCommandResult> RpcGetStateAsync(RpcSessionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SendCommandAsync(request.SessionId, "get_state", null, cancellationToken);
    }

    /// <summary>
    /// Full message history of the attached session (FT-2 slice 5): the
    /// post-resume transcript seed. pi has no <c>since</c> cursor, so the
    /// first fetch after a resume is always the whole list — bounded by the
    /// frame ceiling like every other response.
    /// </summary>
    [JsonRpcMethod("rpc/getMessages")]
    public Task<RpcCommandResult> RpcGetMessagesAsync(RpcSessionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SendCommandAsync(request.SessionId, "get_messages", null, cancellationToken);
    }

    [JsonRpcMethod("rpc/getSessionStats")]
    public Task<RpcCommandResult> RpcGetSessionStatsAsync(RpcSessionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SendCommandAsync(request.SessionId, "get_session_stats", null, cancellationToken);
    }

    [JsonRpcMethod("rpc/getAvailableModels")]
    public Task<RpcCommandResult> RpcGetAvailableModelsAsync(RpcSessionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SendCommandAsync(request.SessionId, "get_available_models", null, cancellationToken);
    }

    [JsonRpcMethod("rpc/getAvailableThinkingLevels")]
    public Task<RpcCommandResult> RpcGetAvailableThinkingLevelsAsync(RpcSessionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SendCommandAsync(request.SessionId, "get_available_thinking_levels", null, cancellationToken);
    }

    [JsonRpcMethod("rpc/setModel")]
    public Task<RpcCommandResult> RpcSetModelAsync(RpcSetModelRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var parameters = new JsonObject
        {
            ["provider"] = request.Provider,
            ["modelId"] = request.ModelId,
        }.ToJsonString();
        return SendCommandAsync(request.SessionId, "set_model", parameters, cancellationToken);
    }

    [JsonRpcMethod("rpc/setThinkingLevel")]
    public Task<RpcCommandResult> RpcSetThinkingLevelAsync(RpcSetThinkingLevelRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var parameters = new JsonObject { ["level"] = request.Level }.ToJsonString();
        return SendCommandAsync(request.SessionId, "set_thinking_level", parameters, cancellationToken);
    }

    /// <summary>
    /// Answers an interactive <c>extension_ui_request</c> dialog (FT-2
    /// slice 4). One-way by contract — pi emits no response frame for
    /// <c>extension_ui_response</c> — so this deliberately bypasses
    /// <see cref="SendCommandAsync"/> (whose await could only ever time
    /// out) and returns once the message is on the child's stdin.
    /// </summary>
    [JsonRpcMethod("rpc/extensionUiResponse")]
    public Task RpcExtensionUiResponseAsync(RpcExtensionUiResponseRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        // Exactly one payload key goes on the wire — pi's response union
        // ({value} | {confirmed} | {cancelled: true}) discriminates by key.
        var parameters = new JsonObject();
        if (request.Cancelled == true)
        {
            parameters["cancelled"] = true;
        }
        else if (request.Confirmed is not null)
        {
            parameters["confirmed"] = request.Confirmed.Value;
        }
        else if (request.Value is not null)
        {
            parameters["value"] = request.Value;
        }
        else
        {
            throw new ArgumentException(
                "one of value/confirmed/cancelled is required", nameof(request));
        }

        return RequireRpcPanes().SendExtensionUiResponseAsync(
            request.SessionId, request.RequestId, parameters.ToJsonString(), cancellationToken);
    }

    private async Task<RpcCommandResult> SendCommandAsync(
        string sessionId, string command, string? parametersJson, CancellationToken cancellationToken)
    {
        var outcome = await RequireRpcPanes()
            .SendCommandAsync(sessionId, command, parametersJson, cancellationToken)
            .ConfigureAwait(false);
        // Same raw-text boundary crossing as PublishEventAsync: STJ on the
        // pi leg, Newtonsoft JToken on the UI leg, joined only via strings.
        var data = outcome.DataJson is null ? null : JToken.Parse(outcome.DataJson);
        return new RpcCommandResult(outcome.Success, outcome.Error, data);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Awaited by the pump on purpose (ordering + backpressure — see
    /// <see cref="RpcPaneService"/>). The raw event JSON is parsed once and
    /// embedded as a native token, not re-escaped as a string value.
    /// </remarks>
    Task IRpcPaneSink.PublishEventAsync(string sessionId, string? eventType, string json, bool truncated)
    {
        var rpc = _rpc;
        if (rpc is null)
        {
            return Task.CompletedTask;
        }

        var payload = new RpcEventNotification(sessionId, eventType, JToken.Parse(json), truncated);
        return rpc.NotifyAsync("rpc/event", payload);
    }

    /// <inheritdoc/>
    Task IRpcPaneSink.PublishExitAsync(string sessionId, int exitCode, string stderrTail)
    {
        var rpc = _rpc;
        if (rpc is null)
        {
            return Task.CompletedTask;
        }

        var payload = new RpcExitNotification(sessionId, exitCode, stderrTail);
        return rpc.NotifyAsync("rpc/exit", payload);
    }

    private RpcPaneService RequireRpcPanes() =>
        _rpcPanes ?? throw new InvalidOperationException("rpc panes are not wired on this host");

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
        // A fresh token should be visible without waiting a poll interval.
        _status?.RefreshNow();
    }

    /// <summary>
    /// Effective settings for the UI (FT-1 slice 3, ADR-0021). The default
    /// kind is resolved through the same chain a real unqualified spawn uses
    /// (<see cref="PaneKinds.Parse"/>), so a settings file this call would
    /// misreport is one a spawn would reject identically — one resolver, one
    /// failure mode.
    /// </summary>
    [JsonRpcMethod("settings/get")]
    public Task<SettingsGetResult> SettingsGetAsync()
    {
        var kind = PaneKinds.Parse(null, _paneSettings);
        return Task.FromResult(new SettingsGetResult(
            PaneKinds.ToWire(kind), _keybindings ?? NoKeybindings));
    }

    /// <summary>
    /// Session browser (FT-2 slice 5): the bounded session-store scan. No
    /// request parameters — same pattern as <see cref="SettingsGetAsync"/>.
    /// </summary>
    [JsonRpcMethod("sessions/list")]
    public Task<SessionsListResult> SessionsListAsync(CancellationToken cancellationToken) =>
        (_sessions ?? throw new InvalidOperationException("the session browser is not wired on this host"))
            .ListAsync(cancellationToken);

    /// <summary>
    /// Session browser (FT-2 slice 5): reconciled per-session worktrees and
    /// orphan candidates. Read-only — reap/unlock stay pi-side commands.
    /// </summary>
    [JsonRpcMethod("worktrees/list")]
    public Task<WorktreesListResult> WorktreesListAsync(CancellationToken cancellationToken) =>
        (_worktrees ?? throw new InvalidOperationException("the session browser is not wired on this host"))
            .ListAsync(cancellationToken);

    [JsonRpcMethod("status/refresh")]
    public Task StatusRefreshAsync()
    {
        // Fire-and-forget by contract: the answer is the next
        // status/snapshot notification (snapshot-replace, ADR-0022).
        _status?.RefreshNow();
        return Task.CompletedTask;
    }

    private void OnStatusSnapshot(object? sender, IReadOnlyList<ProviderSnapshot> snapshots)
    {
        var rpc = _rpc;
        if (rpc is null)
        {
            return;
        }

        var payload = new StatusSnapshotNotification(snapshots);
        _ = rpc.NotifyAsync("status/snapshot", payload);
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
