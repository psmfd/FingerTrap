namespace FingerTrap.Sidecar.PiRpc;

/// <summary>
/// The pi RPC child is gone. Carries the exit code and the bounded stderr
/// tail so every rejected in-flight request surfaces the child's actual
/// last words — pi routes all diagnostics to stderr (stdout is
/// protocol-only, guaranteed), so the tail is the primary post-mortem
/// evidence.
/// </summary>
/// <remarks>
/// A distinct type for the same reason <see cref="Pty.PiNotFoundException"/>
/// is one: callers must be able to tell "the child died" (reject everything,
/// surface exit diagnostics) apart from a single request failing on a live
/// child.
/// </remarks>
public sealed class PiProcessExitedException : Exception
{
    public PiProcessExitedException()
        : base("pi rpc child exited")
    {
    }

    public PiProcessExitedException(string message)
        : base(message)
    {
    }

    public PiProcessExitedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public PiProcessExitedException(int exitCode, string stderrTail, Exception? innerException = null)
        : base(BuildMessage(exitCode, stderrTail), innerException)
    {
        ExitCode = exitCode;
        StderrTail = stderrTail;
    }

    /// <summary>The child's exit code; 0 only on a clean shutdown.</summary>
    public int ExitCode { get; }

    /// <summary>
    /// The last captured stderr bytes (bounded — see
    /// <see cref="PiRpcClientOptions.MaxStderrBytes"/>), decoded as UTF-8.
    /// Empty when the child wrote nothing.
    /// </summary>
    public string StderrTail { get; } = string.Empty;

    private static string BuildMessage(int exitCode, string stderrTail)
    {
        return stderrTail.Length == 0
            ? $"pi rpc child exited with code {exitCode}"
            : $"pi rpc child exited with code {exitCode}; stderr tail: {stderrTail}";
    }
}
