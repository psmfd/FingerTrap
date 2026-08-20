namespace FingerTrap.Sidecar.PiRpc;

/// <summary>
/// The child's <c>hello</c> frame advertised an RPC protocol newer than this
/// supervisor speaks. A typed refusal naming both versions (psmfd/pi#56, FT
/// #148): the client takes the child down on this state, because proceeding
/// against an incompatible protocol produces undefined wire behavior rather
/// than clean errors.
/// </summary>
/// <remarks>
/// A distinct type for the same reason <see cref="PiProcessExitedException"/>
/// is one: callers must be able to tell "this pi is too new for this
/// FingerTrap" (an install/pin mismatch the operator fixes by upgrading)
/// apart from a child that died.
/// </remarks>
public sealed class PiProtocolMismatchException : Exception
{
    public PiProtocolMismatchException(int childProtocol, int supportedProtocol)
        : base($"pi speaks RPC protocol {childProtocol}; this FingerTrap supports protocol {supportedProtocol} — upgrade FingerTrap or pin an older pi")
    {
        ChildProtocol = childProtocol;
        SupportedProtocol = supportedProtocol;
    }

    /// <summary>The protocol the child's hello frame advertised.</summary>
    public int ChildProtocol { get; }

    /// <summary>The protocol this supervisor implements.</summary>
    public int SupportedProtocol { get; }
}
