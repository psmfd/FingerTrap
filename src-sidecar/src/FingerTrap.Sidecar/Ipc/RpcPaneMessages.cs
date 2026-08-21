using Newtonsoft.Json.Linq;

namespace FingerTrap.Sidecar.Ipc;

/// <summary>
/// Wire shapes for the native RPC pane surface (FT-2 slice 2, ADR-0025
/// decision 3). Mirrored by the <c>rpc/*</c> entries in
/// <c>src-ui/src/api.ts</c> — the rpc-pairing check in
/// <c>scripts/check.sh</c> counts both sides.
/// </summary>
/// <param name="SessionPath">
/// Session to resume, applied at spawn time (<c>--session</c>) because
/// selection is a spawn-time CLI concern (docs/rpc-contract.md). Present on
/// the wire from slice 2; the session browser (slice 5) is its first
/// setter.
/// </param>
public sealed record RpcSpawnRequest(
    string SessionId,
    string? Cwd = null,
    string? SessionPath = null,
    IReadOnlyDictionary<string, string>? Env = null);

public sealed record RpcKillRequest(string SessionId);

/// <summary>
/// Result of a successful <c>rpc/spawn</c> (FT-2 #150): the pi child's
/// <c>hello</c> handshake, surfaced so the pane header can show which pi the
/// session is running. <paramref name="PiVersion"/> is null on a pre-hello
/// (legacy) pin — the handshake did not arrive within the grace window;
/// <paramref name="Capabilities"/> is empty in that case. Display data only;
/// the header renders it via textContent (ADR-0022).
/// </summary>
public sealed record RpcSpawnResult(string? PiVersion, IReadOnlyList<string> Capabilities);

/// <param name="StreamingBehavior">
/// <c>"steer"</c> or <c>"followUp"</c> (pi's exact casing), required by pi
/// when a prompt is sent mid-stream; omitted when idle. A plain string,
/// not an enum — pi types it as a TS string-literal union, and the
/// Newtonsoft formatter would serialize a C# enum numerically. Note the
/// null default is load-bearing convention: Newtonsoft does not apply
/// positional-record constructor defaults for omitted JSON properties
/// (Newtonsoft.Json#2765), so request records here must never declare a
/// non-<c>default(T)</c> default.
/// </param>
public sealed record RpcPromptRequest(string SessionId, string Message, string? StreamingBehavior = null);

/// <summary>
/// The prompt ack (asynchronous relative to the prompt's own streamed
/// events — see docs/rpc-contract.md). <paramref name="Error"/> is the
/// contract's plain-string error, paired with success.
/// </summary>
public sealed record RpcPromptResult(bool Success, string? Error);

public sealed record RpcSteerRequest(string SessionId, string Message);

public sealed record RpcFollowUpRequest(string SessionId, string Message);

/// <summary>Session-scoped command with no further parameters
/// (abort, get_state, get_session_stats, get_available_models,
/// get_available_thinking_levels).</summary>
public sealed record RpcSessionRequest(string SessionId);

public sealed record RpcSetModelRequest(string SessionId, string Provider, string ModelId);

public sealed record RpcSetThinkingLevelRequest(string SessionId, string Level);

/// <summary>
/// Answers one interactive <c>extension_ui_request</c> dialog —
/// select/confirm/input/editor (FT-2 slice 4). Exactly one of
/// <paramref name="Value"/> / <paramref name="Confirmed"/> /
/// <paramref name="Cancelled"/> is set, mirroring pi's key-discriminated
/// response union; the sidecar stays kind-unaware and forwards whichever
/// key is present. <paramref name="RequestId"/> is the request event's own
/// pi-assigned id echoed back verbatim — never a sidecar-minted
/// <c>req_N</c>. Null defaults are the Newtonsoft.Json#2765 convention
/// (see <see cref="RpcPromptRequest"/>).
/// </summary>
public sealed record RpcExtensionUiResponseRequest(
    string SessionId,
    string RequestId,
    string? Value = null,
    bool? Confirmed = null,
    bool? Cancelled = null);

/// <summary>
/// A pi command's outcome with its envelope stripped: <paramref name="Data"/>
/// is the response's <c>data</c> payload as an opaque parsed token (null
/// when the command returns none), size-ceilinged sidecar-side. Untrusted
/// content throughout — the UI renders text via textContent only
/// (ADR-0022) and never assumes shape beyond the field it reads.
/// </summary>
public sealed record RpcCommandResult(bool Success, string? Error, JToken? Data);

/// <summary>
/// One relayed pi event, verbatim (thin relay). <paramref name="Event"/>
/// is the parsed event object embedded as a native JSON token — never a
/// re-escaped string, whose 20–40% escaping bloat would hollow out
/// <see cref="PiRpc.RpcEventGuard"/>'s ceiling margin. Everything inside
/// it is untrusted content; the UI renders text via textContent only
/// (ADR-0022).
/// </summary>
public sealed record RpcEventNotification(
    string SessionId,
    string? EventType,
    JToken Event,
    bool Truncated);

/// <summary>
/// The pane's pi child is gone. <paramref name="StderrTail"/> is bounded
/// operator-diagnostic text — untrusted (extension logs reroute to
/// stderr); textContent-only rendering per ADR-0022.
/// </summary>
public sealed record RpcExitNotification(string SessionId, int ExitCode, string StderrTail);
