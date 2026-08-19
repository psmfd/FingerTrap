using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FingerTrap.Sidecar.Abstractions;
using FingerTrap.Sidecar.Settings;
using FingerTrap.Sidecar.Text;
using CredentialCache = FingerTrap.Sidecar.Ipc.CredentialCache;

namespace FingerTrap.Sidecar.Status;

/// <summary>
/// Azure DevOps status provider (ADR-0022): plain <see cref="HttpClient"/>
/// against the WIQL + work-items REST surface with source-generated
/// <see cref="System.Text.Json"/> contexts — the official SDK is rejected
/// (closed-source, dependency-heavy, reflection-based). Work items surface
/// as issue rows; active pull requests and pipeline runs (#72) surface as
/// PR and run rows, the PR surface gated on the optional
/// <c>status.ado.repository</c> setting. PAT is borrowed from the shell-fed
/// <see cref="CredentialCache"/> per fetch, sent as Basic auth with an
/// empty username, and never cached here.
/// </summary>
internal sealed class AdoStatusProvider : IStatusProvider, IDisposable
{
    private const int MaxWorkItems = 20;
    private const string ApiVersion = "7.1";

    /// <summary>Bound on any body read into an error message; snapshots
    /// themselves deserialize streamed, but a failure body is untrusted
    /// remote text and only ever feeds a sanitized one-liner.</summary>
    private const int MaxErrorBodyBytes = 4 * 1024;

    private readonly CredentialCache _credentials;
    private readonly AdoStatusSettings? _settings;
    private readonly HttpClient _http;

    public AdoStatusProvider(
        CredentialCache credentials,
        AdoStatusSettings? settings,
        HttpMessageHandler? handler = null)
    {
        _credentials = credentials;
        _settings = settings;
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    public string Name => "ado";

    public async Task<ProviderSnapshot> FetchAsync(CancellationToken cancellationToken)
    {
        var organization = _settings?.Organization;
        var project = _settings?.Project;
        if (string.IsNullOrWhiteSpace(organization) || string.IsNullOrWhiteSpace(project))
        {
            return ProviderSnapshot.Empty(Name, ProviderStates.NotConfigured,
                "set status.ado.organization and status.ado.project in settings.json");
        }

        if (!_credentials.TryGet(Name, out var token))
        {
            return ProviderSnapshot.Empty(Name, ProviderStates.NotConfigured,
                "no Azure DevOps token — save a read-only PAT below to show linked work items");
        }

        // Org/project/repository travel inside a URL: reject anything that
        // would change its shape rather than trying to escape it.
        if (!IsUrlSafeSegment(organization) || !IsUrlSafeSegment(project))
        {
            return ProviderSnapshot.Empty(Name, ProviderStates.Error,
                "status.ado.organization/project must be plain names (letters, digits, '-', '_', '.', spaces)");
        }

        var repository = _settings?.Repository;
        if (!string.IsNullOrWhiteSpace(repository) && !IsUrlSafeSegment(repository))
        {
            return ProviderSnapshot.Empty(Name, ProviderStates.Error,
                "status.ado.repository must be a plain name (letters, digits, '-', '_', '.', spaces)");
        }

        var projectBase = $"https://dev.azure.com/{Uri.EscapeDataString(organization)}/{Uri.EscapeDataString(project)}";
        var baseUrl = $"{projectBase}/_apis";
        try
        {
            // Sequential on purpose: latencies are sub-second against a 60s
            // cadence, the first error snapshot short-circuits the rest, and
            // deterministic request order keeps the canned-response tests
            // honest.
            var (issues, issuesError) = await FetchWorkItemRowsAsync(baseUrl, projectBase, token, cancellationToken)
                .ConfigureAwait(false);
            if (issuesError is not null)
            {
                return issuesError;
            }

            List<PrRow> prs = [];
            if (!string.IsNullOrWhiteSpace(repository))
            {
                // Opt-in surface (#72): work items and builds need only
                // org/project; the PR route needs a repository segment.
                var (prRows, prError) = await FetchPullRequestRowsAsync(
                    baseUrl, projectBase, repository, token, cancellationToken).ConfigureAwait(false);
                if (prError is not null)
                {
                    return prError;
                }

                prs = prRows!;
            }

            var (runs, runsError) = await FetchBuildRowsAsync(baseUrl, projectBase, token, cancellationToken)
                .ConfigureAwait(false);
            if (runsError is not null)
            {
                return runsError;
            }

            return new ProviderSnapshot(Name, ProviderStates.Ok, null, issues!, prs, runs!);
        }
        catch (JsonException)
        {
            return ProviderSnapshot.Empty(Name, ProviderStates.Error,
                "Azure DevOps returned a response this build cannot parse");
        }
        catch (HttpRequestException)
        {
            return ProviderSnapshot.Empty(Name, ProviderStates.Error, "network error reaching Azure DevOps");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient.Timeout surfaces as a cancellation that is not ours.
            return ProviderSnapshot.Empty(Name, ProviderStates.Error, "Azure DevOps request timed out");
        }
    }

    private async Task<(List<IssueRow>? Rows, ProviderSnapshot? Error)> FetchWorkItemRowsAsync(
        string baseUrl, string projectBase, string token, CancellationToken cancellationToken)
    {
        var wiql = new AdoWiqlRequest(
                "SELECT [System.Id] FROM WorkItems " +
                "WHERE [System.TeamProject] = @project AND [System.State] NOT IN ('Closed', 'Done', 'Removed') " +
                "ORDER BY [System.ChangedDate] DESC");

            using var wiqlRequest = new HttpRequestMessage(
                HttpMethod.Post, $"{baseUrl}/wit/wiql?api-version={ApiVersion}&$top={MaxWorkItems}")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(wiql, AdoJsonContext.Default.AdoWiqlRequest),
                    Encoding.UTF8, "application/json"),
            };
            Authorize(wiqlRequest, token);

