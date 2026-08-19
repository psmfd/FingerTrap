using FingerTrap.Sidecar.Sessions;
using Xunit;

namespace FingerTrap.Sidecar.Tests;

public sealed class SessionStoreTests
{
    [Fact]
    public async Task RealisticTreeIsSummarized()
    {
        var root = Directory.CreateTempSubdirectory("ft-sessions-").FullName;
        try
        {
            var cwd = Directory.CreateTempSubdirectory("ft-cwd-").FullName;
            try
            {
                var file = WriteSession(root, "dir-a", "one.jsonl",
                    Header("11111111-aaaa-bbbb-cccc-000000000001", "2026-08-18T10:00:00.000Z", cwd),
                    """{"type":"model_change","id":"m1","timestamp":"2026-08-18T10:00:01.000Z"}""",
                    UserMessage("first question here", 1765965602000),
                    """{"type":"message","id":"m3","timestamp":"2026-08-18T10:00:05.000Z","message":{"role":"assistant","content":[{"type":"text","text":"an answer"}],"timestamp":1765965605000}}""",
                    """{"type":"session_info","id":"m4","timestamp":"2026-08-18T10:00:06.000Z","name":"  my session  "}""");

                var store = new SessionStore(root);
                var result = await store.ListAsync(TestContext.Current.CancellationToken);

                var summary = Assert.Single(result.Sessions);
                Assert.Equal(1, result.TotalCount);
                Assert.Equal(file, summary.SessionPath);
                Assert.Equal("11111111-aaaa-bbbb-cccc-000000000001", summary.Id);
                Assert.Equal(cwd, summary.Cwd);
                Assert.Equal("my session", summary.Name);
                Assert.Equal("first question here", summary.FirstMessage);
                Assert.Equal(2, summary.MessageCount);
                Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1765965605000), summary.ModifiedAt);
                Assert.Equal(
                    DateTimeOffset.Parse(
                        "2026-08-18T10:00:00.000Z",
                        System.Globalization.CultureInfo.InvariantCulture),
                    summary.CreatedAt);
                Assert.Null(summary.ParentSessionPath);
                Assert.False(summary.CwdMissing);
                Assert.False(summary.ReapedWorktree);
                Assert.Null(summary.OriginalRepo);
            }
            finally
            {
                TryDelete(cwd);
            }
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task MalformedLinesAreSkippedNotFatal()
    {
        var root = Directory.CreateTempSubdirectory("ft-sessions-").FullName;
        try
        {
            WriteSession(root, "dir-a", "one.jsonl",
                Header("id-1", "2026-08-18T10:00:00.000Z", "/no/such/dir"),
                "{not json at all",
                UserMessage("hello", 1765965602000),
                "\"a bare string\"",
                "");

            var store = new SessionStore(root);
            var result = await store.ListAsync(TestContext.Current.CancellationToken);

            var summary = Assert.Single(result.Sessions);
            Assert.Equal(1, summary.MessageCount);
            Assert.Equal("hello", summary.FirstMessage);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task FileWithoutLeadingHeaderIsDroppedButCounted()
    {
        var root = Directory.CreateTempSubdirectory("ft-sessions-").FullName;
        try
        {
            // Header on line 2 — pi always writes it first; anything else is
            // not a session file this store understands.
            WriteSession(root, "dir-a", "bad.jsonl",
                UserMessage("hello", 1765965602000),
                Header("id-1", "2026-08-18T10:00:00.000Z", "/tmp"));
            // The v4 {"kind":"header"} shape elsewhere in the pi tree is
            // deliberately not recognized (unused by the CLI at this pin).
            WriteSession(root, "dir-a", "v4.jsonl",
                """{"kind":"header","version":4,"id":"id-2","cwd":"/tmp"}""");

            var store = new SessionStore(root);
            var result = await store.ListAsync(TestContext.Current.CancellationToken);

            Assert.Empty(result.Sessions);
            Assert.Equal(2, result.TotalCount);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task LatestSessionInfoWinsAndEmptyNameClears()
    {
        var root = Directory.CreateTempSubdirectory("ft-sessions-").FullName;
        try
        {
            WriteSession(root, "dir-a", "named.jsonl",
                Header("id-1", "2026-08-18T10:00:00.000Z", "/no/such/dir"),
                """{"type":"session_info","name":"first name"}""",
                """{"type":"session_info","name":"second name"}""");
            WriteSession(root, "dir-a", "cleared.jsonl",
                Header("id-2", "2026-08-18T11:00:00.000Z", "/no/such/dir"),
                """{"type":"session_info","name":"was named"}""",
                """{"type":"session_info","name":"  "}""");

            var store = new SessionStore(root);
            var result = await store.ListAsync(TestContext.Current.CancellationToken);

            Assert.Equal(2, result.Sessions.Count);
            var byId = result.Sessions.ToDictionary(s => s.Id);
            Assert.Equal("second name", byId["id-1"].Name);
            Assert.Null(byId["id-2"].Name);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task HostileStringsAreSanitizedAtConstruction()
    {
        var root = Directory.CreateTempSubdirectory("ft-sessions-").FullName;
        try
        {
            // ESC/CSI, BEL, and a BiDi override — the terminal-injection and
            // Trojan-Source classes StatusText strips — pushed through the
            // REAL parse path via name, first message, and cwd. The \u
            // escapes below are JSON escapes: the store's JSON parser turns
            // them into real control characters before sanitization runs.
            WriteSession(root, "dir-a", "hostile.jsonl",
                Header("id-1", "2026-08-18T10:00:00.000Z", "/no/such\\u001b[2J/dir"),
                """{"type":"session_info","name":"evil\u001b[2Jname\u0007"}""",
                UserMessage("rtl\\u202egnp.exe attack", 1765965602000));

            var store = new SessionStore(root);
            var result = await store.ListAsync(TestContext.Current.CancellationToken);

            var summary = Assert.Single(result.Sessions);
            Assert.Equal("evil[2Jname", summary.Name);
            Assert.Equal("rtlgnp.exe attack", summary.FirstMessage);
            Assert.Equal("/no/such[2J/dir", summary.Cwd);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task DeepParseCapBoundsWorkButTotalCountIsComplete()
    {
        var root = Directory.CreateTempSubdirectory("ft-sessions-").FullName;
        try
        {
            for (var i = 0; i < 5; i++)
            {
                var file = WriteSession(root, "dir-a", $"s{i}.jsonl",
                    Header($"id-{i}", "2026-08-18T10:00:00.000Z", "/no/such/dir"));
                // Distinct mtimes so "newest first" is deterministic.
                File.SetLastWriteTimeUtc(file, new DateTime(2026, 8, 18, 0, 0, i, DateTimeKind.Utc));
            }

            var store = new SessionStore(root, deepParseCap: 2);
            var result = await store.ListAsync(TestContext.Current.CancellationToken);

            Assert.Equal(5, result.TotalCount);
            Assert.Equal(2, result.Sessions.Count);
            Assert.Equal(["id-4", "id-3"], result.Sessions.Select(s => s.Id).ToArray());
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task ReapedWorktreeDerivesFlagsAndOriginalRepo()
    {
        var root = Directory.CreateTempSubdirectory("ft-sessions-").FullName;
        try
        {
            WriteSession(root, "dir-a", "reaped.jsonl",
                Header("id-1", "2026-08-18T10:00:00.000Z",
                    "/Users/x/projects/repo/.worktrees/0199-abc"),
                """{"type":"session","comment":"n/a"}""");
            // Missing cwd that is NOT a worktree: plain missing, not reaped.
            WriteSession(root, "dir-a", "missing.jsonl",
                Header("id-2", "2026-08-18T10:00:00.000Z", "/no/such/plain/dir"));

            var store = new SessionStore(root);
            var result = await store.ListAsync(TestContext.Current.CancellationToken);

            var byId = result.Sessions.ToDictionary(s => s.Id);
            Assert.True(byId["id-1"].CwdMissing);
            Assert.True(byId["id-1"].ReapedWorktree);
            Assert.Equal("/Users/x/projects/repo", byId["id-1"].OriginalRepo);
            Assert.True(byId["id-2"].CwdMissing);
            Assert.False(byId["id-2"].ReapedWorktree);
            Assert.Null(byId["id-2"].OriginalRepo);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task ParentSessionPathIsSurfacedVerbatim()
    {
        var root = Directory.CreateTempSubdirectory("ft-sessions-").FullName;
        try
        {
            WriteSession(root, "dir-a", "child.jsonl",
                """{"type":"session","version":3,"id":"id-1","timestamp":"2026-08-18T10:00:00.000Z","cwd":"/no/such/dir","parentSession":"/dangling/parent.jsonl"}""");

            var store = new SessionStore(root);
            var result = await store.ListAsync(TestContext.Current.CancellationToken);

            var summary = Assert.Single(result.Sessions);
            // Can dangle — resolution (roots for unresolvable parents) is
            // the UI's job, not the store's.
            Assert.Equal("/dangling/parent.jsonl", summary.ParentSessionPath);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task MissingRootYieldsEmptyResult()
    {
        var store = new SessionStore("/no/such/session/root");

        var result = await store.ListAsync(TestContext.Current.CancellationToken);

        Assert.Empty(result.Sessions);
        Assert.Equal(0, result.TotalCount);
    }

    private static string Header(string id, string timestamp, string cwd) =>
        $$"""{"type":"session","version":3,"id":"{{id}}","timestamp":"{{timestamp}}","cwd":"{{cwd}}"}""";

    private static string UserMessage(string text, long epochMs) =>
        $$$"""{"type":"message","id":"m","timestamp":"2026-08-18T10:00:02.000Z","message":{"role":"user","content":[{"type":"text","text":"{{{text}}}"}],"timestamp":{{{epochMs}}}}}""";

    private static string WriteSession(string root, string dir, string name, params string[] lines)
    {
        var directory = Path.Combine(root, dir);
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, name);
        File.WriteAllText(file, string.Join('\n', lines) + "\n");
        return file;
    }

    private static void TryDelete(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }
}
