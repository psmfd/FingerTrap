using System.Collections.Generic;

namespace FingerTrap.Sidecar.Abstractions;

/// <summary>
/// A read-only status source (ADR-0022): polls its backing service and maps
/// responses into FingerTrap-owned rows AT CONSTRUCTION — no raw SDK model
/// ever crosses the RPC surface, and every free-text field is sanitized
/// before the row exists (the repo-dash rule: a row that exists is safe by
/// construction).
/// </summary>
public interface IStatusProvider
{
    /// <summary>Stable wire name, e.g. <c>"github"</c>.</summary>
    public string Name { get; }

    /// <summary>
    /// Fetch the current snapshot. Never throws for expected states —
    /// missing configuration, missing/rejected credentials, and API errors
    /// all come back as a <see cref="ProviderSnapshot"/> whose
    /// <see cref="ProviderSnapshot.State"/> names the condition, so the UI
    /// renders states, never blanks.
    /// </summary>
    public Task<ProviderSnapshot> FetchAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Wire values for <see cref="ProviderSnapshot.State"/>. Strings, not an
/// enum, on the wire — the UI must render an unrecognized future state as
/// text rather than fail to deserialize.
/// </summary>
public static class ProviderStates
{
    public const string Ok = "ok";
    public const string NotConfigured = "not-configured";
    public const string AuthFailed = "auth-failed";
    public const string Error = "error";
}

/// <summary>
/// One provider's contribution to a <c>status/snapshot</c> notification.
/// <see cref="Detail"/> is an operator-actionable, already-sanitized
/// sentence for any non-ok state — or, for a provider whose entire surface
/// is one line (local git), the surface itself on an ok state.
/// </summary>
public sealed record ProviderSnapshot(
    string Provider,
    string State,
    string? Detail,
    IReadOnlyList<IssueRow> Issues,
    IReadOnlyList<PrRow> PullRequests,
    IReadOnlyList<RunRow> Runs)
{
    public static ProviderSnapshot Empty(string provider, string state, string? detail = null) =>
        new(provider, state, detail, [], [], []);
}

/// <remarks>
/// Row types are deliberately separate per surface — a shared shape with
/// optional run fields would let an issue row silently carry a run's
/// conclusion (the exact drift repo-dash guards against). Keyed by the API
/// <c>Id</c>, never a per-workflow ordinal.
/// </remarks>
public sealed record IssueRow(
    long Id,
    int Number,
    string Title,
    string Author,
    string State,
    string UpdatedAt);

public sealed record PrRow(
    long Id,
    int Number,
    string Title,
    string Author,
    string State,
    bool IsDraft,
    string HeadBranch,
    string UpdatedAt);

/// <remarks>
/// <c>Status</c> says whether the run finished; <c>Conclusion</c> says how
/// (null until completed) — kept unmerged, because a completed run can be a
/// failure and collapsing the pair loses exactly the bit that matters.
/// <c>Outcome</c> is the single-place derivation of the pair; unrecognized
/// values degrade to <c>"unknown"</c>, never pass through as understood.
/// </remarks>
public sealed record RunRow(
    long Id,
    long RunNumber,
    string WorkflowName,
    string DisplayTitle,
    string Status,
    string? Conclusion,
    string Outcome,
    string HeadBranch,
    string CreatedAt);
