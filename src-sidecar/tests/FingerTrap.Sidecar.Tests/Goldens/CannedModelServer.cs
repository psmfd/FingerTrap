using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace FingerTrap.Sidecar.Tests.Goldens;

/// <summary>One scripted model turn: content chunks, with an optional hold.</summary>
internal sealed record CannedTurn
{
    public required IReadOnlyList<string> Chunks { get; init; }

    /// <summary>
    /// Emit this many chunks, then hold the stream open until
    /// <see cref="CannedModelServer.ReleaseHold"/> or the client aborts
    /// (pi's <c>steer</c>/<c>abort</c> path) — the lever that lets a
    /// scenario deterministically act mid-turn.
    /// </summary>
    public int? HoldAfter { get; init; }
}

/// <summary>
/// Deterministic local stand-in for the model backend during golden
/// recording (#139): an OpenAI-completions SSE endpoint serving scripted
/// turns, registered with the recorded pi via a temp-HOME
/// <c>models.json</c>. Real pi, canned model — the RPC layer under test is
/// exercised end to end while assistant text, chunking, and usage numbers
/// stay byte-stable across recordings. Fixed ids/usage; no randomness.
/// </summary>
internal sealed class CannedModelServer : IAsyncDisposable
{
    private static readonly TimeSpan HoldPollInterval = TimeSpan.FromMilliseconds(100);

    private readonly HttpListener _listener;
    private readonly ConcurrentQueue<CannedTurn> _turns = new();
    private readonly SemaphoreSlim _release = new(0);
    private readonly CancellationTokenSource _stopping = new();
    private readonly Task _acceptLoop;

    private CannedModelServer(HttpListener listener, string baseUrl)
    {
        _listener = listener;
        BaseUrl = baseUrl;
        _acceptLoop = Task.Run(AcceptLoopAsync);
    }

    /// <summary>Ends with <c>/v1</c> — the SDK appends <c>/chat/completions</c>.</summary>
    public string BaseUrl { get; }

    public static CannedModelServer Start()
    {
        // HttpListener cannot bind port 0; probe random loopback ports.
        var random = new Random();
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var port = random.Next(20000, 60000);
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            try
            {
                listener.Start();
                return new CannedModelServer(listener, $"http://127.0.0.1:{port}/v1");
            }
            catch (HttpListenerException)
            {
                ((IDisposable)listener).Dispose();
            }
        }

        throw new InvalidOperationException("no free loopback port for the canned model server");
    }

    public void Enqueue(CannedTurn turn) => _turns.Enqueue(turn);

    public void ReleaseHold() => _release.Release();

    public async ValueTask DisposeAsync()
    {
        _stopping.Cancel();
        _listener.Stop();
        try
        {
            await _acceptLoop.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // A wedged handler dies with the listener.
        }

        ((IDisposable)_listener).Dispose();
        _release.Dispose();
        _stopping.Dispose();
    }

    private async Task AcceptLoopAsync()
    {
        while (!_stopping.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException)
            {
                return;
            }

            // Handle concurrently: a steer's replacement request can arrive
            // while the aborted one is still tearing down.
            _ = Task.Run(() => HandleAsync(context));
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        try
        {
            if (!context.Request.Url!.AbsolutePath.EndsWith("/chat/completions", StringComparison.Ordinal))
            {
                context.Response.StatusCode = 404;
                context.Response.Close();
                return;
            }

            // Drain the request body (ignored — turns are scripted).
            using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
            {
                _ = await reader.ReadToEndAsync().ConfigureAwait(false);
            }

            // An unscripted request gets a fixed default turn rather than an
            // error, so an unexpected extra turn shows up in the golden diff
            // as a deterministic "ok" instead of a flaky failure.
            if (!_turns.TryDequeue(out var turn))
            {
                turn = new CannedTurn { Chunks = ["ok"] };
            }

            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/event-stream";
            context.Response.SendChunked = true;
            var output = context.Response.OutputStream;

            await WriteEventAsync(output, Chunk("{\"role\":\"assistant\"}", finish: null)).ConfigureAwait(false);

            var emitted = 0;
            foreach (var chunk in turn.Chunks)
            {
                var deltaJson = System.Text.Json.JsonSerializer.Serialize(chunk);
                await WriteEventAsync(output, Chunk($"{{\"content\":{deltaJson}}}", finish: null))
                    .ConfigureAwait(false);
                emitted++;

                if (turn.HoldAfter == emitted)
                {
                    await HoldAsync(output).ConfigureAwait(false);
                }
            }

            await WriteEventAsync(output, Chunk("{}", finish: "\"stop\"")).ConfigureAwait(false);
            await WriteEventAsync(
                output,
                "{\"id\":\"chatcmpl-canned\",\"object\":\"chat.completion.chunk\",\"created\":0," +
                "\"model\":\"canned-model\",\"choices\":[]," +
                "\"usage\":{\"prompt_tokens\":7,\"completion_tokens\":3,\"total_tokens\":10}}")
                .ConfigureAwait(false);
            await WriteRawAsync(output, "data: [DONE]\n\n").ConfigureAwait(false);
            context.Response.Close();
        }
        catch (Exception ex) when (ex is HttpListenerException or IOException or ObjectDisposedException)
        {
            // Client aborted mid-stream (steer/abort) — expected teardown.
            try
            {
                context.Response.Abort();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    /// <summary>
    /// Holds the stream open, emitting SSE comment pings so a client abort
    /// (steer) is detected as a write failure instead of blocking forever.
    /// Pings are protocol comments — they never become wire events.
    /// </summary>
    private async Task HoldAsync(Stream output)
    {
        while (!await _release.WaitAsync(HoldPollInterval, _stopping.Token).ConfigureAwait(false))
        {
            await WriteRawAsync(output, ": ping\n\n").ConfigureAwait(false);
        }
    }

    private static string Chunk(string delta, string? finish) =>
        "{\"id\":\"chatcmpl-canned\",\"object\":\"chat.completion.chunk\",\"created\":0," +
        $"\"model\":\"canned-model\",\"choices\":[{{\"index\":0,\"delta\":{delta}," +
        $"\"finish_reason\":{finish ?? "null"}}}]}}";

    private static Task WriteEventAsync(Stream output, string json) =>
        WriteRawAsync(output, $"data: {json}\n\n");

    private static async Task WriteRawAsync(Stream output, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        await output.WriteAsync(bytes).ConfigureAwait(false);
        await output.FlushAsync().ConfigureAwait(false);
    }
}
