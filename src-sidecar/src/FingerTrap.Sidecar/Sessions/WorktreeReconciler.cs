using System.Diagnostics;
using System.Text.Json;
using FingerTrap.Sidecar.Ipc;
using FingerTrap.Sidecar.Status;
using FingerTrap.Sidecar.Text;

namespace FingerTrap.Sidecar.Sessions;

/// <summary>
/// Read-only port of the pi_config worktree extension's orphan detection
/// (<c>worktree/lib/reconcile.ts</c>, ADR-0120 there; FT-2 slice 5 /
/// ADR-0026 here). pi fires no event on ungraceful death, so the extension
/// leaves three durable signals — worktree lock reasons carrying pid
/// records, per-session JSON manifests, and <c>refs/pi-wip/*</c> — and this
/// class cross-references them the same way the extension itself does.
/// Surfacing only: reap/unlock stay pi-side <c>/worktree</c> commands.
///
/// The set of repos scanned is the distinct <c>repo</c> values across
/// manifests. Limitation (documented in the plan): a repo with only stray
/// worktree directories and zero manifests is never scanned, because
/// nothing points this sidecar at it.
///
/// Git shelling copies <see cref="LocalGitStatusProvider"/>'s discipline
/// exactly — see the LOAD-BEARING note there: every child redirects
/// stdout/stderr/stdin (an unredirected child corrupts the ADR-0002
/// JSON-RPC framing), takes no optional locks, never prompts, and dies with
/// its process tree after 10s.
/// </summary>
internal sealed class WorktreeReconciler
{
    private static readonly TimeSpan GitTimeout = TimeSpan.FromSeconds(10);

    private readonly string _manifestsDir;
    private readonly Func<int, bool> _isAlive;
    private readonly Func<IReadOnlyList<string>, string, CancellationToken, Task<GitResult>> _runner;

