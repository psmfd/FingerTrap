using System.Collections.Concurrent;
using System.Net;

namespace FingerTrap.Sidecar.Status;

/// <summary>
/// Provider-lifetime ETag store backing <see cref="EtagCachingHandler"/>
/// (issue #70, budgeted in ADR-0022: Octokit has no built-in conditional
/// requests, so the provider keeps its own store). Lives on the provider —
/// the Octokit client, and with it the handler chain, is rebuilt every
/// fetch, and a cache that died with the client would never see a 304.
/// </summary>
internal sealed class EtagStore
{
    /// <summary>
    /// Entry bound. The GitHub provider hits three list endpoints; the bound
    /// exists so a future caller with per-item URIs cannot grow this into an
    /// unbounded response-body cache. Eviction is coarse (clear-all): at this
    /// size an LRU would be bookkeeping for its own sake, and a cleared cache
    /// costs one full-price request per endpoint, once.
    /// </summary>
    private const int MaxEntries = 16;

    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    internal sealed record Entry(string Etag, byte[] Body, string? ContentType);

    public bool TryGet(string uri, out Entry entry)
    {
        var found = _entries.TryGetValue(uri, out var value);
        entry = value!;
        return found;
    }

    public void Set(string uri, Entry entry)
    {
        if (_entries.Count >= MaxEntries && !_entries.ContainsKey(uri))
        {
            _entries.Clear();
        }

        _entries[uri] = entry;
    }
}

/// <summary>
/// Transparent conditional-request layer under Octokit: attaches
/// <c>If-None-Match</c> for GET URIs it has an ETag for, and replays the
/// cached body as a synthesized 200 when the server answers 304. Octokit —
/// and everything above it — sees ordinary fresh responses, so the provider's
/// fetch logic, its partial-failure behavior across parallel calls, and its
/// row construction stay untouched; "no change — reuse last rows" happens by
/// construction. GitHub does not count a 304 against the core rate limit,
/// which is the point (~180 requests/hour at the 60s cadence otherwise).
/// </summary>
/// <remarks>
/// The ETag is stored and echoed verbatim (weak <c>W/</c> prefix included)
/// via <c>TryAddWithoutValidation</c> — GitHub's validators must round-trip
/// exactly. Only GET responses bearing an ETag are cached; POSTs and
/// ETag-less responses pass through unobserved. The store is keyed by full
/// URI and never by token: an entry surviving a token change is safe, because
/// a 304 asserts the content is identical and the new token already proved
/// its access on the request the 304 answered.
/// </remarks>
internal sealed class EtagCachingHandler(EtagStore store, HttpMessageHandler inner)
    : DelegatingHandler(inner)
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri?.AbsoluteUri;
        if (request.Method != HttpMethod.Get || uri is null)
        {
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        var hasCached = store.TryGet(uri, out var cached);
        if (hasCached)
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", cached.Etag);
        }

        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotModified && hasCached)
        {
            var replay = new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new ByteArrayContent(cached.Body),
            };
            // Rate-limit and pagination headers from the 304 stay visible to
            // Octokit; only status and body are substituted.
            foreach (var header in response.Headers)
            {
                replay.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            if (cached.ContentType is not null)
            {
                replay.Content.Headers.TryAddWithoutValidation("Content-Type", cached.ContentType);
            }

            response.Dispose();
            return replay;
        }

        if (response.IsSuccessStatusCode
            && response.Headers.ETag is { } etag
            && response.Content is not null)
        {
            var body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            store.Set(uri, new EtagStore.Entry(
                etag.ToString(), body, response.Content.Headers.ContentType?.ToString()));
            // The original content stream is consumed; hand Octokit a
            // rebuffered copy so it reads a fresh stream.
            var contentType = response.Content.Headers.ContentType;
            response.Content.Dispose();
            response.Content = new ByteArrayContent(body);
            if (contentType is not null)
            {
                response.Content.Headers.TryAddWithoutValidation("Content-Type", contentType.ToString());
            }
        }

        return response;
    }
}
