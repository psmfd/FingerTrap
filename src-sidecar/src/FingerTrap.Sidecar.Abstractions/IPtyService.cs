using System.Collections.Generic;

namespace FingerTrap.Sidecar.Abstractions;

public interface IPtyService : IAsyncDisposable
{
    public Task<int> SpawnAsync(string sessionId, PtySpawnOptions options, CancellationToken cancellationToken);

    public ValueTask WriteAsync(string sessionId, ReadOnlyMemory<byte> data, CancellationToken cancellationToken);

    public void Resize(string sessionId, int cols, int rows);

    public void Close(string sessionId);

    public event EventHandler<PtyOutputEventArgs>? Output;

    public event EventHandler<PtyExitEventArgs>? Exited;
}

/// <summary>
/// What a pane <em>is</em>, as opposed to which binary happens to be spawned
/// in it. FingerTrap is the pi Home (see <c>docs/milestones.md</c>, Home track
/// FT-0), so hosting pi is a named capability rather than "pass a different
/// value for <see cref="PtySpawnOptions.Shell"/>". The distinction is what
/// lets a missing pi be an error instead of a silent fall-through to
/// <c>$SHELL</c>.
/// </summary>
public enum PaneKind
{
    /// <summary>An ordinary interactive shell — the pre-FT-0 behaviour.</summary>
    Shell = 0,

    /// <summary>The pi coding agent, resolved explicitly and never guessed at.</summary>
    Pi = 1,
}

/// <param name="Kind">What the pane is. Drives executable resolution.</param>
/// <param name="Shell">
/// Explicit executable path, overriding resolution for whichever
/// <paramref name="Kind"/> is in force. Named <c>Shell</c> for wire
/// compatibility with the pre-FT-0 contract; it is the executable override for
/// a pi pane just as much as for a shell pane.
/// </param>
/// <param name="SessionPath">
/// Session file to resume (pi's <c>--session</c>), from the session browser
/// (FT-2 slice 5). Only meaningful when <paramref name="Kind"/> is
/// <see cref="PaneKind.Pi"/>.
/// </param>
public sealed record PtySpawnOptions(
    string? Shell,
    string? Cwd,
    int Cols,
    int Rows,
    IReadOnlyDictionary<string, string>? Env,
    PaneKind Kind = PaneKind.Shell,
    string? SessionPath = null);

public sealed class PtyOutputEventArgs : EventArgs
{
    public required string SessionId { get; init; }

    public required ReadOnlyMemory<byte> Data { get; init; }
}

public sealed class PtyExitEventArgs : EventArgs
{
    public required string SessionId { get; init; }

    public required int ExitCode { get; init; }
}
