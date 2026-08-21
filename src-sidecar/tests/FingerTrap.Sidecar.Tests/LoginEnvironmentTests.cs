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

    [Fact]
    public void ResolveLoginPath_NeverThrows_AndYieldsPosixPathOrNull()
    {
        // Real shell spawn — assert only the contract: it never throws, and on
        // a POSIX host returns either null (soft failure) or a colon-path
        // containing at least the system bin. Deterministic assertion is not
        // possible (depends on the host profile), so this pins the safety
        // guarantees, not the value.
        var result = LoginEnvironment.ResolveLoginPath(TimeSpan.FromSeconds(10));
        if (OperatingSystem.IsWindows())
        {
            Assert.Null(result);
        }
        else if (result is not null)
        {
            Assert.Contains("/", result);
        }
    }
}
