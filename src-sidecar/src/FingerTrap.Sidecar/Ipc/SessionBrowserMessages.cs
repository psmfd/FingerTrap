namespace FingerTrap.Sidecar.Ipc;

/// <summary>
/// Wire shapes for the session browser (FT-2 slice 5, ADR-0026). Mirrored by
/// the <c>sessions/list</c> / <c>worktrees/list</c> entries in
/// <c>src-ui/src/api.ts</c> — the rpc-pairing check in <c>scripts/check.sh</c>
/// counts both sides. Null defaults follow the Newtonsoft.Json#2765
/// convention (see <see cref="RpcPromptRequest"/>): optional record
/// parameters must never declare a non-<c>default(T)</c> default.
/// </summary>
/// <param name="SessionPath">
/// Absolute path of the session's JSONL file — the ONLY functional resume
/// key (it is sidecar-enumerated, never round-tripped through
/// sanitization). Everything else on this record is display data.
/// </param>
/// <param name="Name">
/// Operator-assigned session name (latest <c>session_info</c> entry), or
/// null when unset or cleared. Sanitized at construction.
/// </param>
/// <param name="FirstMessage">
/// First user-role message text, as the fallback row label. Sanitized at
/// construction.
/// </param>
/// <param name="ParentSessionPath">
/// The header's <c>parentSession</c> — an absolute session-file path that
/// can dangle (the parent file may be gone). The UI treats unresolvable
/// parents as fork-tree roots.
/// </param>
/// <param name="CwdMissing">
/// The session's recorded cwd no longer exists on disk. RPC-pane resume is
/// disabled in this state (ADR-0026): spawn-time <c>--session</c> in rpc
/// mode hard-exits(1) on a missing cwd.
/// </param>
/// <param name="ReapedWorktree">
/// <see cref="CwdMissing"/> and the cwd was a per-session worktree
/// (contains <c>/.worktrees/</c>) — the normal aftermath of worktree
/// reaping, not data loss.
/// </param>
/// <param name="OriginalRepo">
/// For a reaped worktree, the repo path the worktree hung off (the cwd
/// prefix before <c>/.worktrees/</c>) — the suggested pane cwd for a
/// PTY-pane resume (ADR-0026). Sanitized like <paramref name="Cwd"/>, so a
/// hostile cwd may yield an invalid directory here; pi then errors visibly.
/// </param>
public sealed record SessionSummary(
    string SessionPath,
    string Id,
    string Cwd,
    string? Name,
    string FirstMessage,
    int MessageCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset ModifiedAt,
    string? ParentSessionPath,
    bool CwdMissing,
    bool ReapedWorktree,
    string? OriginalRepo = null);

/// <param name="TotalCount">
/// Total session files on disk, which can exceed
/// <paramref name="Sessions"/>.Count — the store deep-parses only the most
/// recently modified N files (SessionStore.DefaultDeepParseCap), so the UI
/// can show "N of TotalCount".
/// </param>
public sealed record SessionsListResult(
    IReadOnlyList<SessionSummary> Sessions,
    int TotalCount);

/// <summary>
/// One reconciled per-session worktree record (the pi_config worktree
/// extension's durable signals: lock reasons, manifests, refs/pi-wip/*).
/// Read-only surfacing — reap/unlock stay pi-side <c>/worktree</c> commands.
/// </summary>
/// <param name="Host">
/// Hostname recorded by the session that created the worktree
/// (manifest, else lock reason) — display only, verbatim: a pid is only
/// meaningful on the host that recorded it (pi_config#1019), so the UI
/// shows the host instead of this sidecar guessing cross-host liveness.
/// </param>
/// <param name="Shape">
/// <c>live</c> (worktree present, recorded pid alive), <c>dead</c>
/// (worktree present, pid dead, lock or manifest identifies the session),
/// <c>gone</c> (no worktree directory left — manifest and/or wip ref
/// remain), or <c>stray</c> (worktree present with no lock and no
/// manifest — nothing identifies an owning session).
/// </param>
public sealed record WorktreeRecord(
    string Sid,
    string? WorktreePath,
    string? Branch,
    string? Repo,
    string? Host,
    string? WipSha,
    int? Pid,
    bool Alive,
    string Shape);

public sealed record WorktreesListResult(IReadOnlyList<WorktreeRecord> Records);
