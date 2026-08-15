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
        var handler = new CannedHandler([Ok(wiql), Ok(batch)]);
        using var provider = new AdoStatusProvider(WithToken(), Configured, handler);

        var snapshot = await provider.FetchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ProviderStates.Ok, snapshot.State);
        Assert.Equal(2, snapshot.Issues.Count);
        Assert.Equal(7, snapshot.Issues[0].Id);
        Assert.Equal("fix[2J the thing", snapshot.Issues[0].Title);
        Assert.Equal("Ada", snapshot.Issues[0].Author);
        Assert.Equal("Active", snapshot.Issues[0].State);
        Assert.Empty(snapshot.PullRequests);
        Assert.Empty(snapshot.Runs);
    }

    [Fact]
    public async Task SendsBasicAuthWithEmptyUsernameAndNeverEchoesToken()
    {
        var handler = new CannedHandler([Ok("""{"workItems":[]}""")]);
        using var provider = new AdoStatusProvider(WithToken(), Configured, handler);

        var snapshot = await provider.FetchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ProviderStates.Ok, snapshot.State);
        var auth = Assert.Single(handler.Requests).Headers.Authorization;
        Assert.NotNull(auth);
        Assert.Equal("Basic", auth.Scheme);
        Assert.Equal(":pat-value", Encoding.ASCII.GetString(Convert.FromBase64String(auth.Parameter!)));
        Assert.DoesNotContain("pat-value", snapshot.Detail ?? string.Empty);
    }

    [Fact]
    public async Task EmptyWiqlResultIsOkWithNoRowsAndNoBatchCall()
    {
        var handler = new CannedHandler([Ok("""{"workItems":[]}""")]);
        using var provider = new AdoStatusProvider(WithToken(), Configured, handler);

        var snapshot = await provider.FetchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ProviderStates.Ok, snapshot.State);
        Assert.Empty(snapshot.Issues);
        Assert.Single(handler.Requests);
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
