using System.Diagnostics;
using System.Text.Json;
using FingerTrap.Sidecar.Abstractions;
using FingerTrap.Sidecar.PiRpc;
using FingerTrap.Sidecar.Processes;
using FingerTrap.Sidecar.Pty;
using FingerTrap.Sidecar.Runtime;
using Xunit;

namespace FingerTrap.Sidecar.Tests;

public sealed class LaunchProcessTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Replacement_RealChild_EmptyOrExplicitEnvironment(bool pty, bool populated)
    {
        using var fixture = new ManagedLaunchFixture(includeRuntime: true);
        var output = Path.Combine(fixture.Root, "observation.json");
        var previous = Environment.GetEnvironmentVariable("FT_ISOLATION_SENTINEL");
        Environment.SetEnvironmentVariable("FT_ISOLATION_SENTINEL", "synthetic-parent-only");
        try
        {
            var environment = new Dictionary<string, string>();
            if (populated)
            {
                environment["FT_CHILD_EMPTY"] = "";
                environment["FT_CHILD_UNICODE"] = "résumé-日本";
            }

            if (pty)
            {
                var options = new Porta.Pty.PtyOptions
                {
                    App = fixture.Executable, CommandLine = ["--observe", output], Cwd = fixture.Cwd,
                    Rows = 24, Cols = 80, EnvironmentMode = Porta.Pty.EnvironmentMode.Replace,
                    Environment = environment,
                };
                using var connection = await ChildSpawnCoordinator.RunAsync(
                    () => Porta.Pty.PtyProvider.SpawnAsync(options, Token), Token);
                try
                {
                    using var observed = await ObserveAsync(output);
                    AssertEnvironment(observed.RootElement, populated);
                    Assert.Same(environment, options.Environment);
                    Assert.Equal(populated ? 2 : 0, options.Environment.Count);
                }
                finally
                {
                    connection.Kill();
                    Assert.True(connection.WaitForExit(5000));
                }
            }
            else
            {
                await using var client = PiRpcClient.Start(new PiRpcClientOptions
                {
                    ExecutablePath = fixture.Executable, Arguments = ["--observe", output],
                    WorkingDirectory = fixture.Cwd, EnvironmentMode = ChildEnvironmentMode.Replace,
                    EnvironmentOverrides = environment,
                });
                using var observed = await ObserveAsync(output);
                AssertEnvironment(observed.RootElement, populated);
                await client.Exited.WaitAsync(TimeSpan.FromSeconds(5), Token);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("FT_ISOLATION_SENTINEL", previous);
        }
    }

    [Fact]
    public async Task SharedServices_ConcurrentProfilesAndGrandchildren_StayIndependent()
    {
        using var fixture = new ManagedLaunchFixture(includeRuntime: true);
        var registry = new RpcChildRegistry(Path.Combine(fixture.Root, "children.json"));
        var a = new PiLaunchContextResolver(fixture.Selection("alpha"));
        var b = new PiLaunchContextResolver(fixture.Selection("beta"));
        await using var rpcA = new RpcPaneService(ManagedLaunchFixture.Settings("alpha"), registry, a);
        await using var rpcB = new RpcPaneService(ManagedLaunchFixture.Settings("beta"), registry, b);
        await using var ptyA = new PtyService(ManagedLaunchFixture.Settings("alpha"), a);
        await using var ptyB = new PtyService(ManagedLaunchFixture.Settings("beta"), b);
        var previous = Environment.GetEnvironmentVariable("FT_ISOLATION_SENTINEL");
        Environment.SetEnvironmentVariable("FT_ISOLATION_SENTINEL", "synthetic-parent-only");
        try
        {
            var request = new RpcPaneSpawnOptions(fixture.Cwd, null, null);
            var ptyRequest = new PtySpawnOptions(null, fixture.Cwd, 80, 24, null, PaneKind.Pi);
            await Task.WhenAll(rpcA.SpawnAsync("rpc-a", request, Token), rpcB.SpawnAsync("rpc-b", request, Token),
                ptyA.SpawnAsync("pty-a", ptyRequest, Token), ptyB.SpawnAsync("pty-b", ptyRequest, Token));
            var pids = new Dictionary<string, int>();
            foreach (var id in new[] { "alpha", "beta" })
            {
                foreach (var transport in new[] { "rpc", "pty" })
                {
                    var path = Path.Combine(fixture.Profile(id).AgentDir, $"launch-{transport}.json");
                    foreach (var suffix in new[] { "", ".child" })
                    {
                        using var observed = await ObserveAsync(path + suffix);
                        var value = observed.RootElement;
                        Assert.Equal(fixture.Cwd, value.GetProperty("cwd").GetString());
                        Assert.Equal(id, value.GetProperty("values").GetProperty("FINGERTRAP_PROFILE_ID").GetString());
                        Assert.Equal(fixture.Profile(id).HomeDir, value.GetProperty("values").GetProperty("HOME").GetString());
                        Assert.Null(value.GetProperty("values").GetProperty("FT_ISOLATION_SENTINEL").GetString());
                        Assert.DoesNotContain("NODE_OPTIONS", value.GetProperty("keys").EnumerateArray().Select(x => x.GetString()));
                        if (suffix.Length == 0) pids[id + transport] = value.GetProperty("pid").GetInt32();
                    }
                }
            }

            await rpcA.KillAsync("rpc-a", Token);
            using var sibling = Process.GetProcessById(pids["betarpc"]);
            Assert.False(sibling.HasExited);
            ptyA.Close("pty-a");
            using var ptySibling = Process.GetProcessById(pids["betapty"]);
            Assert.False(ptySibling.HasExited);
        }
        finally
        {
            Environment.SetEnvironmentVariable("FT_ISOLATION_SENTINEL", previous);
        }
    }

    [Theory]
    [InlineData("missing-executable")]
    [InlineData("missing-cwd")]
    public async Task NativeReplacement_ExecFailure_ReportsWithoutLiveChild(string failure)
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix native exec-status ABI; Windows uses CreateProcess errors");
        using var fixture = new ManagedLaunchFixture();
        var options = new Porta.Pty.PtyOptions
        {
            App = failure == "missing-executable" ? Path.Combine(fixture.Root, "missing") : fixture.Executable,
            Cwd = failure == "missing-cwd" ? Path.Combine(fixture.Root, "missing") : fixture.Cwd,
            Rows = 24, Cols = 80, EnvironmentMode = Porta.Pty.EnvironmentMode.Replace,
        };
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ChildSpawnCoordinator.RunAsync(() => Porta.Pty.PtyProvider.SpawnAsync(options, Token), Token));
        Assert.Contains("ENOENT", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("", "value")]
    [InlineData("BAD=KEY", "value")]
    [InlineData("BAD\0KEY", "value")]
    [InlineData("KEY", "bad\0value")]
    public async Task InvalidEnvironment_RejectsAtBothTransports(string key, string value)
    {
        using var fixture = new ManagedLaunchFixture();
        var environment = new Dictionary<string, string> { [key] = value };
        Assert.Throws<ArgumentException>(() => PiRpcClient.Start(new PiRpcClientOptions
        {
            ExecutablePath = fixture.Executable, EnvironmentMode = ChildEnvironmentMode.Replace,
            EnvironmentOverrides = environment,
        }));
        await Assert.ThrowsAsync<ArgumentException>(() => Porta.Pty.PtyProvider.SpawnAsync(new()
        {
            App = fixture.Executable, Cwd = fixture.Cwd, EnvironmentMode = Porta.Pty.EnvironmentMode.Replace,
            Environment = environment,
        }, Token));
    }

    [Fact]
    public async Task ProductionServices_ManagedSelection_StillFailsClosed()
    {
        using var fixture = new ManagedLaunchFixture();
        await using var rpc = new RpcPaneService(ManagedLaunchFixture.Settings("alpha"),
            new RpcChildRegistry(Path.Combine(fixture.Root, "children.json")));
        await using var pty = new PtyService(ManagedLaunchFixture.Settings("alpha"));
        await Assert.ThrowsAsync<PiNotFoundException>(() => rpc.SpawnAsync("rpc",
            new(fixture.Cwd, null, null, fixture.Executable), Token));
        await Assert.ThrowsAsync<PiNotFoundException>(() => pty.SpawnAsync("pty",
            new(fixture.Executable, fixture.Cwd, 80, 24, null, PaneKind.Pi), Token));
        Assert.Empty(Directory.EnumerateFileSystemEntries(fixture.AppRoot));
    }

    private static void AssertEnvironment(JsonElement observation, bool populated)
    {
        var values = observation.GetProperty("values");
        Assert.Null(values.GetProperty("FT_ISOLATION_SENTINEL").GetString());
        Assert.Null(values.GetProperty("HOME").GetString());
        Assert.Equal(populated ? "résumé-日本" : null, values.GetProperty("FT_CHILD_UNICODE").GetString());
        var keys = observation.GetProperty("keys").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.DoesNotContain("PATH", keys);
        Assert.Equal(populated, keys.Contains("FT_CHILD_EMPTY", StringComparer.Ordinal));
        if (populated) Assert.Equal("", values.GetProperty("FT_CHILD_EMPTY").GetString());
    }

    [Fact]
    public async Task InheritedPty_PreservesParentAndAppliesOverrides()
    {
        using var fixture = new ManagedLaunchFixture(includeRuntime: true);
        var path = Path.Combine(fixture.Root, "inherited.json");
        var previous = Environment.GetEnvironmentVariable("FT_ISOLATION_SENTINEL");
        Environment.SetEnvironmentVariable("FT_ISOLATION_SENTINEL", "synthetic-parent-only");
        try
        {
            var supplied = new Dictionary<string, string> { ["FT_CHILD_UNICODE"] = "override" };
            using var child = await ChildSpawnCoordinator.RunAsync(() => Porta.Pty.PtyProvider.SpawnAsync(new()
            {
                App = fixture.Executable, Cwd = fixture.Cwd, CommandLine = ["--observe", path],
                Rows = 24, Cols = 80, Environment = supplied,
            }, Token), Token);
            try
            {
                using var observed = await ObserveAsync(path);
                Assert.Equal("synthetic-parent-only", observed.RootElement.GetProperty("values")
                    .GetProperty("FT_ISOLATION_SENTINEL").GetString());
                Assert.Equal("override", observed.RootElement.GetProperty("values").GetProperty("FT_CHILD_UNICODE").GetString());
                Assert.Single(supplied);
            }
            finally
            {
                child.Kill();
                Assert.True(child.WaitForExit(5000));
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("FT_ISOLATION_SENTINEL", previous);
        }
    }

    [Fact]
    public async Task EnvironmentCaseCollisions_FollowPlatformRules()
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal) { ["PATH"] = "one", ["Path"] = "two" };
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(2, ChildEnvironment.Snapshot(values).Count);
            return;
        }

        Assert.Throws<ArgumentException>(() => ChildEnvironment.Snapshot(values));
        using var fixture = new ManagedLaunchFixture();
        await Assert.ThrowsAsync<ArgumentException>(() => Porta.Pty.PtyProvider.SpawnAsync(new()
        {
            App = fixture.Executable, Cwd = fixture.Cwd, Environment = values,
            EnvironmentMode = Porta.Pty.EnvironmentMode.Replace,
        }, Token));
    }

    [Fact]
    public async Task CanceledPtySpawn_RejectsBeforeProcessCreation()
    {
        using var fixture = new ManagedLaunchFixture();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Porta.Pty.PtyProvider.SpawnAsync(new()
        {
            App = fixture.Executable, Cwd = fixture.Cwd, EnvironmentMode = Porta.Pty.EnvironmentMode.Replace,
        }, cancellation.Token));
    }

    private static async Task<JsonDocument> ObserveAsync(string path)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Token);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        while (!File.Exists(path)) await Task.Delay(20, deadline.Token);
        return JsonDocument.Parse(await File.ReadAllBytesAsync(path, deadline.Token));
    }
}
