using System.Text;
using System.Text.Json;
using FingerTrap.Sidecar.Ipc;
using FingerTrap.Sidecar.Text;

namespace FingerTrap.Sidecar.Sessions;

/// <summary>
/// Read-only scan of pi's session store (<c>~/.pi/agent/sessions/*/*.jsonl</c>)
/// for the session browser (FT-2 slice 5, ADR-0026). pi keeps no index file —
/// its own session list rescans the filesystem per call — so this store does
/// the same, bounded: every file is enumerated cheaply (path + mtime only),
/// but only the <see cref="DeepParseCap"/> most recently modified files are
/// line-parsed. The result's TotalCount lets the UI show "N of M".
///
/// A session file's line 1 is the v3 header
/// (<c>{"type":"session","version":3,...}</c>) — the <c>{"kind":"header"}</c>
/// v4 shape that exists elsewhere in the pi tree is unused by the CLI at the
/// pinned version and is deliberately not recognized. Subsequent lines are
/// parsed individually and malformed lines are skipped; message counting
/// covers ALL branches of the session tree, so counts can exceed what a
/// resumed context window shows (accepted). The full message text is never
/// aggregated (files reach tens of MB; the UI filters on name, first
/// message, and cwd only).
///
/// Display fields (name, first message, cwd) pass through
/// <see cref="StatusText.Sanitize"/> at construction (ADR-0022). The
/// sanitized cwd is display data: <see cref="SessionSummary.SessionPath"/> —
/// enumerated by this store, never derived from file content — is the only
/// functional resume key.
/// </summary>
internal sealed class SessionStore
{
    public const int DefaultDeepParseCap = 200;

    /// <summary>Cwd values are paths, not titles — a longer cap than the
    /// 300-char field default, still bounded against a hostile header.</summary>
    private const int CwdMaxLength = 1000;

    private readonly string _root;
    private readonly int _deepParseCap;

