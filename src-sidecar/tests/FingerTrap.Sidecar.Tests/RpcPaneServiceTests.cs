using System.Text;
using System.Text.Json;
using FingerTrap.Sidecar.PiRpc;
using Xunit;
using static FingerTrap.Sidecar.Tests.FakePiShim;

namespace FingerTrap.Sidecar.Tests;

public sealed class RpcEventGuardTests
{
    [Fact]
    public void EnforceCeiling_UnderCeiling_PassesThroughVerbatim()
    {
        var (json, truncated) = RpcEventGuard.EnforceCeiling("{\"type\":\"turn_start\"}", "turn_start");

        Assert.False(truncated);
        Assert.Equal("{\"type\":\"turn_start\"}", json);
    }

    [Fact]
    public void EnforceCeiling_OverCeiling_SubstitutesWellFormedMarker()
    {
        var oversized = "{\"type\":\"tool_execution_update\",\"blob\":\"" +
            new string('x', RpcEventGuard.MaxNotificationPayloadBytes + 16) + "\"}";

        var (json, truncated) = RpcEventGuard.EnforceCeiling(oversized, "tool_execution_update");

        Assert.True(truncated);
        // The marker must itself be small, valid JSON naming the original.
        Assert.True(Encoding.UTF8.GetByteCount(json) < 1024);
        using var parsed = JsonDocument.Parse(json);
        Assert.Equal("rpc_event_truncated", parsed.RootElement.GetProperty("type").GetString());
        Assert.Equal("tool_execution_update", parsed.RootElement.GetProperty("originalType").GetString());
        Assert.True(parsed.RootElement.GetProperty("originalBytes").GetInt32() > RpcEventGuard.MaxNotificationPayloadBytes);
    }

    [Fact]
    public void EnforceCeiling_OversizedExtensionUiRequest_MarkerKeepsIdAndMethod()
    {
        // An unanswerable interactive dialog hangs the agent turn (pi has
        // no timeout on editor), so the marker must keep enough identity
        // for the host to send a cancelled response.
        var oversized = "{\"type\":\"extension_ui_request\",\"id\":\"ui_9\",\"method\":\"editor\"," +
            "\"title\":\"t\",\"prefill\":\"" +
            new string('x', RpcEventGuard.MaxNotificationPayloadBytes + 16) + "\"}";

        var (json, truncated) = RpcEventGuard.EnforceCeiling(oversized, "extension_ui_request");

        Assert.True(truncated);
        using var parsed = JsonDocument.Parse(json);
        Assert.Equal("ui_9", parsed.RootElement.GetProperty("originalId").GetString());
        Assert.Equal("editor", parsed.RootElement.GetProperty("originalMethod").GetString());
    }

    [Fact]
    public void EnforceCeiling_OversizedNonUiEvent_MarkerOmitsIdentityKeys()
    {
        // The id/method fields are extension_ui_request-only: other event
        // types keep the original three-field marker shape.
        var oversized = "{\"type\":\"tool_execution_update\",\"id\":\"tool_1\",\"blob\":\"" +
            new string('x', RpcEventGuard.MaxNotificationPayloadBytes + 16) + "\"}";

        var (json, truncated) = RpcEventGuard.EnforceCeiling(oversized, "tool_execution_update");

        Assert.True(truncated);
        using var parsed = JsonDocument.Parse(json);
        Assert.False(parsed.RootElement.TryGetProperty("originalId", out _));
        Assert.False(parsed.RootElement.TryGetProperty("originalMethod", out _));
    }

    [Fact]
    public void MaxNotificationPayloadBytes_LeavesHeadroomUnderFrameCeiling()
    {
        // Lockstep sanity: the relay ceiling must sit well under the UI
        // transport's 4 MB frame ceiling or the guard is decorative.
        Assert.True(RpcEventGuard.MaxNotificationPayloadBytes
            <= Sidecar.Ipc.FrameCeilingStream.DefaultMaxFrameBytes * 3 / 4);
    }
}

/// <summary>
/// Relay conformance against the FakePi stdio double: ordering, ceiling
/// substitution, exit relay, kill idempotence.
/// </summary>
public sealed class RpcPaneServiceTests
{
    private static readonly TimeSpan TestBudget = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Scripts open with the pinned pi's hello frame so SpawnAsync's ready
    /// gate resolves promptly instead of waiting out the legacy grace —
    /// and every relay assertion below doubles as proof the hello line
    /// never reaches the sink (the Events carve-out).
    /// </summary>
    private const string HelloLine =
        """{"type":"hello","piVersion":"0.84.2","protocol":1,"capabilities":["extension_ui","queue_modes","fork","get_commands","list_sessions"]}""";

