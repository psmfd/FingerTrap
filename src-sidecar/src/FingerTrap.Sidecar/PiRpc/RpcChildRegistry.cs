using System.Diagnostics;
using System.Text.Json;

namespace FingerTrap.Sidecar.PiRpc;

/// <summary>
/// Crash-safe reap-on-restart registry for <c>pi --mode rpc</c> children
/// (#124). If the sidecar dies uncleanly between spawning a child and shutting
/// it down, the child is reparented and keeps running — holding worktree
/// locks and making live, operator-billed LLM calls with no supervisor.
/// macOS has no parent-death-signal, so this cannot be prevented purely
/// child-side; instead each child is recorded at spawn and reaped by the next
/// sidecar launch.
/// </summary>
/// <remarks>
/// Every entry records the child <em>and</em> its owning sidecar, each keyed
/// by pid + process start time. Start time dodges PID reuse — a bare stored
/// pid can point at an unrelated process after wrap-around, so nothing is ever
/// killed on pid alone. The owner key makes reaping safe under concurrent
/// sidecars (#102): a startup only reaps children whose owning sidecar is
/// dead, never a live sibling instance's panes.
/// </remarks>
internal sealed class RpcChildRegistry
{
    internal sealed record Entry(
        int Pid,
        long StartTimeUtcTicks,
        string SessionId,
        int OwnerPid,
        long OwnerStartTimeUtcTicks);

    // Process.StartTime is whole-second granular on some platforms; match
    // within a small tolerance so a serialize/re-probe round-trip still lines
    // up on the same process.
    private static readonly long MatchToleranceTicks = TimeSpan.FromSeconds(2).Ticks;

    private readonly string _path;
    private readonly Func<int, DateTime?> _startTimeProbe;
    private readonly Action<int> _kill;
    private readonly int _ownerPid;
    private readonly long _ownerStartTicks;
    private readonly object _gate = new();

    /// <param name="path">Registry file (default: the app-data fingertrap dir).</param>
    /// <param name="startTimeProbe">
    /// Returns a live pid's start time (UTC), or null when the pid is not
    /// alive. Injectable so the reap logic is testable without real processes.
    /// </param>
    /// <param name="kill">Kills a pid's whole tree. Injectable for tests.</param>
    /// <param name="owner">
    /// This sidecar's identity (pid + start). Defaults to the current process.
    /// </param>
    public RpcChildRegistry(
        string? path = null,
        Func<int, DateTime?>? startTimeProbe = null,
        Action<int>? kill = null,
        (int Pid, DateTime StartUtc)? owner = null)
    {
        _path = path ?? DefaultPath();
        _startTimeProbe = startTimeProbe ?? DefaultStartTimeProbe;
        _kill = kill ?? DefaultKill;
        var resolvedOwner = owner
            ?? (Environment.ProcessId, DefaultStartTimeProbe(Environment.ProcessId) ?? DateTime.UtcNow);
        _ownerPid = resolvedOwner.Item1;
        _ownerStartTicks = resolvedOwner.Item2.Ticks;
    }

    /// <summary>Record a spawned child, owned by this sidecar.</summary>
    public void Register(int childPid, DateTime childStartUtc, string sessionId)
    {
        lock (_gate)
        {
            var entries = Read();
            entries.RemoveAll(e => e.Pid == childPid);
            entries.Add(new Entry(childPid, childStartUtc.Ticks, sessionId, _ownerPid, _ownerStartTicks));
            Write(entries);
        }
    }

    /// <summary>Drop a child on clean exit — only our own entries.</summary>
    public void Unregister(int childPid)
    {
        lock (_gate)
        {
            var entries = Read();
            if (entries.RemoveAll(e => e.Pid == childPid && e.OwnerPid == _ownerPid) > 0)
            {
                Write(entries);
            }
        }
    }

    /// <summary>
    /// Kill any recorded child whose owning sidecar is dead and whose pid is
    /// still alive with the recorded start time — a genuine orphan. Entries
    /// owned by a still-live sidecar (a concurrent instance) are preserved;
    /// dead-owner entries are dropped whether or not the child was still alive.
    /// Returns the number reaped.
    /// </summary>
    public int ReapOrphans()
    {
        lock (_gate)
        {
            var entries = Read();
            var survivors = new List<Entry>();
            var killed = 0;
            foreach (var entry in entries)
            {
                if (Matches(entry.OwnerPid, entry.OwnerStartTimeUtcTicks))
                {
                    survivors.Add(entry);
                    continue;
                }

                if (Matches(entry.Pid, entry.StartTimeUtcTicks))
                {
                    _kill(entry.Pid);
                    killed++;
                }
            }

            if (survivors.Count != entries.Count)
            {
                Write(survivors);
            }

            return killed;
        }
    }

    private bool Matches(int pid, long startTicks)
    {
        var start = _startTimeProbe(pid);
        return start is DateTime s && Math.Abs(s.Ticks - startTicks) <= MatchToleranceTicks;
    }

    private List<Entry> Read()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return new List<Entry>();
            }

            return JsonSerializer.Deserialize<List<Entry>>(File.ReadAllText(_path)) ?? new List<Entry>();
        }
        catch (Exception)
        {
            // Corrupt/unreadable registry: start clean rather than fail startup.
            return new List<Entry>();
        }
    }

    private void Write(List<Entry> entries)
    {
        try
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var tmp = _path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(entries));
            File.Move(tmp, _path, overwrite: true);
        }
        catch (Exception)
        {
            // Best-effort: a failed write means a stale entry the next reap
            // still resolves against live-pid state, never a startup failure.
        }
    }

    private static DateTime? DefaultStartTimeProbe(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return process.StartTime.ToUniversalTime();
        }
        catch (Exception)
        {
            // Not alive, reaped, or access denied — treat as not present.
            return null;
        }
    }

    private static void DefaultKill(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            process.Kill(entireProcessTree: true);
        }
        catch (Exception)
        {
            // Already gone or unreapable — nothing to do.
        }
    }

    private static string DefaultPath()
    {
        // Test/override seam: point the registry at a scratch file so a suite
        // (or a diagnostic run) never touches the operator's real app state.
        var overridePath = Environment.GetEnvironmentVariable("FINGERTRAP_RPC_CHILD_REGISTRY_PATH");
        if (!string.IsNullOrEmpty(overridePath))
        {
            return overridePath;
        }

        var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrEmpty(root))
        {
            root = Environment.CurrentDirectory;
        }

        return Path.Combine(root, "fingertrap", "rpc-children.json");
    }
}
