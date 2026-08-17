using System.Net;
using System.Text;
using FingerTrap.Sidecar.Status;
using Xunit;

namespace FingerTrap.Sidecar.Tests;

/// <summary>
/// Conditional-request layer for the GitHub provider (#70, ADR-0022). The
/// contract under test: transparent to the caller — a 304 replays the cached
/// body as a 200, a 200 is re-readable after caching, and the ETag validator
/// round-trips verbatim.
/// </summary>
public sealed class EtagCachingHandlerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class StubHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } =
            _ => new HttpResponseMessage(HttpStatusCode.OK);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(Respond(request));
        }
    }

    private static HttpResponseMessage Ok(string body, string etag)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        response.Headers.TryAddWithoutValidation("ETag", etag);
        return response;
    }

    private static HttpClient Client(EtagStore store, StubHandler stub) =>
        new(new EtagCachingHandler(store, stub));

    [Fact]
    public async Task FirstGet_CachesAndBodyStaysReadable()
    {
        var stub = new StubHandler { Respond = _ => Ok("""{"a":1}""", "\"e1\"") };
        using var client = Client(new EtagStore(), stub);

        var body = await client.GetStringAsync(new Uri("https://api.github.com/x"), Ct);

        Assert.Equal("""{"a":1}""", body);
        Assert.False(stub.Requests[0].Headers.Contains("If-None-Match"));
    }

    [Fact]
    public async Task SecondGet_SendsValidatorVerbatim_AndReplaysOn304()
    {
        var stub = new StubHandler { Respond = _ => Ok("""{"a":1}""", "W/\"weak1\"") };
        var store = new EtagStore();
        using var client = Client(store, stub);
        _ = await client.GetStringAsync(new Uri("https://api.github.com/x"), Ct);

        stub.Respond = _ => new HttpResponseMessage(HttpStatusCode.NotModified);
        using var second = await client.GetAsync(new Uri("https://api.github.com/x"), Ct);

        Assert.Equal("W/\"weak1\"", stub.Requests[1].Headers.GetValues("If-None-Match").Single());
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal("""{"a":1}""", await second.Content.ReadAsStringAsync(Ct));
        Assert.Equal("application/json", second.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task FreshResponse_ReplacesCacheEntry()
    {
        var stub = new StubHandler { Respond = _ => Ok("""{"a":1}""", "\"e1\"") };
        var store = new EtagStore();
        using var client = Client(store, stub);
        _ = await client.GetStringAsync(new Uri("https://api.github.com/x"), Ct);

        stub.Respond = _ => Ok("""{"a":2}""", "\"e2\"");
        var second = await client.GetStringAsync(new Uri("https://api.github.com/x"), Ct);
        stub.Respond = _ => new HttpResponseMessage(HttpStatusCode.NotModified);
        using var third = await client.GetAsync(new Uri("https://api.github.com/x"), Ct);

        Assert.Equal("""{"a":2}""", second);
        Assert.Equal("\"e2\"", stub.Requests[2].Headers.GetValues("If-None-Match").Single());
        Assert.Equal("""{"a":2}""", await third.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task DistinctUris_UseDistinctEntries()
    {
        var stub = new StubHandler
        {
            Respond = r => Ok($$"""{"uri":"{{r.RequestUri!.AbsolutePath}}"}""", $"\"{r.RequestUri!.AbsolutePath}\""),
        };
        var store = new EtagStore();
        using var client = Client(store, stub);
        _ = await client.GetStringAsync(new Uri("https://api.github.com/a"), Ct);
        _ = await client.GetStringAsync(new Uri("https://api.github.com/b"), Ct);

        stub.Respond = _ => new HttpResponseMessage(HttpStatusCode.NotModified);
        using var a = await client.GetAsync(new Uri("https://api.github.com/a"), Ct);
        using var b = await client.GetAsync(new Uri("https://api.github.com/b"), Ct);

        Assert.Equal("\"/a\"", stub.Requests[2].Headers.GetValues("If-None-Match").Single());
        Assert.Equal("\"/b\"", stub.Requests[3].Headers.GetValues("If-None-Match").Single());
        Assert.Contains("/a", await a.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        Assert.Contains("/b", await b.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Post_PassesThroughUnobserved()
    {
        var stub = new StubHandler { Respond = _ => Ok("""{"a":1}""", "\"e1\"") };
        var store = new EtagStore();
        using var client = Client(store, stub);
        using var content = new StringContent("{}", Encoding.UTF8, "application/json");
        _ = await client.PostAsync(new Uri("https://api.github.com/x"), content, Ct);

        // A later GET to the same URI must not think it has a validator.
        stub.Respond = _ => Ok("""{"a":1}""", "\"e1\"");
        _ = await client.GetStringAsync(new Uri("https://api.github.com/x"), Ct);

        Assert.False(stub.Requests[0].Headers.Contains("If-None-Match"));
        Assert.False(stub.Requests[1].Headers.Contains("If-None-Match"));
    }

    [Fact]
    public async Task NotModifiedWithoutCacheEntry_PassesThrough()
    {
        // Defensive: a 304 the handler did not solicit (e.g. the store was
        // evicted between attach and answer) must surface as-is, not as a
        // fabricated empty 200.
        var stub = new StubHandler { Respond = _ => new HttpResponseMessage(HttpStatusCode.NotModified) };
        using var client = Client(new EtagStore(), stub);

        using var response = await client.GetAsync(new Uri("https://api.github.com/x"), Ct);

        Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
    }

    [Fact]
    public async Task EtaglessResponse_IsNotCached()
    {
        var stub = new StubHandler
        {
            Respond = _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"a":1}""", Encoding.UTF8, "application/json"),
            },
        };
        using var client = Client(new EtagStore(), stub);
        _ = await client.GetStringAsync(new Uri("https://api.github.com/x"), Ct);
        _ = await client.GetStringAsync(new Uri("https://api.github.com/x"), Ct);

        Assert.False(stub.Requests[1].Headers.Contains("If-None-Match"));
    }
}