    /// <param name="manifestsDir">
    /// The worktree extension's manifest directory, or null for the real
    /// one (<c>~/.pi/agent/extensions/worktree/sessions</c>). Injectable
    /// for tests.
    /// </param>
    /// <param name="isAlive">
    /// Pid liveness probe; defaults to <see cref="Process.GetProcessById(int)"/>
    /// wrapped in try/catch. A pid is only meaningful on the recording host
    /// (pi_config#1019) — records carry the recorded host verbatim so the
    /// UI can say so.
    /// </param>
    /// <param name="runner">Git process runner; injectable for tests.</param>
    public WorktreeReconciler(
        string? manifestsDir = null,
        Func<int, bool>? isAlive = null,
        Func<IReadOnlyList<string>, string, CancellationToken, Task<GitResult>>? runner = null)
    {
        _manifestsDir = manifestsDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".pi", "agent", "extensions", "worktree", "sessions");
        _isAlive = isAlive ?? DefaultIsAlive;
        _runner = runner ?? RunGitAsync;
    }

    public async Task<WorktreesListResult> ListAsync(CancellationToken cancellationToken)
    {
        var manifests = LoadManifests();
        var records = new List<WorktreeRecord>();

        foreach (var repo in manifests.Select(m => m.Repo).Distinct(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var worktrees = Array.Empty<WorktreeInfo>() as IReadOnlyList<WorktreeInfo>;
            var wipRefs = Array.Empty<WipRefInfo>() as IReadOnlyList<WipRefInfo>;
            if (Directory.Exists(repo))
            {
                worktrees = ParseWorktreePorcelain(
                    await RunOrEmptyAsync(["worktree", "list", "--porcelain"], repo, cancellationToken)
                        .ConfigureAwait(false));
                wipRefs = ParseWipRefs(
                    await RunOrEmptyAsync(
                        ["for-each-ref", "--format=%(refname) %(objectname)", "refs/pi-wip"],
                        repo, cancellationToken).ConfigureAwait(false));
            }

            var repoManifests = manifests.Where(m => m.Repo == repo).ToList();
            var joined = Reconcile(
                worktrees, repoManifests, wipRefs,
                worktreesRoot: $"{repo}/.worktrees",
                // Unlike the in-session extension, this sidecar owns no
                // session — nothing is excluded from the join.
                ownSid: string.Empty,
                _isAlive);
            foreach (var record in joined)
            {
                records.Add(ToWireRecord(record, repo));
            }
        }

        return new WorktreesListResult(records);
    }

    // ---- pure port of reconcile.ts (internal for tests) ----

    internal sealed record LockInfo(string? Sid, int? Pid, string? Host, string? Started);

    /// <summary>Lock reason string, "" when locked without reason, null when
    /// unlocked — the tri-state <c>git worktree list --porcelain</c> encodes.</summary>
    internal sealed record WorktreeInfo(string Path, string? Head, string? Branch, string? LockReason);

    internal sealed record SessionManifest(
        string SessionId, string Repo, string WorktreePath, string Branch, int Pid, string? Host);

    internal sealed record WipRefInfo(string Sid, string Sha);

    internal sealed class SessionRecord
    {
        public required string Sid { get; init; }

        public WorktreeInfo? Worktree { get; set; }

        public SessionManifest? Manifest { get; set; }

        public string? WipSha { get; set; }

        public int? Pid { get; set; }

        public bool Alive { get; set; }
    }

    /// <summary>Parse a <c>session:&lt;sid&gt; pid:&lt;n&gt; host:&lt;h&gt;
    /// started:&lt;iso&gt;</c> lock reason (whitespace-separated k:v tokens;
    /// junk tokens ignored, non-positive pids rejected).</summary>
    internal static LockInfo ParseLockReason(string reason)
    {
        string? sid = null, host = null, started = null;
        int? pid = null;
        foreach (var token in reason.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = token.IndexOf(':', StringComparison.Ordinal);
            if (idx <= 0 || idx == token.Length - 1)
            {
                continue;
            }

            var key = token[..idx];
            var value = token[(idx + 1)..];
            switch (key)
            {
                case "session":
                    sid = value;
                    break;
                case "pid":
                    if (int.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var n) && n > 0)
                    {
                        pid = n;
                    }

                    break;
                case "host":
                    host = value;
                    break;
                case "started":
                    started = value;
                    break;
            }
        }

        return new LockInfo(sid, pid, host, started);
    }

    /// <summary>Port of git.ts <c>listWorktrees</c>: only the
    /// <c>worktree </c>/<c>HEAD </c>/<c>branch </c>/<c>locked</c> prefixes
    /// matter; <c>refs/heads/</c> is stripped from branch names.</summary>
    internal static IReadOnlyList<WorktreeInfo> ParseWorktreePorcelain(string stdout)
    {
        var result = new List<WorktreeInfo>();
        string? path = null, head = null, branch = null, lockReason = null;

        void Flush()
        {
            if (path is not null)
            {
                result.Add(new WorktreeInfo(path, head, branch, lockReason));
            }

            (path, head, branch, lockReason) = (null, null, null, null);
        }

        foreach (var raw in stdout.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.StartsWith("worktree ", StringComparison.Ordinal))
            {
                Flush();
                path = line["worktree ".Length..];
            }
            else if (path is not null && line.StartsWith("HEAD ", StringComparison.Ordinal))
            {
                head = line["HEAD ".Length..];
            }
            else if (path is not null && line.StartsWith("branch ", StringComparison.Ordinal))
            {
                branch = line["branch ".Length..];
                if (branch.StartsWith("refs/heads/", StringComparison.Ordinal))
                {
                    branch = branch["refs/heads/".Length..];
                }
            }
            else if (path is not null && line.StartsWith("locked", StringComparison.Ordinal))
            {
                lockReason = line.Length > "locked".Length ? line["locked ".Length..] : string.Empty;
            }
        }

        Flush();
        return result;
    }

    internal static IReadOnlyList<WipRefInfo> ParseWipRefs(string stdout)
    {
        const string prefix = "refs/pi-wip/";
        var refs = new List<WipRefInfo>();
        foreach (var raw in stdout.Split('\n'))
        {
            var line = raw.Trim();
            if (!line.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var parts = line.Split(' ');
            if (parts.Length >= 2 && parts[0].Length > prefix.Length && parts[1].Length > 0)
            {
                refs.Add(new WipRefInfo(parts[0][prefix.Length..], parts[1]));
            }
        }

        return refs;
    }

    /// <summary>
    /// Merge the three signals into per-sid records (reconcile.ts:70-107,
    /// ported 1:1): worktrees under <paramref name="worktreesRoot"/> only;
    /// sid from the lock reason, else the directory basename; the manifest
    /// fills the pid only when the lock carried none; records for
    /// <paramref name="ownSid"/> are excluded. A record with a dead — or
    /// unknowable — pid is an orphan candidate.
    /// </summary>
    internal static IReadOnlyList<SessionRecord> Reconcile(
        IReadOnlyList<WorktreeInfo> worktrees,
        IReadOnlyList<SessionManifest> manifests,
        IReadOnlyList<WipRefInfo> wipRefs,
        string worktreesRoot,
        string ownSid,
        Func<int, bool> isAlive)
    {
        var bySid = new Dictionary<string, SessionRecord>(StringComparer.Ordinal);
        SessionRecord Record(string sid)
        {
            if (!bySid.TryGetValue(sid, out var r))
            {
                r = new SessionRecord { Sid = sid };
                bySid.Add(sid, r);
            }

            return r;
        }

        foreach (var wt in worktrees)
        {
            // Only session worktrees under our root — the primary checkout
            // and operator-created worktrees elsewhere are none of our
            // business.
            if (!wt.Path.StartsWith($"{worktreesRoot}/", StringComparison.Ordinal)
                && wt.Path != worktreesRoot)
            {
                continue;
            }

            var lockInfo = wt.LockReason is not null ? ParseLockReason(wt.LockReason) : null;
            var sid = lockInfo?.Sid ?? wt.Path[(wt.Path.LastIndexOf('/') + 1)..];
            var r = Record(sid);
            r.Worktree = wt;
            if (lockInfo?.Pid is not null)
            {
                r.Pid = lockInfo.Pid;
            }
        }

        foreach (var m in manifests)
        {
            var r = Record(m.SessionId);
            r.Manifest = m;
            r.Pid ??= m.Pid;
        }

        foreach (var wip in wipRefs)
        {
            Record(wip.Sid).WipSha = wip.Sha;
        }

        var records = new List<SessionRecord>();
        foreach (var r in bySid.Values)
        {
            if (r.Sid == ownSid)
            {
                continue;
            }

            r.Alive = r.Pid is not null && isAlive(r.Pid.Value);
            records.Add(r);
        }

        records.Sort((a, b) => string.CompareOrdinal(a.Sid, b.Sid));
        return records;
    }

    /// <summary>Shape classification happens at DTO construction. Display
    /// trusts the porcelain branch over the manifest branch (the manifest
    /// drifts after a rename); free-text display fields are sanitized here,
    /// while sid and paths stay verbatim as join/functional keys (same
    /// posture as <see cref="SessionSummary.SessionPath"/>).</summary>
    internal static WorktreeRecord ToWireRecord(SessionRecord record, string repo)
    {
        var shape = record.Worktree is not null
            ? record.Alive
                ? "live"
                : record.Worktree.LockReason is not null || record.Manifest is not null
                    ? "dead"
                    : "stray"
            // No worktree directory left; a manifest and/or a stranded wip
            // ref is all that remains of the session.
            : "gone";

        var lockInfo = record.Worktree?.LockReason is { } reason ? ParseLockReason(reason) : null;
        var branch = record.Worktree?.Branch ?? record.Manifest?.Branch;
        var host = record.Manifest?.Host ?? lockInfo?.Host;

        return new WorktreeRecord(
            Sid: record.Sid,
            WorktreePath: record.Worktree?.Path ?? record.Manifest?.WorktreePath,
            Branch: branch is null ? null : StatusText.Sanitize(branch, 120),
            Repo: repo,
            Host: host is null ? null : StatusText.Sanitize(host, 120),
            WipSha: record.WipSha,
            Pid: record.Pid,
            Alive: record.Alive,
            Shape: shape);
    }

    // ---- IO ----

    /// <summary>Manifests are advisory index files written by the pi_config
    /// worktree extension; anything unreadable or shape-invalid is dropped
    /// (manifest.ts <c>isManifest</c> guard: v==1, string identity fields,
    /// numeric pid).</summary>
    internal IReadOnlyList<SessionManifest> LoadManifests()
    {
        if (!Directory.Exists(_manifestsDir))
        {
            return [];
        }

        var manifests = new List<SessionManifest>();
        foreach (var file in Directory.EnumerateFiles(_manifestsDir, "*.json"))
        {
            string raw;
            try
            {
                raw = File.ReadAllText(file);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            var manifest = ParseManifest(raw);
            if (manifest is not null)
            {
                manifests.Add(manifest);
            }
        }

        return manifests;
    }

    internal static SessionManifest? ParseManifest(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("v", out var v)
                || v.ValueKind != JsonValueKind.Number
                || v.GetInt32() != 1)
            {
                return null;
            }

            var sessionId = StringProperty(root, "sessionId");
            var repo = StringProperty(root, "repo");
            var worktreePath = StringProperty(root, "worktreePath");
            var branch = StringProperty(root, "branch");
            if (sessionId is null || repo is null || worktreePath is null || branch is null
                || !root.TryGetProperty("pid", out var pid)
                || pid.ValueKind != JsonValueKind.Number
                || !pid.TryGetInt32(out var pidValue))
            {
                return null;
            }

            return new SessionManifest(
                sessionId, repo, worktreePath, branch, pidValue, StringProperty(root, "host"));
        }
    }

    private async Task<string> RunOrEmptyAsync(
        IReadOnlyList<string> args, string repo, CancellationToken cancellationToken)
    {
        // git.ts returns [] on any git failure; the C# equivalent of that
        // posture is "no output" — a scan must degrade, not throw.
        try
        {
            var result = await _runner(args, repo, cancellationToken).ConfigureAwait(false);
            return result.ExitCode == 0 ? result.Stdout : string.Empty;
        }
        catch (GitUnavailableException)
        {
            return string.Empty;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return string.Empty;
        }
    }

    private static bool DefaultIsAlive(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static async Task<GitResult> RunGitAsync(
        IReadOnlyList<string> args, string workingDirectory, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
        };
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        startInfo.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                throw new GitUnavailableException("git could not be started");
            }
        }
        catch (System.ComponentModel.Win32Exception)
        {
            throw new GitUnavailableException("git not found on PATH");
        }

        process.StandardInput.Close();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(GitTimeout);

        var stdoutTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Already gone.
            }

            throw;
        }

        return new GitResult(
            process.ExitCode,
            await stdoutTask.ConfigureAwait(false),
            await stderrTask.ConfigureAwait(false));
    }

    private static string? StringProperty(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
