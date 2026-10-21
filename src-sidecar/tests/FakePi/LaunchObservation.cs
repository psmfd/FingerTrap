using System.Collections;
using System.Diagnostics;
using System.Text.Json;

namespace FakePi;

/// <summary>Reports keys and selected non-secret fixture values, never arbitrary environment values.</summary>
internal static class LaunchObservation
{
    public static void Write(string path, bool includeChild)
    {
        string[] names = ["HOME", "USERPROFILE", "PI_CODING_AGENT_DIR", "PI_CODING_AGENT_SESSION_DIR",
            "TMPDIR", "FINGERTRAP_PROFILE_ID", "FT_ISOLATION_SENTINEL", "FT_CHILD_EMPTY", "FT_CHILD_UNICODE"];
        var values = names.ToDictionary(name => name, Environment.GetEnvironmentVariable, StringComparer.Ordinal);
        var keys = Environment.GetEnvironmentVariables().Keys.Cast<string>().Order(StringComparer.Ordinal).ToArray();
        if (includeChild)
        {
            var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false };
            start.ArgumentList.Add("--observe");
            start.ArgumentList.Add(path + ".child");
            using var child = Process.Start(start)!;
            if (!child.WaitForExit(5000))
            {
                child.Kill(entireProcessTree: true);
                child.WaitForExit();
                throw new TimeoutException("observation child did not exit");
            }

            if (child.ExitCode != 0) throw new InvalidOperationException("observation child failed");
        }

        var observation = JsonSerializer.Serialize(new { pid = Environment.ProcessId, cwd = Environment.CurrentDirectory, keys, values });
        File.WriteAllText(path + ".new", observation);
        File.Move(path + ".new", path, overwrite: true);
    }
}
