using FingerTrap.Sidecar.Abstractions;
using FingerTrap.Sidecar.Ipc;
using FingerTrap.Sidecar.Pty;
using FingerTrap.Sidecar.Settings;
using Xunit;

namespace FingerTrap.Sidecar.Tests;

/// <summary>
/// Settings loading and the settings-over-environment precedence (N-1, #52).
/// </summary>
// Serialized with every other env-mutating test class: xUnit v3 runs test
// classes in parallel, and two classes scoping the same process-wide
// environment variable race each other — the local 1-in-3 false red and,
// in all likelihood, CI flake #60. Same collection name = no parallelism.
[Collection("process-environment")]
public sealed class SettingsTests
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

    private static string WriteTemp(string contents)
    {
        var path = Path.Combine(Path.GetTempPath(), $"ft-settings-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, contents);
        return path;
    }

    // --- Parse ------------------------------------------------------------

    [Fact]
    public void Parse_MinimalVersionOnly_Succeeds()
    {
        // A file that sets nothing must behave exactly like no file at all.
        var s = SettingsLoader.Parse("""{"version": 1}""");

        Assert.Equal(1, s.Version);
        Assert.Null(s.Pi);
        Assert.Null(s.Pane);
    }

    [Fact]
    public void Parse_FullDocument_ReadsBothSections()
    {
        var s = SettingsLoader.Parse("""
            {"version": 1, "pi": {"path": "/opt/pi"}, "pane": {"defaultKind": "shell"}}
            """);

        Assert.Equal("/opt/pi", s.Pi?.Path);
        Assert.Equal("shell", s.Pane?.DefaultKind);
    }

    [Fact]
    public void Parse_UnknownKeys_AreTolerated()
    {
        // Forward compatibility WITHIN a version: a newer build may write keys
        // this one does not know. Version is the compatibility gate, not key
        // presence.
        var s = SettingsLoader.Parse("""{"version": 1, "theme": "dark", "pi": {"path": "/x"}}""");

        Assert.Equal("/x", s.Pi?.Path);
    }

    [Fact]
    public void Parse_UnsupportedVersion_Throws()
    {
        // Refuse rather than best-effort: a future schema may reuse a key with
        // different meaning, so reading it under version-1 rules could apply a
        // setting the operator never intended.
        var ex = Assert.Throws<SettingsException>(() => SettingsLoader.Parse("""{"version": 2}"""));

        Assert.Contains("version 2", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_MissingVersion_IsTreatedAsUnsupported()
    {
        // An unversioned file is one this build cannot vouch for: once v2
        // exists it is ambiguous. Rejected with a message saying what to add.
        var ex = Assert.Throws<SettingsException>(() => SettingsLoader.Parse("""{"pi": {"path": "/x"}}"""));

        Assert.Contains("version", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_MalformedJson_Throws()
    {
        Assert.Throws<SettingsException>(() => SettingsLoader.Parse("{ this is not json"));
    }

    [Fact]
    public void Parse_Empty_Throws()
    {
        Assert.Throws<SettingsException>(() => SettingsLoader.Parse("   "));
    }

    // --- LoadFrom ---------------------------------------------------------

    [Fact]
    public void LoadFrom_AbsentFile_ReturnsDefaultsWithoutThrowing()
    {
        // The load-bearing case: a fresh install has no settings file and must
        // work identically to before settings existed.
        var path = Path.Combine(Path.GetTempPath(), $"ft-absent-{Guid.NewGuid():N}.json");

        var s = SettingsLoader.LoadFrom(path);

        Assert.Null(s.Pi);
        Assert.Null(s.Pane);
    }

    [Fact]
    public void LoadFrom_BadFile_ThrowsNamingThePath()
    {
        // The operator needs to know WHICH file is wrong, not just that one is.
        var path = WriteTemp("{ broken");
        try
        {
            var ex = Assert.Throws<SettingsException>(() => SettingsLoader.LoadFrom(path));

            Assert.Contains(path, ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ResolvePath_EndsAtTheDocumentedLocation()
    {
        var path = SettingsLoader.ResolvePath();

        Assert.EndsWith(Path.Combine("fingertrap", "settings.json"), path, StringComparison.Ordinal);
        Assert.True(Path.IsPathRooted(path));
    }

    [Fact]
    public void ResolvePath_RootIsThePlatformAppDataDir_NotAssumedToBeDotConfig()
    {
        // Pinned because assuming `~/.config` everywhere is wrong and cost a
        // debug cycle: on macOS ApplicationData is
        // ~/Library/Application Support, so a settings file written to
        // ~/.config/fingertrap/ is silently never read. Only Linux uses
        // ~/.config. Anything documenting the location for users must say so
        // per platform.
        var path = SettingsLoader.ResolvePath();
        var expectedRoot = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        if (!string.IsNullOrEmpty(expectedRoot))
        {
            Assert.StartsWith(expectedRoot, path, StringComparison.Ordinal);
        }

        if (OperatingSystem.IsMacOS())
        {
            Assert.Contains("Library", path, StringComparison.Ordinal);
        }
    }

    // --- precedence: pi executable ----------------------------------------

    [Fact]
    public void ResolvePi_SettingsOutrankEnvironment()
    {
        using var _ = EnvScope(PtyService.PiPathEnvVar, "/from/env/pi");

        var result = PtyService.ResolvePi(null, new PiSettings { Path = "/from/settings/pi" });

        Assert.Equal("/from/settings/pi", result);
    }

    [Fact]
    public void ResolvePi_ExplicitRequestOutranksSettings()
    {
        var result = PtyService.ResolvePi("/from/request/pi", new PiSettings { Path = "/from/settings/pi" });

        Assert.Equal("/from/request/pi", result);
    }

    [Fact]
    public void ResolvePi_EnvStillWorksWhenSettingsAreSilent()
    {
        // The env var is kept as a lower layer, not retired: nothing that
        // works today stops working.
        using var _ = EnvScope(PtyService.PiPathEnvVar, "/from/env/pi");

        Assert.Equal("/from/env/pi", PtyService.ResolvePi(null, new PiSettings()));
        Assert.Equal("/from/env/pi", PtyService.ResolvePi(null, settings: null));
    }

    [Fact]
    public void ResolvePi_MissingPiStillThrows_AndMessageNamesSettings()
    {
        // Reading from a file instead of the environment must not soften
        // ADR-0013's fail-loud contract.
        using var env = EnvScope(PtyService.PiPathEnvVar, null);
        var dir = Directory.CreateTempSubdirectory("ft-settings-nopi-");
        try
        {
            using var path = EnvScope("PATH", dir.FullName);

            var ex = Assert.Throws<PiNotFoundException>(() => PtyService.ResolvePi(null, new PiSettings()));

            Assert.Contains("pi.path", ex.Message, StringComparison.Ordinal);
            Assert.Contains(PtyService.PiPathEnvVar, ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    // --- precedence: default pane kind ------------------------------------

    [Fact]
    public void PaneKind_SettingsOutrankEnvironment()
    {
        using var _ = EnvScope(PaneKinds.DefaultKindEnvVar, "pi");

        Assert.Equal(PaneKind.Shell, PaneKinds.Parse(null, new PaneSettings { DefaultKind = "shell" }));
    }

    [Fact]
    public void PaneKind_ExplicitRequestOutranksSettings()
    {
        Assert.Equal(PaneKind.Pi, PaneKinds.Parse("pi", new PaneSettings { DefaultKind = "shell" }));
    }

    [Fact]
    public void PaneKind_EnvStillWorksWhenSettingsAreSilent()
    {
        using var _ = EnvScope(PaneKinds.DefaultKindEnvVar, "shell");

        Assert.Equal(PaneKind.Shell, PaneKinds.Parse(null, new PaneSettings()));
        Assert.Equal(PaneKind.Shell, PaneKinds.Parse(null, settings: null));
    }

    [Fact]
    public void PaneKind_NoSettingsNoEnv_StillDefaultsToPi()
    {
        using var _ = EnvScope(PaneKinds.DefaultKindEnvVar, null);

        Assert.Equal(PaneKind.Pi, PaneKinds.Parse(null, new PaneSettings()));
    }

    [Fact]
    public void PaneKind_UnknownValueInSettings_Throws()
    {
        // Same contract as the env var (ADR-0013): a typo must not silently
        // yield the default, which would be indistinguishable from working.
        using var _ = EnvScope(PaneKinds.DefaultKindEnvVar, null);

        Assert.Throws<ArgumentException>(() => PaneKinds.Parse(null, new PaneSettings { DefaultKind = "pie" }));
    }
}
