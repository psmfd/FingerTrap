using FingerTrap.Sidecar.Abstractions;
using FingerTrap.Sidecar.Vm;
using Xunit;

namespace FingerTrap.Sidecar.Tests;

public sealed class VmAuthorizationTests
{
    private const string Epoch = "test-epoch";
    private static readonly VmExecutableIdentity Client = new("/opt/mcm", new string('a', 64));
    private static readonly VmExecutableIdentity Lima = new("/opt/limactl", new string('b', 64));

    [Fact]
    public void Consume_RegisteredNonceConcurrently_HasExactlyOneWinner()
    {
        var gate = new VmLaunchAuthorizationGate(TimeProvider.System, Epoch);
        Assert.True(gate.Register(Authorization(), out _));
        var winners = ParallelEnumerable.Range(0, 32)
            .Count(index => gate.TryConsume(
                "nonce", new VmStatusRequest("devbox"), Client.Path, Lima.Path,
                out var authority, out var detail));

        Assert.Equal(1, winners);
    }

    [Fact]
    public void Consume_TombstoneBlocksReregistrationAndOldEpochAfterRestart()
    {
        var authorization = Authorization();
        var gate = new VmLaunchAuthorizationGate(TimeProvider.System, Epoch);
        Assert.True(gate.Register(authorization, out _));
        Assert.True(gate.TryConsume(
            authorization.Nonce, new("devbox"), Client.Path, Lima.Path, out _, out _));
        Assert.False(gate.Register(authorization, out _));

        var restarted = new VmLaunchAuthorizationGate(TimeProvider.System, "new-epoch");
        Assert.False(restarted.Register(authorization, out _));
    }

    [Fact]
    public void Consume_UnregisteredOrMismatchedNonce_FailsClosed()
    {
        var gate = new VmLaunchAuthorizationGate(TimeProvider.System, Epoch);
        Assert.False(gate.TryConsume(
            "nonce", new VmStatusRequest("devbox"), Client.Path, Lima.Path, out _, out _));

        Assert.True(gate.Register(Authorization(), out _));
        Assert.False(gate.TryConsume(
            "nonce", new VmStatusRequest("other"), Client.Path, Lima.Path, out _, out _));
        Assert.False(gate.TryConsume(
            "nonce", new VmStatusRequest("devbox"), Client.Path, Lima.Path, out _, out _));
    }

    [Fact]
    public void Register_ExpiredMismatchedOrUnboundedAuthority_FailsBeforeStorage()
    {
        var gate = new VmLaunchAuthorizationGate(TimeProvider.System, Epoch);
        Assert.False(gate.Register(
            Authorization() with { ExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1) }, out _));
        Assert.False(gate.Register(
            Authorization() with { Operation = "up" }, out _));
        Assert.False(gate.Register(
            Authorization() with { Nonce = new string('n', 129) }, out _));
        Assert.False(gate.Register(
            Authorization() with { ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(10) }, out _));
    }

    private static VmLaunchAuthorization Authorization() => new(
        "nonce", Epoch, DateTimeOffset.UtcNow.AddMinutes(1), Client, Lima,
        McmProcessContract.InvocationRevision, McmProcessContract.EnvironmentRevision,
        McmProcessContract.StatusOperation, "devbox");
}
