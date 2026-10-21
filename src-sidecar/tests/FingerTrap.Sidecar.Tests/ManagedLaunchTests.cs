using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Text.Json;
using FingerTrap.Sidecar.Processes;
using FingerTrap.Sidecar.Runtime;
using Xunit;

namespace FingerTrap.Sidecar.Tests;

public sealed class ManagedLaunchTests
{
    [Fact]
    public void Resolve_IndependentProfiles_SnapshotsStateAndKeepsParentUnchanged()
    {
        using var fixture = new ManagedLaunchFixture();
        var home = Environment.GetEnvironmentVariable("HOME");
        var cwd = Environment.CurrentDirectory;
        var a = fixture.Resolve();
        var b = fixture.Resolve("beta", rpc: false);
        Assert.Equal(ChildEnvironmentMode.Replace, a.EnvironmentMode);
        Assert.NotEqual(a.Environment["HOME"], b.Environment["HOME"]);
        Assert.Equal(fixture.Cwd, a.WorkingDirectory);
        Assert.Equal(fixture.Executable, a.Executable);
        Assert.Contains("--no-approve", a.Arguments);
        Assert.Contains("--offline", a.Arguments);
        Assert.Contains("--mode", a.Arguments);
        Assert.DoesNotContain("--mode", b.Arguments);
        Assert.Equal("xterm-256color", b.Environment["TERM"]);
        Assert.DoesNotContain("TERM", a.Environment.Keys);
        Assert.DoesNotContain("PATH", a.Environment.Keys);
        Assert.Equal(home, Environment.GetEnvironmentVariable("HOME"));
        Assert.Equal(cwd, Environment.CurrentDirectory);
    }

    [Theory]
    [InlineData("runtimeId", "other")]
    [InlineData("forkRevision", "wrong")]
    [InlineData("platform", "other")]
    [InlineData("architecture", "other")]
    [InlineData("executable", "absent")]
    [InlineData("unknown", "value")]
    public void Resolve_InvalidDescriptor_Rejects(string key, string value)
    {
        using var fixture = new ManagedLaunchFixture();
        fixture.Descriptor[key] = value;
        fixture.SaveDescriptor();
        Assert.Throws<InvalidDataException>(() => fixture.Resolve());
    }

    [Theory]
    [InlineData("tamper")]
    [InlineData("missing")]
    [InlineData("unlisted")]
    [InlineData("digest")]
    [InlineData("extension")]
    [InlineData("duplicate")]
    [InlineData("escape")]
    public void Resolve_UnverifiedClosure_Rejects(string change)
    {
        using var fixture = new ManagedLaunchFixture();
        var entries = fixture.Descriptor["files"]!.AsArray();
        switch (change)
        {
            case "tamper": File.AppendAllText(fixture.Executable, "changed"); break;
            case "missing": File.Delete(fixture.Executable); break;
            case "unlisted": File.WriteAllText(Path.Combine(fixture.ArtifactRoot, "extra.js"), "bad"); break;
            case "digest": File.AppendAllText(fixture.DescriptorPath, " "); break;
            case "extension":
                fixture.Descriptor["extensions"]!.AsArray().Add("missing.ts");
                fixture.SaveDescriptor();
                break;
            case "duplicate":
                entries.Add(entries[0]!.DeepClone());
                fixture.SaveDescriptor();
                break;
            case "escape":
                entries[0]!["path"] = "../outside";
                fixture.SaveDescriptor();
                break;
        }

        if (change == "missing") Assert.ThrowsAny<IOException>(() => fixture.Resolve());
        else Assert.Throws<InvalidDataException>(() => fixture.Resolve());
        Assert.Empty(Directory.EnumerateFileSystemEntries(fixture.AppRoot));
    }

    [Fact]
    public void Resolve_ProjectMigrationAndProfileStartupInputs_Reject()
    {
        using var fixture = new ManagedLaunchFixture();
        var projectPi = Directory.CreateDirectory(Path.Combine(fixture.Cwd, ".pi"));
        Assert.Throws<InvalidOperationException>(() => fixture.Resolve());
        projectPi.Delete();
        var context = fixture.Resolve();
        File.WriteAllText(Path.Combine(context.Environment["PI_CODING_AGENT_DIR"], "auth.json"), "{}");
        Assert.Throws<InvalidOperationException>(() => fixture.Resolve());
    }

