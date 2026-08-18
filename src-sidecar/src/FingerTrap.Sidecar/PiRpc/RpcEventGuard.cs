using System.Text;
using System.Text.Json;

namespace FingerTrap.Sidecar.PiRpc;

/// <summary>
/// Outbound size guard for relayed pi events. The sidecar→WebView leg has a
/// 4 MB frame ceiling enforced client-side
/// (<c>src-ui/src/transport.ts</c> <c>MAX_FRAME_BYTES</c>, the lockstep
/// pair of <see cref="Ipc.FrameCeilingStream.DefaultMaxFrameBytes"/>), and
/// a breach there kills the app's single shared connection — every pane,
/// not just the offender. The child-side line ceiling is 8 MB, so a
/// legally-sized pi event can still be transport-fatal; this guard is the
/// only thing standing between the two.
/// </summary>
internal static class RpcEventGuard
{
    /// <summary>
    /// Deliberately well under the 4 MB frame ceiling: the margin absorbs
    /// the JSON-RPC envelope and any re-serialization variance, and is a
    /// hard-coded constant reviewed alongside
    /// <see cref="Ipc.FrameCeilingStream.DefaultMaxFrameBytes"/> — never
    /// settings-configurable without moving the whole lockstep pair.
    /// </summary>
    internal const int MaxNotificationPayloadBytes = 3 * 1024 * 1024;

    /// <summary>
    /// Passes an event through unchanged, or — over the ceiling —
    /// substitutes a small well-formed <c>rpc_event_truncated</c> marker
    /// naming the original type and byte count. Never byte-truncates the
    /// JSON itself: a truncated JSON string is not JSON, and would break
    /// the UI's parser instead of just losing content.
    /// </summary>
    internal static (string Json, bool Truncated) EnforceCeiling(string json, string? type)
    {
        if (Encoding.UTF8.GetByteCount(json) <= MaxNotificationPayloadBytes)
        {
            return (json, false);
        }

        var marker = JsonSerializer.Serialize(
            new TruncationMarker("rpc_event_truncated", type, Encoding.UTF8.GetByteCount(json)),
            RpcEventGuardJsonContext.Default.TruncationMarker);
        return (marker, true);
    }

    internal sealed record TruncationMarker(string Type, string? OriginalType, int OriginalBytes);
}

[System.Text.Json.Serialization.JsonSerializable(typeof(RpcEventGuard.TruncationMarker))]
[System.Text.Json.Serialization.JsonSourceGenerationOptions(
    PropertyNamingPolicy = System.Text.Json.Serialization.JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class RpcEventGuardJsonContext : System.Text.Json.Serialization.JsonSerializerContext;
