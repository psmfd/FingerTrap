using FingerTrap.Sidecar.Abstractions;
using FingerTrap.Sidecar.Ipc;
using FingerTrap.Sidecar.Pty;
using FingerTrap.Sidecar.Sessions;
using FingerTrap.Sidecar.Settings;
using FingerTrap.Sidecar.Status;
using Nerdbank.Streams;
using Newtonsoft.Json.Serialization;
using StreamJsonRpc;

// stdout is owned by the JSON-RPC framing — any Console.Write here corrupts
// the stream. All status output goes to stderr (ADR-0002).
Console.Error.WriteLine("fingertrap-sidecar: starting");

// Inbound frames pass a Content-Length ceiling before StreamJsonRpc sees
// them (ADR-0022): the declared length is attacker-influenceable once
// provider payloads share the channel, and nothing in the RPC stack bounds
// it. A breach is connection-fatal (IOException), same posture as any other
// framing corruption.
var stdio = FullDuplexStream.Splice(
    new FrameCeilingStream(Console.OpenStandardInput()),
    Console.OpenStandardOutput());

var formatter = new JsonMessageFormatter();
formatter.JsonSerializer.ContractResolver = new CamelCasePropertyNamesContractResolver();

var handler = new HeaderDelimitedMessageHandler(stdio, stdio, formatter);

// Settings are read exactly once per process (N-1, #52). A missing file is a
// supported state and yields defaults; a file that exists but cannot be used
// is fatal here rather than silently ignored, because a typo'd settings file
// that appears to work is the failure ADR-0013 already rejected once for
// FINGERTRAP_PANE_KIND. Reported on stderr — stdout is the RPC framing.
FingerTrapSettings settings;
try
{
    settings = SettingsLoader.Load();
}
catch (SettingsException ex)
{
    Console.Error.WriteLine($"fingertrap-sidecar: {ex.Message}");
    return 1;
}

// Single platform-agnostic PtyService backed by Porta.Pty (ADR-0008);
// platform branching now lives inside the vendored library.
await using var pty = new PtyService(settings.Pi);
// Native RPC panes (FT-2 slice 2, ADR-0025): one pi --mode rpc child per
// attached session, relayed thin through the surface's IRpcPaneSink.
await using var rpcPanes = new FingerTrap.Sidecar.PiRpc.RpcPaneService(settings.Pi);
// Provider tokens live only here, delivered by the shell over stdin
// (credentials/set, ADR-0022); status providers read them per-request.
var credentials = new CredentialCache();
await using var status = new StatusService([
    new GitHubStatusProvider(credentials, settings.Status?.Github),
    new AdoStatusProvider(credentials, settings.Status?.Ado),
    new LocalGitStatusProvider(settings.Status?.Git),
]);
// Session browser (FT-2 slice 5, ADR-0026): read-only scans of pi's session
// store and the worktree extension's durable orphan signals.
var sessionStore = new SessionStore();
var worktreeReconciler = new WorktreeReconciler();
using var surface = new RpcSurface(
    pty, settings.Pane, credentials, status, settings.Keybindings, rpcPanes,
    sessionStore, worktreeReconciler);

var rpc = new JsonRpc(handler);
rpc.AddLocalRpcTarget(surface, new JsonRpcTargetOptions
{
    MethodNameTransform = CommonMethodNameTransforms.CamelCase,
    // vscode-jsonrpc's RequestType1<T> with the default ParameterStructures.auto
    // serializes a single object arg as `params: {...}` (named). With this
    // flag, StreamJsonRpc deserializes the entire params object into the
    // method's single non-CancellationToken parameter, instead of trying to
    // match each top-level key as a separate named argument.
    UseSingleObjectParameterDeserialization = true,
});
surface.AttachRpc(rpc);
rpcPanes.AttachSink(surface);
rpc.StartListening();
status.Start();

Console.Error.WriteLine("fingertrap-sidecar: listening on stdio");
await rpc.Completion;
Console.Error.WriteLine("fingertrap-sidecar: rpc completion");
return 0;
