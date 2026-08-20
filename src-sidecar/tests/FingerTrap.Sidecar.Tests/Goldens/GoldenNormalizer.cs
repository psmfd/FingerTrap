using System.Text.RegularExpressions;

namespace FingerTrap.Sidecar.Tests.Goldens;

/// <summary>
/// Tokenizes the volatile values in a recorded transcript so a golden is
/// byte-reproducible across recordings (#139): known path prefixes (temp
/// HOME, scenario cwds, the canned model base URL), UUIDs, and timestamps.
/// Everything else — model name, assistant text, token counts — is already
/// deterministic because the recorder serves the model turn from a canned
/// local endpoint. Tokens are assigned by first appearance, so identical
/// structure yields identical numbering; the normalizer is idempotent
/// (tokens never re-match), which is what lets the replay lane run its own
/// output through the same pass before comparing.
/// </summary>
internal sealed partial class GoldenNormalizer
{
    private readonly List<(string Value, string Token)> _prefixes;
    private readonly Dictionary<string, string> _uuids = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _timestamps = new(StringComparer.Ordinal);

    /// <param name="knownValues">
    /// Literal value → token pairs (paths, base URLs). Applied longest
    /// value first so nested paths resolve to the most specific token.
    /// </param>
    public GoldenNormalizer(IEnumerable<(string Value, string Token)> knownValues)
    {
        _prefixes = [.. knownValues.OrderByDescending(p => p.Value.Length)];
    }

    [GeneratedRegex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")]
    private static partial Regex UuidPattern();

    // ISO-8601 with colons (wire timestamps) …
    [GeneratedRegex(@"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:?\d{2})?")]
    private static partial Regex IsoTimestampPattern();

    // … and the dashed form pi uses in session file names.
    [GeneratedRegex(@"\d{4}-\d{2}-\d{2}T\d{2}-\d{2}-\d{2}(?:-\d{1,6})?")]
    private static partial Regex DashedTimestampPattern();

    // Millisecond/second epoch values under a "timestamp" key. Keyed so an
    // ordinary token count can never be mistaken for a timestamp.
    [GeneratedRegex("(\"timestamp\"\\s*:\\s*)(\\d{10,13})")]
    private static partial Regex EpochTimestampPattern();

    public IReadOnlyList<GoldenRecord> Normalize(IReadOnlyList<GoldenRecord> records)
    {
        var normalized = records.Select(record => record with
        {
            Line = record.Line is null ? null : NormalizeText(record.Line),
            Cwd = record.Cwd is null ? null : NormalizeText(record.Cwd),
            Args = record.Args?.Select(NormalizeText).ToArray(),
        }).ToList();

        return CanonicalizeWindows(normalized);
    }

    public string NormalizeText(string text)
    {
        var result = text;
        foreach (var (value, token) in _prefixes)
        {
            result = result.Replace(value, token, StringComparison.Ordinal);
        }

        result = UuidPattern().Replace(result, match =>
            Token(_uuids, match.Value, "UUID"));
        result = IsoTimestampPattern().Replace(result, match =>
            Token(_timestamps, match.Value, "TS"));
        result = DashedTimestampPattern().Replace(result, match =>
            Token(_timestamps, match.Value, "TS"));
        result = EpochTimestampPattern().Replace(result, match =>
            match.Groups[1].Value + "\"" + Token(_timestamps, match.Groups[2].Value, "TS") + "\"");
        return result;
    }

    /// <summary>
    /// Cross-direction interleaving is the one thing the contract does not
    /// guarantee (a command's ack "can interleave with the prompt's own
    /// first streaming events" — correlate by id, never by ordering), so a
    /// raw recording of it is not reproducible. Canonical form: within
    /// each run of consecutive inbound records, response lines move to the
    /// front (stable among themselves), events keep their order. Goldens
    /// therefore pin exactly what the contract pins: per-direction
    /// ordering plus id correlation.
    /// </summary>
    public static IReadOnlyList<GoldenRecord> CanonicalizeWindows(IReadOnlyList<GoldenRecord> records)
    {
        var result = new List<GoldenRecord>(records.Count);
        var window = new List<GoldenRecord>();

        void FlushWindow()
        {
            result.AddRange(window.Where(IsResponseLine));
            result.AddRange(window.Where(r => !IsResponseLine(r)));
            window.Clear();
        }

        foreach (var record in records)
        {
            if (record.IsInbound)
            {
                window.Add(record);
                continue;
            }

            FlushWindow();
            result.Add(record);
        }

        FlushWindow();
        return result;
    }

    private static bool IsResponseLine(GoldenRecord record) =>
        record.Line is not null
        && record.Line.Contains("\"type\":\"response\"", StringComparison.Ordinal);

    private static string Token(Dictionary<string, string> map, string value, string kind)
    {
        if (!map.TryGetValue(value, out var token))
        {
            // Token alphabet deliberately avoids characters the default
            // System.Text.Json encoder escapes (<, >, &, +, non-ASCII):
            // replay drivers echo tokens back into commands, and an escaped
            // spelling would no longer match the golden byte-for-byte.
            token = $"@{kind}:{map.Count + 1}@";
            map.Add(value, token);
        }

        return token;
    }
}
