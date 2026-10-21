using System.Text.Json;

namespace FingerTrap.Sidecar.Runtime;

/// <summary>
/// Validated, non-secret profile definition and pure path projection (ADR-0035).
/// Filesystem containment and artifact verification belong to the launcher.
/// </summary>
internal sealed record RuntimeProfile
{
    private RuntimeProfile(string id, string runtimeId, string root, CredentialReference[] references)
    {
        Id = id;
        RuntimeId = runtimeId;
        Root = root;
        CredentialReferences = Array.AsReadOnly(references);
    }

    public string Id { get; }
    public string RuntimeId { get; }
    public string Root { get; }
    public IReadOnlyList<CredentialReference> CredentialReferences { get; }
    public string AgentDir => Path.Combine(Root, "agent");
    public string SessionsDir => Path.Combine(AgentDir, "sessions");
    public string ExtensionsDir => Path.Combine(AgentDir, "extensions");
    public string WorktreeManifestsDir => Path.Combine(ExtensionsDir, "worktree", "sessions");
    public string CacheDir => Path.Combine(Root, "cache");
    public string TempDir => Path.Combine(Root, "tmp");
    public string HomeDir => Path.Combine(Root, "home");

    internal sealed record CredentialReference(string Provider, string Reference);

    public static RuntimeProfile Parse(JsonElement definition, string applicationRoot)
    {
        RequireFields(definition, ["version", "id", "runtimeId", "credentialRefs"]);
        var version = definition.GetProperty("version");
        if (version.ValueKind != JsonValueKind.Number || !version.TryGetDouble(out var number) || number != 1)
        {
            throw new ArgumentException("unsupported runtime profile version", nameof(definition));
        }

        var id = ReadIdentifier(definition, "id", profileId: true);
        var runtimeId = ReadIdentifier(definition, "runtimeId");
        var refs = definition.GetProperty("credentialRefs");
        if (refs.ValueKind != JsonValueKind.Array || refs.GetArrayLength() > 32)
        {
            throw new ArgumentException("credentialRefs must be an array of at most 32 references", nameof(definition));
        }

        var providers = new HashSet<string>(StringComparer.Ordinal);
        var references = new List<CredentialReference>();
        foreach (var reference in refs.EnumerateArray())
        {
            RequireFields(reference, ["provider", "reference"]);
            var provider = ReadIdentifier(reference, "provider");
            if (!providers.Add(provider))
            {
                throw new ArgumentException("duplicate credential provider", nameof(definition));
            }

            references.Add(new CredentialReference(provider, ReadIdentifier(reference, "reference")));
        }

        if (string.IsNullOrWhiteSpace(applicationRoot) || applicationRoot.Contains('\0', StringComparison.Ordinal)
            || !Path.IsPathFullyQualified(applicationRoot))
        {
            throw new ArgumentException("an absolute application-data root is required", nameof(applicationRoot));
        }

        var root = Path.Combine(Path.GetFullPath(applicationRoot), "profiles", id);
        return new RuntimeProfile(id, runtimeId, root, [.. references]);
    }

    internal static bool IsIdentifier(string? value, bool profileId = false)
    {
        if (string.IsNullOrEmpty(value) || value.Length > (profileId ? 48 : 96))
        {
            return false;
        }

        static bool Letter(char c) => c is >= 'a' and <= 'z';
        static bool Digit(char c) => c is >= '0' and <= '9';
        if (!Letter(value[0]) && (profileId || !Digit(value[0])))
        {
            return false;
        }

        foreach (var c in value)
        {
            if (!Letter(c) && !Digit(c) && c != '-' && (profileId || c != '.'))
            {
                return false;
            }
        }

        // Win32 device names remain reserved even with a suffix such as .json.
        var basename = value.Split('.')[0];
        return basename is not ("con" or "prn" or "aux" or "nul")
            && !(basename.Length == 4
                && (basename.StartsWith("com", StringComparison.Ordinal) || basename.StartsWith("lpt", StringComparison.Ordinal))
                && basename[3] is >= '1' and <= '9')
            && !value.EndsWith('.');
    }

    private static string ReadIdentifier(JsonElement value, string name, bool profileId = false)
    {
        var field = value.GetProperty(name);
        var text = field.ValueKind == JsonValueKind.String ? field.GetString() : null;
        if (!IsIdentifier(text, profileId))
        {
            throw new ArgumentException($"invalid runtime profile {name}", nameof(value));
        }

        return text!;
    }

    private static void RequireFields(JsonElement value, string[] names)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("runtime profile fields must be an object", nameof(value));
        }

        foreach (var property in value.EnumerateObject())
        {
            if (!names.Contains(property.Name, StringComparer.Ordinal))
            {
                throw new ArgumentException("unknown runtime profile field", nameof(value));
            }
        }

        foreach (var name in names)
        {
            if (!value.TryGetProperty(name, out _))
            {
                throw new ArgumentException($"missing runtime profile {name}", nameof(value));
            }
        }
    }
}
