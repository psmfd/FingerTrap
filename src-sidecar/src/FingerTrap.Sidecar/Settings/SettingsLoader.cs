using System.Text.Json;

namespace FingerTrap.Sidecar.Settings;

/// <summary>
/// Locates, reads, and validates <c>settings.json</c> (N-1, issue #52).
/// </summary>
/// <remarks>
/// Split into <see cref="Parse"/> (pure), <see cref="LoadFrom"/> (one named
/// file), and <see cref="Load"/> (the real location) so the validation rules
/// are testable without touching the host's actual config directory.
/// </remarks>
internal static class SettingsLoader
{
    /// <summary>
    /// The only schema version this build understands.
    /// </summary>
    internal const int SupportedVersion = 1;

    /// <summary>Directory name under the platform's application-data root.</summary>
    private const string AppDirectoryName = "fingertrap";

    private const string FileName = "settings.json";

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        // Unknown keys are tolerated on purpose: a settings file written by a
        // newer build that still declares version 1 must not break an older
        // one. Version is the compatibility gate, not key presence.
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Absolute path to the settings file: <c>&lt;app-data&gt;/fingertrap/settings.json</c>.
    /// </summary>
    /// <remarks>
    /// <see cref="Environment.SpecialFolder.ApplicationData"/> is each
    /// platform's own application-data root, which is <em>not</em> the same
    /// shape everywhere — verified on-host rather than assumed:
    ///
    /// <list type="bullet">
    ///   <item><description>macOS: <c>~/Library/Application Support/fingertrap/settings.json</c></description></item>
    ///   <item><description>Linux: <c>$XDG_CONFIG_HOME</c> or <c>~/.config</c>, then <c>/fingertrap/settings.json</c></description></item>
    ///   <item><description>Windows: <c>%APPDATA%\fingertrap\settings.json</c></description></item>
    /// </list>
    ///
    /// So this is deliberately <em>not</em> the <c>~/.config/fingertrap/</c>
    /// path issue #41 names — that assumption holds on Linux and is wrong on
    /// macOS. Anything documenting the settings location for users must say
    /// so per platform, or point at this method.
    /// </remarks>
    internal static string ResolvePath()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrEmpty(root))
        {
            // Degenerate environment (no HOME, no APPDATA). Fall back to the
            // working directory rather than throwing: a missing settings file
            // is a supported state, so an unresolvable path should end in
            // "no settings" rather than a hard failure at startup.
            root = Environment.CurrentDirectory;
        }

        return Path.Combine(root, AppDirectoryName, FileName);
    }

    /// <summary>Load from the platform location, or defaults when absent.</summary>
    internal static FingerTrapSettings Load() => LoadFrom(ResolvePath());

    /// <summary>
    /// Load one named file. An absent file yields <see cref="FingerTrapSettings.Defaults"/>;
    /// a present-but-bad file throws.
    /// </summary>
    /// <remarks>
    /// The asymmetry is the point, and it mirrors ADR-0013's treatment of
    /// <c>FINGERTRAP_PANE_KIND</c>: <em>absent</em> means "no opinion, use
    /// defaults", while <em>present and wrong</em> means the operator tried to
    /// configure something and it did not take. Silently reverting to defaults
    /// there would make a typo'd settings file indistinguishable from a
    /// working one — the same failure this project has already rejected once.
    /// </remarks>
    internal static FingerTrapSettings LoadFrom(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        if (!File.Exists(path))
        {
            return FingerTrapSettings.Defaults;
        }

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new SettingsException(
                $"settings file '{path}' exists but could not be read: {ex.Message}", ex);
        }

        try
        {
            return Parse(json);
        }
        catch (SettingsException ex)
        {
            // Re-thrown with the path attached: the bare parse message says
            // what is wrong, and the operator also needs to know which file.
            throw new SettingsException($"settings file '{path}': {ex.Message}", ex);
        }
    }

    /// <summary>Validate and deserialize settings JSON.</summary>
    internal static FingerTrapSettings Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new SettingsException("file is empty; delete it to use defaults");
        }

        FingerTrapSettings? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<FingerTrapSettings>(json, ReadOptions);
        }
        catch (JsonException ex)
        {
            throw new SettingsException($"is not valid JSON: {ex.Message}", ex);
        }

        if (parsed is null)
        {
            throw new SettingsException("parsed to null; expected a JSON object");
        }

        if (parsed.Version is null)
        {
            throw new SettingsException(
                $"is missing the \"version\" key; add \"version\": {SupportedVersion}");
        }

        if (parsed.Version != SupportedVersion)
        {
            // Refuse rather than best-effort. A future schema may reuse a key
            // with different meaning, so reading it under version-1 rules
            // could apply a setting the operator never intended — worse than
            // declining to start the pane.
            throw new SettingsException(
                $"schema version {parsed.Version} is not supported by this build (expected {SupportedVersion})");
        }

        return parsed;
    }
}
