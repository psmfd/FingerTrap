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

internal sealed record AdoIdentity(
    [property: JsonPropertyName("displayName")] string? DisplayName);

/// <remarks>#72: the active-PR list surface. <c>SourceRefName</c> arrives as
/// a full ref (<c>refs/heads/topic</c>); the provider strips the prefix at
/// row construction.</remarks>
internal sealed record AdoPrList(
    [property: JsonPropertyName("value")] IReadOnlyList<AdoPr>? Value);

internal sealed record AdoPr(
    [property: JsonPropertyName("pullRequestId")] long PullRequestId,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("isDraft")] bool IsDraft,
    [property: JsonPropertyName("sourceRefName")] string? SourceRefName,
    [property: JsonPropertyName("creationDate")] string? CreationDate,
    [property: JsonPropertyName("createdBy")] AdoIdentity? CreatedBy);

/// <remarks>#72: the builds surface. <c>Status</c>/<c>Result</c> stay
/// unmerged on the row (repo-dash's rule); <see cref="RunOutcomes.DeriveAdo"/>
/// is the single collapse point.</remarks>
internal sealed record AdoBuildList(
    [property: JsonPropertyName("value")] IReadOnlyList<AdoBuild>? Value);

internal sealed record AdoBuild(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("buildNumber")] string? BuildNumber,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("result")] string? Result,
    [property: JsonPropertyName("sourceBranch")] string? SourceBranch,
    [property: JsonPropertyName("queueTime")] string? QueueTime,
    [property: JsonPropertyName("definition")] AdoBuildDefinition? Definition);

internal sealed record AdoBuildDefinition(
    [property: JsonPropertyName("name")] string? Name);

[JsonSerializable(typeof(AdoWiqlRequest))]
[JsonSerializable(typeof(AdoWiqlResponse))]
[JsonSerializable(typeof(AdoWorkItemBatch))]
[JsonSerializable(typeof(AdoPrList))]
[JsonSerializable(typeof(AdoBuildList))]
internal sealed partial class AdoJsonContext : JsonSerializerContext;
