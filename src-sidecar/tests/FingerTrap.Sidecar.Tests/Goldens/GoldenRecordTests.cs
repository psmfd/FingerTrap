using Xunit;

namespace FingerTrap.Sidecar.Tests.Goldens;

/// <summary>
/// The opt-in recorder lane (#139): set <c>FT_RECORD_GOLDENS=1</c> with a
/// real pi on PATH to (re-)record the committed goldens against the
/// current pin. Self-skips otherwise — env-gated, real-tool, keyless-safe,
/// the <c>scripts/smoke-pty.py</c> shape — so CI never runs it. Each
/// scenario records TWICE from scratch and the normalized transcripts must
/// match byte-for-byte before anything is written: the determinism the
/// pin-bump diff ritual depends on is proven at record time, not assumed.
/// The write lands in the source tree, so a re-record shows up as a
/// working-tree diff — that diff IS the drift report.
/// </summary>
public sealed class GoldenRecordTests
{
    public static TheoryData<string> ScenarioNames => GoldenScenarios.Names;

    [Theory]
    [MemberData(nameof(ScenarioNames))]
    public async Task Record_Scenario_WritesDeterministicGolden(string name)
    {
        Assert.SkipWhen(
            Environment.GetEnvironmentVariable("FT_RECORD_GOLDENS") != "1",
            "recording is opt-in: set FT_RECORD_GOLDENS=1 with a real pi on PATH");
        Assert.SkipWhen(
            OperatingSystem.IsWindows(),
            "the recorder drives real pi through the Unix shutdown ladder; record on macOS/Linux");

        var piExecutable = RecordScenarioHost.ResolvePiOnPath();
        Assert.SkipWhen(piExecutable is null, "no pi executable on PATH");

        var scenario = GoldenScenarios.ByName(name);
        var first = await RecordOnceAsync(scenario, piExecutable!);
        var second = await RecordOnceAsync(scenario, piExecutable!);
        Assert.Equal(
            string.Join("\n", first.Select(GoldenStore.Serialize)),
            string.Join("\n", second.Select(GoldenStore.Serialize)));

        GoldenStore.Write(
            Path.Combine(GoldenStore.SourceDataDir(), GoldenStore.FileName(scenario.Name)),
            first);
    }

    private static async Task<IReadOnlyList<GoldenRecord>> RecordOnceAsync(
        GoldenScenario scenario, string piExecutable)
    {
        await using var host = new RecordScenarioHost(piExecutable);
        await scenario.RunAsync(host, TestContext.Current.CancellationToken);
        return new GoldenNormalizer(host.KnownValues()).Normalize(host.Records);
    }
}
