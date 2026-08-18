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

public sealed record RpcPromptRequest(string SessionId, string Message);

/// <summary>
/// The prompt ack (asynchronous relative to the prompt's own streamed
/// events — see docs/rpc-contract.md). <paramref name="Error"/> is the
/// contract's plain-string error, paired with success.
/// </summary>
public sealed record RpcPromptResult(bool Success, string? Error);

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
