using System.Buffers;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace FingerTrap.Sidecar.PiRpc;

/// <summary>
/// Supervisor for one <c>pi --mode rpc</c> child (ADR-0025 decision 1): one
/// process per attached session, spawned directly — not through the PTY
/// service; this is not a terminal. Implements the supervisor discipline
/// from docs/rpc-contract.md: LF-only JSONL codec, id-correlated
/// pending-request map with per-request timeout, response-vs-event demux,
/// <c>agent_settled</c> turn tracking, all-in-flight rejection on child
/// death, fail-fast sends against a dead child, and the
/// stdin-EOF → SIGTERM → SIGKILL shutdown ladder.
/// </summary>
/// <remarks>
/// <para>
/// Demux mirrors the reference client (<c>rpc-client.ts</c>, verified
/// against the pinned pi): a line with <c>type: "response"</c> and a
/// <em>known pending</em> id resolves that request; a <c>type: "hello"</c>
/// line — the child's first stdout write since <c>v0.84.2-psmfd.1</c> — is
/// consumed by the supervisor as the ready gate (see
/// <see cref="WaitForHelloAsync"/>) and, like the reference client, never
/// published as an event; every other parseable line — including a response
/// whose id is unknown, already timed out, or duplicated — is published as
/// an event. Unparseable lines are ignored.
/// </para>
/// <para>
/// Events flow through one always-on bounded channel, live from spawn.
/// This is a deliberate divergence from <see cref="Pty.PtyService"/>'s
/// multicast-event shape: a channel buffers from the first line, so the
/// listener-before-prompt race the contract warns about cannot exist —
/// there is no subscription moment to order against. The channel is
/// bounded wait-mode, so backpressure propagates to the child via the OS
/// pipe instead of accumulating in sidecar memory; it never replays
/// history and completes (without error) once the child is gone and the
/// buffered tail is drained. <c>agent_settled</c> waiters are signalled
/// before the event is queued, so a full channel can never deadlock turn
/// tracking.
/// </para>
/// <para>
/// This type takes no credential dependency (<c>Ipc.CredentialCache</c> is
/// deliberately not referenced): the child environment is exactly
/// <em>inherited + <see cref="PiRpcClientOptions.EnvironmentOverrides"/></em>,
/// pinned by the conformance suite.
/// </para>
/// </remarks>
internal sealed partial class PiRpcClient : IAsyncDisposable
{
    private const int Sigterm = 15;

    /// <summary>
    /// The RPC protocol this supervisor implements. A hello frame
    /// advertising a higher number is refused — capability additions never
    /// bump this (docs/rpc-contract.md, "Wire framing").
    /// </summary>
    internal const int SupportedRpcProtocol = 1;

    /// <summary>
    /// How long after process exit to wait for the pipe readers to drain
    /// before building the exit fault — bounds the window in which the
    /// stderr tail completes.
    /// </summary>
    private static readonly TimeSpan ExitDrainGrace = TimeSpan.FromSeconds(1);

    private readonly Process _process;
    private readonly PiRpcClientOptions _options;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<PiRpcResponse>> _pending =
        new(StringComparer.Ordinal);
    private readonly Channel<PiRpcEvent> _events;
    private readonly SemaphoreSlim _stdinLock = new(1, 1);
    private readonly BoundedTailBuffer _stderr;
    private readonly TaskCompletionSource<PiProcessExitedException> _exited =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<TaskCompletionSource> _settledWaiters = [];
    private readonly TaskCompletionSource<PiHelloInfo?> _helloReady =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenSource _helloGraceCts = new();
    private readonly Task _stdoutTask;
    private readonly Task _stderrTask;
    private readonly Task _monitorTask;

    private long _nextRequestId;
    private volatile PiHelloInfo? _helloInfo;
    private volatile Exception? _helloFault;
    private volatile PiProcessExitedException? _exitFault;
    private volatile Exception? _streamFault;
    private volatile bool _shuttingDown;
    private bool _disposed;

