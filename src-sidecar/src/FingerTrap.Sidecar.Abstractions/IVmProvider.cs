namespace FingerTrap.Sidecar.Abstractions;

/// <summary>
/// Consumer-owned VM status boundary (ADR-0029). Implementations must not
/// make lifecycle operations reachable through this status-only contract.
/// </summary>
public interface IVmProvider
{
    /// <summary>
    /// Consumes a previously shell-registered nonce and returns one bounded,
    /// FingerTrap-owned VM status result. Caller-supplied executable identity
    /// or operation metadata is never accepted at this boundary.
    /// </summary>
    public Task<VmStatusResult> GetStatusAsync(
        VmStatusRequest request,
        string authorizationNonce,
        CancellationToken cancellationToken);
}

/// <summary>A status request for one validated VM name.</summary>
public sealed record VmStatusRequest(string Name);

/// <summary>An immutable executable pathname and lowercase SHA-256 identity.</summary>
public sealed record VmExecutableIdentity(string Path, string Sha256);

/// <summary>
/// Shell-issued, single-use authority for one exact subprocess invocation.
/// The sidecar consumes <see cref="Nonce"/> before its final trust check and
/// never refunds it, including when spawn or parsing later fails.
/// </summary>
public sealed record VmLaunchAuthorization(
    string Nonce,
    string ProcessEpoch,
    DateTimeOffset ExpiresAtUtc,
    VmExecutableIdentity Client,
    VmExecutableIdentity Lima,
    string InvocationRevision,
    string EnvironmentRevision,
    string Operation,
    string VmName);

/// <summary>Closed internal outcome taxonomy; no VM result crosses RPC yet.</summary>
public enum VmStatusOutcome
{
    Ok,
    Unsupported,
    NotConfigured,
    Unauthorized,
    OperationalFailure,
    InvocationPreconditionFailure,
    SpawnFailure,
    Signaled,
    TimedOut,
    Canceled,
    StdoutOverflow,
    StderrOverflow,
    CleanupFailed,
    MalformedOutput,
    UnsupportedSchema,
    InvalidOutput,
}

/// <summary>FingerTrap-owned projection of mcm schema 1.</summary>
public sealed record VmStatusSnapshot(
    int Schema,
    string Name,
    bool Exists,
    string State,
    bool? Reachable,
    bool? StampPresent,
    string? ProvisionedProfile,
    string? ConfiguredProfile,
    bool? Drift,
    bool? NeedsProvision,
    bool ExpertiseWriteConfigured,
    VmExpertiseTokenStatus? ExpertiseToken,
    int Errors,
    int Warnings,
    string Result);

/// <summary>Bounded expertise-token posture reported by the status contract.</summary>
public sealed record VmExpertiseTokenStatus(bool Present, string? Scope, string? Detail);

/// <summary>One status attempt, including process classification when relevant.</summary>
public sealed record VmStatusResult(
    VmStatusOutcome Outcome,
    VmStatusSnapshot? Snapshot = null,
    string? Detail = null,
    int? ExitCode = null,
    int? Signal = null);
