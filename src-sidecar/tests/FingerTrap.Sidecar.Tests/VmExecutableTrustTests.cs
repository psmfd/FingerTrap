using FingerTrap.Sidecar.Abstractions;
using FingerTrap.Sidecar.Vm;
using Xunit;

namespace FingerTrap.Sidecar.Tests;

public sealed class VmExecutableTrustTests
{
    [Fact]
    public void Construct_IsSafeOnEveryPlatform()
    {
        _ = new VmExecutableTrust();
    }

    [Fact]
    public void Validate_TrustedFixture_Succeeds_ThenDigestReplacementFails()
    {
        if (OperatingSystem.IsWindows()) return;
        using var fake = FakeMcmShim.Create(new { exitCode = 0 });
        var trust = new VmExecutableTrust();
        var identity = fake.Identity(fake.ExecutablePath);

        Assert.True(trust.Validate(identity).Trusted);
        File.AppendAllText(fake.ExecutablePath, "replacement");
        Assert.False(trust.Validate(identity).Trusted);
    }

    [Fact]
    public void Validate_SymlinkOrWritableByOthers_Fails()
    {
        if (OperatingSystem.IsWindows()) return;
        using var fake = FakeMcmShim.Create(new { exitCode = 0 });
        var trust = new VmExecutableTrust();
        var link = fake.ExecutablePath + ".link";
        File.CreateSymbolicLink(link, fake.ExecutablePath);
        Assert.False(trust.Validate(fake.Identity(fake.ExecutablePath) with { Path = link }).Trusted);

        File.SetUnixFileMode(fake.LimaPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.OtherWrite);
        Assert.False(trust.Validate(fake.Identity(fake.LimaPath)).Trusted);
    }

    [Fact]
    public void Validate_DirectoryIsNotAnExecutableRegularFile()
    {
        if (OperatingSystem.IsWindows()) return;
        using var fake = FakeMcmShim.Create(new { exitCode = 0 });
        var directory = Path.GetDirectoryName(fake.ExecutablePath)!;
        var result = new VmExecutableTrust().Validate(
            new VmExecutableIdentity(directory, new string('0', 64)));
        Assert.False(result.Trusted);
    }
}
