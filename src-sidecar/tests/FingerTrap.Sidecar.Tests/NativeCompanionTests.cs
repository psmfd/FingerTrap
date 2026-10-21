using System.Diagnostics;
using System.Runtime.InteropServices;
using FingerTrap.Sidecar.Processes;
using Xunit;

namespace FingerTrap.Sidecar.Tests;

public sealed class NativeCompanionTests
{
    private const string ChildLibraryVariable = "FT_TEST_STALE_PTY_COMPANION";

    [Fact]
    public async Task Replacement_StaleCompanion_FailsClosed()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix companion ABI; Windows uses ConPTY");
        var token = TestContext.Current.CancellationToken;
        var library = Environment.GetEnvironmentVariable(ChildLibraryVariable);
        if (library is not null)
        {
            // This resolver exists only in a separate test process with no prior native imports.
            NativeLibrary.SetDllImportResolver(typeof(Porta.Pty.PtyProvider).Assembly,
                (name, _, _) => name == "libporta_pty" ? NativeLibrary.Load(library) : IntPtr.Zero);
            using var fixture = new ManagedLaunchFixture();
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                ChildSpawnCoordinator.RunAsync(() => Porta.Pty.PtyProvider.SpawnAsync(new()
                {
                    App = fixture.Executable, Cwd = fixture.Cwd, Rows = 24, Cols = 80,
                    EnvironmentMode = Porta.Pty.EnvironmentMode.Replace,
                }, token), token));
            Assert.Contains("lacks replacement support", exception.Message, StringComparison.Ordinal);
            return;
        }

        using var parentFixture = new ManagedLaunchFixture();
        var source = Path.Combine(parentFixture.Root, "legacy.c");
        library = Path.Combine(parentFixture.Root, OperatingSystem.IsMacOS() ? "legacy.dylib" : "legacy.so");
        await File.WriteAllTextAsync(source,
            "typedef struct { int fd; int pid; int error; } result;\n"
            + "__attribute__((visibility(\"default\"))) result pty_spawn(void) { return (result){-1,-1,13}; }\n", token);
        var compiler = new ProcessStartInfo(OperatingSystem.IsMacOS() ? "/usr/bin/clang" : "/usr/bin/cc")
        {
            UseShellExecute = false,
        };
        foreach (var argument in new[] { OperatingSystem.IsMacOS() ? "-dynamiclib" : "-shared", "-fPIC", "-o", library, source })
        {
            compiler.ArgumentList.Add(argument);
        }

        using (var build = ChildProcessLauncher.Start(compiler)!)
        {
            await build.WaitForExitAsync(token).WaitAsync(TimeSpan.FromSeconds(20), token);
            Assert.Equal(0, build.ExitCode);
        }

        var childStart = new ProcessStartInfo(Environment.ProcessPath!)
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
        };
        if (Path.GetFileNameWithoutExtension(Environment.ProcessPath) == "dotnet")
        {
            childStart.ArgumentList.Add(typeof(NativeCompanionTests).Assembly.Location);
        }

        childStart.ArgumentList.Add("--filter-method");
        childStart.ArgumentList.Add("*Replacement_StaleCompanion_FailsClosed");
        childStart.Environment[ChildLibraryVariable] = library;
        using var child = ChildProcessLauncher.Start(childStart)!;
        var stdout = child.StandardOutput.ReadToEndAsync(token);
        var stderr = child.StandardError.ReadToEndAsync(token);
        try
        {
            await child.WaitForExitAsync(token).WaitAsync(TimeSpan.FromSeconds(30), token);
            Assert.True(child.ExitCode == 0, (await stdout) + (await stderr));
        }
        finally
        {
            if (!child.HasExited)
            {
                child.Kill(entireProcessTree: true);
                await child.WaitForExitAsync(CancellationToken.None);
            }
        }
    }
}
