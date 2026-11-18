using System.Runtime.InteropServices;
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
internal static partial class PiExecutableResolver
{
    /// <summary>
    /// Environment variable naming the pi executable outright. The escape
    /// hatch until the settings system (Native track N-1) exists; a pi
    /// installed somewhere unusual should not require a code change.
    /// </summary>
    internal const string PiPathEnvVar = "FINGERTRAP_PI";

    /// <summary>Executable name searched for on <c>PATH</c>.</summary>
    private const string PiExecutableName = "pi";
    private const UnixFileMode AnyExecute =
        UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
    private const int UnixExecuteAccess = 1;
    private const int UnixOperationNotPermitted = 1;
    private const int UnixNoEntry = 2;
    private const int UnixAccessDenied = 13;
    private const int UnixNotDirectory = 20;
    private const int DarwinAtCurrentDirectory = -2;
    private const int DarwinAtEffectiveAccess = 0x0010;
    private const int LinuxAtCurrentDirectory = -100;
    private const int LinuxAtEffectiveAccess = 0x0200;
    private const string MissingFile = "does not name an existing file";
    private const string CannotAccessFile = "could not be accessed";

    /// <summary>
    /// Locate the pi executable. Throws rather than falling back: a pi pane
    /// that quietly opened something else would be worse than one that
    /// refused (see <see cref="PiNotFoundException"/>).
    /// </summary>
    internal static string ResolvePi(string? requested, PiSettings? settings = null)
    {
        // ADR-0035: a parsed managed selection must never enter legacy
        // fallback, even when the request supplies an explicit executable.
        if (settings?.Mode == "managed")
        {
            throw new PiNotFoundException(
                "managed runtime activation is not available in this build; " +
                "profile isolation and runtime qualification must complete first");
        }

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
    /// The legacy Unix PTY reports an exec failure as a child exit. Check a
    /// selected file path before spawn so the pane can render a setup error.
    /// Bare command names still use the PTY child's PATH search.
    /// </summary>
    internal static void ValidateLegacyUnixPtyPath(string executable, string workingDirectory)
    {
        if ((!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux()) || !executable.Contains('/'))
        {
            return;
        }

        var failure = LegacyUnixPathFailure(Path.Combine(workingDirectory, executable));
        if (failure is not null)
        {
            throw InvalidPtyPath(executable, failure);
        }
    }

    /// <summary>Check the configured path using the same component resolution as Unix exec.</summary>
    private static string? LegacyUnixPathFailure(string path)
    {
        if (path.Contains('\0'))
        {
            return MissingFile;
        }

        // .NET's path normalization would remove a symlink/.. pair before lookup.
        var resolved = RealPath(path, IntPtr.Zero);
        if (resolved == IntPtr.Zero)
        {
            return UnixLookupFailure(Marshal.GetLastPInvokeError());
        }

        try
        {
            if ((File.GetAttributes(Marshal.PtrToStringUTF8(resolved)!) & FileAttributes.Directory) != 0)
            {
                return MissingFile;
            }
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return MissingFile;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return CannotAccessFile;
        }
        finally
        {
            Free(resolved);
        }

        // Use the sidecar's effective credentials, which the PTY child inherits.
        var (directory, flags) = OperatingSystem.IsMacOS()
            ? (DarwinAtCurrentDirectory, DarwinAtEffectiveAccess)
            : (LinuxAtCurrentDirectory, LinuxAtEffectiveAccess);
        if (CheckAccess(directory, path, UnixExecuteAccess, flags) == 0)
        {
            return null;
        }

        var error = Marshal.GetLastPInvokeError();
        return error is UnixAccessDenied or UnixOperationNotPermitted
            ? "is not executable"
            : UnixLookupFailure(error);
    }

    private static string UnixLookupFailure(int error) =>
        error is UnixNoEntry or UnixNotDirectory ? MissingFile : CannotAccessFile;

    [LibraryImport("libc", EntryPoint = "realpath", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial IntPtr RealPath(string path, IntPtr buffer);

    [LibraryImport("libc", EntryPoint = "free")]
    private static partial void Free(IntPtr pointer);

    [LibraryImport("libc", EntryPoint = "faccessat", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial int CheckAccess(int directory, string path, int mode, int flags);

    /// <summary>Format a path diagnostic safely for a terminal pane.</summary>
    private static PiNotFoundException InvalidPtyPath(string executable, string reason)
    {
        // This text reaches a terminal pane. Do not render control characters
        // from the configured path as terminal instructions.
        var displayPath = string.Concat(executable.Select(c => char.IsControl(c) ? '\uFFFD' : c));
        return new PiNotFoundException(
            $"Pi executable '{displayPath}' {reason}. Fix: select an executable Pi file in " +
            $"the spawn request, pi.path in settings.json, or ${PiPathEnvVar}.");
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
            if ((mode & AnyExecute) != 0)
            {
                return candidate;
            }
        }

        return null;
    }
}
