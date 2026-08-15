using FingerTrap.Sidecar.Abstractions;
using FingerTrap.Sidecar.Ipc;
using FingerTrap.Sidecar.Pty;
using Xunit;

namespace FingerTrap.Sidecar.Tests;

/// <summary>
/// Pane-kind parsing and pi resolution (FT-0, issue #45).
/// </summary>
/// <remarks>
/// Both units read process environment, so every test that depends on an
/// environment variable sets and restores it rather than assuming a clean
/// host. These are not parallel-safe against each other for that reason;
/// xunit runs tests within a class sequentially, which is why they live in one
/// class rather than being split by concern.
/// </remarks>
public sealed class PaneKindTests
{
    // Concrete return type, not IDisposable: CA1859 is an error in this repo.
    private static EnvVar EnvScope(string name, string? value) => new(name, value);

    private sealed class EnvVar : IDisposable
    {
        private readonly string _name;
        private readonly string? _original;

        public EnvVar(string name, string? value)
        {
            _name = name;
            _original = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose() => Environment.SetEnvironmentVariable(_name, _original);
    }

    // --- PaneKinds.Parse ------------------------------------------------

    [Fact]
    public void Parse_ExplicitPi_ReturnsPi()
    {
        Assert.Equal(PaneKind.Pi, PaneKinds.Parse("pi"));
    }

    [Fact]
    public void Parse_ExplicitShell_ReturnsShell()
    {
        Assert.Equal(PaneKind.Shell, PaneKinds.Parse("shell"));
    }

    [Theory]
    [InlineData("PI")]
    [InlineData("Pi")]
    [InlineData("  pi  ")]
    public void Parse_IsCaseInsensitiveAndTrims(string requested)
    {
        Assert.Equal(PaneKind.Pi, PaneKinds.Parse(requested));
    }

    [Fact]
    public void Parse_AbsentWithNoEnv_ReturnsHostDefaultOfPi()
    {
        // FingerTrap is the pi Home; an unqualified pane is a pi pane.
        using var _ = EnvScope(PaneKinds.DefaultKindEnvVar, null);

        Assert.Equal(PaneKind.Pi, PaneKinds.Parse(null));
        Assert.Equal(PaneKind.Pi, PaneKinds.Parse(""));
        Assert.Equal(PaneKind.Pi, PaneKinds.Parse("   "));
    }

    [Fact]
    public void Parse_AbsentWithEnvOverride_UsesEnv()
    {
        using var _ = EnvScope(PaneKinds.DefaultKindEnvVar, "shell");

        Assert.Equal(PaneKind.Shell, PaneKinds.Parse(null));
    }

    [Fact]
    public void Parse_ExplicitRequestBeatsEnv()
    {
        // A caller that names a kind means it; the env var is only a default.
        using var _ = EnvScope(PaneKinds.DefaultKindEnvVar, "shell");

        Assert.Equal(PaneKind.Pi, PaneKinds.Parse("pi"));
    }

    [Fact]
    public void Parse_UnknownValue_Throws()
    {
        // Never silently fall back to the default: a typo'd value that opened
        // the default pane would be indistinguishable from the setting working.
        using var _ = EnvScope(PaneKinds.DefaultKindEnvVar, null);

        Assert.Throws<ArgumentException>(() => PaneKinds.Parse("pie"));
    }

    [Fact]
    public void Parse_UnknownEnvValue_Throws()
    {
        using var _ = EnvScope(PaneKinds.DefaultKindEnvVar, "pie");

        Assert.Throws<ArgumentException>(() => PaneKinds.Parse(null));
    }

    // --- PtyService.ResolvePi -------------------------------------------

    [Fact]
    public void ResolvePi_ExplicitPath_ReturnsItVerbatim()
    {
        // Not probed for existence: an explicit path is the caller's choice,
        // and the spawn surfaces a bad one at the same layer a bad shell path
        // already does.
        Assert.Equal("/opt/custom/pi", PtyService.ResolvePi("/opt/custom/pi"));
    }

