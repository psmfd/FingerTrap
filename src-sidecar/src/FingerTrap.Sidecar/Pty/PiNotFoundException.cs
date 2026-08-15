namespace FingerTrap.Sidecar.Pty;

/// <summary>
/// Thrown when a pi pane is requested and no pi executable can be located.
/// </summary>
/// <remarks>
/// A distinct type rather than a bare <see cref="InvalidOperationException"/>
/// so callers can tell "pi is not installed here" — an operator-fixable setup
/// problem with a known remedy — apart from a genuine spawn failure. The
/// message is written to be read by a person in a terminal pane, because that
/// is exactly where it surfaces: the UI renders the spawn error into the
/// terminal it failed to fill.
/// </remarks>
public sealed class PiNotFoundException : Exception
{
    public PiNotFoundException()
        : base("no pi executable found")
    {
    }

    public PiNotFoundException(string message)
        : base(message)
    {
    }

    public PiNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
