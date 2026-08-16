namespace FingerTrap.Sidecar.Status;

/// <summary>
/// First gate of the ADR-0023 URL-open policy: a row's <c>Url</c> exists
/// only if it passed this validator at row construction. The Rust shell's
/// <c>open_url</c> command re-runs the same checks (second gate) — the two
/// are kept in lockstep by test, not shared code.
/// </summary>
internal static class StatusUrls
{
    /// <summary>
    /// Returns the canonical absolute URL when <paramref name="candidate"/>
    /// is https, carries no userinfo, and its host is exactly one of
    /// <paramref name="allowedHosts"/>; otherwise null. Null is a degrade,
    /// never an error — the row renders unlinked.
    /// </summary>
    public static string? Validate(string? candidate, params string[] allowedHosts)
    {
        if (string.IsNullOrWhiteSpace(candidate)
            || !Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || uri.UserInfo.Length != 0)
        {
            return null;
        }

        return allowedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase)
            ? uri.AbsoluteUri
            : null;
    }
}
