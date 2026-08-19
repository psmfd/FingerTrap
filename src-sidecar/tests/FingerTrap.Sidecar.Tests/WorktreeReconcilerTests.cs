using System.Text.Json;
using FingerTrap.Sidecar.Sessions;
using FingerTrap.Sidecar.Status;
using Xunit;

namespace FingerTrap.Sidecar.Tests;

/// <summary>
/// Fixtures mirror the source extension's own suite
/// (pi_config <c>worktree/test/reconcile.test.ts</c>) plus the four ground-truth
/// shapes: live / dead / gone / stray.
/// </summary>
public sealed class WorktreeReconcilerTests
{
    private static WorktreeReconciler.WorktreeInfo Wt(
        string path, string? lockReason, string branch = "feat/x") =>
        new(path, "abc123", branch, lockReason);

    private static WorktreeReconciler.SessionManifest Manifest(string sid, int pid) =>
        new(sid, "/repo", $"/repo/.worktrees/{sid}", $"feat/wt-{sid}", pid, "h");

    private static string LockReason(string sid, int pid, string host = "h", string started = "t") =>
        $"session:{sid} pid:{pid} host:{host} started:{started}";

    [Fact]
    public void ParseLockReasonRoundTrips()
    {
        var parsed = WorktreeReconciler.ParseLockReason(
            "session:sid-1 pid:4242 host:myhost started:2026-07-22T01:02:03Z");

        Assert.Equal("sid-1", parsed.Sid);
        Assert.Equal(4242, parsed.Pid);
        Assert.Equal("myhost", parsed.Host);
        Assert.Equal("2026-07-22T01:02:03Z", parsed.Started);
    }

    [Fact]
    public void ParseLockReasonToleratesJunkAndPartialReasons()
    {
        var junk = WorktreeReconciler.ParseLockReason("free text lock");
        Assert.Null(junk.Sid);
        Assert.Null(junk.Pid);
        Assert.Null(junk.Host);
        Assert.Null(junk.Started);

        Assert.Null(WorktreeReconciler.ParseLockReason("pid:-5 session:x").Pid);
        Assert.Null(WorktreeReconciler.ParseLockReason("pid:abc session:x").Pid);
    }

    [Fact]
    public void ReconcileMergesSignalsPerSidAndExcludesOwn()
    {
        var records = WorktreeReconciler.Reconcile(
            worktrees:
            [
                Wt("/repo", null), // primary checkout — ignored
                Wt("/repo/.worktrees/dead", LockReason("dead", 99999999)),
                Wt("/repo/.worktrees/live", LockReason("live", 1)),
                Wt("/repo/.worktrees/own", LockReason("own", 1)),
                Wt("/elsewhere/manual-wt", LockReason("manual", 1)), // outside root — ignored
            ],
            manifests: [Manifest("dead", 99999999), Manifest("gone", 88888888)],
            wipRefs:
            [
                new WorktreeReconciler.WipRefInfo("dead", new string('d', 40)),
                new WorktreeReconciler.WipRefInfo("gone", new string('e', 40)),
            ],
            worktreesRoot: "/repo/.worktrees",
            ownSid: "own",
            isAlive: pid => pid == 1);

        var bySid = records.ToDictionary(r => r.Sid);
        Assert.Equal(["dead", "gone", "live"], bySid.Keys.Order().ToArray());
        Assert.False(bySid["dead"].Alive);
        Assert.Equal(new string('d', 40), bySid["dead"].WipSha);
        Assert.NotNull(bySid["dead"].Worktree);
        Assert.NotNull(bySid["dead"].Manifest);
        Assert.True(bySid["live"].Alive);
        // Manifest+wip but no worktree directory — the reaped-dir case.
        Assert.False(bySid["gone"].Alive);
        Assert.Null(bySid["gone"].Worktree);
    }

    [Fact]
    public void LocklessWorktreeUnderRootFallsBackToDirNameSid()
    {
        var records = WorktreeReconciler.Reconcile(
            worktrees: [Wt("/repo/.worktrees/stray", null)],
            manifests: [],
            wipRefs: [],
            worktreesRoot: "/repo/.worktrees",
            ownSid: "me",
            isAlive: _ => true);

        var record = Assert.Single(records);
        Assert.Equal("stray", record.Sid);
        Assert.False(record.Alive); // no pid signal → orphan candidate
    }

