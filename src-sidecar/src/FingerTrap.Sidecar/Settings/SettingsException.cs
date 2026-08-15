namespace FingerTrap.Sidecar.Settings;

/// <summary>
/// Thrown when a settings file exists but cannot be used.
/// </summary>
/// <remarks>
/// A distinct type, for the same reason
/// <see cref="Pty.PiNotFoundException"/> is one: this is an
/// operator-fixable configuration problem with a known remedy, not a runtime
/// fault. It surfaces through the same channel — an RPC error rendered into
/// the terminal pane — so the message is written to be read by a person, and
/// always names the offending file.
///
/// Never thrown for an <em>absent</em> file. Absent means "no opinion, use
/// defaults" and is a fully supported state.
/// </remarks>
public sealed class SettingsException : Exception
{
    public SettingsException()
        : base("settings could not be loaded")
    {
    }

    public SettingsException(string message)
        : base(message)
    {
    }

    public SettingsException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
