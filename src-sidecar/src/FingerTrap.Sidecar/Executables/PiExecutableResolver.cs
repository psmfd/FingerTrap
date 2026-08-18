using FingerTrap.Sidecar.Pty;
using FingerTrap.Sidecar.Settings;

namespace FingerTrap.Sidecar.Executables;

/// <summary>
/// The single pi-executable resolution chain — explicit request, then
/// settings, then <see cref="PiPathEnvVar"/>, then <c>PATH</c> — shared by
/// the PTY service and the RPC pane service (FT-2 slice 2). One chain on
/// purpose: a pi pane and a native RPC pane finding <em>different</em>
/// binaries would be a confusing, hard-to-diagnose divergence.
/// </summary>
internal static class PiExecutableResolver
{
    /// <summary>
    /// Environment variable naming the pi executable outright. The escape
    /// hatch until the settings system (Native track N-1) exists; a pi
    /// installed somewhere unusual should not require a code change.
    /// </summary>
    internal const string PiPathEnvVar = "FINGERTRAP_PI";

    /// <summary>Executable name searched for on <c>PATH</c>.</summary>
    private const string PiExecutableName = "pi";

    /// <summary>
    /// Locate the pi executable. Throws rather than falling back: a pi pane
    /// that quietly opened something else would be worse than one that
    /// refused (see <see cref="PiNotFoundException"/>).
    /// </summary>
    internal static string ResolvePi(string? requested, PiSettings? settings = null)
    {
        if (!string.IsNullOrEmpty(requested))
        {
            return requested;
        }

        // Settings outrank the environment (N-1, #52); the env var survives as
        // a lower layer for ephemeral overrides rather than being retired.
        if (!string.IsNullOrEmpty(settings?.Path))
        {
            return settings.Path;
        }

        var fromEnv = Environment.GetEnvironmentVariable(PiPathEnvVar);
        if (!string.IsNullOrEmpty(fromEnv))
        {
            return fromEnv;
        }

        var onPath = FindOnPath(PiExecutableName);
        if (onPath is not null)
        {
            return onPath;
        }

        throw new PiNotFoundException(
            $"no pi executable found. Tried: the spawn request's explicit path, settings pi.path, " +
            $"${PiPathEnvVar}, then PATH. Fix: install pi, set pi.path in settings.json or " +
            $"{PiPathEnvVar}=/path/to/pi, or request a shell pane instead.");
    }

    /// <summary>
    /// First executable match for <paramref name="name"/> across <c>PATH</c>,
    /// or null.
    /// </summary>
    /// <remarks>
    /// Hand-rolled rather than shelling out to <c>which</c>/<c>where</c>:
    /// spawning a process to decide what to spawn is a needless dependency on
    /// yet another binary being present, and this runs on the pane-open path.
    /// The executable-bit check is what stops a same-named directory or a
    /// non-executable file from being returned as a usable answer.
    /// </remarks>
    private static string? FindOnPath(string name)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate;
            try
            {
                candidate = Path.Combine(dir.Trim(), name);
            }
            catch (ArgumentException)
            {
                // A PATH entry containing invalid path characters is skipped
                // rather than aborting the whole search — one bad entry must
                // not hide a good one further along.
                continue;
            }

            if (!File.Exists(candidate))
            {
                continue;
            }

            if (OperatingSystem.IsWindows())
            {
                return candidate;
            }

            var mode = File.GetUnixFileMode(candidate);
            const UnixFileMode AnyExecute =
                UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
            if ((mode & AnyExecute) != 0)
            {
                return candidate;
            }
        }

        return null;
    }
}
