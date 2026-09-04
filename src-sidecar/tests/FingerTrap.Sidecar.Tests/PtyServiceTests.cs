using System.Diagnostics;
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
    public void ResolveCommandLine_ShellPane_IsLoginShellRegardlessOfSession()
    {
        string[] loginShell = ["-l"];
        Assert.Equal(loginShell, PtyService.ResolveCommandLine(PaneKind.Shell));
        // SessionPath is a pi concern; a shell pane ignores it.
        Assert.Equal(loginShell, PtyService.ResolveCommandLine(PaneKind.Shell, "/s/one.jsonl"));
    }

    [Fact]
    public void ResolveCommandLine_PiPane_NoSession_TakesNoArguments()
    {
        Assert.Empty(PtyService.ResolveCommandLine(PaneKind.Pi));
        Assert.Empty(PtyService.ResolveCommandLine(PaneKind.Pi, null));
        Assert.Empty(PtyService.ResolveCommandLine(PaneKind.Pi, string.Empty));
    }

    [Fact]
    public void ResolveCommandLine_PiPane_WithSession_ResumesViaSessionFlag()
    {
        // PTY-pane resume from the session browser (FT-2 slice 5,
        // ADR-0026) — same spawn-time `--session` the RPC pane uses.
        string[] expected = ["--session", "/s/one.jsonl"];
        Assert.Equal(expected, PtyService.ResolveCommandLine(PaneKind.Pi, "/s/one.jsonl"));
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

    [Fact]
    public async Task DisposeAsync_LiveSession_KillsEntireProcessTree()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var root = Path.Combine(Path.GetTempPath(), $"fingertrap-pty-tree-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var scriptPath = Path.Combine(root, "parent.sh");
        var childPidPath = Path.Combine(root, "child.pid");
        await File.WriteAllTextAsync(
            scriptPath,
            $"#!/bin/sh\nsleep 300 &\necho $! > '{childPidPath}'\nwait\n",
            TestContext.Current.CancellationToken);
        File.SetUnixFileMode(
            scriptPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        var pty = new PtyService();
        var options = new PtySpawnOptions(scriptPath, null, 80, 24, null, PaneKind.Shell);
        var parentPid = await pty.SpawnAsync(
            "dispose-tree", options, TestContext.Current.CancellationToken);
        var childPid = await WaitForPidFileAsync(childPidPath);

        try
        {
            await pty.DisposeAsync();

            Assert.False(IsProcessAlive(parentPid));
            Assert.False(IsProcessAlive(childPid));
        }
        finally
        {
            TryKillTree(parentPid);
            TryKillTree(childPid);
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task<int> WaitForPidFileAsync(string path)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (File.Exists(path)
                && int.TryParse(await File.ReadAllTextAsync(path), out var pid))
            {
                return pid;
            }

            await Task.Delay(25, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException("PTY child did not publish its pid");
    }

    private static bool IsProcessAlive(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static void TryKillTree(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            // Already exited.
        }
    }
}
