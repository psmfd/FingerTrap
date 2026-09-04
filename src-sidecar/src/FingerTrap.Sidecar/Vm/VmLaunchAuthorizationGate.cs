using System.Security.Cryptography;
using System.Text;
using FingerTrap.Sidecar.Abstractions;

namespace FingerTrap.Sidecar.Vm;

/// <summary>
/// Sidecar-wide pending-authority seam. A trusted shell path registers the
/// immutable record; the provider accepts only its nonce and atomically marks
/// the record consumed before any fallible pre-spawn work.
/// </summary>
internal sealed class VmLaunchAuthorizationGate
{
    private const int MaxNonceLength = 128;
    private const int MaxEpochLength = 128;
    private const int MaxPathLength = 4096;
    private const int MaxAuthorizationRecords = 256;
    private static readonly TimeSpan MaxAuthorizationLifetime = TimeSpan.FromMinutes(2);

    private readonly Dictionary<string, AuthorizationEntry> _records = new(StringComparer.Ordinal);
    private readonly object _sync = new();
    private readonly TimeProvider _timeProvider;

    public VmLaunchAuthorizationGate(TimeProvider timeProvider, string? processEpoch = null)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        ProcessEpoch = processEpoch ?? Guid.NewGuid().ToString("N");
        if (!ValidBounded(ProcessEpoch, MaxEpochLength))
        {
            throw new ArgumentException("process epoch is invalid", nameof(processEpoch));
        }
    }

    /// <summary>
    /// Per-sidecar epoch that the trusted shell registration path includes in
    /// every authorization. A payload captured from an earlier sidecar cannot
    /// be registered after restart.
    /// </summary>
    public string ProcessEpoch { get; }

    public bool Register(VmLaunchAuthorization authorization, out string detail)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        var now = _timeProvider.GetUtcNow();
        if (!ValidShape(authorization, now)
            || !FixedEquals(authorization.ProcessEpoch, ProcessEpoch, MaxEpochLength))
        {
            detail = "launch authorization is invalid";
            return false;
        }

        lock (_sync)
        {
            PruneExpired(now);
            if (_records.Count >= MaxAuthorizationRecords)
            {
                detail = "launch authorization capacity is exhausted";
                return false;
            }

            // Consumed records remain as tombstones through expiry, so an
            // identical still-live registration cannot recreate authority.
            if (!_records.TryAdd(authorization.Nonce, new AuthorizationEntry(authorization)))
            {
                detail = "launch authorization nonce already exists";
                return false;
            }
        }

        detail = string.Empty;
        return true;
    }

    public bool TryConsume(
        string nonce,
        VmStatusRequest request,
        string expectedClientPath,
        string expectedLimaPath,
        out VmLaunchAuthorization? authorization,
        out string detail)
    {
        ArgumentNullException.ThrowIfNull(request);
        authorization = null;
        if (!ValidBounded(nonce, MaxNonceLength))
        {
            detail = "launch authorization nonce is invalid";
            return false;
        }

        VmLaunchAuthorization pending;
        lock (_sync)
        {
            PruneExpired(_timeProvider.GetUtcNow());
            if (!_records.TryGetValue(nonce, out var entry) || entry.Consumed)
            {
                detail = "launch authorization is unavailable or already consumed";
                return false;
            }

            entry.Consumed = true;
            pending = entry.Authorization;
        }

        // Marking above is the consumption point. No mismatch, expiration,
        // trust failure, spawn failure, or parse failure refunds this record.
        var now = _timeProvider.GetUtcNow();
        if (pending.ExpiresAtUtc <= now
            || !FixedEquals(pending.ProcessEpoch, ProcessEpoch, MaxEpochLength)
            || !FixedEquals(pending.Client.Path, expectedClientPath, MaxPathLength)
            || !FixedEquals(pending.Lima.Path, expectedLimaPath, MaxPathLength)
            || !FixedEquals(pending.InvocationRevision, McmProcessContract.InvocationRevision, 128)
            || !FixedEquals(pending.EnvironmentRevision, McmProcessContract.EnvironmentRevision, 128)
            || !FixedEquals(pending.Operation, McmProcessContract.StatusOperation, 64)
            || !FixedEquals(pending.VmName, request.Name, 63))
        {
            detail = "launch authorization does not match the requested operation";
            return false;
        }

        authorization = pending;
        detail = string.Empty;
        return true;
    }

    private static bool ValidShape(VmLaunchAuthorization authorization, DateTimeOffset now) =>
        ValidBounded(authorization.Nonce, MaxNonceLength)
        && ValidBounded(authorization.ProcessEpoch, MaxEpochLength)
        && authorization.ExpiresAtUtc > now
        && authorization.ExpiresAtUtc <= now + MaxAuthorizationLifetime
        && ValidIdentity(authorization.Client)
        && ValidIdentity(authorization.Lima)
        && authorization.InvocationRevision is McmProcessContract.InvocationRevision
        && authorization.EnvironmentRevision is McmProcessContract.EnvironmentRevision
        && authorization.Operation is McmProcessContract.StatusOperation
        && ValidBounded(authorization.VmName, 63);

    private static bool ValidIdentity(VmExecutableIdentity? identity) =>
        identity is not null
        && ValidBounded(identity.Path, MaxPathLength)
        && identity.Sha256 is { Length: 64 }
        && identity.Sha256.All(static character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool ValidBounded(string? value, int maximum) =>
        !string.IsNullOrEmpty(value) && value.Length <= maximum;

    private void PruneExpired(DateTimeOffset now)
    {
        foreach (var nonce in _records
                     .Where(pair => pair.Value.Authorization.ExpiresAtUtc <= now)
                     .Select(static pair => pair.Key)
                     .ToArray())
        {
            _records.Remove(nonce);
        }
    }

    private static bool FixedEquals(string? left, string? right, int maximum)
    {
        if (!ValidBounded(left, maximum) || !ValidBounded(right, maximum))
        {
            return false;
        }

        var leftBytes = Encoding.UTF8.GetBytes(left!);
        var rightBytes = Encoding.UTF8.GetBytes(right!);
        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private sealed class AuthorizationEntry(VmLaunchAuthorization authorization)
    {
        public VmLaunchAuthorization Authorization { get; } = authorization;
        public bool Consumed { get; set; }
    }
}
