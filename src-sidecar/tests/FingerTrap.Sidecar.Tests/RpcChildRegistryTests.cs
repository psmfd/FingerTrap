using FingerTrap.Sidecar.PiRpc;
using Xunit;

namespace FingerTrap.Sidecar.Tests;

public sealed class RpcChildRegistryTests : IDisposable
{
    private readonly string _path = Path.Combine(
        Path.GetTempPath(), $"rpc-children-test-{Guid.NewGuid():N}.json");

    // Fixed start times so pid+start matching is deterministic.
    private static readonly DateTime OwnerAStart = new(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime OwnerBStart = new(2026, 1, 1, 11, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ChildStart = new(2026, 1, 1, 10, 5, 0, DateTimeKind.Utc);

    public void Dispose()
    {
        try { File.Delete(_path); } catch (IOException) { }
    }

    private RpcChildRegistry Make(
        Func<int, DateTime?> probe, Action<int>? kill, (int, DateTime) owner) =>
        new(_path, probe, kill ?? (_ => { }), owner);

    [Fact]
    public void ReapOrphans_DeadOwner_LiveMatchingChild_KillsAndDrops()
    {
        // Sidecar A (pid 100) registers child 200, then crashes.
        Make(Alive((100, OwnerAStart), (200, ChildStart)), null, (100, OwnerAStart))
            .Register(200, ChildStart, "s1");

        // Sidecar B starts: owner A (100) is dead, child 200 still alive.
        var killed = new List<int>();
        var count = Make(Alive((200, ChildStart)), killed.Add, (300, OwnerBStart)).ReapOrphans();

        Assert.Equal(1, count);
        Assert.Equal(200, Assert.Single(killed));
        // Entry dropped: a second reap finds nothing.
        Assert.Equal(0, Make(Alive((200, ChildStart)), _ => Assert.Fail("must not re-kill"), (400, OwnerBStart)).ReapOrphans());
    }

    [Fact]
    public void ReapOrphans_LiveOwner_PreservesChild_NeverKills()
    {
        Make(Alive((100, OwnerAStart), (200, ChildStart)), null, (100, OwnerAStart))
            .Register(200, ChildStart, "s1");

        // Concurrent sidecar B: owner A (100) is STILL alive — its pane is not ours to reap.
        var killed = new List<int>();
        var count = Make(Alive((100, OwnerAStart), (200, ChildStart)), killed.Add, (300, OwnerBStart)).ReapOrphans();

        Assert.Equal(0, count);
        Assert.Empty(killed);
        // Entry preserved for the live owner.
        var reReap = Make(Alive((100, OwnerAStart), (200, ChildStart)), killed.Add, (301, OwnerBStart)).ReapOrphans();
        Assert.Equal(0, reReap);
    }

    [Fact]
    public void ReapOrphans_DeadOwner_ChildPidReused_DoesNotKill()
    {
        Make(Alive((100, OwnerAStart), (200, ChildStart)), null, (100, OwnerAStart))
            .Register(200, ChildStart, "s1");

        // Owner dead; pid 200 is alive but a DIFFERENT process (start time
        // mismatch = PID reuse). Must not kill an unrelated process.
        var killed = new List<int>();
        var laterStart = ChildStart.AddHours(3);
        var count = Make(Alive((200, laterStart)), killed.Add, (300, OwnerBStart)).ReapOrphans();

        Assert.Equal(0, count);
        Assert.Empty(killed);
    }

    [Fact]
    public void ReapOrphans_DeadOwner_ChildAlreadyGone_DropsEntry_NoKill()
    {
        Make(Alive((100, OwnerAStart), (200, ChildStart)), null, (100, OwnerAStart))
            .Register(200, ChildStart, "s1");

        // Owner dead, child already exited (not alive).
        var killed = new List<int>();
        var count = Make(Alive(), killed.Add, (300, OwnerBStart)).ReapOrphans();

        Assert.Equal(0, count);
        Assert.Empty(killed);
    }

    [Fact]
    public void Unregister_RemovesOwnEntry()
    {
        var reg = Make(Alive((100, OwnerAStart), (200, ChildStart)), null, (100, OwnerAStart));
        reg.Register(200, ChildStart, "s1");
        reg.Unregister(200);

        // Nothing left to reap even with a dead owner.
        var killed = new List<int>();
        Assert.Equal(0, Make(Alive((200, ChildStart)), killed.Add, (300, OwnerBStart)).ReapOrphans());
        Assert.Empty(killed);
    }

    [Fact]
    public void Register_ReplacesStaleSamePidEntry()
    {
        var reg = Make(Alive((100, OwnerAStart)), null, (100, OwnerAStart));
        reg.Register(200, ChildStart, "s1");
        var newStart = ChildStart.AddMinutes(30);
        reg.Register(200, newStart, "s2");

        // Only the latest start time reaps: probe with the NEW start matches.
        var killed = new List<int>();
        Assert.Equal(1, Make(Alive((200, newStart)), killed.Add, (300, OwnerBStart)).ReapOrphans());
        Assert.Equal(200, Assert.Single(killed));
    }

    [Fact]
    public void ReapOrphans_NoFile_ReturnsZero()
    {
        Assert.Equal(0, Make(Alive(), _ => Assert.Fail("nothing to kill"), (100, OwnerAStart)).ReapOrphans());
    }

    /// <summary>A start-time probe from a fixed alive-set; unknown pid → null.</summary>
    private static Func<int, DateTime?> Alive(params (int Pid, DateTime Start)[] live)
    {
        var map = live.ToDictionary(x => x.Pid, x => x.Start);
        return pid => map.TryGetValue(pid, out var start) ? start : null;
    }
}
