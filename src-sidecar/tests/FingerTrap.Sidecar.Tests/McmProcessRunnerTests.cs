using System.Diagnostics;
using System.Text.Json;
using FingerTrap.Sidecar.Vm;
using Xunit;

namespace FingerTrap.Sidecar.Tests;

public sealed class McmProcessRunnerTests
{
    private static readonly string?[] ExpectedArguments = ["status", "--name", "devbox", "--json"];
    [Fact]
    public async Task Run_ClosesStdinAndPassesOnlyFixedArgumentsAndEnvironment()
    {
        if (OperatingSystem.IsWindows()) return;
        using var fake = FakeMcmShim.Create(new { reportInvocation = true });
        var runner = new McmProcessRunner();
        var request = Request(fake.ExecutablePath, 32_768, 32_768);

        var result = await runner.RunAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(McmProcessOutcome.Exited, result.Outcome);
        using var report = JsonDocument.Parse(result.Stdout);
        Assert.Equal(ExpectedArguments,
            report.RootElement.GetProperty("args").EnumerateArray().Select(static item => item.GetString()));
        Assert.True(report.RootElement.GetProperty("stdinEof").GetBoolean());
        var names = report.RootElement.GetProperty("environment").EnumerateArray()
            .Select(static item => item.GetString()).ToArray();
        var expectedNames = McmProcessContract.Environment(AppContext.BaseDirectory)
            .Keys.Order(StringComparer.Ordinal).Cast<string?>().ToArray();
        Assert.Equal(expectedNames, names);
        Assert.Equal(AppContext.BaseDirectory, report.RootElement.GetProperty("home").GetString());
        Assert.Equal("/usr/bin:/bin", report.RootElement.GetProperty("path").GetString());
        Assert.Equal("C", report.RootElement.GetProperty("locale").GetString());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Run_IndependentOutputCeiling_TerminatesChild(bool stdout)
    {
        if (OperatingSystem.IsWindows()) return;
        using var fake = FakeMcmShim.Create(stdout
            ? new { stdoutBytes = 65_536, delayMs = 30_000 }
            : new { stderrBytes = 65_536, delayMs = 30_000 });
        var result = await new McmProcessRunner().RunAsync(
            Request(fake.ExecutablePath, stdout ? 1024 : 131_072, stdout ? 131_072 : 1024),
            TestContext.Current.CancellationToken);

        Assert.Equal(stdout ? McmProcessOutcome.StdoutOverflow : McmProcessOutcome.StderrOverflow, result.Outcome);
        Assert.True(result.CleanupConfirmed);
    }

    [Fact]
    public async Task Run_QuickExitAfterOversizedOutput_StillReportsOverflow()
    {
        if (OperatingSystem.IsWindows()) return;
        using var fake = FakeMcmShim.Create(new { stdoutBytes = 65_536 });
        var result = await new McmProcessRunner().RunAsync(
            Request(fake.ExecutablePath, 1024, 32_768),
            TestContext.Current.CancellationToken);

        Assert.Equal(McmProcessOutcome.StdoutOverflow, result.Outcome);
        Assert.True(result.CleanupConfirmed);
    }

    [Fact]
    public async Task Run_CallerCancellation_IsDistinctAndCleansUp()
    {
        if (OperatingSystem.IsWindows()) return;
        using var fake = FakeMcmShim.Create(new { delayMs = 30_000 });
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
        var result = await new McmProcessRunner().RunAsync(
            Request(fake.ExecutablePath, 32_768, 32_768), cancellation.Token);

        Assert.Equal(McmProcessOutcome.Canceled, result.Outcome);
        Assert.True(result.CleanupConfirmed);
    }

    [Fact]
    public async Task Run_NonzeroExit_RemainsAnExitedResultWithExactCode()
    {
        if (OperatingSystem.IsWindows()) return;
        using var fake = FakeMcmShim.Create(new { exitCode = 2 });
        var result = await new McmProcessRunner().RunAsync(
            Request(fake.ExecutablePath, 32_768, 32_768), TestContext.Current.CancellationToken);

        Assert.Equal(McmProcessOutcome.Exited, result.Outcome);
        Assert.Equal(2, result.ExitCode);
    }

    [Fact]
    public async Task Run_SimultaneousOutput_IsDrainedWithoutDeadlock()
    {
        if (OperatingSystem.IsWindows()) return;
        using var fake = FakeMcmShim.Create(new { stdoutBytes = 32_000, stderrBytes = 32_000 });
        var result = await new McmProcessRunner().RunAsync(
            Request(fake.ExecutablePath, 32_768, 32_768), TestContext.Current.CancellationToken);

        Assert.Equal(McmProcessOutcome.Exited, result.Outcome);
        Assert.Equal(32_000, result.Stdout.Length);
        Assert.Equal(32_000, result.Stderr.Length);
    }

    [Fact]
    public async Task Run_InvalidLimitsAreRejectedBeforeSpawn()
    {
        if (OperatingSystem.IsWindows()) return;
        var request = Request("/does/not/exist", 32_768, 32_768) with
        {
            Limits = new McmProcessLimits(TimeSpan.Zero, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), 32_768, 32_768),
        };
        var result = await new McmProcessRunner().RunAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(McmProcessOutcome.InvalidRequest, result.Outcome);
    }

