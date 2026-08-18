using System.Text.Json;

namespace FingerTrap.Sidecar.Tests;

/// <summary>
/// Shared FakePi launch helpers (extracted from RpcPaneServiceTests for
/// the slice-3b round-trip suite): a platform shim that launches FakePi
/// with its script while ignoring the <c>--mode rpc</c> args
/// <see cref="PiRpc.RpcPaneService"/> appends — the seam that lets tests
/// spawn "pi" without a pi.
/// </summary>
internal static class FakePiShim
{
    public static string WriteShim(params string[] steps)
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), $"fakepi-{Guid.NewGuid():N}.json");
        File.WriteAllText(scriptPath, "[" + string.Join(",", steps) + "]");

        var fakePi = FakePiDllPath();
        if (OperatingSystem.IsWindows())
        {
            var cmd = Path.Combine(Path.GetTempPath(), $"fakepi-{Guid.NewGuid():N}.cmd");
            File.WriteAllText(cmd, $"@echo off\r\ndotnet \"{fakePi}\" \"{scriptPath}\"\r\n");
            return cmd;
        }

        var sh = Path.Combine(Path.GetTempPath(), $"fakepi-{Guid.NewGuid():N}.sh");
        File.WriteAllText(sh, $"#!/bin/sh\nexec dotnet \"{fakePi}\" \"{scriptPath}\"\n");
        File.SetUnixFileMode(sh, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return sh;
    }

    public static string FakePiDllPath()
    {
        var baseDir = AppContext.BaseDirectory;
        foreach (var configuration in new[] { "Debug", "Release" })
        {
            var marker = $"{Path.DirectorySeparatorChar}{configuration}{Path.DirectorySeparatorChar}";
            if (!baseDir.Contains(marker, StringComparison.Ordinal))
            {
                continue;
            }

            var candidate = Path.GetFullPath(Path.Combine(
                baseDir, "..", "..", "..", "..", "FakePi", "bin", configuration, "net10.0", "FakePi.dll"));
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException(
            $"FakePi.dll not found relative to test output '{baseDir}' — build src-sidecar/tests/FakePi first");
    }

    public static string Step(string key, string value) =>
        JsonSerializer.Serialize(new Dictionary<string, string> { [key] = value });

    public static string Step(string key, int value) => $"{{\"{key}\":{value}}}";

    public static string Step(string key, bool value) => $"{{\"{key}\":{(value ? "true" : "false")}}}";
}