    [Fact]
    public void ShapesClassifyAtDtoConstruction()
    {
        var records = WorktreeReconciler.Reconcile(
            worktrees:
            [
                Wt("/repo/.worktrees/live", LockReason("live", 1)),
                Wt("/repo/.worktrees/dead", LockReason("dead", 2)),
                Wt("/repo/.worktrees/stray", null),
            ],
            manifests: [Manifest("gone", 3)],
            wipRefs: [],
            worktreesRoot: "/repo/.worktrees",
            ownSid: string.Empty,
            isAlive: pid => pid == 1);

        var shapes = records
            .Select(r => WorktreeReconciler.ToWireRecord(r, "/repo"))
            .ToDictionary(r => r.Sid, r => r.Shape);
        Assert.Equal("live", shapes["live"]);
        Assert.Equal("dead", shapes["dead"]);
        Assert.Equal("gone", shapes["gone"]);
        Assert.Equal("stray", shapes["stray"]);
    }

    [Fact]
    public void WireRecordPrefersPorcelainBranchAndCarriesHost()
    {
        var records = WorktreeReconciler.Reconcile(
            worktrees: [Wt("/repo/.worktrees/s1", LockReason("s1", 7, host: "lockhost"), branch: "feat/renamed")],
            manifests: [Manifest("s1", 7)],
            wipRefs: [],
            worktreesRoot: "/repo/.worktrees",
            ownSid: string.Empty,
            isAlive: _ => false);

        var wire = WorktreeReconciler.ToWireRecord(Assert.Single(records), "/repo");

        // Porcelain wins over the manifest's stale branch (renames drift);
        // the manifest's host wins over the lock's.
        Assert.Equal("feat/renamed", wire.Branch);
        Assert.Equal("h", wire.Host);
        Assert.Equal("/repo/.worktrees/s1", wire.WorktreePath);
        Assert.Equal(7, wire.Pid);
        Assert.Equal("dead", wire.Shape);
    }

    [Fact]
    public void ParseWorktreePorcelainHandlesAllPrefixes()
    {
        var stdout =
            "worktree /repo\n" +
            "HEAD 1111111111111111111111111111111111111111\n" +
            "branch refs/heads/main\n" +
            "\n" +
            "worktree /repo/.worktrees/a\n" +
            "HEAD 2222222222222222222222222222222222222222\n" +
            "branch refs/heads/feat/x\n" +
            "locked session:a pid:5 host:h started:t\n" +
            "\n" +
            "worktree /repo/.worktrees/b\n" +
            "detached\n" +
            "locked\n";

        var worktrees = WorktreeReconciler.ParseWorktreePorcelain(stdout);

        Assert.Equal(3, worktrees.Count);
        Assert.Equal("main", worktrees[0].Branch);
        Assert.Null(worktrees[0].LockReason);
        Assert.Equal("feat/x", worktrees[1].Branch);
        Assert.Equal("session:a pid:5 host:h started:t", worktrees[1].LockReason);
        Assert.Null(worktrees[2].Branch);
        // Locked without reason is "", distinct from unlocked null.
        Assert.Equal(string.Empty, worktrees[2].LockReason);
    }

    [Fact]
    public void ParseWipRefsFiltersToPrefix()
    {
        var stdout =
            "refs/pi-wip/sid-1 " + new string('a', 40) + "\n" +
            "refs/heads/main " + new string('b', 40) + "\n" +
            "garbage\n";

        var refs = WorktreeReconciler.ParseWipRefs(stdout);

        var wip = Assert.Single(refs);
        Assert.Equal("sid-1", wip.Sid);
        Assert.Equal(new string('a', 40), wip.Sha);
    }

