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

    private static Task SpawnAsync(RpcPaneService service, string sessionId, params string[] steps)
    {
        var options = new RpcPaneSpawnOptions(
            Cwd: null, SessionPath: null, Env: null, RequestedPath: WriteShim(steps));
        return service.SpawnAsync(sessionId, options, TestContext.Current.CancellationToken);
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
