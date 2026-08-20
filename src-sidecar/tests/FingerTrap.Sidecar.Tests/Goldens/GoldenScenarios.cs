using System.Text.Json;
using FingerTrap.Sidecar.PiRpc;
using Xunit;

namespace FingerTrap.Sidecar.Tests.Goldens;

/// <summary>One golden scenario: a name and a driver over <see cref="ScenarioHost"/>.</summary>
internal sealed record GoldenScenario
{
    public required string Name { get; init; }

    /// <summary>
    /// True for scenarios whose recorded exit path is Unix-signal-shaped
    /// (exit 143/137) — Windows has no SIGTERM, so neither lane can
    /// reproduce them there (see the shutdown-ladder note on
    /// <see cref="PiRpcClient.ShutdownAsync"/>).
    /// </summary>
    public bool UnixOnly { get; init; }

    public required Func<ScenarioHost, CancellationToken, Task> RunAsync { get; init; }
}

/// <summary>
/// The contract's load-bearing behaviors as record–replay scenarios
/// (#139; scenario list from the issue deliverable + the ADR-0026 resume
/// matrix). Each driver runs identically under
/// <see cref="RecordScenarioHost"/> (real pi) and
/// <see cref="ReplayScenarioHost"/> (FakePi speaking the golden); every
/// send is gated on an event at-or-after the end of the preceding inbound
/// window, which is what makes the transcript order reproducible.
/// </summary>
internal static class GoldenScenarios
{
    public static readonly IReadOnlyList<GoldenScenario> All =
    [
        new()
        {
            Name = "prompt-settled",
            RunAsync = static async (host, ct) =>
            {
                host.EnqueueTurn(new CannedTurn { Chunks = ["ok"] });
                await host.SpawnAsync(new ScenarioSpawn());

                var ack = await host.Client.SendAsync("prompt", """{"message":"Say ok."}""", ct);
                Assert.True(ack.Success, ack.Error);
                await host.NextEventAsync("agent_settled", ct);

                Assert.Equal(0, await host.ShutdownChildAsync(ct));
            },
        },
        new()
        {
            Name = "follow-up-queue",
            RunAsync = static async (host, ct) =>
            {
                host.EnqueueTurn(new CannedTurn { Chunks = ["one", "two"], HoldAfter = 1 });
                host.EnqueueTurn(new CannedTurn { Chunks = ["done"] });
                await host.SpawnAsync(new ScenarioSpawn());

                var ack = await host.Client.SendAsync("prompt", """{"message":"First."}""", ct);
                Assert.True(ack.Success, ack.Error);

                // First assistant delta = turn 1 is streaming and now held.
                await host.NextEventAsync("message_update", ct);

                var followUp = await host.Client.SendAsync("follow_up", """{"message":"Second."}""", ct);
                Assert.True(followUp.Success, followUp.Error);
                await host.NextEventAsync("queue_update", ct);

                // Recorded discovery: the queued follow-up is delivered
                // inside the SAME agent block (turn_end → turn_start, one
                // agent_end carrying all four messages) — agent_settled
                // fires ONCE per block, not once per queued message.
                host.ReleaseModelHold();
                await host.NextEventAsync("agent_settled", ct);

                Assert.Equal(0, await host.ShutdownChildAsync(ct));
            },
        },
        new()
        {
            Name = "steer-interrupt",
            RunAsync = static async (host, ct) =>
            {
                host.EnqueueTurn(new CannedTurn { Chunks = ["aaa", "bbb"], HoldAfter = 1 });
                host.EnqueueTurn(new CannedTurn { Chunks = ["steered"] });
                await host.SpawnAsync(new ScenarioSpawn());

                var ack = await host.Client.SendAsync("prompt", """{"message":"Start."}""", ct);
                Assert.True(ack.Success, ack.Error);
                await host.NextEventAsync("message_update", ct);

                var steer = await host.Client.SendAsync("steer", """{"message":"Change course."}""", ct);
                Assert.True(steer.Success, steer.Error);
                await host.NextEventAsync("queue_update", ct);

                // Recorded discovery: steer does NOT abort an in-flight
                // model request — it queues (queue_update, steering:[...])
                // and is delivered at the next turn boundary, so the held
                // stream must be released for the interrupt to land.
                host.ReleaseModelHold();
                await host.NextEventAsync("agent_settled", ct);

                // The same pipes keep working after the interrupt.
                var state = await host.Client.SendAsync("get_state", cancellationToken: ct);
                Assert.True(state.Success, state.Error);

                Assert.Equal(0, await host.ShutdownChildAsync(ct));
            },
        },
        new()
        {
            Name = "dialog-roundtrip",
            RunAsync = static async (host, ct) =>
            {
                // The fixture raises its dialogs from the first agent_start
                // — turn-time, the way guard extensions do. (Spawn-time
                // dialogs are the separate case pinned by
                // session-start-dialog-roundtrip.)
                host.EnqueueTurn(new CannedTurn { Chunks = ["ok"] });
                await host.SpawnAsync(new ScenarioSpawn { ExtensionFixture = "dialog-fixture.ts" });

                var ack = await host.Client.SendAsync("prompt", """{"message":"Say ok."}""", ct);
                Assert.True(ack.Success, ack.Error);

                var confirm = await host.NextEventAsync("extension_ui_request", ct);
                await host.Client.SendMessageAsync(
                    "extension_ui_response", RequestId(confirm), """{"confirmed":true}""", ct);

                var input = await host.NextEventAsync("extension_ui_request", ct);
                await host.Client.SendMessageAsync(
                    "extension_ui_response", RequestId(input), """{"value":"golden"}""", ct);

                var select = await host.NextEventAsync("extension_ui_request", ct);
                await host.Client.SendMessageAsync(
                    "extension_ui_response", RequestId(select), """{"cancelled":true}""", ct);

                // All three answered → the blocked hook completes and the
                // turn proceeds to the model and settles.
                await host.NextEventAsync("agent_settled", ct);

                var state = await host.Client.SendAsync("get_state", cancellationToken: ct);
                Assert.True(state.Success, state.Error);

                Assert.Equal(0, await host.ShutdownChildAsync(ct));
            },
        },
        new()
        {
            // Flipped at pin v0.84.2-psmfd.1 (psmfd-patch-012, closing
            // psmfd/pi#57): the stdin reader now attaches BEFORE extensions
            // bind, so a dialog awaited inside session_start is answerable.
            // Under earlier pins this scenario (as session-start-dialog-exit)
            // pinned the silent death instead: the request could never be
            // answered and pi exited 0 once its event loop drained. The
            // fixture notifies after the confirm resolves, so the completed
            // round-trip is observable on the wire.
            Name = "session-start-dialog-roundtrip",
            RunAsync = static async (host, ct) =>
            {
                await host.SpawnAsync(new ScenarioSpawn { ExtensionFixture = "session-start-dialog.ts" });

                var request = await host.NextEventAsync("extension_ui_request", ct);
                Assert.Contains("golden-spawn-confirm", request.Json, StringComparison.Ordinal);

                await host.Client.SendMessageAsync(
                    "extension_ui_response", RequestId(request), """{"confirmed":true}""", ct);

                var notify = await host.NextEventAsync("extension_ui_request", ct);
                Assert.Contains("golden-spawn-confirm-resolved:true", notify.Json, StringComparison.Ordinal);

                Assert.Equal(0, await host.ShutdownChildAsync(ct));
            },
        },
        new()
        {
            // psmfd-patch-011 (psmfd/pi#54): list_sessions returns
            // header-only session metadata (no allMessagesText). A settled
            // turn persists the session first — pi only writes the file
            // once an assistant message exists.
            Name = "list-sessions",
            RunAsync = static async (host, ct) =>
            {
                host.EnqueueTurn(new CannedTurn { Chunks = ["ok"] });
                await host.SpawnAsync(new ScenarioSpawn());

                var ack = await host.Client.SendAsync("prompt", """{"message":"Say ok."}""", ct);
                Assert.True(ack.Success, ack.Error);
                await host.NextEventAsync("agent_settled", ct);

                var list = await host.Client.SendAsync("list_sessions", cancellationToken: ct);
                Assert.True(list.Success, list.Error);
                Assert.Equal(1, SessionCount(list));

                Assert.Equal(0, await host.ShutdownChildAsync(ct));
            },
        },
        new()
        {
            // The fully-pinned scenario: no volatile values on either side,
            // so its golden commits verbatim and catches prose drift in
            // pi's parse-error shape for free.
            Name = "malformed-line-parse-error",
            RunAsync = static async (host, ct) =>
            {
                await host.SpawnAsync(new ScenarioSpawn());

                await host.Client.SendRawLineForConformanceAsync("this is not json", ct);

                // No known id → the error response falls through the demux
                // to the event listeners, per contract.
                var parseError = await host.NextEventAsync("response", ct);
                Assert.Contains("\"command\":\"parse\"", parseError.Json, StringComparison.Ordinal);
                Assert.DoesNotContain("\"id\"", parseError.Json, StringComparison.Ordinal);

                Assert.Equal(0, await host.ShutdownChildAsync(ct));
            },
        },
        new()
        {
            Name = "unknown-id-fallthrough",
            RunAsync = static async (host, ct) =>
            {
                await host.SpawnAsync(new ScenarioSpawn());

                // A response pi correlates to no pending request: forge a
                // command through the uncorrelated send path (no pending-map
                // entry) — deterministic, unlike racing a timeout.
                await host.Client.SendMessageAsync("get_state", "forged_1", null, ct);

                var stray = await host.NextEventAsync("response", ct);
                Assert.Contains("\"id\":\"forged_1\"", stray.Json, StringComparison.Ordinal);

                Assert.Equal(0, await host.ShutdownChildAsync(ct));
            },
        },
        new()
        {
            Name = "eof-clean-shutdown",
            RunAsync = static async (host, ct) =>
            {
                await host.SpawnAsync(new ScenarioSpawn());

                var state = await host.Client.SendAsync("get_state", cancellationToken: ct);
                Assert.True(state.Success, state.Error);

                // stdin EOF is the clean trigger: flush and exit 0.
                Assert.Equal(0, await host.ShutdownChildAsync(ct));
            },
        },
        new()
        {
            Name = "eof-mid-turn",
            UnixOnly = true,
            RunAsync = static async (host, ct) =>
            {
                host.EnqueueTurn(new CannedTurn { Chunks = ["never", "finishes"], HoldAfter = 1 });
                await host.SpawnAsync(new ScenarioSpawn { EofGrace = TimeSpan.FromMilliseconds(500) });

                var ack = await host.Client.SendAsync("prompt", """{"message":"Start."}""", ct);
                Assert.True(ack.Success, ack.Error);

                // Consume BOTH events of the held first chunk (text_start
                // + text_delta) so the wire is quiescent before EOF — the
                // stdin_eof control record's position must not race the
                // pump.
                await host.NextEventAsync("message_update", ct);
                await host.NextEventAsync("message_update", ct);

                // EOF lands mid-turn with the model stream held open — the
                // golden records how far up the ladder that actually goes.
                await host.ShutdownChildAsync(ct);
            },
        },
        new()
        {
            Name = "resume-live-cwd",
            RunAsync = static async (host, ct) =>
            {
                host.EnqueueTurn(new CannedTurn { Chunks = ["ok"] });
                await host.SpawnAsync(new ScenarioSpawn());

                var ack = await host.Client.SendAsync("prompt", """{"message":"Say ok."}""", ct);
                Assert.True(ack.Success, ack.Error);
                await host.NextEventAsync("agent_settled", ct);

                var sessionFile = SessionFile(await host.Client.SendAsync("get_state", cancellationToken: ct));
                Assert.Equal(0, await host.ShutdownChildAsync(ct));

                // ADR-0026 matrix, live-cwd arm: spawn-time --session against
                // the same, still-existing cwd resumes with history intact.
                await host.SpawnAsync(new ScenarioSpawn { ExtraArgs = ["--session", sessionFile] });
                var resumed = await host.Client.SendAsync("get_state", cancellationToken: ct);
                Assert.True(resumed.Success, resumed.Error);
                Assert.True(MessageCount(resumed) > 0, "resumed session shows no messages");

                Assert.Equal(0, await host.ShutdownChildAsync(ct));
            },
        },
        new()
        {
            Name = "resume-missing-cwd",
            RunAsync = static async (host, ct) =>
            {
                var doomed = host.CreateCwd("@CWD:doomed@");
                host.EnqueueTurn(new CannedTurn { Chunks = ["ok"] });
                await host.SpawnAsync(new ScenarioSpawn { CwdToken = doomed });

                var ack = await host.Client.SendAsync("prompt", """{"message":"Say ok."}""", ct);
                Assert.True(ack.Success, ack.Error);
                await host.NextEventAsync("agent_settled", ct);

                var sessionFile = SessionFile(await host.Client.SendAsync("get_state", cancellationToken: ct));
                Assert.Equal(0, await host.ShutdownChildAsync(ct));

                // ADR-0026 matrix, reaped-cwd arm: spawn-time --session
                // against a session whose recorded cwd is gone is a HARD
                // STARTUP FAILURE — pi exits 1 ("Stored session working
                // directory does not exist" on stderr) before serving any
                // command. The golden pins that exit; it is the recorded
                // ground for ADR-0026's PTY-fallback policy.
                host.DeleteCwd(doomed);
                await host.SpawnAsync(new ScenarioSpawn { ExtraArgs = ["--session", sessionFile] });
                Assert.Equal(1, await host.AwaitChildExitAsync(ct));
            },
        },
    ];

