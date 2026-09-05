using FingerTrap.Sidecar.Abstractions;
using FingerTrap.Sidecar.Text;

namespace FingerTrap.Sidecar.Vm;

internal sealed record McmProviderOptions(
    string ClientExecutablePath,
    string LimaExecutablePath,
    string HomeDirectory,
    McmProcessLimits? Limits = null);

/// <summary>
/// Fake-only status provider foundation for #174. It is intentionally absent
/// from Program.cs, settings, and RPC registration; tests are its sole caller.
/// </summary>
internal sealed class McmVmProvider : IVmProvider
{
    private readonly McmProviderOptions? _options;
    private readonly VmLaunchAuthorizationGate _authorizationGate;
    private readonly IMcmProcessRunner _runner;
    private readonly VmExecutableTrust _trust;
    private readonly Func<bool> _isSupportedPlatform;
    private readonly Dictionary<string, OperationLockEntry> _operationLocks = new(StringComparer.Ordinal);
    private readonly object _operationLocksSync = new();

    public McmVmProvider(
        McmProviderOptions? options,
        VmLaunchAuthorizationGate authorizationGate,
        IMcmProcessRunner? runner = null,
        VmExecutableTrust? trust = null,
        Func<bool>? isSupportedPlatform = null)
    {
        _options = options;
        _authorizationGate = authorizationGate ?? throw new ArgumentNullException(nameof(authorizationGate));
        _runner = runner ?? new McmProcessRunner();
        _trust = trust ?? new VmExecutableTrust();
        _isSupportedPlatform = isSupportedPlatform ?? OperatingSystem.IsMacOS;
    }

    public async Task<VmStatusResult> GetStatusAsync(
        VmStatusRequest request,
        string authorizationNonce,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_isSupportedPlatform())
        {
            return Result(VmStatusOutcome.Unsupported, "VM status is supported only on macOS");
        }

        if (_options is null)
        {
            return Result(VmStatusOutcome.NotConfigured, "VM status is not configured");
        }

        if (!ValidOptions(_options))
        {
            return Result(VmStatusOutcome.InvocationPreconditionFailure, "VM status configuration is invalid");
        }

        if (!ValidName(request.Name))
        {
            return Result(VmStatusOutcome.InvocationPreconditionFailure, "VM name is invalid");
        }

        OperationLockLease operationLock;
        try
        {
            operationLock = await AcquireOperationLockAsync(request.Name, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return Result(VmStatusOutcome.Canceled, "VM status was canceled");
        }

        using (operationLock)
        {
            if (!_authorizationGate.TryConsume(
                    authorizationNonce,
                    request,
                    _options.ClientExecutablePath,
                    _options.LimaExecutablePath,
                    out var authorization,
                    out var authorizationDetail))
            {
                return Result(VmStatusOutcome.Unauthorized, authorizationDetail);
            }

            var clientTrust = _trust.Validate(authorization!.Client);
            if (!clientTrust.Trusted)
            {
                return Result(VmStatusOutcome.InvocationPreconditionFailure, clientTrust.Detail);
            }

            var limaTrust = _trust.Validate(authorization.Lima);
            if (!limaTrust.Trusted)
            {
                return Result(VmStatusOutcome.InvocationPreconditionFailure, limaTrust.Detail);
            }

            var process = await _runner.RunAsync(
                new McmProcessRequest(
                    _options.ClientExecutablePath,
                    McmProcessContract.StatusArguments(request.Name),
                    McmProcessContract.Environment(_options.HomeDirectory),
                    _options.Limits ?? McmProcessLimits.Default),
                cancellationToken).ConfigureAwait(false);

            return MapProcessResult(process, request.Name);
        }
    }

