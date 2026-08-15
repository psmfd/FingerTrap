using FingerTrap.Sidecar.Abstractions;

namespace FingerTrap.Sidecar.Ipc;

/// <param name="Kind">
/// <c>"pi"</c> or <c>"shell"</c>, case-insensitive. Absent means "use the host
/// default" — see <see cref="PaneKinds.Parse"/>. An unrecognised value is an
/// error, never a silent fall-back to the default.
/// </param>
public sealed record PtySpawnRequest(
    string SessionId,
    string? Shell,
    string? Cwd,
    int Cols,
    int Rows,
    IReadOnlyDictionary<string, string>? Env,
    string? Kind = null);

/// <summary>
/// Wire-string ↔ <see cref="PaneKind"/> conversion, and the host default.
/// </summary>
internal static class PaneKinds
{
    /// <summary>
    /// Overrides the default pane kind for hosts that want a plain shell.
    /// Interim until the settings system (Native track N-1) lands.
    /// </summary>
    internal const string DefaultKindEnvVar = "FINGERTRAP_PANE_KIND";

    /// <summary>
    /// FingerTrap is the pi Home, so an unqualified pane is a pi pane.
    /// </summary>
    internal const PaneKind HostDefault = PaneKind.Pi;

    /// <summary>
    /// Resolve a wire value to a pane kind: explicit request wins, then
    /// <see cref="DefaultKindEnvVar"/>, then <see cref="HostDefault"/>.
    /// </summary>
    /// <remarks>
    /// The default lives here rather than in the UI because the UI is a
    /// WebView and cannot read process environment. Keeping it sidecar-side
    /// also keeps it unit-testable.
    ///
    /// An unrecognised value throws instead of defaulting. A typo'd
    /// <c>FINGERTRAP_PANE_KIND=pie</c> that silently opened a pi pane would be
    /// indistinguishable from the variable working, which is the failure this
    /// deliberately makes loud.
    /// </remarks>
    internal static PaneKind Parse(string? requested)
    {
        var value = requested;
        if (string.IsNullOrWhiteSpace(value))
        {
            value = Environment.GetEnvironmentVariable(DefaultKindEnvVar);
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return HostDefault;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "pi" => PaneKind.Pi,
            "shell" => PaneKind.Shell,
            _ => throw new ArgumentException(
                $"unknown pane kind '{value}'; expected 'pi' or 'shell'", nameof(requested)),
        };
    }
}

public sealed record PtySpawnResult(int Pid);

public sealed record PtyWriteRequest(string SessionId, string DataBase64);

public sealed record PtyResizeRequest(string SessionId, int Cols, int Rows);

public sealed record PtyOutputNotification(string SessionId, string DataBase64);

public sealed record PtyExitNotification(string SessionId, int ExitCode);
