using System.Text;
using System.Text.Json;
using FingerTrap.Sidecar.PiRpc;
using Xunit;

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

    [Fact]
    public async Task Pump_RelaysEventsInOrder_TaggedWithSession()
    {
        var sink = new CollectingSink();
        await using var service = new RpcPaneService();
        service.AttachSink(sink);

        await SpawnFakePiAsync(service, "s1",
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

        await SpawnFakePiAsync(service, "s1",
            Step("writeStderrLine", "relay: child exploded"),
            Step("delayMs", 50),
            Step("exit", 9));

        var exit = await sink.WaitForExitAsync();
        Assert.Equal("s1", exit.SessionId);
        Assert.Equal(9, exit.ExitCode);
        Assert.Contains("relay: child exploded", exit.StderrTail, StringComparison.Ordinal);

        // The session retired: a new spawn under the same id succeeds.
        await SpawnFakePiAsync(service, "s1", Step("exit", 0));
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

        await SpawnFakePiAsync(service, "dup", Step("waitForEof", true));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SpawnFakePiAsync(service, "dup", Step("waitForEof", true)));

        await service.KillAsync("dup", TestContext.Current.CancellationToken);
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

    /// <summary>
    /// Platform shim that launches FakePi with its script, ignoring the
    /// <c>--mode rpc</c> args the service appends — the seam that lets the
    /// relay spawn "pi" without a pi.
    /// </summary>
    private static string WriteShim(string[] steps)
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), $"fakepi-{Guid.NewGuid():N}.json");
        File.WriteAllText(scriptPath, "[" + string.Join(",", steps) + "]");

        var fakePi = FakePiDllPath();
        if (OperatingSystem.IsWindows())
        {
            var cmd = Path.Combine(Path.GetTempPath(), $"fakepi-{Guid.NewGuid():N}.cmd");
            File.WriteAllText(cmd, $"@echo off\r\ndotnet \"{fakePi}\" \"{scriptPath}\"\r\n");
            return cmd;
        }

        var sh = Path.Combine(Path.GetTempPath(), $"fakepi-{Guid.NewGuid():N}.sh");
        File.WriteAllText(sh, $"#!/bin/sh\nexec dotnet \"{fakePi}\" \"{scriptPath}\"\n");
        File.SetUnixFileMode(sh, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return sh;
    }

    private static string FakePiDllPath()
    {
        var baseDir = AppContext.BaseDirectory;
        foreach (var configuration in new[] { "Debug", "Release" })
        {
            var marker = $"{Path.DirectorySeparatorChar}{configuration}{Path.DirectorySeparatorChar}";
            if (!baseDir.Contains(marker, StringComparison.Ordinal))
            {
                continue;
            }

            var candidate = Path.GetFullPath(Path.Combine(
                baseDir, "..", "..", "..", "..", "FakePi", "bin", configuration, "net10.0", "FakePi.dll"));
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException(
            $"FakePi.dll not found relative to test output '{baseDir}' — build src-sidecar/tests/FakePi first");
    }

    private static string Step(string key, string value) =>
        JsonSerializer.Serialize(new Dictionary<string, string> { [key] = value });

    private static string Step(string key, int value) => $"{{\"{key}\":{value}}}";

    private static string Step(string key, bool value) => $"{{\"{key}\":{(value ? "true" : "false")}}}";

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