        using var wiqlResponse = await _http.SendAsync(wiqlRequest, cancellationToken).ConfigureAwait(false);
        if (!IsUsableSuccess(wiqlResponse))
        {
            return (null, await ToErrorSnapshotAsync(wiqlResponse, cancellationToken).ConfigureAwait(false));
        }

        var refs = await ReadAsync(wiqlResponse, AdoJsonContext.Default.AdoWiqlResponse, cancellationToken)
            .ConfigureAwait(false);
        var ids = (refs?.WorkItems ?? []).Take(MaxWorkItems).Select(w => w.Id).ToList();
        if (ids.Count == 0)
        {
            return ([], null);
        }

        var fields = "System.Title,System.State,System.CreatedBy,System.ChangedDate";
        using var batchRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"{baseUrl}/wit/workitems?ids={string.Join(',', ids)}&fields={fields}&api-version={ApiVersion}");
        Authorize(batchRequest, token);

        using var batchResponse = await _http.SendAsync(batchRequest, cancellationToken).ConfigureAwait(false);
        if (!IsUsableSuccess(batchResponse))
        {
            return (null, await ToErrorSnapshotAsync(batchResponse, cancellationToken).ConfigureAwait(false));
        }

        var batch = await ReadAsync(batchResponse, AdoJsonContext.Default.AdoWorkItemBatch, cancellationToken)
            .ConfigureAwait(false);

        // ADR-0023: the work-item URL is CONSTRUCTED from the already
        // url-safe org/project, never taken from the response body.
        var itemUrlBase = $"{projectBase}/_workitems/edit/";
        return ((batch?.Value ?? []).Select(w => ToIssueRow(w, itemUrlBase)).ToList(), null);
    }

    private async Task<(List<PrRow>? Rows, ProviderSnapshot? Error)> FetchPullRequestRowsAsync(
        string baseUrl, string projectBase, string repository, string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{baseUrl}/git/repositories/{Uri.EscapeDataString(repository)}/pullrequests" +
            $"?searchCriteria.status=active&$top={MaxWorkItems}&api-version={ApiVersion}");
        Authorize(request, token);

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!IsUsableSuccess(response))
        {
            return (null, await ToErrorSnapshotAsync(response, cancellationToken,
                "repository not found (404) — check status.ado.repository").ConfigureAwait(false));
        }

        var list = await ReadAsync(response, AdoJsonContext.Default.AdoPrList, cancellationToken)
            .ConfigureAwait(false);

        // ADR-0023: constructed from the validated org/project/repository
        // segments, never from the response body.
        var prUrlBase = $"{projectBase}/_git/{Uri.EscapeDataString(repository)}/pullrequest/";
        return ((list?.Value ?? []).Take(MaxWorkItems).Select(pr => ToPrRow(pr, prUrlBase)).ToList(), null);
    }

    private async Task<(List<RunRow>? Rows, ProviderSnapshot? Error)> FetchBuildRowsAsync(
        string baseUrl, string projectBase, string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get, $"{baseUrl}/build/builds?$top={MaxWorkItems}&api-version={ApiVersion}");
        Authorize(request, token);

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!IsUsableSuccess(response))
        {
            return (null, await ToErrorSnapshotAsync(response, cancellationToken).ConfigureAwait(false));
        }

        var list = await ReadAsync(response, AdoJsonContext.Default.AdoBuildList, cancellationToken)
            .ConfigureAwait(false);

        var buildUrlBase = $"{projectBase}/_build/results?buildId=";
        return ((list?.Value ?? []).Take(MaxWorkItems).Select(b => ToRunRow(b, buildUrlBase)).ToList(), null);
    }

    public void Dispose() => _http.Dispose();

    /// <summary>ADO's signature auth quirk: an unaccepted token can get a
    /// 203 carrying the sign-in HTML page — a "success" status that is
    /// anything but. Only a plain 2xx that is not 203 is usable.</summary>
    private static bool IsUsableSuccess(HttpResponseMessage response) =>
        response.IsSuccessStatusCode
            && response.StatusCode != HttpStatusCode.NonAuthoritativeInformation;

    private static void Authorize(HttpRequestMessage request, string token)
    {
        // ADO PAT convention: Basic with an empty username.
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($":{token}")));
    }

    private static async Task<T?> ReadAsync<T>(
        HttpResponseMessage response,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken)
    {
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            return await JsonSerializer.DeserializeAsync(stream, typeInfo, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<ProviderSnapshot> ToErrorSnapshotAsync(
        HttpResponseMessage response, CancellationToken cancellationToken,
        string notFoundDetail = "organization or project not found (404)")
    {
        switch (response.StatusCode)
        {
            case HttpStatusCode.Unauthorized:
                return ProviderSnapshot.Empty(Name, ProviderStates.AuthFailed,
                    "Azure DevOps rejected the token (401) — expired or revoked; save a new one");
            case HttpStatusCode.NonAuthoritativeInformation:
                // ADO's signature quirk: an unauthenticated request can get a
                // 203 with the sign-in HTML page instead of a 401.
                return ProviderSnapshot.Empty(Name, ProviderStates.AuthFailed,
                    "Azure DevOps did not accept the token (203 sign-in redirect); save a new one");
            case HttpStatusCode.Forbidden:
                return ProviderSnapshot.Empty(Name, ProviderStates.AuthFailed,
                    "token has no access to this organization/project (403) — check the PAT's scope and org");
            case HttpStatusCode.NotFound:
                return ProviderSnapshot.Empty(Name, ProviderStates.Error, notFoundDetail);
            default:
            {
                var body = await ReadBoundedAsync(response, cancellationToken).ConfigureAwait(false);
                return ProviderSnapshot.Empty(Name, ProviderStates.Error,
                    StatusText.Sanitize($"Azure DevOps API error {(int)response.StatusCode}: {body}", 200));
            }
        }
    }

    private static async Task<string> ReadBoundedAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                var buffer = new byte[MaxErrorBodyBytes];
                var read = await stream.ReadAtLeastAsync(buffer, MaxErrorBodyBytes, false, cancellationToken)
                    .ConfigureAwait(false);
                return Encoding.UTF8.GetString(buffer, 0, read);
            }
        }
        catch (Exception e) when (e is IOException or HttpRequestException or ObjectDisposedException)
        {
            return string.Empty;
        }
    }

    private static bool IsUrlSafeSegment(string value) =>
        value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ' ');

    private static IssueRow ToIssueRow(AdoWorkItem item, string itemUrlBase)
    {
        var fields = item.Fields;
        return new IssueRow(
            item.Id,
            (int)item.Id,
            StatusText.Sanitize(FieldString(fields, "System.Title")),
            StatusText.Sanitize(IdentityDisplayName(fields, "System.CreatedBy"), 80),
            StatusText.Sanitize(FieldString(fields, "System.State"), 40),
            StatusText.Sanitize(FieldString(fields, "System.ChangedDate"), 40),
            StatusUrls.Validate(itemUrlBase + item.Id, "dev.azure.com"));
    }

    private static PrRow ToPrRow(AdoPr pr, string prUrlBase) => new(
        pr.PullRequestId,
        (int)pr.PullRequestId,
        StatusText.Sanitize(pr.Title),
        StatusText.Sanitize(pr.CreatedBy?.DisplayName, 80),
        StatusText.Sanitize(pr.Status, 40),
        pr.IsDraft,
        StatusText.Sanitize(StripRefPrefix(pr.SourceRefName), 120),
        StatusText.Sanitize(pr.CreationDate, 40),
        StatusUrls.Validate(prUrlBase + pr.PullRequestId, "dev.azure.com"));

    /// <remarks><c>RunNumber</c> is 0: ADO build numbers are operator-format
    /// strings ("20260817.3"), carried in <c>DisplayTitle</c> instead; rows
    /// key on the API <c>Id</c> (repo-dash's rule) and the UI does not render
    /// the number.</remarks>
    private static RunRow ToRunRow(AdoBuild build, string buildUrlBase) => new(
        build.Id,
        0,
        StatusText.Sanitize(build.Definition?.Name, 120),
        StatusText.Sanitize(build.BuildNumber),
        StatusText.Sanitize(build.Status, 40),
        build.Result is null ? null : StatusText.Sanitize(build.Result, 40),
        RunOutcomes.DeriveAdo(build.Status, build.Result),
        StatusText.Sanitize(StripRefPrefix(build.SourceBranch), 120),
        StatusText.Sanitize(build.QueueTime, 40),
        StatusUrls.Validate(buildUrlBase + build.Id, "dev.azure.com"));

    private static string? StripRefPrefix(string? refName) =>
        refName is not null && refName.StartsWith("refs/heads/", StringComparison.Ordinal)
            ? refName["refs/heads/".Length..]
            : refName;

    private static string FieldString(Dictionary<string, JsonElement>? fields, string key) =>
        fields is not null && fields.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static string IdentityDisplayName(Dictionary<string, JsonElement>? fields, string key) =>
        fields is not null
            && fields.TryGetValue(key, out var value)
            && value.ValueKind == JsonValueKind.Object
            && value.TryGetProperty("displayName", out var name)
            && name.ValueKind == JsonValueKind.String
            ? name.GetString() ?? string.Empty
            : string.Empty;
}
