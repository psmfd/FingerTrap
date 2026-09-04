using System.Diagnostics;
using System.Buffers;
using System.Collections.Concurrent;
using FingerTrap.Sidecar.Abstractions;
using FingerTrap.Sidecar.Settings;

namespace FingerTrap.Sidecar.Pty;

/// <summary>
/// Adapter that maps <see cref="IPtyService"/> onto Porta.Pty's
/// <see cref="global::Porta.Pty.PtyProvider"/>. Porta.Pty handles
/// platform branching (Mac/Linux/Windows) internally via a non-variadic
/// C shim — see ADR-0008.
/// </summary>
internal sealed class PtyService : IPtyService
{
    private const int ResizeDebounceMs = 50;

    private readonly ConcurrentDictionary<string, Session> _sessions = new(StringComparer.Ordinal);

    private readonly PiSettings? _piSettings;
    private int _disposeStarted;

    /// <param name="piSettings">
    /// Persisted pi configuration (N-1, #52), or null to rely on the
    /// environment and PATH alone. Injected rather than loaded here so the
    /// settings file is read exactly once per process, in Program.cs.
    /// </param>
    public PtyService(PiSettings? piSettings = null)
    {
        _piSettings = piSettings;
    }

    public event EventHandler<PtyOutputEventArgs>? Output;

    public event EventHandler<PtyExitEventArgs>? Exited;

