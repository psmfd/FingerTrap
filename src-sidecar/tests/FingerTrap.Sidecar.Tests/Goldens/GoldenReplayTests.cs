using Xunit;

namespace FingerTrap.Sidecar.Tests.Goldens;

/// <summary>
/// The keyless CI replay lane (#139): every PR, each committed golden is
/// compiled into a FakePi script and its scenario re-driven through
/// <see cref="PiRpc.PiRpcClient"/>, asserting the supervisor produces the
/// same transcript against real-pi wire shapes. The comparison runs both
/// transcripts through the same normalizer pass (idempotent on the
/// already-tokenized golden), so record and replay are judged by one rule
/// set. An event rename in pi shows up here as a transcript mismatch the
/// moment the goldens are re-recorded — and a supervisor regression shows
/// up against the existing goldens immediately.
/// </summary>
public sealed class GoldenReplayTests
{
    public static TheoryData<string> ScenarioNames => GoldenScenarios.Names;

    [Theory]
    [MemberData(nameof(ScenarioNames))]
    public async Task Replay_Golden_ReproducesTranscript(string name)
    {
        var scenario = GoldenScenarios.ByName(name);
        var goldenPath = Path.Combine(GoldenStore.OutputDataDir, GoldenStore.FileName(name));
        Assert.True(
            File.Exists(goldenPath),
            $"missing golden '{goldenPath}' — record it with FT_RECORD_GOLDENS=1 (see CONTRIBUTING)");

        var golden = GoldenStore.Read(goldenPath);
        Assert.SkipWhen(
            OperatingSystem.IsWindows() && (scenario.UnixOnly || HasSignalExit(golden)),
            "golden ends in a Unix signal exit; Windows has no SIGTERM semantics");

        await using var host = new ReplayScenarioHost(golden);
        await scenario.RunAsync(host, TestContext.Current.CancellationToken);

        var replayed = new GoldenNormalizer([]).Normalize(host.Records);
        Assert.Equal(
            string.Join("\n", golden.Select(GoldenStore.Serialize)),
            string.Join("\n", replayed.Select(GoldenStore.Serialize)));
    }

    private static bool HasSignalExit(IReadOnlyList<GoldenRecord> golden) =>
        golden.Any(r => string.Equals(r.Ctl, "exit", StringComparison.Ordinal) && r.Code is 143 or 137);
}
