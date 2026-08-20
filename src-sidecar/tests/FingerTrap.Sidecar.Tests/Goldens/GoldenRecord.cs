using System.Text.Json;
using System.Text.Json.Serialization;

namespace FingerTrap.Sidecar.Tests.Goldens;

/// <summary>
/// One line of a golden transcript (#139): either a control record (the
/// scenario driver's lifecycle actions — <c>spawn</c>, <c>stdin_eof</c>,
/// <c>exit</c>) or a wire record (<c>dir</c> + the raw JSONL line exactly
/// as it crossed the pipe, captured by
/// <see cref="PiRpc.PiRpcClientOptions.WireTap"/>). Goldens are JSONL —
/// one record per LF-terminated line — so a pin-bump diff reads as a
/// line-per-change wire diff.
/// </summary>
internal sealed record GoldenRecord
{
    /// <summary>Control kind: <c>spawn</c>, <c>stdin_eof</c>, or <c>exit</c>.</summary>
    [JsonPropertyName("ctl")]
    public string? Ctl { get; init; }

    /// <summary>Spawn arguments (for <c>ctl: "spawn"</c>), normalized.</summary>
    [JsonPropertyName("args")]
    public IReadOnlyList<string>? Args { get; init; }

    /// <summary>Spawn working-directory token (for <c>ctl: "spawn"</c>).</summary>
    [JsonPropertyName("cwd")]
    public string? Cwd { get; init; }

    /// <summary>Child exit code (for <c>ctl: "exit"</c>).</summary>
    [JsonPropertyName("code")]
    public int? Code { get; init; }

    /// <summary>Wire direction: <c>out</c> (supervisor → pi) or <c>in</c>.</summary>
    [JsonPropertyName("dir")]
    public string? Dir { get; init; }

    /// <summary>The raw wire line, no trailing newline.</summary>
    [JsonPropertyName("line")]
    public string? Line { get; init; }

    [JsonIgnore]
    public bool IsOutbound => string.Equals(Dir, "out", StringComparison.Ordinal);

    [JsonIgnore]
    public bool IsInbound => string.Equals(Dir, "in", StringComparison.Ordinal);

    public static GoldenRecord Spawn(IReadOnlyList<string> args, string cwd) =>
        new() { Ctl = "spawn", Args = args, Cwd = cwd };

    public static GoldenRecord StdinEof() => new() { Ctl = "stdin_eof" };

    public static GoldenRecord Exit(int code) => new() { Ctl = "exit", Code = code };

    public static GoldenRecord Wire(bool outbound, string line) =>
        new() { Dir = outbound ? "out" : "in", Line = line };
}

/// <summary>
/// Reads and writes golden files. The replay lane reads the copies under
/// the test output directory (csproj <c>Content</c> items); the recorder
/// writes to the source tree, located by walking up to the repo root — so
/// a re-record lands as a working-tree diff, which IS the drift report
/// (docs/rpc-contract.md, pin-bump ritual).
/// </summary>
internal static class GoldenStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string OutputDataDir =>
        Path.Combine(AppContext.BaseDirectory, "Goldens", "data");

    public static string SourceDataDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            // .git is a directory in a normal clone and a file in a linked
            // worktree — both mark the repo root.
            if (Directory.Exists(Path.Combine(dir.FullName, ".git"))
                || File.Exists(Path.Combine(dir.FullName, ".git")))
            {
                return Path.Combine(
                    dir.FullName, "src-sidecar", "tests", "FingerTrap.Sidecar.Tests", "Goldens", "data");
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            $"repo root (.git) not found above test output '{AppContext.BaseDirectory}'");
    }

    public static string FileName(string scenarioName) => $"{scenarioName}.golden.jsonl";

    public static string Serialize(GoldenRecord record) =>
        JsonSerializer.Serialize(record, SerializerOptions);

    public static IReadOnlyList<GoldenRecord> Read(string path)
    {
        var records = new List<GoldenRecord>();
        foreach (var line in File.ReadAllLines(path))
        {
            if (line.Length == 0)
            {
                continue;
            }

            records.Add(JsonSerializer.Deserialize<GoldenRecord>(line, SerializerOptions)
                ?? throw new InvalidOperationException($"null golden record in {path}"));
        }

        return records;
    }

    public static void Write(string path, IEnumerable<GoldenRecord> records)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // LF-only, trailing newline — matches the repo's `* text=auto eol=lf`
        // and keeps goldens byte-identical across recording hosts.
        var payload = string.Join(
            "\n", records.Select(r => JsonSerializer.Serialize(r, SerializerOptions)));
        File.WriteAllText(path, payload + "\n");
    }
}
