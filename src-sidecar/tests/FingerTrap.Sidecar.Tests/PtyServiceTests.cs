using FingerTrap.Sidecar.Abstractions;
using FingerTrap.Sidecar.Pty;
using Xunit;

namespace FingerTrap.Sidecar.Tests;

public sealed class PtyServiceTests
{
    [Fact]
    public void ResolveCwd_NullRequest_ReturnsUserProfile()
    {
        var result = PtyService.ResolveCwd(null);

        var expected = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ResolveCwd_EmptyRequest_ReturnsUserProfile()
    {
        var result = PtyService.ResolveCwd(string.Empty);

        var expected = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ResolveCwd_ExplicitPath_ReturnsThatPath()
    {
        var result = PtyService.ResolveCwd("/tmp/foo");

        Assert.Equal("/tmp/foo", result);
    }

    [Fact]
    public void ResolveCwd_WhitespaceRequest_ReturnsRequestAsIs()
    {
        // We intentionally do not trim. An explicit-but-weird cwd is
        // the caller's choice; the spawn will fail validation downstream
        // if the directory doesn't exist, which is the right error path.
        var result = PtyService.ResolveCwd("   ");

        Assert.Equal("   ", result);
    }

    [Fact]
    public async Task Close_UnknownSession_IsANoOp()
    {
        // pty/kill's idempotency (ADR-0021) bottoms out here: closing a
        // session that never existed, or already exited and removed itself,
        // must not throw.
        await using var pty = new PtyService();

        pty.Close("never-spawned");
    }

    [Fact]
    public async Task Close_LiveSession_KillsProcessAndRaisesExited()
    {
        if (OperatingSystem.IsWindows())
        {
            // pty/spawn throws PlatformNotSupportedException until a ConPty
            // backend lands (docs/milestones.md).
            return;
        }

        // Regression pin for the kill-path wedge: Close() must end with the
        // process reaped and Exited raised. The broken ordering (tear the
        // read loop down, then kill) left the child uninterruptibly stuck in
        // terminal teardown on macOS — no exit event, ever.
        await using var pty = new PtyService();
        var exited = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        pty.Exited += (_, e) =>
        {
            if (e.SessionId == "close-live")
            {
                exited.TrySetResult(e.ExitCode);
            }
        };

        var options = new PtySpawnOptions("/bin/sh", null, 80, 24, null, PaneKind.Shell);
        _ = await pty.SpawnAsync("close-live", options, TestContext.Current.CancellationToken);

        pty.Close("close-live");

        var done = await Task.WhenAny(
            exited.Task,
            Task.Delay(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        Assert.Same(exited.Task, done);
    }
}
