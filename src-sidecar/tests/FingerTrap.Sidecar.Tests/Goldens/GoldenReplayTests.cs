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

    [Fact]
    public async Task MalformedLineScenario_RecordsDeferredHelloBeforeShutdown()
    {
        var golden = GoldenStore.Read(Path.Combine(
            GoldenStore.OutputDataDir, GoldenStore.FileName("malformed-line-parse-error")));
        var inbound = golden.Where(record => record.IsInbound).ToArray();
        Assert.Equal(2, inbound.Length);
        var executable = FakePiShim.WriteShim(
            FakePiShim.Step("waitForLine", "this is not json"),
            FakePiShim.Step("writeLine", inbound[0].Line!),
            // Longer than the default legacy hello grace: readiness must be
            // observed, rather than inferred from a quiet interval.
            FakePiShim.Step("delayMs", 1000),
            FakePiShim.Step("writeLine", inbound[1].Line!),
            FakePiShim.Step("waitForEof", true),
            FakePiShim.Step("exit", 0));

        await using var host = new RecordScenarioHost(executable);
        await GoldenScenarios.ByName("malformed-line-parse-error")
            .RunAsync(host, TestContext.Current.CancellationToken);

        var records = host.Records.ToList();
        var helloIndex = records.FindIndex(record => record.IsInbound && record.Line == inbound[1].Line);
        var eofIndex = records.FindIndex(record => record.Ctl == "stdin_eof");
        Assert.True(helloIndex >= 0 && helloIndex < eofIndex,
            "the hello wire record must precede the scenario's stdin EOF");
    }

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
