using System.Collections.ObjectModel;

namespace FingerTrap.Sidecar.Processes;

internal enum ChildEnvironmentMode
{
    Inherit,
    Replace,
}

internal static class ChildEnvironment
{
    public static IReadOnlyDictionary<string, string> Snapshot(
        IReadOnlyDictionary<string, string>? values)
    {
        var result = new Dictionary<string, string>(
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        if (values is not null)
        {
            foreach (var (key, value) in values)
            {
                if (string.IsNullOrEmpty(key) || key.Contains('=', StringComparison.Ordinal)
                    || key.Contains('\0', StringComparison.Ordinal) || value is null
                    || value.Contains('\0', StringComparison.Ordinal) || !result.TryAdd(key, value))
                {
                    throw new ArgumentException("invalid or duplicate child environment entry", nameof(values));
                }
            }
        }

        return new ReadOnlyDictionary<string, string>(result);
    }
}