    [Fact]
    public void Resolve_ArtifactLink_Rejects()
    {
        using var fixture = new ManagedLaunchFixture();
        var outside = Path.Combine(fixture.Root, "outside");
        File.Move(fixture.Executable, outside);
        File.CreateSymbolicLink(fixture.Executable, outside);
        Assert.ThrowsAny<IOException>(() => fixture.Resolve());
    }

    [Fact]
    public void Resolve_ProfileDirectoryLink_Rejects()
    {
        using var fixture = new ManagedLaunchFixture();
        Directory.CreateDirectory(Path.Combine(fixture.AppRoot, "profiles"));
        Directory.CreateSymbolicLink(Path.Combine(fixture.AppRoot, "profiles", "alpha"), fixture.Cwd);
        Assert.ThrowsAny<IOException>(() => fixture.Resolve());
        Assert.Empty(Directory.EnumerateFileSystemEntries(fixture.Cwd));
    }

    [Theory]
    [InlineData("path")]
    [InlineData("environment")]
    [InlineData("resume")]
    [InlineData("identity")]
    public void Resolve_RequestCannotChangeHostPolicy(string request)
    {
        using var fixture = new ManagedLaunchFixture();
        var resolver = new PiLaunchContextResolver(fixture.Selection("alpha"));
        Assert.Throws<InvalidOperationException>(() => resolver.Resolve(
            ManagedLaunchFixture.Settings(request == "identity" ? "beta" : "alpha"), true,
            request == "path" ? fixture.Executable : null, fixture.Cwd,
            request == "resume" ? "/other/session" : null,
            request == "environment" ? new Dictionary<string, string>() : null, CancellationToken.None));
    }

    [Fact]
    public void Resolve_CanceledVerification_DoesNotCreateProfile()
    {
        using var fixture = new ManagedLaunchFixture();
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => new PiLaunchContextResolver(fixture.Selection("alpha"))
            .Resolve(ManagedLaunchFixture.Settings("alpha"), true, null, fixture.Cwd, null, null, canceled.Token));
        Assert.Empty(Directory.EnumerateFileSystemEntries(fixture.AppRoot));
    }

    [Fact]
    public void Resolve_ToolPathsAreExplicitAndSnapshotIsImmutable()
    {
        using var fixture = new ManagedLaunchFixture();
        var directories = new[] { fixture.ArtifactRoot };
        var resolver = new PiLaunchContextResolver(fixture.Selection("alpha") with { ToolDirectories = directories });
        directories[0] = "relative-untrusted";
        var context = resolver.Resolve(ManagedLaunchFixture.Settings("alpha"), true, null,
            fixture.Cwd, null, null, CancellationToken.None);
        Assert.Equal(fixture.ArtifactRoot, context.Environment["PATH"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Resolve_DuplicateFieldsOrOversizedDescriptor_Rejects(bool oversized)
    {
        using var fixture = new ManagedLaunchFixture();
        var text = oversized ? new string(' ', RuntimeArtifact.MaxDescriptorBytes + 1)
            : fixture.Descriptor.ToJsonString().Replace("\"version\":1", "\"version\":1,\"version\":1", StringComparison.Ordinal);
        File.WriteAllText(fixture.DescriptorPath, text);
        var digest = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(fixture.DescriptorPath)));
        var selection = fixture.Selection("alpha") with { DescriptorSha256 = digest };
        Assert.Throws<InvalidDataException>(() => new PiLaunchContextResolver(selection).Resolve(
            ManagedLaunchFixture.Settings("alpha"), true, null, fixture.Cwd, null, null, CancellationToken.None));
    }

    [Fact]
    public void Resolve_UnresolvedCredentialReference_RejectsWithoutLoadingAuth()
    {
        using var fixture = new ManagedLaunchFixture();
        using var definition = JsonDocument.Parse("""
            {"version":1,"id":"alpha","runtimeId":"pi-0.85.1-psmfd.1",
            "credentialRefs":[{"provider":"synthetic","reference":"fixture"}]}
            """);
        var selection = fixture.Selection("alpha") with { Profile = RuntimeProfile.Parse(definition.RootElement, fixture.AppRoot) };
        Assert.Throws<InvalidOperationException>(() => new PiLaunchContextResolver(selection).Resolve(
            ManagedLaunchFixture.Settings("alpha"), true, null, fixture.Cwd, null, null, CancellationToken.None));
        Assert.Empty(Directory.EnumerateFileSystemEntries(fixture.AppRoot));
    }
}
