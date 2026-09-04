using System.Security.Cryptography;
using System.Text.Json;
using FingerTrap.Sidecar.Abstractions;

namespace FingerTrap.Sidecar.Tests;

internal sealed class FakeMcmShim : IDisposable
{
    private readonly string _directory;

    private FakeMcmShim(string directory, string executablePath, string limaPath)
    {
        _directory = directory;
        ExecutablePath = executablePath;
        LimaPath = limaPath;
    }

    public string ExecutablePath { get; }
    public string LimaPath { get; }

    public static FakeMcmShim Create(object scenario)
    {
        if (OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException();
        }

        var sourceDirectory = FindBuildDirectory();
        var fixtureRoot = Path.Combine(AppContext.BaseDirectory, "vm-fixtures");
        Directory.CreateDirectory(fixtureRoot);
        var directory = Path.Combine(fixtureRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        foreach (var source in Directory.EnumerateFiles(sourceDirectory, "FakeMcm*"))
        {
            File.Copy(source, Path.Combine(directory, Path.GetFileName(source)));
        }

        var executableName = OperatingSystem.IsWindows() ? "FakeMcm.exe" : "FakeMcm";
        var executable = Path.Combine(directory, executableName);
        File.WriteAllText(executable + ".scenario.json", JsonSerializer.Serialize(scenario));
        File.SetUnixFileMode(executable,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        var lima = Path.Combine(directory, "fake-limactl");
        File.Copy(executable, lima);
        File.SetUnixFileMode(lima,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return new FakeMcmShim(directory, executable, lima);
    }

    public VmExecutableIdentity Identity(string path)
    {
        if (!Path.GetFullPath(path).StartsWith(_directory + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException("identity path is outside the fake fixture", nameof(path));
        }

        return new(path, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant());
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // A failed containment test may briefly retain an executable handle.
        }
    }

    private static string FindBuildDirectory()
    {
        var baseDirectory = AppContext.BaseDirectory;
        foreach (var configuration in new[] { "Debug", "Release" })
        {
            var marker = $"{Path.DirectorySeparatorChar}{configuration}{Path.DirectorySeparatorChar}";
            if (!baseDirectory.Contains(marker, StringComparison.Ordinal))
            {
                continue;
            }

            var candidate = Path.GetFullPath(Path.Combine(
                baseDirectory, "..", "..", "..", "..", "FakeMcm", "bin", configuration, "net10.0"));
            if (File.Exists(Path.Combine(candidate, OperatingSystem.IsWindows() ? "FakeMcm.exe" : "FakeMcm")))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"FakeMcm output not found relative to '{baseDirectory}'");
    }
}