    public static TheoryData<string> Names
    {
        get
        {
            var names = new TheoryData<string>();
            foreach (var scenario in All)
            {
                names.Add(scenario.Name);
            }

            return names;
        }
    }

    public static GoldenScenario ByName(string name) =>
        All.Single(s => string.Equals(s.Name, name, StringComparison.Ordinal));

    private static string RequestId(PiRpcEvent uiRequest)
    {
        using var parsed = JsonDocument.Parse(uiRequest.Json);
        return parsed.RootElement.GetProperty("id").GetString()
            ?? throw new InvalidOperationException($"extension_ui_request without id: {uiRequest.Json}");
    }

    private static string SessionFile(PiRpcResponse state)
    {
        Assert.True(state.Success, state.Error);
        using var parsed = JsonDocument.Parse(state.Json);
        return parsed.RootElement.GetProperty("data").GetProperty("sessionFile").GetString()
            ?? throw new InvalidOperationException($"get_state without sessionFile: {state.Json}");
    }

    private static int MessageCount(PiRpcResponse state)
    {
        using var parsed = JsonDocument.Parse(state.Json);
        return parsed.RootElement.GetProperty("data").GetProperty("messageCount").GetInt32();
    }

    private static int SessionCount(PiRpcResponse list)
    {
        using var parsed = JsonDocument.Parse(list.Json);
        return parsed.RootElement.GetProperty("data").GetProperty("sessions").GetArrayLength();
    }
}
