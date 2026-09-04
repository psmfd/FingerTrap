using FingerTrap.Sidecar.Abstractions;
using FingerTrap.Sidecar.Vm;
using Xunit;

namespace FingerTrap.Sidecar.Tests;

public sealed class McmVmProviderTests
{
    private const string TestEpoch = "test-epoch";
    private const string ValidStatus = """{"schema":1,"name":"devbox","exists":true,"state":"running","reachable":true,"stamp_present":true,"provisioned_profile":"personal","configured_profile":"personal","drift":false,"needs_provision":false,"expertise_write_configured":false,"expertise_token":null,"errors":0,"warnings":0,"result":"PASS"}""";
    private const string FailedStatus = """{"schema":1,"name":"devbox","exists":true,"state":"running","reachable":false,"stamp_present":false,"provisioned_profile":null,"configured_profile":"personal","drift":null,"needs_provision":true,"expertise_write_configured":false,"expertise_token":null,"errors":1,"warnings":1,"result":"FAIL"}""";

    [Fact]
    public async Task GetStatus_ValidAuthorizedFake_ReturnsSnapshot()
    {
        if (OperatingSystem.IsWindows()) return;
        using var fake = FakeMcmShim.Create(new { stdout = ValidStatus, exitCode = 0 });
        var authorization = Authorization(fake, "devbox");
        var gate = RegisteredGate(authorization);
        var provider = new McmVmProvider(
            new McmProviderOptions(fake.ExecutablePath, fake.LimaPath, AppContext.BaseDirectory),
            gate,
            isSupportedPlatform: static () => true);

        var result = await provider.GetStatusAsync(
            new VmStatusRequest("devbox"), authorization.Nonce, TestContext.Current.CancellationToken);

        Assert.Equal(VmStatusOutcome.Ok, result.Outcome);
        Assert.Equal("running", result.Snapshot?.State);
    }

    [Fact]
    public async Task GetStatus_NonMacPlatform_ReturnsUnsupportedWithoutPathOrNonceAccess()
    {
        var provider = new McmVmProvider(
            new McmProviderOptions("/does/not/exist", "/also/missing", "/missing"),
            new VmLaunchAuthorizationGate(TimeProvider.System, TestEpoch),
            isSupportedPlatform: static () => false);
        var result = await provider.GetStatusAsync(
            new VmStatusRequest("devbox"), "unregistered", TestContext.Current.CancellationToken);

        Assert.Equal(VmStatusOutcome.Unsupported, result.Outcome);
    }

    [Fact]
    public async Task GetStatus_ReplayedAuthorization_NeverLaunchesAgain()
    {
        if (OperatingSystem.IsWindows()) return;
        using var fake = FakeMcmShim.Create(new { stdout = ValidStatus, exitCode = 0 });
        var authorization = Authorization(fake, "devbox");
        var provider = new McmVmProvider(
            new McmProviderOptions(fake.ExecutablePath, fake.LimaPath, AppContext.BaseDirectory),
            RegisteredGate(authorization),
            isSupportedPlatform: static () => true);

        Assert.Equal(VmStatusOutcome.Ok, (await provider.GetStatusAsync(
            new("devbox"), authorization.Nonce, TestContext.Current.CancellationToken)).Outcome);
        Assert.Equal(VmStatusOutcome.Unauthorized, (await provider.GetStatusAsync(
            new("devbox"), authorization.Nonce, TestContext.Current.CancellationToken)).Outcome);
    }

