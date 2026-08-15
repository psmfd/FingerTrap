using System.Text.Json;
using System.Text.Json.Serialization;

namespace FingerTrap.Sidecar.Status;

/// <summary>
/// Azure DevOps wire DTOs for the WIQL + work-items REST surface
/// (ADR-0022). Source-generated serialization context — trim/AOT-neutral
/// from day one, per the ADR's rejection of the reflection-based SDK.
/// These records never cross <c>RpcSurface</c>; <see cref="AdoStatusProvider"/>
/// maps them into sanitized rows at construction.
/// </summary>
internal sealed record AdoWiqlRequest(
    [property: JsonPropertyName("query")] string Query);

internal sealed record AdoWiqlResponse(
    [property: JsonPropertyName("workItems")] IReadOnlyList<AdoWorkItemRef>? WorkItems);

internal sealed record AdoWorkItemRef(
    [property: JsonPropertyName("id")] long Id);

internal sealed record AdoWorkItemBatch(
    [property: JsonPropertyName("value")] IReadOnlyList<AdoWorkItem>? Value);

/// <remarks>
/// <c>Fields</c> stays a <see cref="JsonElement"/> map: ADO field values are
/// heterogeneous (<c>System.CreatedBy</c> is an identity object, most others
/// are strings) and the set requested is chosen per call — a typed record
/// per shape would be fiction.
/// </remarks>
internal sealed record AdoWorkItem(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("fields")] Dictionary<string, JsonElement>? Fields);

[JsonSerializable(typeof(AdoWiqlRequest))]
[JsonSerializable(typeof(AdoWiqlResponse))]
[JsonSerializable(typeof(AdoWorkItemBatch))]
internal sealed partial class AdoJsonContext : JsonSerializerContext;
