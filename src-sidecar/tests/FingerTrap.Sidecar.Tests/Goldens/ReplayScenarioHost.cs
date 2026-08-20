using FingerTrap.Sidecar.PiRpc;

namespace FingerTrap.Sidecar.Tests.Goldens;

/// <summary>
/// Binds a golden scenario to FakePi speaking a committed golden — the
/// keyless CI lane (#139): each <c>spawn</c> segment of the golden is
/// compiled into a FakePi script (<see cref="FakePiScriptGenerator"/>),
/// and the scenario then drives <see cref="PiRpcClient"/> against it
/// exactly as it drove real pi at record time. Model/cwd levers are
/// no-ops here; the golden already embodies their effects.
/// </summary>
internal sealed class ReplayScenarioHost : ScenarioHost
{
    private readonly Queue<IReadOnlyList<GoldenRecord>> _segments;

    public ReplayScenarioHost(IReadOnlyList<GoldenRecord> golden)
    {
        _segments = SplitSegments(golden);
    }

    public override string CreateCwd(string token) => token;

    public override void DeleteCwd(string token)
    {
    }

    public override void EnqueueTurn(CannedTurn turn)
    {
    }

    public override void ReleaseModelHold()
    {
    }

    protected override Task<PiRpcClient> SpawnCoreAsync(ScenarioSpawn spawn)
    {
        if (_segments.Count == 0)
        {
            throw new InvalidOperationException("scenario spawned more children than the golden records");
        }

        var segment = _segments.Dequeue();
        var spawnCtl = segment[0];

        // The replay must ask for exactly the spawn the golden recorded —
        // argument drift here is a scenario/golden mismatch, not a wire
        // difference, so it fails loudly before any pipe traffic.
        var args = BuildArgs(spawn, fixture => $"@FIXTURE:{fixture}@");
        var cwdToken = spawn.CwdToken ?? MainCwdToken;
        if (!args.SequenceEqual(spawnCtl.Args ?? [], StringComparer.Ordinal)
            || !string.Equals(cwdToken, spawnCtl.Cwd, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "replay spawn does not match the golden's spawn record — " +
                $"asked [{string.Join(" ", args)}] in {cwdToken}, " +
                $"golden [{string.Join(" ", spawnCtl.Args ?? [])}] in {spawnCtl.Cwd}");
        }

        Append(GoldenRecord.Spawn(args, cwdToken));

        var steps = FakePiScriptGenerator.Generate(segment.Skip(1));
        var options = new PiRpcClientOptions
        {
            ExecutablePath = FakePiShim.WriteShim([.. steps]),
            Arguments = args,
            WireTap = OnWire,
        };

        return Task.FromResult(PiRpcClient.Start(ApplySpawnOverrides(options, spawn)));
    }

    private static Queue<IReadOnlyList<GoldenRecord>> SplitSegments(IReadOnlyList<GoldenRecord> golden)
    {
        var segments = new Queue<IReadOnlyList<GoldenRecord>>();
        List<GoldenRecord>? current = null;
        foreach (var record in golden)
        {
            if (string.Equals(record.Ctl, "spawn", StringComparison.Ordinal))
            {
                current = [];
                segments.Enqueue(current);
            }

            if (current is null)
            {
                throw new InvalidOperationException("golden does not start with a spawn record");
            }

            current.Add(record);
        }

        if (segments.Count == 0)
        {
            throw new InvalidOperationException("golden contains no spawn record");
        }

        return segments;
    }
}

/// <summary>
/// Compiles one golden spawn segment into FakePi steps. Outbound lines
/// become <c>waitForLine</c> barriers on the full line (the replay client
/// rebuilds byte-identical commands, so the line is its own needle);
/// inbound lines are emitted verbatim — tokens included, which is exactly
/// what lets the scenario echo a tokenized id back and still match the
/// golden. Control records map onto FakePi's lifecycle steps.
/// </summary>
internal static class FakePiScriptGenerator
{
    public static IReadOnlyList<string> Generate(IEnumerable<GoldenRecord> records)
    {
        var steps = new List<string>();
        var sawEof = false;
        foreach (var record in records)
        {
            if (record.Line is { } line && line.Contains("{{", StringComparison.Ordinal))
            {
                // FakePi's writeLine substitutes {{...}} templates; a golden
                // line containing that sequence would be corrupted silently.
                throw new InvalidOperationException($"golden line contains a FakePi template marker: {line}");
            }

            if (record.IsOutbound)
            {
                steps.Add(FakePiShim.Step("waitForLine", record.Line!));
            }
            else if (record.IsInbound)
            {
                steps.Add(FakePiShim.Step("writeLine", record.Line!));
            }
            else
            {
                switch (record.Ctl)
                {
                    case "stdin_eof":
                        sawEof = true;
                        steps.Add(FakePiShim.Step("waitForEof", true));
                        break;
                    case "exit" when record.Code == 0:
                        steps.Add(FakePiShim.Step("exit", 0));
                        break;
                    case "exit" when sawEof:
                        // A non-zero exit after EOF was signal-delivered at
                        // record time (SIGTERM 143 / SIGKILL 137): linger so
                        // the replay ladder kills FakePi the same way.
                        steps.Add(FakePiShim.Step("delayMs", 600_000));
                        break;
                    case "exit":
                        steps.Add(FakePiShim.Step("exit", record.Code!.Value));
                        break;
                    default:
                        throw new InvalidOperationException($"unknown golden control record: {record.Ctl}");
                }
            }
        }

        return steps;
    }
}