    [Fact]
    public void ResolvePi_EnvVar_UsedWhenNoExplicitPath()
    {
        using var _ = EnvScope(PtyService.PiPathEnvVar, "/opt/env/pi");

        Assert.Equal("/opt/env/pi", PtyService.ResolvePi(null));
    }

    [Fact]
    public void ResolvePi_ExplicitPathBeatsEnvVar()
    {
        using var _ = EnvScope(PtyService.PiPathEnvVar, "/opt/env/pi");

        Assert.Equal("/opt/explicit/pi", PtyService.ResolvePi("/opt/explicit/pi"));
    }

    [Fact]
    public void ResolvePi_FoundOnPath_ReturnsAbsoluteCandidate()
    {
        // Build a throwaway PATH entry holding an executable named `pi`, so
        // the test proves the PATH search rather than depending on whether the
        // host happens to have pi installed.
        using var env = EnvScope(PtyService.PiPathEnvVar, null);
        var dir = Directory.CreateTempSubdirectory("ft-pi-path-");
        try
        {
            var fake = Path.Combine(dir.FullName, "pi");
            File.WriteAllText(fake, "#!/bin/sh\nexit 0\n");
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(fake, UnixFileMode.UserRead | UnixFileMode.UserExecute);
            }

            using var path = EnvScope("PATH", dir.FullName);

            Assert.Equal(fake, PtyService.ResolvePi(null));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void ResolvePi_NonExecutableOnPath_IsIgnored()
    {
        // A same-named but non-executable file must not be returned as a
        // usable answer — it would fail at spawn with a far less clear error.
        if (OperatingSystem.IsWindows())
        {
            return; // no Unix mode bits to assert against
        }

        using var env = EnvScope(PtyService.PiPathEnvVar, null);
        var dir = Directory.CreateTempSubdirectory("ft-pi-noexec-");
        try
        {
            var fake = Path.Combine(dir.FullName, "pi");
            File.WriteAllText(fake, "not executable");
            File.SetUnixFileMode(fake, UnixFileMode.UserRead);

            using var path = EnvScope("PATH", dir.FullName);

            Assert.Throws<PiNotFoundException>(() => PtyService.ResolvePi(null));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void ResolvePi_NotFoundAnywhere_ThrowsWithActionableMessage()
    {
        // The core FT-0 contract: a pi pane never degrades to a shell.
        using var env = EnvScope(PtyService.PiPathEnvVar, null);
        var dir = Directory.CreateTempSubdirectory("ft-pi-empty-");
        try
        {
            using var path = EnvScope("PATH", dir.FullName);

            var ex = Assert.Throws<PiNotFoundException>(() => PtyService.ResolvePi(null));

            // The message is read by a person staring at an empty pane, so it
            // must name both places that were searched and how to fix it.
            Assert.Contains(PtyService.PiPathEnvVar, ex.Message, StringComparison.Ordinal);
            Assert.Contains("PATH", ex.Message, StringComparison.Ordinal);
            Assert.Contains("shell pane", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    // --- PtyService.ResolveExecutable ------------------------------------

    [Fact]
    public void ResolveExecutable_ShellKind_KeepsPreFt0Behaviour()
    {
        // The shell path must be unchanged by the pane-kind work.
        Assert.Equal("/bin/zsh", PtyService.ResolveExecutable(PaneKind.Shell, "/bin/zsh"));
    }

    [Fact]
    public void ResolveExecutable_ShellKind_NeverThrowsWhenUnresolved()
    {
        // Asymmetry with pi is deliberate: some shell essentially always
        // exists, so guessing is harmless; guessing at pi is not.
        var result = PtyService.ResolveExecutable(PaneKind.Shell, null);

        Assert.False(string.IsNullOrEmpty(result));
    }

    [Fact]
    public void ResolveExecutable_PiKind_RoutesToPiResolution()
    {
        Assert.Equal("/opt/custom/pi", PtyService.ResolveExecutable(PaneKind.Pi, "/opt/custom/pi"));
    }
}
