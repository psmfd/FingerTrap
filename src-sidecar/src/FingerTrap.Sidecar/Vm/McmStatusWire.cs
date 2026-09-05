using System.Text.Json.Serialization;

namespace FingerTrap.Sidecar.Vm;

internal sealed record McmStatusWire
{
    [JsonPropertyName("schema")]
    public int Schema { get; init; }
    [JsonPropertyName("name")]
    public string? Name { get; init; }
    [JsonPropertyName("exists")]
    public bool Exists { get; init; }
    [JsonPropertyName("state")]
    public string? State { get; init; }
    [JsonPropertyName("reachable")]
    public bool? Reachable { get; init; }
    [JsonPropertyName("stamp_present")]
    public bool? StampPresent { get; init; }
    [JsonPropertyName("provisioned_profile")]
    public string? ProvisionedProfile { get; init; }
    [JsonPropertyName("configured_profile")]
    public string? ConfiguredProfile { get; init; }
    [JsonPropertyName("drift")]
    public bool? Drift { get; init; }
    [JsonPropertyName("needs_provision")]
    public bool? NeedsProvision { get; init; }
    [JsonPropertyName("expertise_write_configured")]
    public bool ExpertiseWriteConfigured { get; init; }
    [JsonPropertyName("expertise_token")]
    public McmExpertiseTokenWire? ExpertiseToken { get; init; }
    [JsonPropertyName("errors")]
    public int Errors { get; init; }
    [JsonPropertyName("warnings")]
    public int Warnings { get; init; }
    [JsonPropertyName("result")]
    public string? Result { get; init; }
}

internal sealed record McmExpertiseTokenWire
{
    [JsonPropertyName("present")]
    public bool Present { get; init; }
    [JsonPropertyName("scope")]
    public string? Scope { get; init; }
    [JsonPropertyName("detail")]
    public string? Detail { get; init; }
}

[JsonSerializable(typeof(McmStatusWire))]
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = false)]
internal sealed partial class McmStatusJsonContext : JsonSerializerContext;
