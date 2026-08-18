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
    /// Longest identity string carried into a truncation marker. pi's ids
    /// are UUIDs and its UI method names are short literals; anything
    /// larger is dropped so the marker itself can never approach the
    /// ceiling it exists to enforce.
    /// </summary>
    private const int MaxPreservedIdentityChars = 256;

    /// <summary>
    /// Passes an event through unchanged, or — over the ceiling —
    /// substitutes a small well-formed <c>rpc_event_truncated</c> marker
    /// naming the original type and byte count. Never byte-truncates the
    /// JSON itself: a truncated JSON string is not JSON, and would break
    /// the UI's parser instead of just losing content. An oversized
    /// <c>extension_ui_request</c> additionally keeps its <c>id</c> and
    /// <c>method</c> (FT-2 slice 4): interactive dialog requests must stay
    /// answerable — pi has no timeout on <c>editor</c> and guard-style
    /// extensions opt out of one — so the host needs the id to send a
    /// <c>cancelled</c> response instead of hanging the turn.
    /// </summary>
    internal static (string Json, bool Truncated) EnforceCeiling(string json, string? type)
    {
        if (Encoding.UTF8.GetByteCount(json) <= MaxNotificationPayloadBytes)
        {
            return (json, false);
        }

        string? originalId = null;
        string? originalMethod = null;
        if (string.Equals(type, "extension_ui_request", StringComparison.Ordinal))
        {
            (originalId, originalMethod) = ExtractUiRequestIdentity(json);
        }

        var marker = JsonSerializer.Serialize(
            new TruncationMarker(
                "rpc_event_truncated", type, Encoding.UTF8.GetByteCount(json), originalId, originalMethod),
            RpcEventGuardJsonContext.Default.TruncationMarker);
        return (marker, true);
    }

    private static (string? Id, string? Method) ExtractUiRequestIdentity(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? (ReadShortString(document.RootElement, "id"), ReadShortString(document.RootElement, "method"))
                : (null, null);
        }
        catch (JsonException)
        {
            // The pump hands over whatever the supervisor read; an
            // unparseable oversized line still gets the generic marker.
            return (null, null);
        }
    }

    private static string? ReadShortString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.String
            && value.GetString() is { Length: <= MaxPreservedIdentityChars } text
                ? text
                : null;

    internal sealed record TruncationMarker(
        string Type,
        string? OriginalType,
        int OriginalBytes,
        [property: System.Text.Json.Serialization.JsonIgnore(
            Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        string? OriginalId,
        [property: System.Text.Json.Serialization.JsonIgnore(
            Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        string? OriginalMethod);
}

[System.Text.Json.Serialization.JsonSerializable(typeof(RpcEventGuard.TruncationMarker))]
[System.Text.Json.Serialization.JsonSourceGenerationOptions(
    PropertyNamingPolicy = System.Text.Json.Serialization.JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class RpcEventGuardJsonContext : System.Text.Json.Serialization.JsonSerializerContext;