    /// <param name="root">
    /// Session-store root, or null for pi's real one. Injectable for tests.
    /// </param>
    /// <param name="deepParseCap">
    /// Maximum number of files to line-parse per scan, newest first.
    /// </param>
    public SessionStore(string? root = null, int deepParseCap = DefaultDeepParseCap)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(deepParseCap);
        _root = root ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".pi", "agent", "sessions");
        _deepParseCap = deepParseCap;
    }

    public async Task<SessionsListResult> ListAsync(CancellationToken cancellationToken)
    {
        var files = new List<(string Path, DateTime MtimeUtc)>();
        if (Directory.Exists(_root))
        {
            foreach (var dir in Directory.EnumerateDirectories(_root))
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var file in Directory.EnumerateFiles(dir, "*.jsonl"))
                {
                    files.Add((file, File.GetLastWriteTimeUtc(file)));
                }
            }
        }

        var sessions = new List<SessionSummary>();
        foreach (var (path, mtime) in files
            .OrderByDescending(f => f.MtimeUtc)
            .Take(_deepParseCap))
        {
            var summary = await ParseFileAsync(path, mtime, cancellationToken).ConfigureAwait(false);
            if (summary is not null)
            {
                sessions.Add(summary);
            }
        }

        // mtime ordered the deep-parse budget; the rows themselves sort on
        // the content-derived timestamp, which is what the UI displays.
        sessions.Sort((a, b) => b.ModifiedAt.CompareTo(a.ModifiedAt));
        return new SessionsListResult(sessions, files.Count);
    }

    /// <summary>One file → one summary, or null when the file has no valid
    /// v3 header as its first line. Internal for tests.</summary>
    internal static async Task<SessionSummary?> ParseFileAsync(
        string path, DateTime mtimeUtc, CancellationToken cancellationToken)
    {
        // Live pi sessions append to these files; never take a lock they
        // would conflict with.
        FileStream stream;
        try
        {
            stream = new FileStream(
                path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        await using (stream.ConfigureAwait(false))
        {
            using var reader = new StreamReader(stream, Encoding.UTF8);

            var headerLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            var header = ParseHeader(headerLine);
            if (header is null)
            {
                return null;
            }

            string? name = null;
            string firstMessage = string.Empty;
            var messageCount = 0;
            DateTimeOffset? lastActivity = null;

            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) is not null)
            {
                if (line.Length == 0)
                {
                    continue;
                }

                JsonDocument entry;
                try
                {
                    entry = JsonDocument.Parse(line);
                }
                catch (JsonException)
                {
                    continue;
                }

                using (entry)
                {
                    var root = entry.RootElement;
                    if (root.ValueKind != JsonValueKind.Object
                        || !root.TryGetProperty("type", out var type)
                        || type.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    switch (type.GetString())
                    {
                        case "session_info":
                            // Latest entry wins; an empty name clears.
                            var candidate = StringProperty(root, "name")?.Trim();
                            name = string.IsNullOrEmpty(candidate) ? null : candidate;
                            break;

                        case "message":
                            messageCount++;
                            if (!root.TryGetProperty("message", out var message)
                                || message.ValueKind != JsonValueKind.Object)
                            {
                                break;
                            }

                            var activity = MessageTimestamp(message) ?? EntryTimestamp(root);
                            if (activity is not null
                                && (lastActivity is null || activity > lastActivity))
                            {
                                lastActivity = activity;
                            }

                            if (firstMessage.Length == 0
                                && StringProperty(message, "role") == "user")
                            {
                                // Only latch actual text — a whitespace-only
                                // first message must not shadow a later one.
                                var text = ExtractText(message);
                                if (!string.IsNullOrWhiteSpace(text))
                                {
                                    firstMessage = text;
                                }
                            }

                            break;
                    }
                }
            }

            var modified = lastActivity ?? header.Timestamp ?? mtimeUtc;
            var created = header.Timestamp ?? mtimeUtc;

            var cwdMissing = !Directory.Exists(header.Cwd);
            var worktreeIndex = header.Cwd.IndexOf("/.worktrees/", StringComparison.Ordinal);
            var reaped = cwdMissing && worktreeIndex > 0;

            return new SessionSummary(
                SessionPath: path,
                Id: header.Id,
                Cwd: StatusText.Sanitize(header.Cwd, CwdMaxLength),
                Name: name is null ? null : StatusText.Sanitize(name),
                FirstMessage: StatusText.Sanitize(firstMessage),
                MessageCount: messageCount,
                CreatedAt: created,
                ModifiedAt: modified,
                ParentSessionPath: header.ParentSession,
                CwdMissing: cwdMissing,
                ReapedWorktree: reaped,
                OriginalRepo: reaped
                    ? StatusText.Sanitize(header.Cwd[..worktreeIndex], CwdMaxLength)
                    : null);
        }
    }

    private sealed record Header(
        string Id, string Cwd, DateTimeOffset? Timestamp, string? ParentSession);

    private static Header? ParseHeader(string? line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return null;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(line);
        }
        catch (JsonException)
        {
            return null;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || StringProperty(root, "type") != "session"
                || !root.TryGetProperty("version", out var version)
                || version.ValueKind != JsonValueKind.Number
                || version.GetInt32() != 3)
            {
                return null;
            }

            var id = StringProperty(root, "id");
            var cwd = StringProperty(root, "cwd");
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(cwd))
            {
                return null;
            }

            return new Header(id, cwd, EntryTimestamp(root), StringProperty(root, "parentSession"));
        }
    }

    /// <summary>Message-level timestamps are epoch milliseconds.</summary>
    private static DateTimeOffset? MessageTimestamp(JsonElement message)
    {
        if (message.TryGetProperty("timestamp", out var ts)
            && ts.ValueKind == JsonValueKind.Number
            && ts.TryGetInt64(out var epochMs))
        {
            try
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(epochMs);
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        return null;
    }

    /// <summary>Entry-level timestamps are ISO-8601 strings.</summary>
    private static DateTimeOffset? EntryTimestamp(JsonElement entry)
    {
        var value = StringProperty(entry, "timestamp");
        return DateTimeOffset.TryParse(
            value, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : null;
    }

    /// <summary>Message content is either a plain string or an array of
    /// content blocks, of which the <c>text</c> blocks join with spaces.</summary>
    private static string ExtractText(JsonElement message)
    {
        if (!message.TryGetProperty("content", out var content))
        {
            return string.Empty;
        }

        if (content.ValueKind == JsonValueKind.String)
        {
            return content.GetString() ?? string.Empty;
        }

        if (content.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var parts = new List<string>();
        foreach (var block in content.EnumerateArray())
        {
            if (block.ValueKind == JsonValueKind.Object
                && StringProperty(block, "type") == "text")
            {
                var text = StringProperty(block, "text");
                if (!string.IsNullOrEmpty(text))
                {
                    parts.Add(text);
                }
            }
        }

        return string.Join(' ', parts);
    }

    private static string? StringProperty(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