    private PiRpcClient(Process process, PiRpcClientOptions options)
    {
        _process = process;
        _options = options;
        _stderr = new BoundedTailBuffer(options.MaxStderrBytes);
        _events = Channel.CreateBounded<PiRpcEvent>(new BoundedChannelOptions(options.EventChannelCapacity)
        {
            SingleWriter = true,
            SingleReader = false,
            FullMode = BoundedChannelFullMode.Wait,
        });
        _stdoutTask = Task.Run(ReadStdoutAsync);
        _stderrTask = Task.Run(ReadStderrAsync);
        _monitorTask = Task.Run(MonitorExitAsync);
        _ = Task.Run(WatchHelloGraceAsync);
    }

    /// <summary>
    /// All non-response lines from the child, verbatim (thin relay,
    /// ADR-0025 decision 3) — with one carve-out: the <c>hello</c>
    /// handshake frame is consumed by the supervisor (ready gate +
    /// capability discovery, mirroring the reference client) and never
    /// appears here; surface it via <see cref="Hello"/> /
    /// <see cref="WaitForHelloAsync"/> instead. The wire tap still records
    /// hello — it fires before demux. Completes once the child has exited
    /// and the buffered tail is consumed; child death itself is observed
    /// via <see cref="Exited"/>, not as a channel error.
    /// </summary>
    public ChannelReader<PiRpcEvent> Events => _events.Reader;

    /// <summary>
    /// The parsed hello frame, or null before it arrives / on a legacy
    /// (pre-hello) pin. Synchronous read of already-resolved state — use
    /// <see cref="WaitForHelloAsync"/> to await the handshake outcome.
    /// </summary>
    public PiHelloInfo? Hello => _helloInfo;

    /// <summary>The child's OS process id — for the crash-reap registry (#124).</summary>
    public int ProcessId => _process.Id;

    /// <summary>
    /// The child's start time in UTC — the PID-reuse discriminator the reap
    /// registry keys on (#124). Read at spawn, while the process is alive.
    /// </summary>
    public DateTime ProcessStartTimeUtc => _process.StartTime.ToUniversalTime();

    /// <summary>
    /// Completes when the child is fully down — exit observed, pipes
    /// drained, all in-flight requests rejected — yielding the fault that
    /// rejected them (exit code + stderr tail; a clean shutdown yields
    /// code 0).
    /// </summary>
    public Task<PiProcessExitedException> Exited => _exited.Task;

    /// <summary>
    /// Spawns the child. Since pi <c>v0.84.2-psmfd.1</c> the protocol HAS a
    /// ready signal and version handshake — the <c>hello</c> first stdout
    /// line (psmfd/pi#56) — surfaced via <see cref="WaitForHelloAsync"/>;
    /// this method itself still returns immediately after the OS spawn, so
    /// callers that skip the gate keep today's behavior (a child that exits
    /// before ever responding surfaces as
    /// <see cref="PiProcessExitedException"/> from the first send). Spawn
    /// uses <see cref="ProcessStartInfo.ArgumentList"/> exclusively — one
    /// element per argument, never a concatenated string — with
    /// <see cref="ProcessStartInfo.UseShellExecute"/> false (required for
    /// redirection; also means no shell ever parses the arguments).
    /// </summary>
    public static PiRpcClient Start(PiRpcClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var startInfo = new ProcessStartInfo(options.ExecutablePath)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in options.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (options.WorkingDirectory is not null)
        {
            startInfo.WorkingDirectory = options.WorkingDirectory;
        }

        if (options.EnvironmentOverrides is not null)
        {
            foreach (var (key, value) in options.EnvironmentOverrides)
            {
                startInfo.Environment[key] = value;
            }
        }

        var process = new Process { StartInfo = startInfo };
        process.Start();
        return new PiRpcClient(process, options);
    }

