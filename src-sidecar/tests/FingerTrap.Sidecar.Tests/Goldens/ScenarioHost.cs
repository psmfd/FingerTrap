using FingerTrap.Sidecar.PiRpc;

namespace FingerTrap.Sidecar.Tests.Goldens;

/// <summary>Per-spawn knobs a scenario passes to <see cref="ScenarioHost.SpawnAsync"/>.</summary>
internal sealed record ScenarioSpawn
{
    public IReadOnlyList<string> ExtraArgs { get; init; } = [];

    /// <summary>Cwd token from <see cref="ScenarioHost.CreateCwd"/>; default main cwd when null.</summary>
    public string? CwdToken { get; init; }

    /// <summary>
    /// File name of a fixture extension under <c>Goldens/fixtures/</c> to
    /// load via <c>--extension</c> (e.g. <c>dialog-fixture.ts</c>).
    /// </summary>
    public string? ExtensionFixture { get; init; }

    public TimeSpan? EofGrace { get; init; }

    public TimeSpan? RequestTimeout { get; init; }
}

/// <summary>
/// The single seam a golden scenario runs against (#139): the recorder
/// binds it to a real <c>pi --mode rpc</c> child (temp HOME, canned model
/// backend) and the replay lane binds it to FakePi speaking the committed
/// golden — one scenario definition drives both, so the two lanes cannot
/// drift apart. The host accumulates the transcript: control records from
/// the lifecycle calls here, wire records from
/// <see cref="PiRpcClientOptions.WireTap"/>.
/// </summary>
internal abstract class ScenarioHost : IAsyncDisposable
{
    public const string MainCwdToken = "@CWD:main@";

    public static readonly TimeSpan EventBudget = TimeSpan.FromSeconds(60);

    private readonly Lock _recordsLock = new();
    private readonly List<GoldenRecord> _records = [];
    private PiRpcClient? _client;

    public PiRpcClient Client =>
        _client ?? throw new InvalidOperationException("no live child — SpawnAsync first");

    public IReadOnlyList<GoldenRecord> Records
    {
        get
        {
            lock (_recordsLock)
            {
                return [.. _records];
            }
        }
    }

    /// <summary>Creates a scenario working directory and returns its token.</summary>
    public abstract string CreateCwd(string token);

    /// <summary>Deletes a scenario cwd (the reaped-worktree simulation). Replay: no-op.</summary>
    public abstract void DeleteCwd(string token);

    /// <summary>Scripts the next model turn. Replay: no-op (the golden already holds the result).</summary>
    public abstract void EnqueueTurn(CannedTurn turn);

    /// <summary>Releases a <see cref="CannedTurn.HoldAfter"/> hold. Replay: no-op.</summary>
    public abstract void ReleaseModelHold();

    public async Task<PiRpcClient> SpawnAsync(ScenarioSpawn spawn)
    {
        if (_client is not null)
        {
            throw new InvalidOperationException("previous child still live — shut it down first");
        }

        _client = await SpawnCoreAsync(spawn).ConfigureAwait(false);
        return _client;
    }

    protected abstract Task<PiRpcClient> SpawnCoreAsync(ScenarioSpawn spawn);

    /// <summary>
    /// Drives the shutdown ladder, recording the <c>stdin_eof</c> trigger
    /// and the observed exit code, and returns that code.
    /// </summary>
    public async Task<int> ShutdownChildAsync(CancellationToken cancellationToken)
    {
        var client = Client;
        Append(GoldenRecord.StdinEof());
        await client.ShutdownAsync(cancellationToken).ConfigureAwait(false);
        var fault = await client.Exited.WaitAsync(EventBudget, cancellationToken).ConfigureAwait(false);
        Append(GoldenRecord.Exit(fault.ExitCode));
        await client.DisposeAsync().ConfigureAwait(false);
        _client = null;
        return fault.ExitCode;
    }

    /// <summary>
    /// For scenarios where the child exits on its own (e.g. resuming a
    /// session whose cwd is gone): waits for the exit and records it
    /// without touching stdin.
    /// </summary>
    public async Task<int> AwaitChildExitAsync(CancellationToken cancellationToken)
    {
        var client = Client;
        var fault = await client.Exited.WaitAsync(EventBudget, cancellationToken).ConfigureAwait(false);
        Append(GoldenRecord.Exit(fault.ExitCode));
        await client.DisposeAsync().ConfigureAwait(false);
        _client = null;
        return fault.ExitCode;
    }

    /// <summary>
    /// Consumes events until one of <paramref name="type"/> arrives.
    /// Sequential consumption from the client's single buffered channel —
    /// no subscription race, matching the contract's listener-before-prompt
    /// guidance.
    /// </summary>
    public async Task<PiRpcEvent> NextEventAsync(string type, CancellationToken cancellationToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(EventBudget);
        try
        {
            await foreach (var piEvent in Client.Events.ReadAllAsync(budget.Token))
            {
                if (string.Equals(piEvent.Type, type, StringComparison.Ordinal))
                {
                    return piEvent;
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException(
                $"timed out waiting for a '{type}' event; transcript tail:\n{TranscriptTail()}");
        }

        // Channel completion means the child is gone — surface its exit
        // fault (code + stderr tail) alongside the transcript.
        string exitDetail;
        try
        {
            var fault = await Client.Exited.WaitAsync(TimeSpan.FromSeconds(2), CancellationToken.None);
            exitDetail = fault.Message;
        }
        catch (TimeoutException)
        {
            exitDetail = "(exit fault not yet available)";
        }

        throw new InvalidOperationException(
            $"event channel completed without a '{type}' event; {exitDetail}; " +
            $"transcript tail:\n{TranscriptTail()}");
    }

    public virtual async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            await _client.DisposeAsync().ConfigureAwait(false);
            _client = null;
        }
    }

    private string TranscriptTail()
    {
        var records = Records;
        return string.Join(
            "\n",
            records.Skip(Math.Max(0, records.Count - 15)).Select(GoldenStore.Serialize));
    }

    protected void Append(GoldenRecord record)
    {
        lock (_recordsLock)
        {
            _records.Add(record);
        }
    }

    protected void OnWire(PiWireDirection direction, string line) =>
        Append(GoldenRecord.Wire(direction == PiWireDirection.ToChild, line));

    /// <summary>
    /// The spawn argument vector both hosts must build identically — it is
    /// compared against the golden's <c>spawn</c> control record at replay.
    /// </summary>
    protected static List<string> BuildArgs(ScenarioSpawn spawn, Func<string, string> resolveFixture)
    {
        var args = new List<string> { "--mode", "rpc", "--provider", "canned", "--model", "canned-model" };
        if (spawn.ExtensionFixture is { } fixture)
        {
            args.Add("--extension");
            args.Add(resolveFixture(fixture));
        }

        args.AddRange(spawn.ExtraArgs);
        return args;
    }

    protected static PiRpcClientOptions ApplySpawnOverrides(PiRpcClientOptions options, ScenarioSpawn spawn)
    {
        if (spawn.EofGrace is { } eofGrace)
        {
            options = options with { EofGrace = eofGrace };
        }

        if (spawn.RequestTimeout is { } requestTimeout)
        {
            options = options with { RequestTimeout = requestTimeout };
        }

        return options;
    }
}
