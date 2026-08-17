using FingerTrap.Sidecar.Abstractions;
using FingerTrap.Sidecar.Settings;

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
    /// settings, then <see cref="DefaultKindEnvVar"/>, then
    /// <see cref="HostDefault"/>.
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
    internal static PaneKind Parse(string? requested, PaneSettings? settings = null)
    {
        var value = requested;
        if (string.IsNullOrWhiteSpace(value))
        {
            // Settings outrank the environment (N-1, #52). The env var is kept
            // as a lower layer rather than retired: it stays the natural fit
            // for ephemeral overrides — CI, or a one-off launch — where
            // editing a file and putting it back is the wrong shape.
            value = settings?.DefaultKind;
        }

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

    /// <summary>Inverse of <see cref="Parse"/> for the wire (settings/get).</summary>
    internal static string ToWire(PaneKind kind) => kind switch
    {
        PaneKind.Pi => "pi",
        PaneKind.Shell => "shell",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}

/// <summary>
/// Effective settings the UI needs (FT-1 slice 3, ADR-0021). The WebView
/// cannot read the settings file or the environment, so the sidecar — the
/// single reader with the single precedence chain — answers with what it
/// already resolved. <paramref name="PaneDefaultKind"/> is the kind an
/// unqualified spawn gets (request → settings → env → host default);
/// <paramref name="Keybindings"/> is the operator's override map, served
/// verbatim (empty when unset) — chord semantics are the UI's.
/// </summary>
public sealed record SettingsGetResult(
    string PaneDefaultKind,
    IReadOnlyDictionary<string, string> Keybindings);

public sealed record PtySpawnResult(int Pid);

public sealed record PtyWriteRequest(string SessionId, string DataBase64);

public sealed record PtyResizeRequest(string SessionId, int Cols, int Rows);

/// <summary>
/// Ends a session's process and releases its PTY (FT-1, ADR-0021).
/// Idempotent: killing a session that already exited — or never existed — is
/// success, because the caller's intent ("this session must not be running")
/// already holds. The close-tab path must not race the process's own exit.
/// </summary>
public sealed record PtyKillRequest(string SessionId);

/// <summary>
/// Shell-originated (ADR-0022): written by the Rust shell into the sidecar's
/// stdin — deliberately a notification, because sidecar stdout is relayed
/// wholesale to the WebView and a notification has no response frame, so no
/// secret-bearing frame ever travels toward the WebView. Has no `api.ts`
/// counterpart by design. A null/empty <paramref name="Token"/> clears the
/// provider. Never log this record.
/// </summary>
public sealed record CredentialsSetNotification(string Provider, string? Token);

/// <summary>
/// Full-state snapshot of every provider (ADR-0022): snapshot-replace, so a
/// dropped notification costs nothing — the next one supersedes it.
/// </summary>
public sealed record StatusSnapshotNotification(
    IReadOnlyList<FingerTrap.Sidecar.Abstractions.ProviderSnapshot> Providers);

public sealed record PtyOutputNotification(string SessionId, string DataBase64);

public sealed record PtyExitNotification(string SessionId, int ExitCode);
