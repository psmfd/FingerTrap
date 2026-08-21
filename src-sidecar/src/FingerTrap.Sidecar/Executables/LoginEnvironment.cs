using System.Diagnostics;

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
        if (string.IsNullOrEmpty(shell))
        {
            shell = "/bin/sh";
        }

        try
        {
            var startInfo = new ProcessStartInfo(shell)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            // -l re-reads the login profile (~/.zprofile, ~/.profile) where
            // PATH additions live; printf keeps the output free of a trailing
            // newline the shell's echo would add.
            startInfo.ArgumentList.Add("-l");
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add("printf %s \"$PATH\"");

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            var stdout = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit((int)timeout.TotalMilliseconds))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception)
                {
                    // Best effort; a wedged login shell must not wedge startup.
                }

                return null;
            }

            return process.ExitCode == 0 ? stdout.Trim() : null;
        }
        catch (Exception)
        {
            return null;
        }
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
