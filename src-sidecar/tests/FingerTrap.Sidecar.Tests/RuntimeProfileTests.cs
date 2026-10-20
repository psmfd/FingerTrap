using System.Text.Json;
using FingerTrap.Sidecar.Runtime;
using Xunit;

namespace FingerTrap.Sidecar.Tests;

public sealed class RuntimeProfileTests
{
    private static JsonDocument ReadFixtures() => JsonDocument.Parse(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "RuntimeFixtures", "runtime-profile.v1.cases.json")));

    public static TheoryData<string, bool, string> ContractCases()
    {
        using var document = ReadFixtures();
        var cases = new TheoryData<string, bool, string>();
        foreach (var fixture in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            cases.Add(fixture.GetProperty("name").GetString()!,
                fixture.GetProperty("valid").GetBoolean(), fixture.GetProperty("input").GetRawText());
        }

        return cases;
    }

    [Theory]
    [MemberData(nameof(ContractCases))]
    public void Parse_SharedContract_AgreesWithRuntime(string name, bool valid, string json)
    {
        Assert.False(string.IsNullOrEmpty(name));
        using var definition = JsonDocument.Parse(json);
        var appRoot = Path.Combine(Path.GetTempPath(), "fingertrap-profile-contract");
        if (!valid)
        {
            Assert.Throws<ArgumentException>(() => RuntimeProfile.Parse(definition.RootElement, appRoot));
            return;
        }

        var profile = RuntimeProfile.Parse(definition.RootElement, appRoot);
        Assert.Equal(Path.Combine(appRoot, "profiles", profile.Id), profile.Root);
        var paths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["agentDir"] = profile.AgentDir,
            ["sessionsDir"] = profile.SessionsDir,
            ["extensionsDir"] = profile.ExtensionsDir,
            ["worktreeManifestsDir"] = profile.WorktreeManifestsDir,
            ["cacheDir"] = profile.CacheDir,
            ["tempDir"] = profile.TempDir,
            ["homeDir"] = profile.HomeDir,
        };
        using var fixtures = ReadFixtures();
        foreach (var property in fixtures.RootElement.GetProperty("layout").EnumerateObject())
        {
            Assert.Equal(Path.Combine([profile.Root, .. property.Value.GetString()!.Split('/')]), paths[property.Name]);
        }

        Assert.Equal(definition.RootElement.GetProperty("runtimeId").GetString(), profile.RuntimeId);
        var expectedRefs = definition.RootElement.GetProperty("credentialRefs").EnumerateArray().ToArray();
        Assert.Equal(expectedRefs.Length, profile.CredentialReferences.Count);
        for (var i = 0; i < expectedRefs.Length; i++)
        {
            Assert.Equal(expectedRefs[i].GetProperty("provider").GetString(), profile.CredentialReferences[i].Provider);
            Assert.Equal(expectedRefs[i].GetProperty("reference").GetString(), profile.CredentialReferences[i].Reference);
        }
    }

    [Fact]
    public void Parse_TwoProfiles_HaveIndependentPathsWithoutCreatingFiles()
    {
        var appRoot = Directory.CreateTempSubdirectory("fingertrap-profile-contract-");
        try
        {
            using var fixtures = ReadFixtures();
            var cases = fixtures.RootElement.GetProperty("cases");
            var alpha = RuntimeProfile.Parse(cases[0].GetProperty("input"), appRoot.FullName);
            var beta = RuntimeProfile.Parse(cases[1].GetProperty("input"), appRoot.FullName);
            Assert.NotEqual(alpha.SessionsDir, beta.SessionsDir);
            Assert.NotEqual(alpha.WorktreeManifestsDir, beta.WorktreeManifestsDir);
            Assert.Empty(appRoot.EnumerateFileSystemInfos());
        }
        finally
        {
            appRoot.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(".")]
    [InlineData("../app")]
    [InlineData("relative/app")]
    [InlineData("/invalid\0root")]
    public void Parse_InvalidApplicationRoot_RejectsWithoutFallback(string root)
    {
        using var fixtures = ReadFixtures();
        Assert.Throws<ArgumentException>(() => RuntimeProfile.Parse(
            fixtures.RootElement.GetProperty("cases")[0].GetProperty("input"), root));
    }

    [Fact]
    public void Parse_TooManyCredentialReferences_Rejects()
    {
        var refs = Enumerable.Range(0, 33).Select(i => new { provider = $"provider-{i}", reference = "fixture" });
        using var definition = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            version = 1, id = "alpha", runtimeId = "pi-0.85.1", credentialRefs = refs,
        }));
        Assert.Throws<ArgumentException>(() => RuntimeProfile.Parse(definition.RootElement, Path.GetTempPath()));
    }

    [Fact]
    public void Parse_WindowsRootedPath_RequiresDriveOrShare()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixtures = ReadFixtures();
        foreach (var root in new[] { @"\rooted-without-drive", "/rooted-without-drive", "C:relative" })
        {
            Assert.Throws<ArgumentException>(() => RuntimeProfile.Parse(
                fixtures.RootElement.GetProperty("cases")[0].GetProperty("input"), root));
        }
    }
}
