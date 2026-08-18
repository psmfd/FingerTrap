using System.Text.Json;
using System.Text.Json.Serialization;

namespace FingerTrap.Sidecar.PiRpc;

/// <summary>
/// The minimal typed slice of a pi RPC wire line: just the discriminator
/// fields the supervisor's demux needs (docs/rpc-contract.md, "Wire
/// framing"). Everything else — command payloads and the entire open
/// <c>AgentSessionEvent</c> union — deliberately stays untyped raw JSON the
/// caller re-parses on demand; slice 1 passes events through verbatim, and
/// modeling a large evolving union here would only add drift surface
/// against unpinned pi versions.
/// </summary>
internal sealed record PiWireEnvelope
{
    public string? Id { get; init; }

    public string? Type { get; init; }

    public string? Command { get; init; }

    public bool? Success { get; init; }

    public string? Error { get; init; }
}

/// <summary>
/// One non-response line from the child, passed through verbatim.
/// <paramref name="Json"/> is the raw decoded line (a plain string, no
/// backing-document lifetime coupling), so it can sit in the event channel
/// indefinitely; consumers re-parse the line-sized payload on demand.
/// </summary>
internal sealed record PiRpcEvent(string? Type, string Json);

/// <summary>
/// A resolved command response. <see cref="Command"/> and
/// <see cref="Error"/> are preserved together as the contract requires —
/// the error shape has no structured code, so the pairing is the only
/// diagnostic identity an error carries. <see cref="Json"/> is the full raw
/// line for callers needing the command-specific payload.
/// </summary>
internal sealed record PiRpcResponse
{
    public string? Command { get; init; }

    public bool Success { get; init; }

    public string? Error { get; init; }

    public required string Json { get; init; }
}

/// <summary>
/// Source-generated serializer context for the envelope — the always-hot
/// per-line decode path; avoids reflection metadata resolution per line and
/// stays trim/AOT-safe should the sidecar ever publish trimmed.
/// </summary>
[JsonSerializable(typeof(PiWireEnvelope))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class PiWireJsonContext : JsonSerializerContext;