    [Fact]
    public async Task Run_MissingExecutableIsSpawnFailure()
    {
        if (OperatingSystem.IsWindows()) return;
        var result = await new McmProcessRunner().RunAsync(
            Request("/does/not/exist", 32_768, 32_768), TestContext.Current.CancellationToken);
        Assert.Equal(McmProcessOutcome.SpawnFailure, result.Outcome);
    }

    [Fact]
    public async Task Run_SignalExitPreservesSignalNumber()
    {
        if (OperatingSystem.IsWindows()) return;
        using var fake = FakeMcmShim.Create(new { signal = 9 });
        var result = await new McmProcessRunner().RunAsync(
            Request(fake.ExecutablePath, 32_768, 32_768), TestContext.Current.CancellationToken);

        Assert.Equal(McmProcessOutcome.Signaled, result.Outcome);
        Assert.Equal(9, result.Signal);
    }

    [Fact]
    public async Task Run_ConcurrentChildren_DoNotRetainEachOthersOutputDescriptors()
    {
        if (OperatingSystem.IsWindows()) return;
        var fakes = Enumerable.Range(0, 8)
            .Select(_ => FakeMcmShim.Create(new { reportInvocation = true }))
            .ToArray();
        try
        {
            var runner = new McmProcessRunner();
            var results = await Task.WhenAll(fakes.Select(fake =>
                runner.RunAsync(Request(fake.ExecutablePath, 32_768, 32_768), TestContext.Current.CancellationToken)));
            Assert.All(results, static result => Assert.Equal(McmProcessOutcome.Exited, result.Outcome));
        }
        finally
        {
            foreach (var fake in fakes) fake.Dispose();
        }
    }

    [Fact]
    public async Task Run_Timeout_KillsGrandchildProcessGroupBeforeReturn()
    {
        if (OperatingSystem.IsWindows()) return;
        var pidFile = Path.Combine(AppContext.BaseDirectory, $"grandchild-{Guid.NewGuid():N}.pid");
        using var fake = FakeMcmShim.Create(new { spawnGrandchild = true, pidFile, ignoreSigterm = true, delayMs = 30_000 });
        var request = Request(fake.ExecutablePath, 32_768, 32_768) with
        {
            Limits = new McmProcessLimits(TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(2), 32_768, 32_768),
        };

        var result = await new McmProcessRunner().RunAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(McmProcessOutcome.TimedOut, result.Outcome);
        Assert.True(result.CleanupConfirmed);
        Assert.True(File.Exists(pidFile), "the fake must prove that a real grandchild was launched");
        var pid = int.Parse(
            await File.ReadAllTextAsync(pidFile, TestContext.Current.CancellationToken),
            System.Globalization.CultureInfo.InvariantCulture);
        Assert.False(IsAlive(pid));
        File.Delete(pidFile);
    }

    private static McmProcessRequest Request(string executable, int stdoutLimit, int stderrLimit) => new(
        executable,
        ["status", "--name", "devbox", "--json"],
        McmProcessContract.Environment(AppContext.BaseDirectory),
        new McmProcessLimits(TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(2), stdoutLimit, stderrLimit));

    private static bool IsAlive(int pid)
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
}