    private static VmStatusResult MapProcessResult(McmProcessResult process, string requestedName)
    {
        var mapped = process.Outcome switch
        {
            McmProcessOutcome.InvalidRequest => VmStatusOutcome.InvocationPreconditionFailure,
            McmProcessOutcome.SpawnFailure => VmStatusOutcome.SpawnFailure,
            McmProcessOutcome.Signaled => VmStatusOutcome.Signaled,
            McmProcessOutcome.TimedOut => VmStatusOutcome.TimedOut,
            McmProcessOutcome.Canceled => VmStatusOutcome.Canceled,
            McmProcessOutcome.StdoutOverflow => VmStatusOutcome.StdoutOverflow,
            McmProcessOutcome.StderrOverflow => VmStatusOutcome.StderrOverflow,
            McmProcessOutcome.CleanupFailed => VmStatusOutcome.CleanupFailed,
            _ => VmStatusOutcome.Ok,
        };
        if (process.Outcome != McmProcessOutcome.Exited)
        {
            return new VmStatusResult(mapped, Detail: Sanitize(process.Detail), ExitCode: process.ExitCode, Signal: process.Signal);
        }

        if (process.ExitCode == 2)
        {
            return new VmStatusResult(
                VmStatusOutcome.InvocationPreconditionFailure,
                Detail: "VM status invocation was rejected",
                ExitCode: process.ExitCode);
        }

        if (process.ExitCode is not (0 or 1))
        {
            return new VmStatusResult(
                VmStatusOutcome.OperationalFailure,
                Detail: "VM status invocation failed",
                ExitCode: process.ExitCode);
        }

        var parsed = McmStatusParser.Parse(process.Stdout);
        if (parsed.Outcome != VmStatusOutcome.Ok)
        {
            return new VmStatusResult(parsed.Outcome, Detail: Sanitize(parsed.Detail), ExitCode: process.ExitCode);
        }

        if (!string.Equals(parsed.Snapshot!.Name, requestedName, StringComparison.Ordinal))
        {
            return new VmStatusResult(
                VmStatusOutcome.InvalidOutput,
                Detail: "VM status identifies a different VM",
                ExitCode: process.ExitCode);
        }

        var expectedPass = process.ExitCode == 0;
        if ((parsed.Snapshot.Result == "PASS") != expectedPass)
        {
            return new VmStatusResult(
                VmStatusOutcome.InvalidOutput,
                Detail: "VM status result conflicts with its exit code",
                ExitCode: process.ExitCode);
        }

        return process.ExitCode == 0
            ? new VmStatusResult(VmStatusOutcome.Ok, parsed.Snapshot, ExitCode: process.ExitCode)
            : new VmStatusResult(
                VmStatusOutcome.OperationalFailure,
                parsed.Snapshot,
                "VM status reported an operational failure",
                process.ExitCode);
    }

    private async Task<OperationLockLease> AcquireOperationLockAsync(
        string name,
        CancellationToken cancellationToken)
    {
        OperationLockEntry entry;
        lock (_operationLocksSync)
        {
            if (!_operationLocks.TryGetValue(name, out entry!))
            {
                entry = new OperationLockEntry();
                _operationLocks.Add(name, entry);
            }

            entry.References++;
        }

        try
        {
            await entry.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new OperationLockLease(this, name, entry);
        }
        catch
        {
            ReleaseReference(name, entry, releaseSemaphore: false);
            throw;
        }
    }

    private void ReleaseReference(string name, OperationLockEntry entry, bool releaseSemaphore)
    {
        if (releaseSemaphore)
        {
            entry.Semaphore.Release();
        }

        lock (_operationLocksSync)
        {
            entry.References--;
            if (entry.References == 0)
            {
                _operationLocks.Remove(name);
                entry.Semaphore.Dispose();
            }
        }
    }

    private static bool ValidOptions(McmProviderOptions options) =>
        options.ClientExecutablePath is { Length: > 0 and <= 4096 }
        && Path.IsPathFullyQualified(options.ClientExecutablePath)
        && options.LimaExecutablePath is { Length: > 0 and <= 4096 }
        && Path.IsPathFullyQualified(options.LimaExecutablePath)
        && options.HomeDirectory is { Length: > 0 and <= 4096 }
        && Path.IsPathFullyQualified(options.HomeDirectory);

    private static bool ValidName(string name) => name is { Length: > 0 and <= 63 }
        && name[0] != '-'
        && name.All(static character => char.IsAsciiLetterOrDigit(character) || character == '-');

    private static VmStatusResult Result(VmStatusOutcome outcome, string? detail) =>
        new(outcome, Detail: Sanitize(detail));

    private static string? Sanitize(string? detail) => detail is null ? null : StatusText.Sanitize(detail);

    private sealed class OperationLockEntry
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
        public int References { get; set; }
    }

    private sealed class OperationLockLease(
        McmVmProvider owner,
        string name,
        OperationLockEntry entry) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            owner.ReleaseReference(name, entry, releaseSemaphore: true);
        }
    }
}
