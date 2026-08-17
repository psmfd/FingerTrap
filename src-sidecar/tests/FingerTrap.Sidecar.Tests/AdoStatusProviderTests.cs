using System.Net;
using System.Text;
using FingerTrap.Sidecar.Abstractions;
using FingerTrap.Sidecar.Settings;
using CredentialCache = FingerTrap.Sidecar.Ipc.CredentialCache;
using FingerTrap.Sidecar.Status;
using Xunit;

namespace FingerTrap.Sidecar.Tests;

public sealed class AdoStatusProviderTests
{
    private static AdoStatusSettings Configured => new() { Organization = "org", Project = "proj" };

    private static CredentialCache WithToken()
    {
        var cache = new CredentialCache();
        cache.Set("ado", "pat-value");
        return cache;
    }

    [Fact]
    public async Task NoSettingsIsNotConfigured()
    {
        using var provider = new AdoStatusProvider(new CredentialCache(), null);

        var snapshot = await provider.FetchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ProviderStates.NotConfigured, snapshot.State);
        Assert.Contains("status.ado.organization", snapshot.Detail);
    }

    [Fact]
    public async Task NoTokenIsNotConfigured()
    {
        using var provider = new AdoStatusProvider(new CredentialCache(), Configured);

        var snapshot = await provider.FetchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ProviderStates.NotConfigured, snapshot.State);
        Assert.Contains("token", snapshot.Detail);
    }

    [Fact]
    public async Task UrlHostileOrgIsErrorWithoutAnyRequest()
    {
        var handler = new CannedHandler([]);
        using var provider = new AdoStatusProvider(
            WithToken(), new AdoStatusSettings { Organization = "org/../evil", Project = "proj" }, handler);

        var snapshot = await provider.FetchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ProviderStates.Error, snapshot.State);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task MapsWorkItemsToSanitizedIssueRows()
    {
        var wiql = """{"workItems":[{"id":7},{"id":9}]}""";
        var batch = """
            {"value":[
              {"id":7,"fields":{
                "System.Title":"fix\u001b[2J the thing",
                "System.State":"Active",
                "System.CreatedBy":{"displayName":"Ada"},
                "System.ChangedDate":"2026-08-15T10:00:00Z"}},
              {"id":9,"fields":{
                "System.Title":"plain",
                "System.State":"New",
                "System.CreatedBy":{"displayName":"Grace"},
                "System.ChangedDate":"2026-08-14T10:00:00Z"}}
            ]}
            """;
        var handler = new CannedHandler([Ok(wiql), Ok(batch), Ok("""{"value":[]}""")]);
        using var provider = new AdoStatusProvider(WithToken(), Configured, handler);

        var snapshot = await provider.FetchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ProviderStates.Ok, snapshot.State);
        Assert.Equal(2, snapshot.Issues.Count);
        Assert.Equal(7, snapshot.Issues[0].Id);
        Assert.Equal("fix[2J the thing", snapshot.Issues[0].Title);
        Assert.Equal("Ada", snapshot.Issues[0].Author);
        Assert.Equal("Active", snapshot.Issues[0].State);
        Assert.Equal("https://dev.azure.com/org/proj/_workitems/edit/7", snapshot.Issues[0].Url);
        Assert.Empty(snapshot.PullRequests);
        Assert.Empty(snapshot.Runs);
    }

    [Fact]
    public async Task SendsBasicAuthWithEmptyUsernameAndNeverEchoesToken()
    {
        var handler = new CannedHandler([Ok("""{"workItems":[]}"""), Ok("""{"value":[]}""")]);
        using var provider = new AdoStatusProvider(WithToken(), Configured, handler);

        var snapshot = await provider.FetchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ProviderStates.Ok, snapshot.State);
        var auth = handler.Requests[0].Headers.Authorization;
        Assert.NotNull(auth);
        Assert.Equal("Basic", auth.Scheme);
        Assert.Equal(":pat-value", Encoding.ASCII.GetString(Convert.FromBase64String(auth.Parameter!)));
        Assert.DoesNotContain("pat-value", snapshot.Detail ?? string.Empty);
    }

    [Fact]
    public async Task EmptyWiqlResultIsOkWithNoRowsAndNoBatchCall()
    {
        var handler = new CannedHandler([Ok("""{"workItems":[]}"""), Ok("""{"value":[]}""")]);
        using var provider = new AdoStatusProvider(WithToken(), Configured, handler);

        var snapshot = await provider.FetchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ProviderStates.Ok, snapshot.State);
        Assert.Empty(snapshot.Issues);
        // wiql + builds only — the empty id list must not become a batch GET.
        Assert.Equal(2, handler.Requests.Count);
        Assert.DoesNotContain(handler.Requests, r => r.RequestUri!.AbsolutePath.Contains("/wit/workitems"));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.NonAuthoritativeInformation)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task AuthShapedResponsesAreAuthFailed(HttpStatusCode code)
    {
        // 203 is ADO's signature quirk: an unaccepted token can get the
        // sign-in page instead of a 401.
        var handler = new CannedHandler([new HttpResponseMessage(code)]);
        using var provider = new AdoStatusProvider(WithToken(), Configured, handler);

        var snapshot = await provider.FetchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ProviderStates.AuthFailed, snapshot.State);
        Assert.NotNull(snapshot.Detail);
    }

    [Fact]
    public async Task NotFoundIsErrorNotAuthFailed()
    {
        var handler = new CannedHandler([new HttpResponseMessage(HttpStatusCode.NotFound)]);
        using var provider = new AdoStatusProvider(WithToken(), Configured, handler);

        var snapshot = await provider.FetchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ProviderStates.Error, snapshot.State);
        Assert.Contains("404", snapshot.Detail);
    }

    [Fact]
    public async Task UnexpectedStatusSanitizesTheBodyIntoTheDetail()
    {
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("boom[2J" + new string('x', 10_000)),
        };
        var handler = new CannedHandler([response]);
        using var provider = new AdoStatusProvider(WithToken(), Configured, handler);

        var snapshot = await provider.FetchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ProviderStates.Error, snapshot.State);
        Assert.Contains("500", snapshot.Detail);
        Assert.DoesNotContain("", snapshot.Detail);
        // Sanitize's bound is maxLength plus the one-char '…' marker.
        Assert.True(snapshot.Detail!.Length <= 201);
    }

    [Fact]
    public async Task MalformedJsonIsErrorState()
    {
        var handler = new CannedHandler([Ok("<!doctype html><html>sign in</html>")]);
        using var provider = new AdoStatusProvider(WithToken(), Configured, handler);

        var snapshot = await provider.FetchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ProviderStates.Error, snapshot.State);
        Assert.Contains("cannot parse", snapshot.Detail);
    }

    [Fact]
    public async Task NetworkFailureIsErrorState()
    {
        var handler = new ThrowingHandler(new HttpRequestException("dns"));
        using var provider = new AdoStatusProvider(WithToken(), Configured, handler);

        var snapshot = await provider.FetchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ProviderStates.Error, snapshot.State);
        Assert.Contains("network", snapshot.Detail);
    }

    private static AdoStatusSettings ConfiguredWithRepo =>
        new() { Organization = "org", Project = "proj", Repository = "repo" };

    [Fact]
    public async Task MapsActivePrsAndBuildsToSanitizedRows()
    {
        var prs = """
            {"value":[{
              "pullRequestId":5,
              "title":"add\u001b[2J thing",
              "status":"active",
              "isDraft":true,
              "sourceRefName":"refs/heads/feat/x",
              "creationDate":"2026-08-16T09:00:00Z",
              "createdBy":{"displayName":"Ada"}}]}
            """;
        var builds = """
            {"value":[{
              "id":42,
              "buildNumber":"20260817.3",
              "status":"completed",
              "result":"succeeded",
              "sourceBranch":"refs/heads/dev",
              "queueTime":"2026-08-17T08:00:00Z",
              "definition":{"name":"CI"}}]}
            """;
        var handler = new CannedHandler([Ok("""{"workItems":[]}"""), Ok(prs), Ok(builds)]);
        using var provider = new AdoStatusProvider(WithToken(), ConfiguredWithRepo, handler);

        var snapshot = await provider.FetchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ProviderStates.Ok, snapshot.State);
        var pr = Assert.Single(snapshot.PullRequests);
        Assert.Equal(5, pr.Id);
        Assert.Equal("add[2J thing", pr.Title);
        Assert.Equal("Ada", pr.Author);
        Assert.True(pr.IsDraft);
        Assert.Equal("feat/x", pr.HeadBranch);
        Assert.Equal("https://dev.azure.com/org/proj/_git/repo/pullrequest/5", pr.Url);
        var run = Assert.Single(snapshot.Runs);
        Assert.Equal(42, run.Id);
        Assert.Equal("CI", run.WorkflowName);
        Assert.Equal("20260817.3", run.DisplayTitle);
        Assert.Equal("completed", run.Status);
        Assert.Equal("succeeded", run.Conclusion);
        Assert.Equal("success", run.Outcome);
        Assert.Equal("dev", run.HeadBranch);
        Assert.Equal("https://dev.azure.com/org/proj/_build/results?buildId=42", run.Url);
    }

    [Fact]
    public async Task NoRepositorySettingSkipsThePullRequestSurface()
    {
        var handler = new CannedHandler([Ok("""{"workItems":[]}"""), Ok("""{"value":[]}""")]);
        using var provider = new AdoStatusProvider(WithToken(), Configured, handler);

        var snapshot = await provider.FetchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ProviderStates.Ok, snapshot.State);
        Assert.Empty(snapshot.PullRequests);
        Assert.Equal(2, handler.Requests.Count);
        Assert.DoesNotContain(handler.Requests, r => r.RequestUri!.AbsolutePath.Contains("/git/repositories/"));
    }

    [Fact]
    public async Task RepositoryNotFoundNamesTheRepositorySetting()
    {
        var handler = new CannedHandler(
            [Ok("""{"workItems":[]}"""), new HttpResponseMessage(HttpStatusCode.NotFound)]);
        using var provider = new AdoStatusProvider(WithToken(), ConfiguredWithRepo, handler);

        var snapshot = await provider.FetchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ProviderStates.Error, snapshot.State);
        Assert.Contains("status.ado.repository", snapshot.Detail);
    }

    [Fact]
    public async Task UrlHostileRepositoryIsErrorWithoutAnyRequest()
    {
        var handler = new CannedHandler([]);
        using var provider = new AdoStatusProvider(
            WithToken(),
            new AdoStatusSettings { Organization = "org", Project = "proj", Repository = "x/../y" },
            handler);

        var snapshot = await provider.FetchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ProviderStates.Error, snapshot.State);
        Assert.Contains("status.ado.repository", snapshot.Detail);
        Assert.Empty(handler.Requests);
    }

    private static HttpResponseMessage Ok(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    private sealed class CannedHandler(IReadOnlyList<HttpResponseMessage> responses) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (Requests.Count > responses.Count)
            {
                throw new InvalidOperationException($"unexpected request #{Requests.Count}: {request.RequestUri}");
            }

            return Task.FromResult(responses[Requests.Count - 1]);
        }
    }

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(exception);
    }
}
