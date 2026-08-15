using System.Globalization;
using System.Text;

namespace FingerTrap.Sidecar.Ipc;

/// <summary>
/// Read-only passthrough over the RPC input stream that enforces a ceiling on
/// each frame's declared <c>Content-Length</c> (ADR-0022). Neither
/// StreamJsonRpc nor vscode-jsonrpc bounds this, and once provider payloads
/// share the channel an uncapped declared length is a self-inflicted DoS: the
/// reader would happily allocate whatever the header claims. A violation
/// throws <see cref="IOException"/>, which is connection-fatal by design —
/// consistent with ADR-0002's "framing corruption is fatal" posture; no
/// resync heuristics.
/// </summary>
/// <remarks>
/// The ceiling must match the WebView reader's (<c>src-ui/src/transport.ts</c>
/// <c>MAX_FRAME_BYTES</c>) — lockstep pair; change both together.
/// </remarks>
internal sealed class FrameCeilingStream : Stream
{
    public const int DefaultMaxFrameBytes = 4 * 1024 * 1024;

    /// <summary>
    /// Headers are a handful of short ASCII lines; a header section that
    /// exceeds this is malformed or hostile, not large.
    /// </summary>
    private const int MaxHeaderBytes = 16 * 1024;

    private static readonly byte[] Terminator = "\r\n\r\n"u8.ToArray();

    private readonly Stream _inner;
    private readonly int _maxFrameBytes;
    private readonly List<byte> _header = new();
    private long _bodyRemaining;

    public FrameCeilingStream(Stream inner, int maxFrameBytes = DefaultMaxFrameBytes)
    {
        _inner = inner;
        _maxFrameBytes = maxFrameBytes;
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = _inner.Read(buffer, offset, count);
        Inspect(buffer.AsSpan(offset, read));
        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var read = await _inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        Inspect(buffer.Span[..read]);
        return read;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Runs the framing state machine over bytes already read, throwing
    /// before they are handed to the JSON-RPC reader if any frame's declared
    /// length breaches the ceiling. The bytes themselves pass through
    /// untouched — this never buffers or rewrites the stream.
    /// </summary>
    private void Inspect(ReadOnlySpan<byte> chunk)
    {
        while (!chunk.IsEmpty)
        {
            if (_bodyRemaining > 0)
            {
                var consumed = (int)Math.Min(_bodyRemaining, chunk.Length);
                _bodyRemaining -= consumed;
                chunk = chunk[consumed..];
                continue;
            }

            // Header mode: accumulate until the \r\n\r\n terminator arrives,
            // possibly split across reads.
            var before = _header.Count;
            foreach (var b in chunk)
            {
                _header.Add(b);
            }

            if (_header.Count > MaxHeaderBytes)
            {
                throw new IOException(
                    $"rpc frame header exceeded {MaxHeaderBytes} bytes without terminating — malformed framing");
            }

            var terminatorAt = FindTerminator(Math.Max(0, before - (Terminator.Length - 1)));
            if (terminatorAt < 0)
            {
                return;
            }

            var declared = ParseContentLength(terminatorAt);
            if (declared > _maxFrameBytes)
            {
                throw new IOException(
                    $"rpc frame declared Content-Length {declared} exceeds the {_maxFrameBytes}-byte ceiling (ADR-0022)");
            }

            // Bytes past the terminator in this chunk belong to the body (and
            // possibly the next frame's header, handled by looping).
            var afterTerminator = _header.Count - (terminatorAt + Terminator.Length);
            _header.Clear();
            _bodyRemaining = declared;

            chunk = chunk[(chunk.Length - afterTerminator)..];
        }
    }

    private int FindTerminator(int searchFrom)
    {
        for (var i = searchFrom; i + Terminator.Length <= _header.Count; i++)
        {
            if (_header[i] == Terminator[0]
                && _header[i + 1] == Terminator[1]
                && _header[i + 2] == Terminator[2]
                && _header[i + 3] == Terminator[3])
            {
                return i;
            }
        }

        return -1;
    }

    private long ParseContentLength(int headerLength)
    {
        var text = Encoding.ASCII.GetString(_header.GetRange(0, headerLength).ToArray());
        foreach (var line in text.Split("\r\n"))
        {
            var separator = line.IndexOf(':', StringComparison.Ordinal);
            if (separator < 0)
            {
                continue;
            }

            if (!line.AsSpan(0, separator).Trim().Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (long.TryParse(line.AsSpan(separator + 1).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }

            throw new IOException($"rpc frame carried an unparseable Content-Length: '{line.Trim()}'");
        }

        throw new IOException("rpc frame header had no Content-Length");
    }
}