    [Fact]
    public void ManifestGuardDropsWrongVersionAndMissingFields()
    {
        Assert.NotNull(WorktreeReconciler.ParseManifest(
            """{"v":1,"sessionId":"s","repo":"/r","worktreePath":"/r/.worktrees/s","branch":"b","pid":5,"host":"h"}"""));
        Assert.Null(WorktreeReconciler.ParseManifest(
            """{"v":2,"sessionId":"s","repo":"/r","worktreePath":"/r/.worktrees/s","branch":"b","pid":5}"""));
        Assert.Null(WorktreeReconciler.ParseManifest(
            """{"v":1,"sessionId":"s","repo":"/r","worktreePath":"/r/.worktrees/s","branch":"b"}"""));
        Assert.Null(WorktreeReconciler.ParseManifest("""{"v":1,"sessionId":5}"""));
        Assert.Null(WorktreeReconciler.ParseManifest("not json"));
    }

    [Fact]
    public async Task ListAsyncJoinsManifestsWithInjectedGitOutput()
    {
        var manifestsDir = Directory.CreateTempSubdirectory("ft-manifests-").FullName;
        var repo = Directory.CreateTempSubdirectory("ft-repo-").FullName;
        try
        {
            // Serialized rather than string-built: a real temp path carries
            // backslashes on Windows and must arrive JSON-escaped.
            await File.WriteAllTextAsync(
                Path.Combine(manifestsDir, "dead.json"),
                JsonSerializer.Serialize(new
                {
                    v = 1,
                    sessionId = "dead",
                    repo,
                    worktreePath = $"{repo}/.worktrees/dead",
                    branch = "feat/dead",
                    pid = 424242,
                    host = "otherhost",
                }),
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(manifestsDir, "bad.json"), "{not json",
                TestContext.Current.CancellationToken);

            var gitCalls = new List<string>();
            var reconciler = new WorktreeReconciler(
                manifestsDir,
                isAlive: _ => false,
                runner: (args, cwd, _) =>
                {
                    gitCalls.Add(string.Join(' ', args));
                    Assert.Equal(repo, cwd);
                    var stdout = args[0] == "worktree"
                        ? $"worktree {repo}/.worktrees/dead\nHEAD {new string('1', 40)}\n" +
                          "branch refs/heads/feat/dead\nlocked session:dead pid:424242 host:otherhost started:t\n"
                        : $"refs/pi-wip/dead {new string('2', 40)}\n";
                    return Task.FromResult(new GitResult(0, stdout, string.Empty));
                });

            var result = await reconciler.ListAsync(TestContext.Current.CancellationToken);

            var record = Assert.Single(result.Records);
            Assert.Equal("dead", record.Sid);
            Assert.Equal("dead", record.Shape);
            Assert.Equal("otherhost", record.Host);
            Assert.Equal(repo, record.Repo);
            Assert.Equal(new string('2', 40), record.WipSha);
            Assert.False(record.Alive);
            Assert.Equal(2, gitCalls.Count);
        }
        finally
        {
            TryDelete(manifestsDir);
            TryDelete(repo);
        }
    }

    [Fact]
    public async Task MissingRepoDirectoryYieldsGoneWithoutRunningGit()
    {
        var manifestsDir = Directory.CreateTempSubdirectory("ft-manifests-").FullName;
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(manifestsDir, "gone.json"),
                """{"v":1,"sessionId":"gone","repo":"/no/such/repo","worktreePath":"/no/such/repo/.worktrees/gone","branch":"b","pid":1,"host":"h"}""",
                TestContext.Current.CancellationToken);

            var ran = false;
            var reconciler = new WorktreeReconciler(
                manifestsDir,
                isAlive: _ => true,
                runner: (_, _, _) =>
                {
                    ran = true;
                    return Task.FromResult(new GitResult(0, string.Empty, string.Empty));
                });

            var result = await reconciler.ListAsync(TestContext.Current.CancellationToken);

            var record = Assert.Single(result.Records);
            Assert.Equal("gone", record.Shape);
            // Liveness still honors the recorded pid even when the repo is
            // gone — but a gone repo never shells out.
            Assert.True(record.Alive);
            Assert.False(ran);
        }
        finally
        {
            TryDelete(manifestsDir);
        }
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