    [Fact]
    public async Task Pump_RelaysEventsInOrder_TaggedWithSession()
    {
        var sink = new CollectingSink();
        await using var service = new RpcPaneService();
        service.AttachSink(sink);

        await SpawnFakePiAsync(service, "s1",
            Step("writeLine", HelloLine),
            Step("writeLine", """{"type":"turn_start"}"""),
            Step("writeLine", """{"type":"message_start"}"""),
            Step("writeLine", """{"type":"agent_settled"}"""),
            Step("waitForEof", true));

        await sink.WaitForEventsAsync(3);
        await service.KillAsync("s1", TestContext.Current.CancellationToken);

        var types = sink.Events.Select(e => e.EventType ?? "").ToArray();
        Assert.Equal(["turn_start", "message_start", "agent_settled"], types);
        Assert.All(sink.Events, e => Assert.Equal("s1", e.SessionId));
        Assert.All(sink.Events, e => Assert.False(e.Truncated));
    }

    [Fact]
    public async Task Pump_OversizedEvent_SubstitutesTruncationMarker()
    {
        var sink = new CollectingSink();
        await using var service = new RpcPaneService();
        service.AttachSink(sink);

        // The fake writes an event whose JSON exceeds the 3 MB relay
        // ceiling but stays under the supervisor's 8 MB line ceiling.
        var blob = new string('x', RpcEventGuard.MaxNotificationPayloadBytes + 1024);
        await SpawnFakePiAsync(service, "s1",
            Step("writeLine", HelloLine),
            Step("writeLine", "{\"type\":\"tool_execution_update\",\"blob\":\"" + blob + "\"}"),
            Step("writeLine", """{"type":"agent_settled"}"""),
            Step("waitForEof", true));

        await sink.WaitForEventsAsync(2);
        await service.KillAsync("s1", TestContext.Current.CancellationToken);

        var first = sink.Events[0];
        Assert.True(first.Truncated);
        Assert.Contains("rpc_event_truncated", first.Json, StringComparison.Ordinal);
        // The stream survives the substitution: the next event flows.
        Assert.Equal("agent_settled", sink.Events[1].EventType);
    }

    [Fact]
    public async Task ExitPump_ForwardsExitCodeAndStderrTail_AndRetiresSession()
    {
        var sink = new CollectingSink();
        await using var service = new RpcPaneService();
        service.AttachSink(sink);

        // hello first: this test pins the POST-ready exit relay — a death
        // before hello is the spawn-failure class, covered separately below.
        await SpawnFakePiAsync(service, "s1",
            Step("writeLine", HelloLine),
            Step("writeStderrLine", "relay: child exploded"),
            Step("delayMs", 50),
            Step("exit", 9));

        var exit = await sink.WaitForExitAsync();
        Assert.Equal("s1", exit.SessionId);
        Assert.Equal(9, exit.ExitCode);
        Assert.Contains("relay: child exploded", exit.StderrTail, StringComparison.Ordinal);

        // The session retired: a new spawn under the same id succeeds.
        await SpawnFakePiAsync(service, "s1",
            Step("writeLine", HelloLine),
            Step("exit", 0));
    }

