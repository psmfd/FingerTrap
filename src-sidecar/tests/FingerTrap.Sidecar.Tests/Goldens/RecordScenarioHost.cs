using FingerTrap.Sidecar.PiRpc;

namespace FingerTrap.Sidecar.Tests.Goldens;

/// <summary>
/// Binds a golden scenario to a REAL <c>pi --mode rpc</c> child: hermetic
/// temp HOME (so <c>~/.pi</c> is scenario-owned — the operator's config
/// never leaks into a golden), a <c>models.json</c> whose only provider is
/// the <see cref="CannedModelServer"/>, and temp working directories per
/// cwd token. Everything volatile the run produces — paths, the server
/// port — is registered as a known value for the normalizer, so the raw
/// transcript tokenizes into a byte-reproducible golden.
/// </summary>
internal sealed class RecordScenarioHost : ScenarioHost
{
    private readonly string _root;
    private readonly string _home;
    private readonly string _piExecutable;
    private readonly CannedModelServer _server;
    private readonly Dictionary<string, string> _cwds = new(StringComparer.Ordinal);
    private readonly string _fixturesDir;

    public RecordScenarioHost(string piExecutable)
    {
        _piExecutable = piExecutable;
        _root = Path.Combine(Path.GetTempPath(), $"ft-goldens-{Guid.NewGuid():N}");
        _home = Path.Combine(_root, "home");
        Directory.CreateDirectory(Path.Combine(_home, ".pi", "agent"));
        _fixturesDir = Path.Combine(AppContext.BaseDirectory, "Goldens", "fixtures");

        _server = CannedModelServer.Start();
        File.WriteAllText(
            Path.Combine(_home, ".pi", "agent", "models.json"),
            ModelsJson(_server.BaseUrl));

        CreateCwd(MainCwdToken);
    }

    /// <summary>
    /// Value → token pairs for the normalizer. Paths are registered in
    /// both their raw and <c>/private</c>-prefixed forms: macOS temp paths
    /// live under the <c>/var → /private/var</c> symlink, and pi may
    /// report either spelling.
    /// </summary>
    public IReadOnlyList<(string Value, string Token)> KnownValues()
    {
        var pairs = new List<(string, string)>();
        foreach (var (token, path) in _cwds)
        {
            AddPathVariants(pairs, path, token);
        }

        AddPathVariants(pairs, _home, "@HOME@");
        foreach (var fixture in Directory.EnumerateFiles(_fixturesDir))
        {
            AddPathVariants(pairs, fixture, $"@FIXTURE:{Path.GetFileName(fixture)}@");
        }

        pairs.Add((_server.BaseUrl, "@MODEL_BASE_URL@"));
        return pairs;
    }

    public override string CreateCwd(string token)
    {
        var path = Path.Combine(_root, $"cwd-{_cwds.Count + 1}");
        Directory.CreateDirectory(path);
        _cwds.Add(token, path);
        return token;
    }

    public override void DeleteCwd(string token) => Directory.Delete(_cwds[token], recursive: true);

    public override void EnqueueTurn(CannedTurn turn) => _server.Enqueue(turn);

    public override void ReleaseModelHold() => _server.ReleaseHold();

    protected override Task<PiRpcClient> SpawnCoreAsync(ScenarioSpawn spawn)
    {
        var cwdToken = spawn.CwdToken ?? MainCwdToken;
        var args = BuildArgs(spawn, fixture => Path.Combine(_fixturesDir, fixture));
        Append(GoldenRecord.Spawn(args, _cwds[cwdToken]));

        var options = new PiRpcClientOptions
        {
            ExecutablePath = _piExecutable,
            Arguments = args,
            WorkingDirectory = _cwds[cwdToken],
            EnvironmentOverrides = new Dictionary<string, string> { ["HOME"] = _home },
            WireTap = OnWire,
        };

        return Task.FromResult(PiRpcClient.Start(ApplySpawnOverrides(options, spawn)));
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync().ConfigureAwait(false);
        await _server.DisposeAsync().ConfigureAwait(false);
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort temp cleanup; the OS reaps stragglers.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Finds pi on PATH; null keeps the recorder self-skipping keyless/toolless.</summary>
    public static string? ResolvePiOnPath()
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir, "pi");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static void AddPathVariants(List<(string, string)> pairs, string path, string token)
    {
        var slashForms = new List<string> { path };
        if (path.StartsWith("/var/", StringComparison.Ordinal))
        {
            slashForms.Add("/private" + path);
        }
        else if (path.StartsWith("/private/var/", StringComparison.Ordinal))
        {
            slashForms.Add(path["/private".Length..]);
        }

        foreach (var form in slashForms)
        {
            pairs.Add((form, token));
            // pi encodes the cwd into its session directory name with
            // slashes flattened to dashes (`--<cwd-dashes>--`) — the
            // mangled spelling must tokenize to the same token.
            pairs.Add((form.Replace('/', '-'), token));
        }
    }

    private static string ModelsJson(string baseUrl) =>
        $$"""
        {
          "providers": {
            "canned": {
              "baseUrl": "{{baseUrl}}",
              "api": "openai-completions",
              "apiKey": "canned-key",
              "models": [
                {
                  "id": "canned-model",
                  "name": "canned-model",
                  "reasoning": false,
                  "input": ["text"],
                  "contextWindow": 32768,
                  "maxTokens": 4096,
                  "cost": { "input": 0, "output": 0, "cacheRead": 0, "cacheWrite": 0 }
                }
              ]
            }
          }
        }
        """;
}