    /// <summary>
    /// Sends one command and awaits its correlated response. The response
    /// may be asynchronous relative to its own events (<c>prompt</c>'s ack
    /// is post-preflight and races its first streaming events) —
    /// correlation is by id, never by ordering. Fails fast with
    /// <see cref="PiProcessExitedException"/> against a dead child instead
    /// of hanging; a request timing out (<see cref="PiRpcClientOptions.RequestTimeout"/>)
    /// throws <see cref="TimeoutException"/> and removes the pending entry,
    /// so a late response falls through the demux to <see cref="Events"/>.
    /// </summary>
    /// <param name="type">The command type, e.g. <c>get_state</c>.</param>
    /// <param name="parametersJson">
    /// Optional JSON object whose properties are merged into the command
    /// line beside <c>id</c> and <c>type</c>. Must not carry those two
    /// reserved keys.
    /// </param>
    public async Task<PiRpcResponse> SendAsync(
        string type,
        string? parametersJson = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(type);
        if (_shuttingDown)
        {
            throw new InvalidOperationException("pi rpc client is shutting down");
        }

        ThrowIfExited();

        var id = $"req_{Interlocked.Increment(ref _nextRequestId)}";
        var pending = new TaskCompletionSource<PiRpcResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending.TryAdd(id, pending);

        // Recheck after the add: a child-exit drain that raced the TryAdd
        // may have enumerated _pending before this entry appeared. Either
        // the drain swept it (pending is already faulted) or it survived —
        // in which case this recheck self-rejects it. Standard
        // check-add-recheck; no global lock needed.
        if (_exitFault is { } fault)
        {
            _pending.TryRemove(id, out _);
            throw fault;
        }

        try
        {
            var payload = BuildCommandJson(id, type, parametersJson);
            try
            {
                await WriteStdinAsync(payload, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception writeFailure) when (writeFailure is IOException or ObjectDisposedException)
            {
                // A broken stdin pipe usually means the child just died —
                // surface the richer exit fault when it materializes.
                throw await ResolveWriteFailureAsync(writeFailure).ConfigureAwait(false);
            }

            return await pending.Task
                .WaitAsync(_options.RequestTimeout, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            // Non-negotiable: WaitAsync's timeout abandons the await but
            // does not complete the underlying task — without this remove,
            // a timed-out entry leaks in _pending for the child's lifetime.
            _pending.TryRemove(id, out _);
        }
    }

    /// <summary>
    /// Writes one uncorrelated stdin message: caller-supplied id, no
    /// pending-map entry, no response awaited — the shape
    /// <c>extension_ui_response</c> requires (docs/rpc-contract.md: a
    /// special stdin message, not a command; pi emits no response frame,
    /// and an unknown/expired id is silently dropped). Do not route such
    /// messages through <see cref="SendAsync"/>: its await would only ever
    /// end in <see cref="TimeoutException"/>. Completes once the bytes are
    /// flushed; failures surface exactly like <see cref="SendAsync"/>'s
    /// write path (fail-fast against a dead child, richer exit fault when
    /// the pipe broke because the child died).
    /// </summary>
    /// <param name="id">
    /// Echoed verbatim — for <c>extension_ui_response</c> this is the
    /// original request's pi-assigned id, never a fresh <c>req_N</c>.
    /// </param>
    public async Task SendMessageAsync(
        string type,
        string id,
        string? parametersJson = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(type);
        ArgumentException.ThrowIfNullOrEmpty(id);
        if (_shuttingDown)
        {
            throw new InvalidOperationException("pi rpc client is shutting down");
        }

        ThrowIfExited();

        var payload = BuildCommandJson(id, type, parametersJson);
        try
        {
            await WriteStdinAsync(payload, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception writeFailure) when (writeFailure is IOException or ObjectDisposedException)
        {
            throw await ResolveWriteFailureAsync(writeFailure).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Conformance-golden seam (#139): writes one raw stdin line verbatim —
    /// deliberately malformed input included — so the recorder can pin pi's
    /// <c>command: "parse"</c> error response. Bypasses command
    /// construction and correlation entirely (pi's reply, carrying no
    /// known id, surfaces via <see cref="Events"/>); the wire tap still
    /// observes the line. Test-only by convention; no product call sites.
    /// </summary>
    internal Task SendRawLineForConformanceAsync(string line, CancellationToken cancellationToken = default) =>
        WriteStdinAsync(line, cancellationToken);

    /// <summary>
    /// Completes on the next <c>agent_settled</c> — the sole turn boundary
    /// (docs/rpc-contract.md). Register the waiter <em>before</em> sending
    /// the prompt it should observe, as the reference <c>promptAndWait</c>
    /// does; registration is independent of <see cref="Events"/>
    /// consumption, so an undrained channel cannot miss the boundary.
    /// Faults with <see cref="PiProcessExitedException"/> if the child dies
    /// before settling.
    /// </summary>
    public Task WaitForSettledAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ThrowIfExited();

        var waiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_settledWaiters)
        {
            _settledWaiters.Add(waiter);
        }

        // Same recheck as SendAsync: an exit drain racing the add either
        // swept the waiter or this self-faults it.
        if (_exitFault is { } fault)
        {
            waiter.TrySetException(fault);
        }

        return waiter.Task.WaitAsync(cancellationToken);
    }

    /// <summary>
    /// The ready gate (psmfd/pi#56, FT#148): completes when the handshake
    /// outcome is known. Returns the parsed hello frame; or null when
    /// <see cref="PiRpcClientOptions.HelloGrace"/> expired first — a legacy
    /// (pre-hello) pin, in which case the client works exactly as it did
    /// before the handshake existed. Throws
    /// <see cref="PiProtocolMismatchException"/> when the child advertised
    /// a newer protocol (the client has already taken the child down), and
    /// the <see cref="PiProcessExitedException"/> exit fault when the child
    /// died before hello — the distinguishable spawn-failure class that
    /// grounds ADR-0026's missing-cwd exit-1 case, which dies before the
    /// JSONL channel is up. The outcome is memoized: one shared task
    /// resolves once, so later calls return instantly; the token only
    /// abandons this caller's wait, never the class-lifetime grace timer.
    /// </summary>
    public async Task<PiHelloInfo?> WaitForHelloAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await _helloReady.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Capability advertisement replaces probing (docs/rpc-contract.md,
    /// "Wire framing"). Synchronous read of already-resolved state — false
    /// before hello arrives and in legacy mode, deliberately never an
    /// implicit await that could block or pay grace latency from an
    /// unexpected call site.
    /// </summary>
    public bool Supports(string capability) => _helloInfo?.Supports(capability) ?? false;

    /// <summary>
    /// The contract's shutdown ladder: close stdin (EOF — the clean
    /// trigger; pi flushes and exits 0), then SIGTERM (exit 143, no
    /// flush), then <see cref="Process.Kill(bool)"/> with the entire
    /// process tree so pi's own subprocesses are not orphaned. Windows has
    /// no SIGTERM — the ladder there is EOF → tree-kill, and exit-code-143
    /// semantics do not apply. Idempotent; completes once in-flight
    /// requests are rejected and <see cref="Events"/> is completed.
    /// </summary>
    public async Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        _shuttingDown = true;

        await _stdinLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _process.StandardInput.Close();
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
        {
            // The child may already be gone; EOF on a dead pipe is fine.
        }
        finally
        {
            _stdinLock.Release();
        }

        var exited = await WaitForExitWithinAsync(_options.EofGrace, cancellationToken).ConfigureAwait(false);
        if (!exited && !OperatingSystem.IsWindows())
        {
            TrySigterm();
            exited = await WaitForExitWithinAsync(_options.SigtermGrace, cancellationToken).ConfigureAwait(false);
        }

        if (!exited)
        {
            TryKillTree();
        }

        await _exited.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            await ShutdownAsync(budget.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKillTree();
        }

        try
        {
            await _monitorTask.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // Monitor is stuck on a pipe that never closed; abandon it —
            // process teardown will reclaim the handles.
        }

        _process.Dispose();
        _stdinLock.Dispose();
        TryCancelHelloGrace();
        _helloGraceCts.Dispose();
    }

    // The BCL has no API to send SIGTERM to a child on any platform —
    // Process.Kill is SIGKILL-equivalent everywhere — so the contract's
    // middle shutdown stage is a raw libc kill(2) on Unix. ESRCH (child
    // already gone) is success-equivalent and deliberately ignored.
    [LibraryImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static partial int NativeKill(int pid, int signal);

    private void TrySigterm()
    {
        try
        {
            // Signalling takes the raw pid, so bound the PID-reuse window
            // with a liveness check on the retained Process object
            // immediately before. Never resolve the target via
            // Process.GetProcessById — the retained object is the only
            // identity this class ever signals.
            if (!_process.HasExited)
            {
                _ = NativeKill(_process.Id, Sigterm);
            }
        }
        catch (InvalidOperationException)
        {
            // No process associated / already reaped — nothing to signal.
        }
    }

    private void TryKillTree()
    {
        try
        {
            _process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            // Already exited or already reaped.
        }
    }

    private async Task<bool> WaitForExitWithinAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            await _process.WaitForExitAsync(cancellationToken)
                .WaitAsync(timeout, CancellationToken.None)
                .ConfigureAwait(false);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    private async Task ReadStdoutAsync()
    {
        try
        {
            await foreach (var line in JsonlCodec.ReadLinesAsync(
                _process.StandardOutput.BaseStream, _options.MaxLineBytes).ConfigureAwait(false))
            {
                _options.WireTap?.Invoke(PiWireDirection.FromChild, line);
                await DispatchLineAsync(line).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            // Ceiling breach or pipe failure: connection-fatal for this
            // child only. Record the cause for the exit fault and take the
            // child down; the monitor owns the rest of the teardown.
            _streamFault ??= ex;
            TryKillTree();
        }
    }

    private async Task DispatchLineAsync(string line)
    {
        PiWireEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize(line, PiWireJsonContext.Default.PiWireEnvelope);
        }
        catch (JsonException)
        {
            envelope = null;
        }

        if (envelope is null)
        {
            // Unparseable child lines are ignored client-side, per contract.
            return;
        }

        if (string.Equals(envelope.Type, "hello", StringComparison.Ordinal))
        {
            // Consumed by the supervisor, never published as an event —
            // the reference client withholds it from listeners too. The
            // wire tap already recorded the line (it fires before demux).
            OnHello(line);
            return;
        }

        if (string.Equals(envelope.Type, "response", StringComparison.Ordinal)
            && envelope.Id is not null
            && _pending.TryRemove(envelope.Id, out var pending))
        {
            pending.TrySetResult(new PiRpcResponse
            {
                Command = envelope.Command,
                Success = envelope.Success ?? false,
                Error = envelope.Error,
                Json = line,
            });
            return;
        }

        if (string.Equals(envelope.Type, "agent_settled", StringComparison.Ordinal))
        {
            SignalSettled();
        }

        await _events.Writer.WriteAsync(new PiRpcEvent(envelope.Type, line)).ConfigureAwait(false);
    }

    private void OnHello(string line)
    {
        var info = ParseHello(line);
        if (info.Protocol > SupportedRpcProtocol)
        {
            // Fault the ready task FIRST so the typed refusal
            // deterministically wins the race against MonitorExitAsync's
            // generic exit fault once the kill below lands.
            var refusal = new PiProtocolMismatchException(info.Protocol, SupportedRpcProtocol);
            _helloFault = refusal;
            _helloReady.TrySetException(refusal);
            TryCancelHelloGrace();
            TryKillTree();
            return;
        }

        _helloInfo = info;
        _helloReady.TrySetResult(info);
        TryCancelHelloGrace();
    }

    // Field-tolerant by design: the envelope parse already proved the line
    // is a JSON object, and a hello missing a field degrades to defaults
    // (protocol 0 is accepted) rather than faulting the channel.
    private static PiHelloInfo ParseHello(string line)
    {
        using var document = JsonDocument.Parse(line);
        var root = document.RootElement;

        var piVersion = root.TryGetProperty("piVersion", out var version)
            && version.ValueKind == JsonValueKind.String
                ? version.GetString() ?? string.Empty
                : string.Empty;

        var protocol = root.TryGetProperty("protocol", out var protocolValue)
            && protocolValue.ValueKind == JsonValueKind.Number
            && protocolValue.TryGetInt32(out var parsed)
                ? parsed
                : 0;

        var capabilities = new List<string>();
        if (root.TryGetProperty("capabilities", out var caps)
            && caps.ValueKind == JsonValueKind.Array)
        {
            foreach (var capability in caps.EnumerateArray())
            {
                if (capability.ValueKind == JsonValueKind.String)
                {
                    capabilities.Add(capability.GetString()!);
                }
            }
        }

        return new PiHelloInfo(piVersion, protocol, capabilities);
    }

    private async Task WatchHelloGraceAsync()
    {
        try
        {
            await Task.Delay(_options.HelloGrace, _helloGraceCts.Token).ConfigureAwait(false);
            // Grace expired with no hello: a pre-hello pin. Legacy mode.
            _helloReady.TrySetResult(null);
        }
        catch (OperationCanceledException)
        {
            // The handshake resolved some other way (hello, refusal, exit).
        }
    }

    // The grace CTS can be raced by a monitor task DisposeAsync abandoned;
    // cancelling a disposed CTS throws, and none of the callers care.
    private void TryCancelHelloGrace()
    {
        try
        {
            _helloGraceCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task ReadStderrAsync()
    {
        var buffer = new byte[4096];
        try
        {
            while (true)
            {
                var read = await _process.StandardError.BaseStream
                    .ReadAsync(buffer.AsMemory())
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                _stderr.Append(buffer.AsSpan(0, read));
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            // Pipe torn down with the child; the captured tail stands.
        }
    }

    private async Task MonitorExitAsync()
    {
        await _process.WaitForExitAsync().ConfigureAwait(false);

        // Bounded drain so the exit fault carries the complete stderr tail
        // and the event channel gets every line the child managed to write.
        _ = await Task.WhenAny(
            Task.WhenAll(_stdoutTask, _stderrTask),
            Task.Delay(ExitDrainGrace)).ConfigureAwait(false);

        var fault = new PiProcessExitedException(_process.ExitCode, _stderr.Snapshot(), _streamFault);
        _exitFault = fault;

        // Fourth fan-out target beside _pending/_settledWaiters/Events: a
        // child dead before hello resolves the ready gate with the exit
        // fault ("died before hello"). TrySet* no-ops when the handshake
        // already resolved (hello received, legacy grace, or refusal).
        _helloReady.TrySetException(fault);
        TryCancelHelloGrace();

        foreach (var entry in _pending.ToArray())
        {
            if (_pending.TryRemove(entry.Key, out var pending))
            {
                pending.TrySetException(fault);
            }
        }

        TaskCompletionSource[] waiters;
        lock (_settledWaiters)
        {
            waiters = [.. _settledWaiters];
            _settledWaiters.Clear();
        }

        foreach (var waiter in waiters)
        {
            waiter.TrySetException(fault);
        }

        _events.Writer.TryComplete();
        _exited.TrySetResult(fault);
    }

    private void SignalSettled()
    {
        TaskCompletionSource[] waiters;
        lock (_settledWaiters)
        {
            waiters = [.. _settledWaiters];
            _settledWaiters.Clear();
        }

        foreach (var waiter in waiters)
        {
            waiter.TrySetResult();
        }
    }

    private void ThrowIfExited()
    {
        // The protocol refusal outranks the exit fault it causes (the
        // refusal path kills the child, so both are set afterwards): a
        // caller that bypassed WaitForHelloAsync still gets the typed
        // refusal, not the generic kill diagnostics.
        if (_helloFault is { } refusal)
        {
            throw refusal;
        }

        if (_exitFault is { } fault)
        {
            throw fault;
        }
    }

    private async Task<Exception> ResolveWriteFailureAsync(Exception writeFailure)
    {
        try
        {
            return await _exited.Task.WaitAsync(ExitDrainGrace).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return writeFailure;
        }
    }

    private async Task WriteStdinAsync(string json, CancellationToken cancellationToken)
    {
        var payload = JsonlCodec.EncodeLine(json);
        await _stdinLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_shuttingDown)
            {
                throw new InvalidOperationException("pi rpc client is shutting down");
            }

            // Tap before the write, inside the lock: the child can answer a
            // flushed line before this task resumes, and a tap fired after
            // the flush could then record the answer ahead of its own
            // question. Send-attempt-ordered is the recording invariant.
            _options.WireTap?.Invoke(PiWireDirection.ToChild, json);

            var stdin = _process.StandardInput.BaseStream;
            await stdin.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
            await stdin.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _stdinLock.Release();
        }
    }

    private static string BuildCommandJson(string id, string type, string? parametersJson)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("id", id);
            writer.WriteString("type", type);

            if (parametersJson is not null)
            {
                using var parameters = JsonDocument.Parse(parametersJson);
                if (parameters.RootElement.ValueKind != JsonValueKind.Object)
                {
                    throw new ArgumentException("parametersJson must be a JSON object", nameof(parametersJson));
                }

                foreach (var property in parameters.RootElement.EnumerateObject())
                {
                    if (property.NameEquals("id") || property.NameEquals("type"))
                    {
                        throw new ArgumentException(
                            $"parametersJson must not carry the reserved '{property.Name}' key",
                            nameof(parametersJson));
                    }

                    property.WriteTo(writer);
                }
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>
    /// Fixed-capacity tail capture: appends overwrite the oldest bytes
    /// once full, so a chatty child costs a constant, not a leak, and the
    /// snapshot is always the most recent — the only part error enrichment
    /// ever needs.
    /// </summary>
    private sealed class BoundedTailBuffer
    {
        private readonly object _lock = new();
        private readonly byte[] _buffer;
        private int _next;
        private bool _wrapped;

        public BoundedTailBuffer(int capacity)
        {
            _buffer = new byte[capacity];
        }

        public void Append(ReadOnlySpan<byte> data)
        {
            lock (_lock)
            {
                if (data.Length >= _buffer.Length)
                {
                    data[^_buffer.Length..].CopyTo(_buffer);
                    _next = 0;
                    _wrapped = true;
                    return;
                }

                var head = Math.Min(data.Length, _buffer.Length - _next);
                data[..head].CopyTo(_buffer.AsSpan(_next));
                if (head < data.Length)
                {
                    data[head..].CopyTo(_buffer);
                    _wrapped = true;
                }

                _next = (_next + data.Length) % _buffer.Length;
                if (_next < head)
                {
                    _wrapped = true;
                }
            }
        }

        public string Snapshot()
        {
            lock (_lock)
            {
                if (!_wrapped)
                {
                    return Encoding.UTF8.GetString(_buffer, 0, _next);
                }

                var ordered = new byte[_buffer.Length];
                var tailLength = _buffer.Length - _next;
                Array.Copy(_buffer, _next, ordered, 0, tailLength);
                Array.Copy(_buffer, 0, ordered, tailLength, _next);
                return Encoding.UTF8.GetString(ordered);
            }
        }
    }
}
