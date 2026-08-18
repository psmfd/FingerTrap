using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace FakePi;

/// <summary>
/// Scripted stdio double for <c>pi --mode rpc</c> — the conformance-test
/// seam for <c>PiRpcClient</c>, as CannedHandler is for the status
/// providers (ADR-0025 decision 1). Runs a JSON script (path in argv[0])
/// as a sequence of steps, so each test supplies its own transcript
/// without touching this source.
/// </summary>
/// <remarks>
/// Steps (one key per object): <c>waitForLine</c> (block until a stdin
/// line containing the substring arrives; remembers the line's <c>id</c>
/// for <c>{{lastId}}</c>), <c>writeLine</c> (LF-terminated stdout write
/// with <c>{{lastId}}</c> / <c>{{env:NAME}}</c> substitution),
/// <c>writeRaw</c> (exact bytes, no newline appended — CRLF and
/// unterminated-line framing cases), <c>writeStderrLine</c>,
/// <c>delayMs</c>, <c>ignoreSigterm</c>, <c>waitForEof</c>, and
/// <c>exit</c>. All stdout/stderr writes are raw UTF-8 with explicit
/// terminators — never <see cref="Console.WriteLine()"/>, whose newline
/// is platform-dependent.
/// </remarks>
internal static class Program
{
    private static readonly List<PosixSignalRegistration> Registrations = [];

    private static string? _lastId;

    private static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("usage: FakePi <script.json>");
            return 64;
        }

        using var stdout = Console.OpenStandardOutput();
        using var stderr = Console.OpenStandardError();
        using var stdin = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8);

        using var script = JsonDocument.Parse(File.ReadAllBytes(args[0]));
        foreach (var step in script.RootElement.EnumerateArray())
        {
            var exit = RunStep(step, stdin, stdout, stderr);
            if (exit is { } code)
            {
                return code;
            }
        }

        return 0;
    }

    private static int? RunStep(JsonElement step, StreamReader stdin, Stream stdout, Stream stderr)
    {
        if (step.TryGetProperty("waitForLine", out var waitFor))
        {
            var needle = waitFor.GetString() ?? string.Empty;
            while (true)
            {
                var line = stdin.ReadLine();
                if (line is null)
                {
                    // EOF while a line was still expected: fail loudly so
                    // the driving test sees a nonzero exit, not a hang.
                    return 65;
                }

                RememberId(line);
                if (line.Contains(needle, StringComparison.Ordinal))
                {
                    return null;
                }
            }
        }

        if (step.TryGetProperty("writeLine", out var writeLine))
        {
            Write(stdout, Substitute(writeLine.GetString() ?? string.Empty) + "\n");
            return null;
        }

        if (step.TryGetProperty("writeRaw", out var writeRaw))
        {
            Write(stdout, Substitute(writeRaw.GetString() ?? string.Empty));
            return null;
        }

        if (step.TryGetProperty("writeStderrLine", out var writeStderr))
        {
            Write(stderr, Substitute(writeStderr.GetString() ?? string.Empty) + "\n");
            return null;
        }

        if (step.TryGetProperty("delayMs", out var delay))
        {
            Thread.Sleep(delay.GetInt32());
            return null;
        }

        if (step.TryGetProperty("ignoreSigterm", out var ignore) && ignore.GetBoolean())
        {
            if (!OperatingSystem.IsWindows())
            {
                Registrations.Add(PosixSignalRegistration.Create(
                    PosixSignal.SIGTERM,
                    context => context.Cancel = true));
            }

            return null;
        }

        if (step.TryGetProperty("waitForEof", out var eof) && eof.GetBoolean())
        {
            while (stdin.ReadLine() is { } line)
            {
                RememberId(line);
            }

            return null;
        }

        if (step.TryGetProperty("exit", out var exitCode))
        {
            return exitCode.GetInt32();
        }

        throw new InvalidOperationException($"unknown fake-pi step: {step.GetRawText()}");
    }

    private static void RememberId(string line)
    {
        try
        {
            using var parsed = JsonDocument.Parse(line);
            if (parsed.RootElement.ValueKind == JsonValueKind.Object
                && parsed.RootElement.TryGetProperty("id", out var id)
                && id.ValueKind == JsonValueKind.String)
            {
                _lastId = id.GetString();
            }
        }
        catch (JsonException)
        {
            // Non-JSON stdin lines carry no id; leave the last one intact.
        }
    }

    private static string Substitute(string template)
    {
        var result = template.Replace("{{lastId}}", _lastId ?? string.Empty, StringComparison.Ordinal);
        while (true)
        {
            var start = result.IndexOf("{{env:", StringComparison.Ordinal);
            if (start < 0)
            {
                return result;
            }

            var end = result.IndexOf("}}", start, StringComparison.Ordinal);
            if (end < 0)
            {
                return result;
            }

            var name = result[(start + 6)..end];
            var value = Environment.GetEnvironmentVariable(name) ?? string.Empty;
            result = string.Concat(result.AsSpan(0, start), value, result.AsSpan(end + 2));
        }
    }

    private static void Write(Stream stream, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        stream.Write(bytes);
        stream.Flush();
    }
}