    public async Task<int> SpawnAsync(string sessionId, PtySpawnOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeStarted) != 0, this);

        var shellPath = ResolveExecutable(options.Kind, options.Shell, _piSettings);

        var ptyOptions = new global::Porta.Pty.PtyOptions
        {
            App = shellPath,
            CommandLine = ResolveCommandLine(options.Kind, options.SessionPath),
            Cwd = ResolveCwd(options.Cwd),
            Cols = Math.Max(1, options.Cols),
            Rows = Math.Max(1, options.Rows),
            Environment = options.Env is not null
                ? new Dictionary<string, string>(options.Env)
                : new Dictionary<string, string>(),
        };

        var connection = await global::Porta.Pty.PtyProvider.SpawnAsync(ptyOptions, cancellationToken).ConfigureAwait(false);

        var session = new Session(sessionId, connection);
        if (!_sessions.TryAdd(sessionId, session))
        {
            session.Dispose();
            throw new InvalidOperationException($"session '{sessionId}' is already active");
        }

        connection.ProcessExited += (_, e) =>
        {
            // Signal waiters before disposing the connection; app shutdown
            // keeps the PTY reader alive until the process tree is gone.
            session.MarkExited();
            _sessions.TryRemove(sessionId, out Session? _);
            session.Dispose();

            Exited?.Invoke(this, new PtyExitEventArgs
            {
                SessionId = sessionId,
                ExitCode = e.ExitCode,
            });
        };

        StartReadLoop(session);
        return connection.Pid;
    }

    public async ValueTask WriteAsync(string sessionId, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new InvalidOperationException($"session '{sessionId}' is not active");
        }

        await session.Connection.WriterStream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
        await session.Connection.WriterStream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Resize(string sessionId, int cols, int rows)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.QueueResize(cols, rows);
        }
    }

    public void Close(string sessionId)
    {
        // Kill while the PTY reader is still draining. Process.Kill(true) is
        // shared with whole-app shutdown so shell grandchildren cannot outlive
        // a closed pane; the Porta.Pty kill remains the fallback when process
        // enumeration is unavailable.
        if (_sessions.TryRemove(sessionId, out var session))
        {
            KillTree(session);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeStarted, 1) != 0)
        {
            return;
        }

        var sessions = _sessions.Values.ToArray();
        _sessions.Clear();

        foreach (var session in sessions)
        {
            KillTree(session);
        }

        try
        {
            await Task.WhenAll(sessions.Select(session => session.ExitedTask))
                .WaitAsync(TimeSpan.FromSeconds(2))
                .ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // A broken PTY backend must not make application quit unbounded.
        }

        foreach (var session in sessions)
        {
            session.Dispose();
        }
    }

    private static void KillTree(Session session)
    {
        try
        {
            using var process = Process.GetProcessById(session.Connection.Pid);
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            return;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException
            or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            // Fall through to Porta.Pty's platform-specific direct kill.
        }

        try
        {
            session.Connection.Kill();
        }
        catch
        {
            // Already exited or already reaped.
        }
    }

    /// <summary>
    /// Environment variable naming the pi executable outright. The escape
    /// hatch until the settings system (Native track N-1) exists; a pi
    /// installed somewhere unusual should not require a code change.
    /// </summary>
    internal const string PiPathEnvVar = Executables.PiExecutableResolver.PiPathEnvVar;

    /// <summary>
    /// Map a pane kind onto the executable to spawn.
    /// </summary>
    /// <remarks>
    /// Deliberately asymmetric. Shell resolution ends in a fallback chain
    /// because <em>some</em> shell essentially always exists and guessing is
    /// harmless. pi resolution ends in a <see cref="PiNotFoundException"/>,
    /// because a pi pane that quietly opened a shell would be worse than one
    /// that refused: the operator would be typing at something that is not the
    /// thing they asked for, and the difference is not obvious at a glance.
    /// </remarks>
    internal static string ResolveExecutable(PaneKind kind, string? requested, PiSettings? settings = null) => kind switch
    {
        PaneKind.Pi => ResolvePi(requested, settings),
        PaneKind.Shell => ResolveShell(requested),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "unknown pane kind"),
    };

    /// <summary>
    /// Arguments for the resolved executable, by pane kind.
    /// </summary>
    /// <remarks>
    /// Shell panes get <c>-l</c> (login shell — zsh, bash, sh, and fish all
    /// accept it), the convention Terminal.app, iTerm2, and VS Code follow.
    /// A launchd-started app inherits the bare system <c>PATH</c>, and only a
    /// login shell re-reads the profile files (<c>~/.zprofile</c> on macOS)
    /// where users put <c>PATH</c> additions — a non-login pane shell leaves
    /// user-installed tools unreachable (#77). pi panes are spawned by
    /// resolved absolute path and take no arguments, unless the session
    /// browser is resuming a session (FT-2 slice 5) — then pi gets
    /// <c>--session &lt;path&gt;</c>, mirroring the RPC-pane spawn
    /// (<see cref="PiRpc.RpcPaneService"/>).
    /// </remarks>
    internal static string[] ResolveCommandLine(PaneKind kind, string? sessionPath = null) => kind switch
    {
        PaneKind.Shell => new[] { "-l" },
        PaneKind.Pi when !string.IsNullOrEmpty(sessionPath) => new[] { "--session", sessionPath },
        _ => Array.Empty<string>(),
    };

    /// <summary>
    /// Locate the pi executable: explicit request, then settings, then
    /// <see cref="PiPathEnvVar"/>, then <c>PATH</c>. Delegates to the shared
    /// <see cref="Executables.PiExecutableResolver"/> (FT-2 slice 2) so PTY
    /// panes and native RPC panes cannot diverge on which pi they find.
    /// </summary>
    internal static string ResolvePi(string? requested, PiSettings? settings = null) =>
        Executables.PiExecutableResolver.ResolvePi(requested, settings);

    private static string ResolveShell(string? requested)
    {
        if (!string.IsNullOrEmpty(requested))
        {
            return requested;
        }

        var fromEnv = Environment.GetEnvironmentVariable("SHELL");
        if (!string.IsNullOrEmpty(fromEnv))
        {
            return fromEnv;
        }

        // macOS Catalina+ default is zsh; /bin/bash on macOS is bash 3.2
        // (frozen at GPL2). Linux defaults to bash. /bin/sh is the
        // last-resort fallback present on every POSIX system.
        if (File.Exists("/bin/zsh"))
        {
            return "/bin/zsh";
        }

        if (File.Exists("/bin/bash"))
        {
            return "/bin/bash";
        }

        return "/bin/sh";
    }

    internal static string ResolveCwd(string? requested)
    {
        if (!string.IsNullOrEmpty(requested))
        {
            return requested;
        }

        // Default to the user's home directory so GUI launches (e.g.,
        // macOS `open FingerTrap.app`) don't inherit the app process's
        // cwd ("/"). SpecialFolder.UserProfile returns $HOME on Unix
        // and %USERPROFILE% on Windows. Fall back to CurrentDirectory
        // only if neither is set — a degenerate environment where the
        // spawn would likely fail anyway, but preserving the prior
        // behaviour rather than throwing here keeps the spawn error
        // path visible at the same layer as before.
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return !string.IsNullOrEmpty(home) ? home : Environment.CurrentDirectory;
    }

    private void StartReadLoop(Session session)
    {
        _ = Task.Run(async () =>
        {
            var buffer = ArrayPool<byte>.Shared.Rent(4096);
            try
            {
                while (true)
                {
                    int read;
                    try
                    {
                        read = await session.Connection.ReaderStream
                            .ReadAsync(buffer.AsMemory(), session.Cancellation.Token)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (IOException)
                    {
                        // Linux signals master EOF as EIO (IOException).
                        // macOS signals it as a clean 0-byte read.
                        break;
                    }

                    if (read == 0)
                    {
                        break;
                    }

                    var copy = new byte[read];
                    Buffer.BlockCopy(buffer, 0, copy, 0, read);
                    Output?.Invoke(this, new PtyOutputEventArgs
                    {
                        SessionId = session.Id,
                        Data = copy,
                    });
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        });
    }

    private sealed class Session : IDisposable
    {
        private readonly object _resizeLock = new();
        private int _pendingCols;
        private int _pendingRows;
        private Timer? _resizeTimer;
        private bool _disposed;
        private readonly TaskCompletionSource _exited =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Session(string id, global::Porta.Pty.IPtyConnection connection)
        {
            Id = id;
            Connection = connection;
        }

        public string Id { get; }

        public global::Porta.Pty.IPtyConnection Connection { get; }

        public CancellationTokenSource Cancellation { get; } = new();

        public Task ExitedTask => _exited.Task;

        public void MarkExited() => _exited.TrySetResult();

        public void QueueResize(int cols, int rows)
        {
            lock (_resizeLock)
            {
                if (_disposed)
                {
                    return;
                }

                _pendingCols = cols;
                _pendingRows = rows;
                _resizeTimer ??= new Timer(_ => ApplyPendingResize(), null, Timeout.Infinite, Timeout.Infinite);
                _resizeTimer.Change(ResizeDebounceMs, Timeout.Infinite);
            }
        }

        private void ApplyPendingResize()
        {
            int cols;
            int rows;
            lock (_resizeLock)
            {
                if (_disposed)
                {
                    return;
                }

                cols = _pendingCols;
                rows = _pendingRows;
            }

            try
            {
                Connection.Resize(Math.Max(1, cols), Math.Max(1, rows));
            }
            catch
            {
                // best-effort; connection may have been killed concurrently
            }
        }

        public void Dispose()
        {
            lock (_resizeLock)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
            }

            try
            {
                Cancellation.Cancel();
            }
            catch
            {
                // best-effort
            }

            _resizeTimer?.Dispose();
            _resizeTimer = null;

            try
            {
                Connection.Dispose();
            }
            catch
            {
                // best-effort
            }

            try
            {
                Cancellation.Dispose();
            }
            catch
            {
                // best-effort
            }
        }
    }
}
