using FingerTrap.Sidecar.Abstractions;
using FingerTrap.Sidecar.Ipc;
using FingerTrap.Sidecar.PiRpc;
using Nerdbank.Streams;
using Newtonsoft.Json.Serialization;
using NSubstitute;
using StreamJsonRpc;
using Xunit;
using static FingerTrap.Sidecar.Tests.FakePiShim;

namespace FingerTrap.Sidecar.Tests;

/// <summary>
/// True JSON-RPC round-trips through the exact Program.cs wiring
/// (JsonMessageFormatter + camelCase resolver +
/// UseSingleObjectParameterDeserialization). Direct-call tests like
/// RpcSurfaceTests never exercise the Newtonsoft wire-deserialization
/// path, which has a known silent hazard: positional-record constructor
/// defaults are dropped for omitted JSON properties
/// (Newtonsoft.Json#2765). These lock the request-record → method →
/// FakePi → RpcCommandResult chain end to end.
/// </summary>
public sealed class RpcSurfaceRoundTripTests
{
    [Fact]
    public async Task RpcGetState_RoundTrips_EnvelopeStrippedDataReachesClient()
    {
        await using var service = new RpcPaneService();
        await SpawnAsync(service, "rt1",
            Step("waitForLine", "get_state"),
            Step("writeLine",
                """{"id":"{{lastId}}","type":"response","command":"get_state","success":true,"data":{"isStreaming":false,"thinkingLevel":"high"}}"""),
            Step("waitForEof", true));
        using var surface = new RpcSurface(Substitute.For<IPtyService>(), rpcPanes: service);

        var (clientRpc, serverRpc) = CreateConnectedPair(surface);
        using (serverRpc)
        using (clientRpc)
        {
            var result = await clientRpc.InvokeWithParameterObjectAsync<RpcCommandResult>(
                "rpc/getState",
                new { sessionId = "rt1" },
                TestContext.Current.CancellationToken);

            Assert.True(result.Success);
            Assert.Equal("high", (string?)result.Data?["thinkingLevel"]);
            Assert.Null(result.Data?["id"]);
        }

        await service.KillAsync("rt1", TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RpcSetThinkingLevel_RoundTrips_MultiFieldRecordDeserializes()
    {
        await using var service = new RpcPaneService();
        await SpawnAsync(service, "rt2",
            Step("waitForLine", "set_thinking_level"),
            Step("writeLine",
                """{"id":"{{lastId}}","type":"response","command":"set_thinking_level","success":true}"""),
            Step("waitForEof", true));
        using var surface = new RpcSurface(Substitute.For<IPtyService>(), rpcPanes: service);

        var (clientRpc, serverRpc) = CreateConnectedPair(surface);
        using (serverRpc)
        using (clientRpc)
        {
            // FakePi only answers after matching "set_thinking_level" on
            // stdin, so a successful result proves the camelCase params
            // object landed in RpcSetThinkingLevelRequest and the command
            // actually crossed the pi wire.
            var result = await clientRpc.InvokeWithParameterObjectAsync<RpcCommandResult>(
                "rpc/setThinkingLevel",
                new { sessionId = "rt2", level = "medium" },
                TestContext.Current.CancellationToken);

            Assert.True(result.Success);
            Assert.Null(result.Data);
        }

        await service.KillAsync("rt2", TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RpcExtensionUiResponse_RoundTrips_ValueSurvivesTheCamelCaseWireUntouched()
    {
        // The FakePi script is sequential: get_state is only answered after
        // the exact ui-response line matched, so the final assert proves the
        // multi-field record deserialized (Newtonsoft.Json#2765 path) AND
        // the value string crossed both JSON stacks verbatim — the
        // camelCase resolver renames C# properties, never payload content.
        await using var service = new RpcPaneService();
        await SpawnAsync(service, "rt3",
            Step("waitForLine", """{"id":"ui_7","type":"extension_ui_response","value":"Keep Going"}"""),
            Step("waitForLine", "get_state"),
            Step("writeLine",
                """{"id":"{{lastId}}","type":"response","command":"get_state","success":true}"""),
            Step("waitForEof", true));
        using var surface = new RpcSurface(Substitute.For<IPtyService>(), rpcPanes: service);

        var (clientRpc, serverRpc) = CreateConnectedPair(surface);
        using (serverRpc)
        using (clientRpc)
        {
            await clientRpc.InvokeWithParameterObjectAsync(
                "rpc/extensionUiResponse",
                new { sessionId = "rt3", requestId = "ui_7", value = "Keep Going" },
                TestContext.Current.CancellationToken);

            var result = await clientRpc.InvokeWithParameterObjectAsync<RpcCommandResult>(
                "rpc/getState",
                new { sessionId = "rt3" },
                TestContext.Current.CancellationToken);
            Assert.True(result.Success);
        }

        await service.KillAsync("rt3", TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RpcExtensionUiResponse_RoundTrips_ConfirmedFalseIsNotCancelled()
    {
        // confirmed:false is an explicit denial, a different wire shape
        // from cancelled — and false is the value most at risk of being
        // conflated with an omitted field across the Newtonsoft leg.
        await using var service = new RpcPaneService();
        await SpawnAsync(service, "rt4",
            Step("waitForLine", """{"id":"ui_8","type":"extension_ui_response","confirmed":false}"""),
            Step("waitForLine", "get_state"),
            Step("writeLine",
                """{"id":"{{lastId}}","type":"response","command":"get_state","success":true}"""),
            Step("waitForEof", true));
        using var surface = new RpcSurface(Substitute.For<IPtyService>(), rpcPanes: service);

        var (clientRpc, serverRpc) = CreateConnectedPair(surface);
        using (serverRpc)
        using (clientRpc)
        {
            await clientRpc.InvokeWithParameterObjectAsync(
                "rpc/extensionUiResponse",
                new { sessionId = "rt4", requestId = "ui_8", confirmed = false },
                TestContext.Current.CancellationToken);

            var result = await clientRpc.InvokeWithParameterObjectAsync<RpcCommandResult>(
                "rpc/getState",
                new { sessionId = "rt4" },
                TestContext.Current.CancellationToken);
            Assert.True(result.Success);
        }

        await service.KillAsync("rt4", TestContext.Current.CancellationToken);
    }

    private static async Task SpawnAsync(RpcPaneService service, string sessionId, params string[] steps)
    {
        var options = new RpcPaneSpawnOptions(
            Cwd: null, SessionPath: null, Env: null, RequestedPath: WriteShim(steps));
        // The hello result is unused here; these tests exercise the relay, not #150.
        await service.SpawnAsync(sessionId, options, TestContext.Current.CancellationToken);
    }

    /// <summary>Mirrors Program.cs's transport setup on both ends of an
    /// in-memory duplex pair; the surface is the server-side target.</summary>
    private static (JsonRpc Client, JsonRpc Server) CreateConnectedPair(RpcSurface surface)
    {
        var (clientStream, serverStream) = FullDuplexStream.CreatePair();

        var serverRpc = new JsonRpc(BuildHandler(serverStream));
        serverRpc.AddLocalRpcTarget(surface, new JsonRpcTargetOptions
        {
            MethodNameTransform = CommonMethodNameTransforms.CamelCase,
            UseSingleObjectParameterDeserialization = true,
        });
        serverRpc.StartListening();

        var clientRpc = new JsonRpc(BuildHandler(clientStream));
        clientRpc.StartListening();

        return (clientRpc, serverRpc);
    }

    private static HeaderDelimitedMessageHandler BuildHandler(System.IO.Stream stream)
    {
        var formatter = new JsonMessageFormatter();
        formatter.JsonSerializer.ContractResolver = new CamelCasePropertyNamesContractResolver();
        return new HeaderDelimitedMessageHandler(stream, stream, formatter);
    }
}
