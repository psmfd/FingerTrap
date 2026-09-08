using FingerTrap.Sidecar.Processes;
using Xunit;

namespace FingerTrap.Sidecar.Tests;

public sealed class ChildSpawnCoordinatorTests
{
    [Fact]
    public void Run_DarwinSpawnFailure_ReleasesGate()
    {
        if (!OperatingSystem.IsMacOS()) return;

        Assert.Throws<InvalidOperationException>(() =>
            ChildSpawnCoordinator.Run<int>(() => throw new InvalidOperationException("expected")));

        Assert.Equal(42, ChildSpawnCoordinator.Run(() => 42));
    }

    [Fact]
    public async Task RunAsync_DarwinSpawnFailure_ReleasesGate()
    {
        if (!OperatingSystem.IsMacOS()) return;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ChildSpawnCoordinator.RunAsync<int>(
                () => Task.FromException<int>(new InvalidOperationException("expected")),
                TestContext.Current.CancellationToken));

        Assert.Equal(42, ChildSpawnCoordinator.Run(() => 42));
    }

    [Fact]
    public async Task RunAsync_DarwinCanceledWait_DoesNotCorruptGate()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var cancellationToken = TestContext.Current.CancellationToken;
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var holder = Task.Run(() => ChildSpawnCoordinator.Run(() =>
        {
            entered.Set();
            release.Wait(cancellationToken);
            return 0;
        }), cancellationToken);
        Assert.True(entered.Wait(TimeSpan.FromSeconds(2), cancellationToken));

        try
        {
            using var canceled = new CancellationTokenSource();
            canceled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                ChildSpawnCoordinator.RunAsync(() => Task.FromResult(1), canceled.Token));
        }
        finally
        {
            release.Set();
            await holder.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
        }

        Assert.Equal(42, ChildSpawnCoordinator.Run(() => 42));
    }

    [Fact]
    public async Task Run_NonDarwin_DoesNotSerializeSpawnCallbacks()
    {
        if (OperatingSystem.IsMacOS()) return;

        var cancellationToken = TestContext.Current.CancellationToken;
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var holder = Task.Run(() => ChildSpawnCoordinator.Run(() =>
        {
            entered.Set();
            release.Wait(cancellationToken);
            return 0;
        }), cancellationToken);
        Assert.True(entered.Wait(TimeSpan.FromSeconds(2), cancellationToken));

        try
        {
            var concurrent = Task.Run(
                () => ChildSpawnCoordinator.Run(() => 42),
                cancellationToken);
            Assert.Equal(
                42,
                await concurrent.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken));
        }
        finally
        {
            release.Set();
            await holder.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
        }
    }
}