    [Fact]
    public async Task Spawn_ChildDiesBeforeHello_ThrowsFromSpawn_AndLeavesNoPane()
    {
        // The ADR-0026 grounding: a missing-cwd --session resume hard-exits
        // 1 before the JSONL channel is up. With the ready gate, that death
        // is a spawn-time rpc/spawn error, not a later rpc/exit
        // notification — and no pane entry lingers.
        var sink = new CollectingSink();
        await using var service = new RpcPaneService();
        service.AttachSink(sink);

        var fault = await Assert.ThrowsAsync<PiProcessExitedException>(() =>
            SpawnFakePiAsync(service, "doa", Step("exit", 1)));
        Assert.Equal(1, fault.ExitCode);

        // No entry left behind: the same id spawns cleanly afterwards.
        await SpawnFakePiAsync(service, "doa",
            Step("writeLine", HelloLine),
            Step("waitForEof", true));
        await service.KillAsync("doa", TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Spawn_LegacyChildWithoutHello_ProceedsAfterGrace()
    {
        // Pre-hello pins keep working: the gate resolves legacy (null)
        // after HelloGrace and the pane comes up as before the handshake.
        await using var service = new RpcPaneService();

        await SpawnFakePiAsync(service, "legacy",
            Step("waitForLine", "get_state"),
            Step("writeLine", """{"id":"{{lastId}}","type":"response","command":"get_state","success":true}"""),
            Step("waitForEof", true));

        var outcome = await service.SendCommandAsync(
            "legacy", "get_state", null, TestContext.Current.CancellationToken);
        Assert.True(outcome.Success);

        await service.KillAsync("legacy", TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Kill_UnknownSession_IsANoOp()
    {
        await using var service = new RpcPaneService();

        await service.KillAsync("never-spawned", TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Spawn_DuplicateSession_Throws()
    {
        var sink = new CollectingSink();
        await using var service = new RpcPaneService();
        service.AttachSink(sink);

        await SpawnFakePiAsync(service, "dup",
            Step("writeLine", HelloLine),
            Step("waitForEof", true));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SpawnFakePiAsync(service, "dup",
                Step("writeLine", HelloLine),
                Step("waitForEof", true)));

        await service.KillAsync("dup", TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SendCommandAsync_StripsEnvelope_ForwardsDataVerbatim()
    {
        await using var service = new RpcPaneService();
        await SpawnFakePiAsync(service, "s1",
            Step("writeLine", HelloLine),
            Step("waitForLine", "get_state"),
            Step("writeLine",
                """{"id":"{{lastId}}","type":"response","command":"get_state","success":true,"data":{"isStreaming":false,"thinkingLevel":"high"}}"""),
            Step("waitForEof", true));

        var outcome = await service.SendCommandAsync(
            "s1", "get_state", null, TestContext.Current.CancellationToken);
        await service.KillAsync("s1", TestContext.Current.CancellationToken);

        Assert.True(outcome.Success);
        Assert.Null(outcome.Error);
        // data only — no envelope keys (id/type/command) leak to the UI leg.
        using var data = JsonDocument.Parse(outcome.DataJson!);
        Assert.Equal("high", data.RootElement.GetProperty("thinkingLevel").GetString());
        Assert.False(data.RootElement.TryGetProperty("id", out _));
        Assert.DoesNotContain("\"command\"", outcome.DataJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendCommandAsync_NoDataKey_YieldsNullData()
    {
        await using var service = new RpcPaneService();
        await SpawnFakePiAsync(service, "s1",
            Step("writeLine", HelloLine),
            Step("waitForLine", "abort"),
            Step("writeLine", """{"id":"{{lastId}}","type":"response","command":"abort","success":true}"""),
            Step("waitForEof", true));

        var outcome = await service.SendCommandAsync(
            "s1", "abort", null, TestContext.Current.CancellationToken);
        await service.KillAsync("s1", TestContext.Current.CancellationToken);

        Assert.True(outcome.Success);
        Assert.Null(outcome.DataJson);
    }

    [Fact]
    public async Task SendCommandAsync_ErrorResponse_PropagatesPlainStringError()
    {
        await using var service = new RpcPaneService();
        await SpawnFakePiAsync(service, "s1",
            Step("writeLine", HelloLine),
            Step("waitForLine", "set_model"),
            Step("writeLine",
                """{"id":"{{lastId}}","type":"response","command":"set_model","success":false,"error":"unknown model: nope"}"""),
            Step("waitForEof", true));

        var outcome = await service.SendCommandAsync(
            "s1", "set_model", """{"provider":"x","modelId":"nope"}""", TestContext.Current.CancellationToken);
        await service.KillAsync("s1", TestContext.Current.CancellationToken);

        Assert.False(outcome.Success);
        Assert.Equal("unknown model: nope", outcome.Error);
        Assert.Null(outcome.DataJson);
    }

    [Fact]
    public async Task SendCommandAsync_OversizedData_FailsWithCeilingError()
    {
        await using var service = new RpcPaneService();
        // Over the 3 MB relay ceiling, under the supervisor's 8 MB line
        // ceiling — unlike the notification path this surfaces as a failed
        // command, never a substitute marker.
        var blob = new string('x', RpcEventGuard.MaxNotificationPayloadBytes + 1024);
        await SpawnFakePiAsync(service, "s1",
            Step("writeLine", HelloLine),
            Step("waitForLine", "get_messages"),
            Step("writeLine",
                "{\"id\":\"{{lastId}}\",\"type\":\"response\",\"command\":\"get_messages\",\"success\":true,\"data\":{\"blob\":\"" + blob + "\"}}"),
            Step("waitForEof", true));

        var outcome = await service.SendCommandAsync(
            "s1", "get_messages", null, TestContext.Current.CancellationToken);
        await service.KillAsync("s1", TestContext.Current.CancellationToken);

        Assert.False(outcome.Success);
        Assert.Contains("relay ceiling", outcome.Error, StringComparison.Ordinal);
        Assert.Null(outcome.DataJson);
    }

    [Fact]
    public async Task SendExtensionUiResponse_FireAndForget_EchoesIdAndNeedsNoReply()
    {
        var sink = new CollectingSink();
        await using var service = new RpcPaneService();
        service.AttachSink(sink);

        // The exact-line match pins the wire shape: the original request's
        // id echoed (never a fresh req_N) beside the single payload key.
        // FakePi writes agent_settled only after the match, and never
        // writes a response frame — a regression onto the command path
        // (pending map + RequestTimeout) would hang the send instead of
        // completing on flush.
        await SpawnFakePiAsync(service, "s1",
            Step("writeLine", HelloLine),
            Step("waitForLine", """{"id":"ui_42","type":"extension_ui_response","cancelled":true}"""),
            Step("writeLine", """{"type":"agent_settled"}"""),
            Step("waitForEof", true));

        await service.SendExtensionUiResponseAsync(
                "s1", "ui_42", """{"cancelled":true}""", TestContext.Current.CancellationToken)
            .WaitAsync(TestBudget, TestContext.Current.CancellationToken);

        await sink.WaitForEventsAsync(1);
        await service.KillAsync("s1", TestContext.Current.CancellationToken);

        Assert.Equal("agent_settled", sink.Events[0].EventType);
    }

    private static Task SpawnFakePiAsync(RpcPaneService service, string sessionId, params string[] steps)
    {
        // RequestedPath override: the resolver returns the explicit request
        // verbatim, so the relay tests never depend on a real pi install.
        // The request points at a shim (FakePi needs its dll + script as
        // leading argv, and the service always appends --mode rpc, which
        // FakePi ignores — it reads argv[0] only).
        var options = new RpcPaneSpawnOptions(
            Cwd: null,
            SessionPath: null,
            Env: null,
            RequestedPath: WriteShim(steps));
        return service.SpawnAsync(sessionId, options, TestContext.Current.CancellationToken);
    }

    private sealed record CollectedEvent(string SessionId, string? EventType, string Json, bool Truncated);

    private sealed record CollectedExit(string SessionId, int ExitCode, string StderrTail);

    private sealed class CollectingSink : IRpcPaneSink, IDisposable
    {
        private readonly List<CollectedEvent> _events = [];
        private readonly TaskCompletionSource<CollectedExit> _exit =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly SemaphoreSlim _arrived = new(0);

        public IReadOnlyList<CollectedEvent> Events
        {
            get
            {
                lock (_events)
                {
                    return [.. _events];
                }
            }
        }

        public Task PublishEventAsync(string sessionId, string? eventType, string json, bool truncated)
        {
            lock (_events)
            {
                _events.Add(new CollectedEvent(sessionId, eventType, json, truncated));
            }

            _arrived.Release();
            return Task.CompletedTask;
        }

        public Task PublishExitAsync(string sessionId, int exitCode, string stderrTail)
        {
            _exit.TrySetResult(new CollectedExit(sessionId, exitCode, stderrTail));
            return Task.CompletedTask;
        }

        public async Task WaitForEventsAsync(int count)
        {
            for (var i = 0; i < count; i++)
            {
                Assert.True(
                    await _arrived.WaitAsync(TestBudget, TestContext.Current.CancellationToken),
                    $"timed out waiting for relayed event {i + 1} of {count}");
            }
        }

        public Task<CollectedExit> WaitForExitAsync() =>
            _exit.Task.WaitAsync(TestBudget, TestContext.Current.CancellationToken);

        public void Dispose() => _arrived.Dispose();
    }
}
