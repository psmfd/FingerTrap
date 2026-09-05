using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace FakeMcm;

internal static partial class Program
{
    private static readonly List<PosixSignalRegistration> Registrations = [];
    private static readonly JsonSerializerOptions ScenarioJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static async Task<int> Main(string[] args)
    {
        if (Environment.GetEnvironmentVariable("FAKEMCM_GRANDCHILD") == "1")
        {
            IgnoreSigterm();
            var pidFile = Environment.GetEnvironmentVariable("FAKEMCM_PID_FILE");
            if (!string.IsNullOrEmpty(pidFile))
            {
                await File.WriteAllTextAsync(pidFile, Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            await Task.Delay(Timeout.InfiniteTimeSpan);
            return 0;
        }

        var executable = Environment.ProcessPath
            ?? throw new InvalidOperationException("process path is unavailable");
        var scenarioPath = executable + ".scenario.json";
        if (!File.Exists(scenarioPath))
        {
            await Console.Error.WriteAsync("missing fake-mcm scenario");
            return 66;
        }

        var scenario = JsonSerializer.Deserialize<Scenario>(
            await File.ReadAllTextAsync(scenarioPath),
            ScenarioJsonOptions)
            ?? throw new InvalidOperationException("invalid fake-mcm scenario");

        if (scenario.IgnoreSigterm)
        {
            IgnoreSigterm();
        }

        Process? grandchild = null;
        if (scenario.SpawnGrandchild)
        {
            var start = new ProcessStartInfo(executable)
            {
                UseShellExecute = false,
            };
            start.Environment["FAKEMCM_GRANDCHILD"] = "1";
            if (!string.IsNullOrEmpty(scenario.PidFile))
            {
                start.Environment["FAKEMCM_PID_FILE"] = scenario.PidFile;
            }

            grandchild = Process.Start(start);
        }

        if (scenario.ReportInvocation)
        {
            var inputProbe = Marshal.AllocHGlobal(1);
            bool stdinEof;
            try
            {
                stdinEof = NativeRead(0, inputProbe, 1) <= 0;
            }
            finally
            {
                Marshal.FreeHGlobal(inputProbe);
            }
            var report = JsonSerializer.Serialize(new
            {
                args,
                environment = Environment.GetEnvironmentVariables()
                    .Keys.Cast<object>()
                    .Select(static key => key.ToString())
                    .Where(static key => key is not null)
                    .Order(StringComparer.Ordinal),
                home = Environment.GetEnvironmentVariable("HOME"),
                path = Environment.GetEnvironmentVariable("PATH"),
                locale = Environment.GetEnvironmentVariable("LC_ALL"),
                stdinEof,
            });
            await WriteAsync(Console.OpenStandardOutput(), report);
        }
        else
        {
            var stdoutTask = WritePayloadAsync(Console.OpenStandardOutput(), scenario.Stdout, scenario.StdoutBytes, 'O');
            var stderrTask = WritePayloadAsync(Console.OpenStandardError(), scenario.Stderr, scenario.StderrBytes, 'E');
            await Task.WhenAll(stdoutTask, stderrTask);
        }

        if (scenario.DelayMs > 0)
        {
            await Task.Delay(scenario.DelayMs);
        }

        if (scenario.Signal > 0)
        {
            _ = NativeKill(Environment.ProcessId, scenario.Signal);
        }

        GC.KeepAlive(grandchild);
        return scenario.ExitCode;
    }

    private static void IgnoreSigterm()
    {
        if (!OperatingSystem.IsWindows())
        {
            Registrations.Add(PosixSignalRegistration.Create(
                PosixSignal.SIGTERM,
                static context => context.Cancel = true));
        }
    }

    private static async Task WritePayloadAsync(Stream stream, string? text, int count, char fill)
    {
        if (text is not null)
        {
            await WriteAsync(stream, text);
        }

        if (count > 0)
        {
            var chunk = Encoding.UTF8.GetBytes(new string(fill, Math.Min(count, 4096)));
            var remaining = count;
            while (remaining > 0)
            {
                var length = Math.Min(remaining, chunk.Length);
                await stream.WriteAsync(chunk.AsMemory(0, length));
                await stream.FlushAsync();
                remaining -= length;
            }
        }
    }

    private static async Task WriteAsync(Stream stream, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        await stream.WriteAsync(bytes);
        await stream.FlushAsync();
    }

    [LibraryImport("libc", EntryPoint = "read", SetLastError = true)]
    private static partial nint NativeRead(int fileDescriptor, nint buffer, nuint count);

    [LibraryImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static partial int NativeKill(int processId, int signal);

    private sealed record Scenario
    {
        public string? Stdout { get; init; }
        public string? Stderr { get; init; }
        public int StdoutBytes { get; init; }
        public int StderrBytes { get; init; }
        public int ExitCode { get; init; }
        public int Signal { get; init; }
        public int DelayMs { get; init; }
        public bool IgnoreSigterm { get; init; }
        public bool SpawnGrandchild { get; init; }
        public string? PidFile { get; init; }
        public bool ReportInvocation { get; init; }
    }
}
