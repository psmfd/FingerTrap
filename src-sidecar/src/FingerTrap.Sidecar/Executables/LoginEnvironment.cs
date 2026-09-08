using System.Diagnostics;
using System.Text;
using FingerTrap.Sidecar.Processes;

namespace FingerTrap.Sidecar.Executables;

/// <summary>
/// Resolves the operator's login-shell <c>PATH</c> so pane children — pi, and
/// the pi extensions that spawn tools like <c>gh</c> — can find user-installed
/// tools even when the app was launched from Finder/Launchpad with the bare
/// launchd <c>PATH</c> (#155, #77).
/// </summary>
/// <remarks>
/// A launchd-started GUI app inherits only <c>/usr/bin:/bin:/usr/sbin:/sbin</c>.
/// Shell panes already recover the profile <c>PATH</c> via the <c>-l</c> login
/// shell (<see cref="Pty.PtyService"/>), but pi panes — PTY and native RPC —
/// spawn pi by absolute path with no shell, so their child <c>PATH</c> is
/// whatever this process holds. Resolving the login <c>PATH</c> once at
/// startup and applying it to this process makes every spawned child inherit
/// it, exactly as a terminal-launched app would. Fails soft: any error leaves
/// <c>PATH</c> untouched.
/// </remarks>
internal static class LoginEnvironment
{
    /// <summary>
    /// Merge <paramref name="loginPath"/> with <paramref name="currentPath"/>:
    /// login entries first (in their own order), then any current entries not
    /// already present, de-duplicated. Returns null when
    /// <paramref name="loginPath"/> is null/empty, signalling the caller to
    /// leave <c>PATH</c> untouched. The login <c>PATH</c> is a superset of the
    /// bare launchd one, so this never drops a directory the process already
    /// had while giving user dirs priority.
    /// </summary>
    public static string? AugmentPath(string? currentPath, string? loginPath)
    {
        if (string.IsNullOrEmpty(loginPath))
        {
            return null;
        }

        // One seen-set dedups both sources, so login's own internal
        // duplicates are collapsed too. Login entries keep their order and
        // priority; current-only entries append (no regression against the
        // pre-fix PATH).
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var merged = new List<string>();
        foreach (var dir in Split(loginPath))
        {
            if (seen.Add(dir))
            {
                merged.Add(dir);
            }
        }

        foreach (var dir in Split(currentPath))
        {
            if (seen.Add(dir))
            {
                merged.Add(dir);
            }
        }

        return merged.Count == 0 ? null : string.Join(':', merged);
    }

    private static string[] Split(string? path) =>
        (path ?? string.Empty).Split(':', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// Capture the login-shell <c>PATH</c> by running
    /// <c>$SHELL -l -c 'printf %s "$PATH"'</c>. Returns null on any failure
    /// (Windows, no resolvable shell, spawn error, timeout, non-zero exit),
    /// so a broken profile can never wedge or corrupt startup.
    /// </summary>
    public static string? ResolveLoginPath(TimeSpan timeout)
    {
        if (OperatingSystem.IsWindows())
        {
            return null;
        }

        var shell = Environment.GetEnvironmentVariable("SHELL");
        var startInfo = new ProcessStartInfo(string.IsNullOrEmpty(shell) ? "/bin/sh" : shell);
        var marker = $"FINGERTRAP_PATH_{Guid.NewGuid():N}";
        startInfo.ArgumentList.Add("-l");
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add($"printf '\n{marker}\n%s\n{marker}\n' \"$PATH\"");
        return ProbeAsync(startInfo, marker, timeout).GetAwaiter().GetResult();
    }

    // Injectable process command keeps regression tests away from operator profiles.
    // One budget covers both pipes and exit: waiting for EOF before starting the
    // timer deadlocks on a hung profile or a descendant holding a pipe open.
    internal static async Task<string?> ProbeAsync(ProcessStartInfo startInfo, string marker, TimeSpan timeout)
    {
        using var budget = new CancellationTokenSource(timeout);
        using var process = new Process { StartInfo = startInfo };
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.RedirectStandardInput = true;
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        try
        {
            await ChildSpawnCoordinator.RunAsync(() => Task.FromResult(process.Start()), budget.Token)
                .ConfigureAwait(false);
            process.StandardInput.Close();
            var stdout = ReadBoundedAsync(process.StandardOutput, budget.Token);
            var stderr = DrainAsync(process.StandardError, budget.Token);
            await Task.WhenAll(stdout, stderr, process.WaitForExitAsync(budget.Token))
                .WaitAsync(budget.Token).ConfigureAwait(false);
            return process.ExitCode == 0 ? ExtractPath(await stdout.ConfigureAwait(false), marker) : null;
        }
        catch (Exception)
        {
            // Profile discovery is optional. Cancellation also stops pipe reads;
            // do not wait for inherited handles to reach EOF during cleanup.
            await budget.CancelAsync().ConfigureAwait(false);
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (Exception)
            {
                // Spawn failed, process already gone, or cleanup unavailable.
            }

            return null;
        }
    }

    private static async Task<string?> ReadBoundedAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        const int maxChars = 64 * 1024;
        var output = new StringBuilder();
        var buffer = new char[4096];
        var overflow = false;
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) != 0)
        {
            if (output.Length + count <= maxChars && !overflow)
            {
                output.Append(buffer, 0, count);
            }
            else
            {
                overflow = true;
            }
        }

        return overflow ? null : output.ToString();
    }

    private static async Task DrainAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var buffer = new char[4096];
        while (await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false) != 0)
        {
            // Drain profile diagnostics without retaining or logging their content.
        }
    }

    private static string? ExtractPath(string? output, string marker)
    {
        if (output is null) return null;
        var delimiter = $"\n{marker}\n";
        var start = output.IndexOf(delimiter, StringComparison.Ordinal);
        if (start < 0) return null;
        start += delimiter.Length;
        var end = output.IndexOf(delimiter, start, StringComparison.Ordinal);
        if (end <= start) return null;
        var path = output[start..end];
        return path.Any(char.IsControl) ? null : path;
    }

    /// <summary>
    /// Resolve and apply the login <c>PATH</c> to this process so spawned
    /// children inherit it. No-op on Windows or on any resolution failure.
    /// Idempotent and safe to call once at startup before any pane spawns.
    /// </summary>
    public static void ApplyToProcess(TimeSpan timeout)
    {
        var merged = AugmentPath(Environment.GetEnvironmentVariable("PATH"), ResolveLoginPath(timeout));
        if (merged is not null)
        {
            Environment.SetEnvironmentVariable("PATH", merged);
        }
    }
}