    [Fact]
    public async Task GetStatus_ExitOneReturnsOperationalFailureWithValidatedSnapshot()
    {
        if (OperatingSystem.IsWindows()) return;
        using var fake = FakeMcmShim.Create(new { stdout = FailedStatus, exitCode = 1 });
        var authorization = Authorization(fake, "devbox");
        var provider = new McmVmProvider(
            new McmProviderOptions(fake.ExecutablePath, fake.LimaPath, AppContext.BaseDirectory),
            RegisteredGate(authorization),
            isSupportedPlatform: static () => true);

        var result = await provider.GetStatusAsync(
            new("devbox"), authorization.Nonce, TestContext.Current.CancellationToken);

        Assert.Equal(VmStatusOutcome.OperationalFailure, result.Outcome);
        Assert.Equal("FAIL", result.Snapshot?.Result);
        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public async Task GetStatus_SnapshotForDifferentVmIsRejected()
    {
        if (OperatingSystem.IsWindows()) return;
        using var fake = FakeMcmShim.Create(new
        {
            stdout = ValidStatus.Replace("devbox", "other", StringComparison.Ordinal),
            exitCode = 0,
        });
        var authorization = Authorization(fake, "devbox");
        var provider = new McmVmProvider(
            new McmProviderOptions(fake.ExecutablePath, fake.LimaPath, AppContext.BaseDirectory),
            RegisteredGate(authorization),
            isSupportedPlatform: static () => true);

        var result = await provider.GetStatusAsync(
            new("devbox"), authorization.Nonce, TestContext.Current.CancellationToken);
        Assert.Equal(VmStatusOutcome.InvalidOutput, result.Outcome);
    }

    [Fact]
    public async Task GetStatus_ExitTwoAndUnexpectedExitRemainDistinct()
    {
        if (OperatingSystem.IsWindows()) return;
        using var preconditionFake = FakeMcmShim.Create(new { exitCode = 2 });
        var preconditionAuthorization = Authorization(preconditionFake, "devbox");
        var preconditionProvider = new McmVmProvider(
            new McmProviderOptions(preconditionFake.ExecutablePath, preconditionFake.LimaPath, AppContext.BaseDirectory),
            RegisteredGate(preconditionAuthorization),
            isSupportedPlatform: static () => true);
        var precondition = await preconditionProvider.GetStatusAsync(
            new("devbox"), preconditionAuthorization.Nonce, TestContext.Current.CancellationToken);
        Assert.Equal(VmStatusOutcome.InvocationPreconditionFailure, precondition.Outcome);

        using var operationalFake = FakeMcmShim.Create(new { exitCode = 7 });
        var operationalAuthorization = Authorization(operationalFake, "devbox");
        var operationalProvider = new McmVmProvider(
            new McmProviderOptions(operationalFake.ExecutablePath, operationalFake.LimaPath, AppContext.BaseDirectory),
            RegisteredGate(operationalAuthorization),
            isSupportedPlatform: static () => true);
        var operational = await operationalProvider.GetStatusAsync(
            new("devbox"), operationalAuthorization.Nonce, TestContext.Current.CancellationToken);
        Assert.Equal(VmStatusOutcome.OperationalFailure, operational.Outcome);
    }

    [Fact]
    public async Task GetStatus_TrustFailureConsumesAuthorizationWithoutRefund()
    {
        if (OperatingSystem.IsWindows()) return;
        using var fake = FakeMcmShim.Create(new { stdout = ValidStatus });
        var authorization = Authorization(fake, "devbox") with
        {
            Client = fake.Identity(fake.ExecutablePath) with { Sha256 = new string('0', 64) },
        };
        var provider = new McmVmProvider(
            new McmProviderOptions(fake.ExecutablePath, fake.LimaPath, AppContext.BaseDirectory),
            RegisteredGate(authorization),
            isSupportedPlatform: static () => true);

        var first = await provider.GetStatusAsync(
            new("devbox"), authorization.Nonce, TestContext.Current.CancellationToken);
        var replay = await provider.GetStatusAsync(
            new("devbox"), authorization.Nonce, TestContext.Current.CancellationToken);
        Assert.Equal(VmStatusOutcome.InvocationPreconditionFailure, first.Outcome);
        Assert.Equal(VmStatusOutcome.Unauthorized, replay.Outcome);
    }

    [Fact]
    public async Task GetStatus_SerializesSameNameButAllowsDifferentNamesToOverlap()
    {
        if (OperatingSystem.IsWindows()) return;
        using var fake = FakeMcmShim.Create(new { exitCode = 0 });

        var first = Authorization(fake, "devbox");
        var second = Authorization(fake, "devbox");
        var serializedRunner = new ProbeRunner();
        var serializedProvider = new McmVmProvider(
            new McmProviderOptions(fake.ExecutablePath, fake.LimaPath, AppContext.BaseDirectory),
            RegisteredGate(first, second), serializedRunner, isSupportedPlatform: static () => true);
        var firstTask = serializedProvider.GetStatusAsync(new("devbox"), first.Nonce, TestContext.Current.CancellationToken);
        await serializedRunner.WaitForCallsAsync(1);
        var secondTask = serializedProvider.GetStatusAsync(new("devbox"), second.Nonce, TestContext.Current.CancellationToken);
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.Equal(1, serializedRunner.Calls);
        serializedRunner.Release();
        await Task.WhenAll(firstTask, secondTask);

        var devbox = Authorization(fake, "devbox");
        var other = Authorization(fake, "other");
        var parallelRunner = new ProbeRunner();
        var parallelProvider = new McmVmProvider(
            new McmProviderOptions(fake.ExecutablePath, fake.LimaPath, AppContext.BaseDirectory),
            RegisteredGate(devbox, other), parallelRunner, isSupportedPlatform: static () => true);
        var devboxTask = parallelProvider.GetStatusAsync(new("devbox"), devbox.Nonce, TestContext.Current.CancellationToken);
        var otherTask = parallelProvider.GetStatusAsync(new("other"), other.Nonce, TestContext.Current.CancellationToken);
        await parallelRunner.WaitForCallsAsync(2);
        parallelRunner.Release();
        await Task.WhenAll(devboxTask, otherTask);
    }

    private static VmLaunchAuthorizationGate RegisteredGate(params VmLaunchAuthorization[] authorizations)
    {
        var gate = new VmLaunchAuthorizationGate(TimeProvider.System, TestEpoch);
        foreach (var authorization in authorizations)
        {
            Assert.True(gate.Register(authorization, out _));
        }

        return gate;
    }

    private sealed class ProbeRunner : IMcmProcessRunner
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public async Task<McmProcessResult> RunAsync(
            McmProcessRequest request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            await _release.Task.WaitAsync(cancellationToken);
            var name = request.Arguments[2];
            var status = ValidStatus.Replace("devbox", name, StringComparison.Ordinal);
            return new McmProcessResult(
                McmProcessOutcome.Exited, System.Text.Encoding.UTF8.GetBytes(status), [], true, ExitCode: 0);
        }

        public void Release() => _release.TrySetResult();

        public async Task WaitForCallsAsync(int expected)
        {
            for (var attempt = 0; attempt < 100; attempt++)
            {
                if (Calls >= expected) return;
                await Task.Delay(10, TestContext.Current.CancellationToken);
            }

            throw new TimeoutException($"expected {expected} runner calls, observed {Calls}");
        }
    }

    private static VmLaunchAuthorization Authorization(FakeMcmShim fake, string name) => new(
        Guid.NewGuid().ToString("N"), TestEpoch, DateTimeOffset.UtcNow.AddMinutes(1),
        fake.Identity(fake.ExecutablePath), fake.Identity(fake.LimaPath),
        McmProcessContract.InvocationRevision, McmProcessContract.EnvironmentRevision,
        McmProcessContract.StatusOperation, name);
}
