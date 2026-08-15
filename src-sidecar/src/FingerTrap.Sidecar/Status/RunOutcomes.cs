namespace FingerTrap.Sidecar.Status;

/// <summary>
/// The ONLY place a workflow run's <c>status</c>/<c>conclusion</c> pair is
/// collapsed into one displayable outcome (ADR-0022, repo-dash's rule): two
/// independent derivations would eventually disagree about the same run.
/// <c>status</c> says whether the run finished; <c>conclusion</c> says how —
/// a <c>completed</c> run can be a failure, which is exactly the bit that a
/// naive status-only mapping loses.
/// </summary>
internal static class RunOutcomes
{
    /// <summary>
    /// Conclusions GitHub documents for a completed run. An unrecognized
    /// value degrades to <c>"unknown"</c> rather than passing through: the
    /// API has grown conclusions before (<c>startup_failure</c>), and a
    /// value this module has never seen must not render as understood.
    /// </summary>
    private static readonly HashSet<string> KnownConclusions = new(StringComparer.Ordinal)
    {
        "success",
        "failure",
        "cancelled",
        "skipped",
        "timed_out",
        "action_required",
        "neutral",
        "stale",
        "startup_failure",
    };

    /// <summary>Pre-completion statuses GitHub emits.</summary>
    private static readonly HashSet<string> PendingStatuses = new(StringComparer.Ordinal)
    {
        "queued",
        "waiting",
        "requested",
        "pending",
        "in_progress",
    };

    public static string Derive(string? status, string? conclusion)
    {
        if (!string.IsNullOrEmpty(conclusion))
        {
            return KnownConclusions.Contains(conclusion) ? conclusion : "unknown";
        }

        if (!string.IsNullOrEmpty(status) && PendingStatuses.Contains(status))
        {
            return status == "in_progress" ? "in_progress" : "queued";
        }

        return "unknown";
    }
}
