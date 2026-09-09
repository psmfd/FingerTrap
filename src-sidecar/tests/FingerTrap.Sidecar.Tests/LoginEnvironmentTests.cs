using System.Diagnostics;
using FingerTrap.Sidecar.Executables;
using Xunit;

namespace FingerTrap.Sidecar.Tests;

public sealed class LoginEnvironmentTests
{
    [Fact]
    public void AugmentPath_NullOrEmptyLogin_ReturnsNull_LeavesPathUntouched()
    {
        Assert.Null(LoginEnvironment.AugmentPath("/usr/bin:/bin", null));
        Assert.Null(LoginEnvironment.AugmentPath("/usr/bin:/bin", ""));
    }

    [Fact]
    public void AugmentPath_LoginSupersetOfBare_GivesUserDirsPriority()
    {
        // The launchd bare PATH vs a real login PATH with homebrew + ~/.local/bin.
        var bare = "/usr/bin:/bin:/usr/sbin:/sbin";
        var login = "/Users/x/.local/bin:/opt/homebrew/bin:/usr/bin:/bin:/usr/sbin:/sbin";

        var merged = LoginEnvironment.AugmentPath(bare, login);

        // Login order preserved, user dirs first; no duplication of the shared
        // system dirs; nothing dropped.
        Assert.Equal("/Users/x/.local/bin:/opt/homebrew/bin:/usr/bin:/bin:/usr/sbin:/sbin", merged);
    }

    [Fact]
    public void AugmentPath_CurrentHasDirNotInLogin_AppendsItAtTheEnd()
    {
        // A directory the process holds but the login shell does not must not
        // be lost (no regression against the pre-fix PATH).
        var current = "/usr/bin:/opt/only-in-process";
        var login = "/opt/homebrew/bin:/usr/bin";

        var merged = LoginEnvironment.AugmentPath(current, login);

        Assert.Equal("/opt/homebrew/bin:/usr/bin:/opt/only-in-process", merged);
    }

    [Fact]
    public void AugmentPath_Deduplicates_PreservingLoginOrder()
    {
        var merged = LoginEnvironment.AugmentPath("/a:/b:/a", "/b:/c:/b");
        Assert.Equal("/b:/c:/a", merged);
    }

    [Fact]
    public void AugmentPath_NullCurrent_ReturnsLoginVerbatim()
    {
        Assert.Equal("/opt/homebrew/bin:/usr/bin", LoginEnvironment.AugmentPath(null, "/opt/homebrew/bin:/usr/bin"));
    }

    [Theory]
    [InlineData("printf 'profile banner\\nMARK\\n/opt/tools:/usr/bin\\nMARK\\nlogout banner'", "/opt/tools:/usr/bin")]
    [InlineData("printf '/usr/bin'", null)]
    [InlineData("printf '\\nMARK\\n/usr/bin\\nMARK\\n'; exit 1", null)]
    [InlineData("head -c 131072 /dev/zero >&2; printf '\\nMARK\\n/usr/bin\\nMARK\\n'", "/usr/bin")]
    [InlineData("head -c 131072 /dev/zero; printf '\\nMARK\\n/usr/bin\\nMARK\\n'", null)]
    public async Task Probe_IsolatesProfileOutputAndBoundsCapture(string command, string? expected)
    {
        if (OperatingSystem.IsWindows()) return;
        var result = await LoginEnvironment.ProbeAsync(Shell(command), "MARK", TimeSpan.FromSeconds(3));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("sleep 30")]
    [InlineData("head -c 131072 /dev/zero >&2; sleep 30")]
    public async Task Probe_HungShell_ReturnsWithinBudget(string command)
    {
        if (OperatingSystem.IsWindows()) return;
        var result = await LoginEnvironment.ProbeAsync(Shell(command), "MARK", TimeSpan.FromMilliseconds(200))
            .WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        Assert.Null(result);
    }

    [Fact]
    public async Task Probe_ExitedShellWithInheritedPipe_DoesNotWaitForEof()
    {
        if (OperatingSystem.IsWindows()) return;
        // A short-lived descendant outlives its shell and retains both pipes.
        // It self-exits so the test never leaves an unbounded orphan behind.
        var result = await LoginEnvironment.ProbeAsync(Shell("sleep 2 & exit 0"), "MARK", TimeSpan.FromMilliseconds(100))
            .WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        Assert.Null(result);
    }

    private static ProcessStartInfo Shell(string command)
    {
        var start = new ProcessStartInfo("/bin/sh");
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add(command);
        return start;
    }
}
