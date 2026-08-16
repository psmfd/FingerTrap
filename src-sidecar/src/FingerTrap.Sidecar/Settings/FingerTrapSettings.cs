using System.Text.Json.Serialization;

namespace FingerTrap.Sidecar.Settings;

/// <summary>
/// Persisted user configuration (Native track N-1, issue #52).
/// </summary>
/// <remarks>
/// Deliberately small. This exists first to absorb the two interim
/// environment variables FT-0 introduced (<see cref="PiSettings.Path"/> and
/// <see cref="PaneSettings.DefaultKind"/>), before FT-1 adds a third piece of
/// ad-hoc configuration and the debt compounds. Theme, font, profiles, and
/// persisted layout are the rest of N-1 and are not modelled here yet —
/// layout in particular cannot be, because panes do not exist until FT-1.
///
/// Every field is nullable and every section optional: an absent settings
/// file, an empty object, and a file setting only one key must all behave
/// identically to the pre-settings defaults. A fresh install must not require
/// a config file.
/// </remarks>
internal sealed record FingerTrapSettings
{
    /// <summary>
    /// Schema version. See <see cref="SettingsLoader.SupportedVersion"/>.
    /// </summary>
    /// <remarks>
    /// Nullable, and with no initializer, specifically so an <em>absent</em>
    /// <c>version</c> is distinguishable from one that happens to equal the
    /// current value. A default would make an unversioned file silently parse
    /// as v1, which defeats the point of versioning it: once v2 exists, that
    /// file is ambiguous — written for v1, or hand-written against v2 by
    /// someone who omitted the key? Requiring it costs one line in a file the
    /// operator is already editing deliberately.
    /// </remarks>
    [JsonPropertyName("version")]
    public int? Version { get; init; }

    [JsonPropertyName("pi")]
    public PiSettings? Pi { get; init; }

    [JsonPropertyName("pane")]
    public PaneSettings? Pane { get; init; }

    [JsonPropertyName("status")]
    public StatusSettings? Status { get; init; }

    /// <summary>Everything unset — the behaviour of an absent settings file.</summary>
    internal static FingerTrapSettings Defaults { get; } = new() { Version = SettingsLoader.SupportedVersion };
}

internal sealed record PiSettings
{
    /// <summary>
    /// Explicit pi executable path. Outranks <c>$FINGERTRAP_PI</c> and the
    /// <c>PATH</c> search; outranked by a path named in the spawn request.
    /// </summary>
    [JsonPropertyName("path")]
    public string? Path { get; init; }
}

/// <summary>
/// Status-provider configuration (FT-1 slice 2, ADR-0022). Additive within
/// schema v1: unknown keys are tolerated, version is the compatibility gate.
/// Credentials do NOT live here — they are keychain-held by the shell.
/// </summary>
internal sealed record StatusSettings
{
    [JsonPropertyName("github")]
    public GitHubStatusSettings? Github { get; init; }

    [JsonPropertyName("ado")]
    public AdoStatusSettings? Ado { get; init; }

    [JsonPropertyName("git")]
    public LocalGitStatusSettings? Git { get; init; }
}

internal sealed record GitHubStatusSettings
{
    /// <summary>Repository to watch, as <c>"owner/name"</c>.</summary>
    [JsonPropertyName("repo")]
    public string? Repo { get; init; }
}

internal sealed record AdoStatusSettings
{
    /// <summary>Organization name — the <c>{org}</c> in
    /// <c>https://dev.azure.com/{org}</c>, not a URL.</summary>
    [JsonPropertyName("organization")]
    public string? Organization { get; init; }

    /// <summary>Project name or id within the organization.</summary>
    [JsonPropertyName("project")]
    public string? Project { get; init; }
}

internal sealed record LocalGitStatusSettings
{
    /// <summary>Absolute path of the working tree to watch.</summary>
    [JsonPropertyName("path")]
    public string? Path { get; init; }
}

internal sealed record PaneSettings
{
    /// <summary>
    /// Default pane kind — <c>"pi"</c> or <c>"shell"</c>. Outranks
    /// <c>$FINGERTRAP_PANE_KIND</c>; outranked by a kind named in the spawn
    /// request. An unrecognised value is an error, exactly as it is for the
    /// environment variable (ADR-0013).
    /// </summary>
    [JsonPropertyName("defaultKind")]
    public string? DefaultKind { get; init; }
}
