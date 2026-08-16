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

        var shellPath = ResolveExecutable(options.Kind, options.Shell, _piSettings);

        var ptyOptions = new global::Porta.Pty.PtyOptions
        {
            App = shellPath,
            CommandLine = ResolveCommandLine(options.Kind),
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
            // The session may already be out of the map (Close() removes it
            // before the process dies); dispose the captured session either
            // way — Dispose is idempotent — so a killed session's timer, CTS
            // and streams are still released.
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
        // Kill, don't Dispose. Dispose tears the read loop down FIRST, and a
        // PTY child dying with nothing draining the master can wedge
        // uninterruptibly in terminal teardown on macOS — SIGKILL pending,
        // waitpid never returning, no exit event (found by the FT-1 stdio
        // probe; the process sat in `ps` state `?Es` indefinitely). Killing
        // while the reader still drains lets the process actually exit; the
        // ProcessExited handler then disposes the session and raises Exited,
        // so a kill produces the same pty/exit notification as a self-exit.
        if (_sessions.TryRemove(sessionId, out var session))
        {
            try
            {
                session.Connection.Kill();
            }
            catch
            {
                // Already exited — the ProcessExited handler owns cleanup.
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        foreach (var session in _sessions.Values)
        {
            session.Dispose();
        }

        _sessions.Clear();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Environment variable naming the pi executable outright. The escape
    /// hatch until the settings system (Native track N-1) exists; a pi
    /// installed somewhere unusual should not require a code change.
    /// </summary>
    internal const string PiPathEnvVar = "FINGERTRAP_PI";

    /// <summary>Executable name searched for on <c>PATH</c>.</summary>
    private const string PiExecutableName = "pi";

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
    /// resolved absolute path and take no arguments.
    /// </remarks>
    internal static string[] ResolveCommandLine(PaneKind kind) => kind switch
    {
        PaneKind.Shell => new[] { "-l" },
        _ => Array.Empty<string>(),
    };

    /// <summary>
    /// Locate the pi executable: explicit request, then settings, then
    /// <see cref="PiPathEnvVar"/>, then <c>PATH</c>. Throws rather than
    /// falling back.
    /// </summary>
    internal static string ResolvePi(string? requested, PiSettings? settings = null)
    {
        if (!string.IsNullOrEmpty(requested))
        {
            return requested;
        }

        // Settings outrank the environment (N-1, #52); the env var survives as
        // a lower layer for ephemeral overrides rather than being retired.
        if (!string.IsNullOrEmpty(settings?.Path))
        {
            return settings.Path;
        }

        var fromEnv = Environment.GetEnvironmentVariable(PiPathEnvVar);
        if (!string.IsNullOrEmpty(fromEnv))
        {
            return fromEnv;
        }

        var onPath = FindOnPath(PiExecutableName);
        if (onPath is not null)
        {
            return onPath;
        }

        throw new PiNotFoundException(
            $"no pi executable found. Tried: the spawn request's explicit path, settings pi.path, " +
            $"${PiPathEnvVar}, then PATH. Fix: install pi, set pi.path in settings.json or " +
            $"{PiPathEnvVar}=/path/to/pi, or request a shell pane instead.");
    }

    /// <summary>
    /// First executable match for <paramref name="name"/> across <c>PATH</c>,
    /// or null.
    /// </summary>
    /// <remarks>
    /// Hand-rolled rather than shelling out to <c>which</c>/<c>where</c>:
    /// spawning a process to decide what to spawn is a needless dependency on
    /// yet another binary being present, and this runs on the pane-open path.
    /// The executable-bit check is what stops a same-named directory or a
    /// non-executable file from being returned as a usable answer.
    /// </remarks>
    private static string? FindOnPath(string name)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate;
            try
            {
                candidate = Path.Combine(dir.Trim(), name);
            }
            catch (ArgumentException)
            {
                // A PATH entry containing invalid path characters is skipped
                // rather than aborting the whole search — one bad entry must
                // not hide a good one further along.
                continue;
            }

            if (!File.Exists(candidate))
            {
                continue;
            }

            if (OperatingSystem.IsWindows())
            {
                return candidate;
            }

            var mode = File.GetUnixFileMode(candidate);
            const UnixFileMode AnyExecute =
                UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
            if ((mode & AnyExecute) != 0)
            {
                return candidate;
            }
        }

        return null;
    }

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

        public Session(string id, global::Porta.Pty.IPtyConnection connection)
        {
            Id = id;
            Connection = connection;
        }

        public string Id { get; }

        public global::Porta.Pty.IPtyConnection Connection { get; }

        public CancellationTokenSource Cancellation { get; } = new();

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
