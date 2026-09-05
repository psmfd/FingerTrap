using System.Text.Json;
using FingerTrap.Sidecar.Abstractions;
using FingerTrap.Sidecar.Text;

namespace FingerTrap.Sidecar.Vm;

internal sealed record McmParseResult(VmStatusOutcome Outcome, VmStatusSnapshot? Snapshot, string? Detail);

internal static class McmStatusParser
{
    private const int MaxJsonBytes = 256 * 1024;
    private const int MaxNameLength = 63;
    private const int MaxProfileLength = 64;
    private const int MaxCount = 1_000_000;

    private static readonly HashSet<string> Required = new(StringComparer.Ordinal)
    {
        "schema", "name", "exists", "state", "reachable", "stamp_present",
        "provisioned_profile", "configured_profile", "drift", "needs_provision",
        "expertise_write_configured", "expertise_token", "errors", "warnings", "result",
    };

    private static readonly string[] RequiredTokenProperties = ["present", "scope", "detail"];

    public static McmParseResult Parse(ReadOnlySpan<byte> json)
    {
        if (json.Length == 0 || json.Length > MaxJsonBytes)
        {
            return Invalid(VmStatusOutcome.MalformedOutput, "status document size is invalid");
        }

        try
        {
            using var document = JsonDocument.Parse(json.ToArray(), new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 8,
            });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Invalid(VmStatusOutcome.InvalidOutput, "status root is not an object");
            }

            var tokenCount = 0;
            if (!WithinStructuralLimits(document.RootElement, ref tokenCount))
            {
                return Invalid(VmStatusOutcome.InvalidOutput, "status structure exceeds its bounds");
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!seen.Add(property.Name))
                {
                    return Invalid(VmStatusOutcome.InvalidOutput, "status contains a duplicate property");
                }
            }

            if (!document.RootElement.TryGetProperty("schema", out var schemaElement)
                || schemaElement.ValueKind != JsonValueKind.Number
                || !schemaElement.TryGetInt32(out var schema))
            {
                return Invalid(VmStatusOutcome.InvalidOutput, "status schema is not an integer");
            }

            if (schema != 1)
            {
                return Invalid(VmStatusOutcome.UnsupportedSchema, "status schema is unsupported");
            }

            if (!Required.IsSubsetOf(seen))
            {
                return Invalid(VmStatusOutcome.InvalidOutput, "status is missing a required property");
            }

            if (document.RootElement.GetProperty("expertise_token") is { ValueKind: JsonValueKind.Object } token)
            {
                var tokenNames = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in token.EnumerateObject())
                {
                    if (!tokenNames.Add(property.Name))
                    {
                        return Invalid(VmStatusOutcome.InvalidOutput, "token status contains a duplicate property");
                    }
                }

                if (!RequiredTokenProperties.All(tokenNames.Contains))
                {
                    return Invalid(VmStatusOutcome.InvalidOutput, "token status is incomplete");
                }
            }

            McmStatusWire? wire;
            try
            {
                wire = JsonSerializer.Deserialize(json, McmStatusJsonContext.Default.McmStatusWire);
            }
            catch (JsonException)
            {
                return Invalid(VmStatusOutcome.InvalidOutput, "status property types are invalid");
            }

            if (wire is null)
            {
                return Invalid(VmStatusOutcome.InvalidOutput, "status could not be decoded");
            }

            return Validate(wire);
        }
        catch (JsonException)
        {
            return Invalid(VmStatusOutcome.MalformedOutput, "status is not one JSON document");
        }
    }

    private static McmParseResult Validate(McmStatusWire wire)
    {
        if (!ValidName(wire.Name)
            || wire.State is not ("running" or "stopped" or "absent")
            || wire.Result is not ("PASS" or "FAIL")
            || wire.Errors is < 0 or > MaxCount
            || wire.Warnings is < 0 or > MaxCount
            || wire.Exists == (wire.State == "absent")
            || (wire.Result == "PASS") != (wire.Errors == 0)
            || !ValidProfile(wire.ProvisionedProfile)
            || !ValidProfile(wire.ConfiguredProfile)
            || (wire.State == "running"
                && (wire.Reachable is null || wire.StampPresent is null || wire.NeedsProvision is null))
            || (wire.State != "running"
                && (wire.Reachable is not null || wire.StampPresent is not null
                    || wire.ProvisionedProfile is not null || wire.Drift is not null || wire.NeedsProvision is not null))
            || (wire.StampPresent == true && wire.NeedsProvision != false)
            || (wire.StampPresent == false
                && (wire.NeedsProvision != true || wire.ProvisionedProfile is not null || wire.Drift is not null))
            || (wire.ProvisionedProfile is not null
                && (wire.StampPresent != true || wire.Drift is null))
            || (wire.Drift == true
                && (wire.ProvisionedProfile is null || wire.ConfiguredProfile is null
                    || wire.ProvisionedProfile == wire.ConfiguredProfile)))
        {
            return Invalid(VmStatusOutcome.InvalidOutput, "status fields are inconsistent");
        }

        VmExpertiseTokenStatus? token = null;
        if (wire.ExpertiseToken is { } tokenWire)
        {
            if ((tokenWire.Scope is not null && tokenWire.Scope is not ("read" or "write"))
                || (tokenWire.Detail?.Length ?? 0) > StatusText.MaxFieldLength * 4
                || (tokenWire.Present && (tokenWire.Scope is null || tokenWire.Detail is null))
                || (!tokenWire.Present && (tokenWire.Scope is not null || tokenWire.Detail is not null)))
            {
                return Invalid(VmStatusOutcome.InvalidOutput, "token status is inconsistent");
            }

            token = new VmExpertiseTokenStatus(
                tokenWire.Present,
                tokenWire.Scope,
                tokenWire.Detail is null ? null : StatusText.Sanitize(tokenWire.Detail));
        }

        return new McmParseResult(
            VmStatusOutcome.Ok,
            new VmStatusSnapshot(
                wire.Schema,
                wire.Name!,
                wire.Exists,
                wire.State!,
                wire.Reachable,
                wire.StampPresent,
                wire.ProvisionedProfile,
                wire.ConfiguredProfile,
                wire.Drift,
                wire.NeedsProvision,
                wire.ExpertiseWriteConfigured,
                token,
                wire.Errors,
                wire.Warnings,
                wire.Result!),
            null);
    }

    private static bool WithinStructuralLimits(JsonElement element, ref int tokenCount)
    {
        if (++tokenCount > 256)
        {
            return false;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in element.EnumerateObject())
                {
                    if (property.Name.Length > 128
                        || !names.Add(property.Name)
                        || !WithinStructuralLimits(property.Value, ref tokenCount))
                    {
                        return false;
                    }
                }

                return true;
            case JsonValueKind.Array:
                var count = 0;
                foreach (var item in element.EnumerateArray())
                {
                    if (++count > 64 || !WithinStructuralLimits(item, ref tokenCount))
                    {
                        return false;
                    }
                }

                return true;
            case JsonValueKind.String:
                return (element.GetString()?.Length ?? 0) <= StatusText.MaxFieldLength * 4;
            default:
                return true;
        }
    }

    private static bool ValidName(string? name) => name is { Length: > 0 and <= MaxNameLength }
        && name[0] != '-'
        && name.All(static character => char.IsAsciiLetterOrDigit(character) || character == '-');

    private static bool ValidProfile(string? profile) => profile is null
        || (profile.Length <= MaxProfileLength
            && profile is "business" or "personal" or "localai-business" or "localai-personal" or "build-personal");

    private static McmParseResult Invalid(VmStatusOutcome outcome, string detail) => new(outcome, null, detail);
}
