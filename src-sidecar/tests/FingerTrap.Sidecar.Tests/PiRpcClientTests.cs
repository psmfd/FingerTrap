using System.Text.Json;
using FingerTrap.Sidecar.PiRpc;
using Xunit;

namespace FingerTrap.Sidecar.Tests;

/// <summary>
/// Conformance suite for <see cref="PiRpcClient"/>, keyed to
/// docs/rpc-contract.md and driven by the FakePi scripted stdio double —
/// a real child process over real OS pipes, because that is where the
/// framing, timing, and shutdown bugs live.
/// </summary>
public sealed class PiRpcClientTests
{
    private static readonly TimeSpan TestBudget = TimeSpan.FromSeconds(15);

    [Fact]
    public async Task SendAsync_RoundTrip_ResolvesWithCommandAndRawJson()
    {
        await using var client = StartFakePi(
            Step("waitForLine", "get_state"),
            Step("writeLine", """{"id":"{{lastId}}","type":"response","command":"get_state","success":true,"data":{"model":"m"}}"""),
            Step("waitForEof", true));

        var response = await client.SendAsync(
            "get_state", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(response.Success);
        Assert.Equal("get_state", response.Command);
        Assert.Contains("\"model\":\"m\"", response.Json, StringComparison.Ordinal);

        await client.ShutdownAsync(TestContext.Current.CancellationToken)
            .WaitAsync(TestBudget, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task PromptAck_EventsInterleaveBeforeAck_AckStillResolvesById()
    {
        // prompt's response is asynchronous and may race the prompt's own
        // first events — correlation is by id, never by ordering.
        await using var client = StartFakePi(
            Step("waitForLine", "prompt"),
            Step("writeLine", """{"type":"turn_start"}"""),
            Step("writeLine", """{"type":"message_start"}"""),
            Step("writeLine", """{"id":"{{lastId}}","type":"response","command":"prompt","success":true}"""),
            Step("writeLine", """{"type":"agent_settled"}"""),
            Step("waitForEof", true));

        var response = await client.SendAsync(
            "prompt", """{"message":"hi"}""", TestContext.Current.CancellationToken);

        Assert.True(response.Success);
        Assert.Equal("prompt", response.Command);

        var first = await NextEventAsync(client, "turn_start");
        Assert.Contains("turn_start", first.Json, StringComparison.Ordinal);

        await client.ShutdownAsync(TestContext.Current.CancellationToken)
            .WaitAsync(TestBudget, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UnknownCommand_ResponsePreservesIdCommandAndError()
    {
        await using var client = StartFakePi(
            Step("waitForLine", "bogus_cmd"),
            Step("writeLine", """{"id":"{{lastId}}","type":"response","command":"bogus_cmd","success":false,"error":"Unknown command: bogus_cmd"}"""),
            Step("waitForEof", true));

        var response = await client.SendAsync(
            "bogus_cmd", cancellationToken: TestContext.Current.CancellationToken);

        // command + error preserved together — the error shape has no
        // structured code, so the pairing is the diagnostic identity.
        Assert.False(response.Success);
        Assert.Equal("bogus_cmd", response.Command);
        Assert.Equal("Unknown command: bogus_cmd", response.Error);

        await client.ShutdownAsync(TestContext.Current.CancellationToken)
            .WaitAsync(TestBudget, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ResponseWithoutId_RoutesToEvents()
    {
        // A malformed inbound line yields command:"parse" with the id key
        // absent (JSON.stringify drops undefined). No pending id can match,
        // so per the reference demux it is published as an event.
        await using var client = StartFakePi(
            Step("writeLine", """{"type":"response","command":"parse","success":false,"error":"Invalid JSON"}"""),
            Step("waitForEof", true));

        var routed = await NextEventAsync(client, "response");
        Assert.Contains("\"command\":\"parse\"", routed.Json, StringComparison.Ordinal);

        await client.ShutdownAsync(TestContext.Current.CancellationToken)
            .WaitAsync(TestBudget, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ResponseWithUnknownId_RoutesToEvents()
    {
        await using var client = StartFakePi(
            Step("writeLine", """{"id":"req_999","type":"response","command":"ghost","success":true}"""),
            Step("waitForEof", true));

        var routed = await NextEventAsync(client, "response");
        Assert.Contains("req_999", routed.Json, StringComparison.Ordinal);

        await client.ShutdownAsync(TestContext.Current.CancellationToken)
            .WaitAsync(TestBudget, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UnparseableChildLines_AreIgnored()
    {
        await using var client = StartFakePi(
            Step("writeLine", "this is not json"),
            Step("writeLine", """{"type":"turn_start"}"""),
            Step("waitForEof", true));

        // The garbage line is dropped client-side; the next real event
        // still comes through on an intact connection.
        var next = await NextEventAsync(client, "turn_start");
        Assert.Equal("turn_start", next.Type);

        await client.ShutdownAsync(TestContext.Current.CancellationToken)
            .WaitAsync(TestBudget, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RequestTimeout_Expires_AndLateResponseBecomesEvent()
    {
        await using var client = StartFakePi(
            options => options with { RequestTimeout = TimeSpan.FromMilliseconds(150) },
            Step("waitForLine", "slow_cmd"),
            Step("delayMs", 600),
            Step("writeLine", """{"id":"{{lastId}}","type":"response","command":"slow_cmd","success":true}"""),
            Step("waitForEof", true));

        await Assert.ThrowsAsync<TimeoutException>(() => client.SendAsync(
            "slow_cmd", cancellationToken: TestContext.Current.CancellationToken));

        // The timed-out entry left the pending map, so the late response
        // falls through the demux to the event stream — proving cleanup.
        var late = await NextEventAsync(client, "response");
        Assert.Contains("slow_cmd", late.Json, StringComparison.Ordinal);

        await client.ShutdownAsync(TestContext.Current.CancellationToken)
            .WaitAsync(TestBudget, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SendAsync_AfterChildExit_FailsFast()
    {
        await using var client = StartFakePi(Step("exit", 0));

        await client.Exited.WaitAsync(TestBudget, TestContext.Current.CancellationToken);

        // Guarded on liveness: fails fast instead of hanging into the
        // request timeout.
        await Assert.ThrowsAsync<PiProcessExitedException>(() => client.SendAsync(
            "get_state", cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StartupEarlyExit_FirstSendSurfacesExitFault()
    {
        // No ready signal exists in the protocol (psmfd/pi#56): a child
        // that dies immediately must surface through the first send, not
        // hang it.
        await using var client = StartFakePi(Step("exit", 3));

        var fault = await Assert.ThrowsAsync<PiProcessExitedException>(() => client.SendAsync(
            "get_state", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(3, fault.ExitCode);
    }

    [Fact]
    public async Task ChildDiesMidFlight_RejectsInFlightWithExitCodeAndStderrTail()
    {
        await using var client = StartFakePi(
            Step("waitForLine", "get_state"),
            Step("writeStderrLine", "boom: preflight exploded"),
            Step("delayMs", 50),
            Step("exit", 7));

        var fault = await Assert.ThrowsAsync<PiProcessExitedException>(() => client.SendAsync(
            "get_state", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(7, fault.ExitCode);
        Assert.Contains("boom: preflight exploded", fault.StderrTail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WaitForSettled_RegisteredBeforePrompt_ObservesBoundary()
    {
        await using var client = StartFakePi(
            Step("waitForLine", "prompt"),
            Step("writeLine", """{"id":"{{lastId}}","type":"response","command":"prompt","success":true}"""),
            Step("writeLine", """{"type":"agent_settled"}"""),
            Step("waitForEof", true));

        // The contract's discipline: register the waiter before the send it
        // observes, as the reference promptAndWait does.
        var settled = client.WaitForSettledAsync(TestContext.Current.CancellationToken);
        await client.SendAsync("prompt", """{"message":"hi"}""", TestContext.Current.CancellationToken);
        await settled.WaitAsync(TestBudget, TestContext.Current.CancellationToken);

        await client.ShutdownAsync(TestContext.Current.CancellationToken)
            .WaitAsync(TestBudget, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Shutdown_EofHonored_FlushesAndExitsZero()
    {
        await using var client = StartFakePi(
            Step("waitForEof", true),
            Step("writeLine", """{"type":"session_info_changed","flushed":true}"""),
            Step("exit", 0));

        await client.ShutdownAsync(TestContext.Current.CancellationToken)
            .WaitAsync(TestBudget, TestContext.Current.CancellationToken);

        var fault = await client.Exited.WaitAsync(TestBudget, TestContext.Current.CancellationToken);
        Assert.Equal(0, fault.ExitCode);

        // stdin-EOF is the flushing shutdown: the child's final line made
        // it into the event stream before the channel completed.
        var flushed = await NextEventAsync(client, "session_info_changed");
        Assert.Contains("\"flushed\":true", flushed.Json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Shutdown_ChildIgnoresEof_SigtermEndsIt()
    {
        if (OperatingSystem.IsWindows())
        {
            // No SIGTERM exists on Windows; the ladder there is
            // EOF → tree-kill, covered by the escalation test below.
            return;
        }

        await using var client = StartFakePi(
            options => options with
            {
                EofGrace = TimeSpan.FromMilliseconds(200),
                SigtermGrace = TimeSpan.FromSeconds(5),
            },
            Step("waitForEof", true),
            Step("delayMs", 60_000));

        await client.ShutdownAsync(TestContext.Current.CancellationToken)
            .WaitAsync(TestBudget, TestContext.Current.CancellationToken);

        var fault = await client.Exited.WaitAsync(TestBudget, TestContext.Current.CancellationToken);
        Assert.Equal(143, fault.ExitCode);
    }

    [Fact]
    public async Task Shutdown_ChildIgnoresEverything_SigkillEscalationEndsIt()
    {
        await using var client = StartFakePi(
            options => options with
            {
                EofGrace = TimeSpan.FromMilliseconds(200),
                SigtermGrace = TimeSpan.FromMilliseconds(200),
            },
            Step("ignoreSigterm", true),
            Step("waitForEof", true),
            Step("delayMs", 60_000));

        await client.ShutdownAsync(TestContext.Current.CancellationToken)
            .WaitAsync(TestBudget, TestContext.Current.CancellationToken);

        var fault = await client.Exited.WaitAsync(TestBudget, TestContext.Current.CancellationToken);
        Assert.NotEqual(0, fault.ExitCode);
    }

    [Fact]
    public async Task Spawn_PathWithSpaces_Works()
    {
        // ArgumentList-only spawning must survive the paths GUI platforms
        // actually use (macOS "Application Support"-style).
        var spacedDir = Path.Combine(Path.GetTempPath(), $"fake pi {Guid.NewGuid():N}");
        Directory.CreateDirectory(spacedDir);
        try
        {
            foreach (var file in Directory.GetFiles(Path.GetDirectoryName(FakePiDllPath())!))
            {
                File.Copy(file, Path.Combine(spacedDir, Path.GetFileName(file)));
            }

            var scriptPath = WriteScript(
                Step("waitForLine", "get_state"),
                Step("writeLine", """{"id":"{{lastId}}","type":"response","command":"get_state","success":true}"""),
                Step("waitForEof", true));

            await using var client = PiRpcClient.Start(new PiRpcClientOptions
            {
                ExecutablePath = "dotnet",
                Arguments = [Path.Combine(spacedDir, "FakePi.dll"), scriptPath],
            });

            var response = await client.SendAsync(
                "get_state", cancellationToken: TestContext.Current.CancellationToken);
            Assert.True(response.Success);

            await client.ShutdownAsync(TestContext.Current.CancellationToken)
                .WaitAsync(TestBudget, TestContext.Current.CancellationToken);
        }
        finally
        {
            Directory.Delete(spacedDir, recursive: true);
        }
    }

    [Fact]
    public async Task EnvironmentPolicy_InheritedPlusOverrides_NothingElse()
    {
        // The child environment is exactly inherited + overrides:
        // PiRpcClient injects nothing of its own (and takes no credential
        // dependency to inject from — CredentialCache is not referenced).
        var inheritedName = $"FT_TEST_INHERITED_{Guid.NewGuid():N}";
        var overrideName = $"FT_TEST_OVERRIDE_{Guid.NewGuid():N}";
        var absentName = $"FT_TEST_ABSENT_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(inheritedName, "from-parent");
        try
        {
            var probeLine =
                "{\"type\":\"env_probe\"," +
                "\"inherited\":\"{{env:" + inheritedName + "}}\"," +
                "\"override\":\"{{env:" + overrideName + "}}\"," +
                "\"absent\":\"{{env:" + absentName + "}}\"}";
            await using var client = StartFakePi(
                options => options with
                {
                    EnvironmentOverrides = new Dictionary<string, string> { [overrideName] = "from-override" },
                },
                Step("writeLine", probeLine),
                Step("waitForEof", true));

            var probe = await NextEventAsync(client, "env_probe");
            using var parsed = JsonDocument.Parse(probe.Json);
            Assert.Equal("from-parent", parsed.RootElement.GetProperty("inherited").GetString());
            Assert.Equal("from-override", parsed.RootElement.GetProperty("override").GetString());
            Assert.Equal(string.Empty, parsed.RootElement.GetProperty("absent").GetString());

            await client.ShutdownAsync(TestContext.Current.CancellationToken)
                .WaitAsync(TestBudget, TestContext.Current.CancellationToken);
        }
        finally
        {
            Environment.SetEnvironmentVariable(inheritedName, null);
        }
    }

    private static PiRpcClient StartFakePi(params string[] steps) => StartFakePi(options => options, steps);

    private static PiRpcClient StartFakePi(
        Func<PiRpcClientOptions, PiRpcClientOptions> configure,
        params string[] steps)
    {
        var options = new PiRpcClientOptions
        {
            ExecutablePath = "dotnet",
            Arguments = [FakePiDllPath(), WriteScript(steps)],
        };
        return PiRpcClient.Start(configure(options));
    }

    private static string WriteScript(params string[] steps)
    {
        var path = Path.Combine(Path.GetTempPath(), $"fakepi-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, "[" + string.Join(",", steps) + "]");
        return path;
    }

    private static string Step(string key, string value) =>
        JsonSerializer.Serialize(new Dictionary<string, string> { [key] = value });

    private static string Step(string key, int value) =>
        $"{{\"{key}\":{value}}}";

    private static string Step(string key, bool value) =>
        $"{{\"{key}\":{(value ? "true" : "false")}}}";

    private static async Task<PiRpcEvent> NextEventAsync(PiRpcClient client, string type)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TestBudget);
        await foreach (var piEvent in client.Events.ReadAllAsync(budget.Token))
        {
            if (string.Equals(piEvent.Type, type, StringComparison.Ordinal))
            {
                return piEvent;
            }
        }

        throw new InvalidOperationException($"event channel completed without a '{type}' event");
    }

    private static string FakePiDllPath()
    {
        // Test output: .../tests/FingerTrap.Sidecar.Tests/bin/<cfg>/net10.0/
        // FakePi output: .../tests/FakePi/bin/<cfg>/net10.0/FakePi.dll
        // (built whenever the tests build, via the build-order-only
        // ProjectReference in the test csproj).
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
}
