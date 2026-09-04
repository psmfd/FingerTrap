namespace FingerTrap.Sidecar.Vm;

/// <summary>Frozen fake-only subprocess contract for issue #174.</summary>
internal static class McmProcessContract
{
    public const string InvocationRevision = "mcm-status-fake-v1";
    public const string EnvironmentRevision = "mcm-environment-fake-v1";
    public const string StatusOperation = "status";

    public static IReadOnlyList<string> StatusArguments(string name) =>
        [StatusOperation, "--name", name, "--json"];

    public static IReadOnlyDictionary<string, string> Environment(string home)
    {
        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["HOME"] = home,
            ["PATH"] = "/usr/bin:/bin",
            ["LC_ALL"] = "C",
        };
        var dotnetRoot = System.Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (!string.IsNullOrEmpty(dotnetRoot))
        {
            // FakeMcm is a framework-dependent test apphost. Production mcm
            // does not need this allowlisted fixture-only runtime locator.
            environment["DOTNET_ROOT"] = dotnetRoot;
        }

        if (OperatingSystem.IsMacOS())
        {
            // CoreFoundation injects this key when it is absent. Supplying a
            // fixed value keeps the child-visible environment deterministic.
            environment["__CF_USER_TEXT_ENCODING"] = "0x0:0x0:0x0";
        }

        return environment;
    }
}

internal sealed record McmProcessLimits(
    TimeSpan Timeout,
    TimeSpan TerminateGrace,
    TimeSpan KillGrace,
    int MaxStdoutBytes,
    int MaxStderrBytes)
{
    public static McmProcessLimits Default { get; } = new(
        TimeSpan.FromSeconds(10),
        TimeSpan.FromMilliseconds(500),
        TimeSpan.FromSeconds(2),
        256 * 1024,
        64 * 1024);
}

internal sealed record McmProcessRequest(
    string ExecutablePath,
    IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string, string> Environment,
    McmProcessLimits Limits);

internal enum McmProcessOutcome
{
    InvalidRequest,
    Exited,
    SpawnFailure,
    Signaled,
    TimedOut,
    Canceled,
    StdoutOverflow,
    StderrOverflow,
    CleanupFailed,
}

internal sealed record McmProcessResult(
    McmProcessOutcome Outcome,
    byte[] Stdout,
    byte[] Stderr,
    bool CleanupConfirmed,
    int? ExitCode = null,
    int? Signal = null,
    string? Detail = null);

internal interface IMcmProcessRunner
{
    public Task<McmProcessResult> RunAsync(McmProcessRequest request, CancellationToken cancellationToken);
}
