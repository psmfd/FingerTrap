using System.Buffers;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text;

namespace FingerTrap.Sidecar.PiRpc;

/// <summary>
/// LF-only JSONL wire framing for the pi <c>--mode rpc</c> stdio protocol
/// (docs/rpc-contract.md, "Wire framing"). Reads split strictly on
/// <c>0x0A</c> — never on U+2028/U+2029, which are legal inside JSON string
/// payloads — with exactly one trailing <c>0x0D</c> trimmed per line
/// (CRLF-tolerant in). An unterminated final buffer is flushed as a line at
/// stream end. Writes are LF-terminated raw UTF-8; never
/// <see cref="TextWriter.WriteLine(string)"/>, whose newline is
/// <see cref="Environment.NewLine"/> (<c>\r\n</c> on Windows — a silent
/// Windows-only spec violation).
/// </summary>
/// <remarks>
/// A line that grows past <paramref name="maxLineBytes"/> without
/// terminating throws <see cref="IOException"/> — connection-fatal for the
/// child by design. This is the inbound analog of
/// <see cref="Ipc.FrameCeilingStream"/>'s posture (ADR-0022): unbounded
/// accumulation of a single hostile or corrupt line is a self-inflicted
/// DoS, and framing corruption gets no resync heuristics. The ceiling is
/// checked per read, so the worst-case overshoot is one pipe segment.
/// </remarks>
internal static class JsonlCodec
{
    /// <summary>
    /// Generous headroom above any legitimate single pi event — pi bounds
    /// its own payloads by truncation, not by this ceiling — while still
    /// low enough that a runaway line cannot take down the sidecar.
    /// </summary>
    public const int DefaultMaxLineBytes = 8 * 1024 * 1024;

    /// <summary>
    /// Yields one decoded line per LF-terminated JSONL record until the
    /// stream ends, flushing any unterminated final buffer as a last line.
    /// The enumeration reads incrementally: a slow consumer stops calling
    /// <c>MoveNextAsync</c>, the pipe buffer fills, and the child's writes
    /// block — the backpressure chain the contract intends.
    /// </summary>
    public static async IAsyncEnumerable<string> ReadLinesAsync(
        Stream stream,
        int maxLineBytes = DefaultMaxLineBytes,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var reader = PipeReader.Create(stream);
        try
        {
            while (true)
            {
                var result = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                var buffer = result.Buffer;

                while (TryReadLine(ref buffer, out var line))
                {
                    yield return DecodeLine(line);
                }

                if (result.IsCompleted)
                {
                    if (!buffer.IsEmpty)
                    {
                        yield return DecodeLine(buffer);
                    }

                    reader.AdvanceTo(buffer.End);
                    break;
                }

                if (buffer.Length > maxLineBytes)
                {
                    throw new IOException(
                        $"pi rpc line exceeded the {maxLineBytes}-byte ceiling without terminating — " +
                        "malformed or hostile stream; connection-fatal for this child");
                }

                // consumed = start of the remaining partial line; examined =
                // everything (no more delimiters found). Any other pairing is
                // one of the two classic PipeReader bugs: data loss or a
                // no-backpressure spin. Pinned by JsonlCodecTests.
                reader.AdvanceTo(buffer.Start, buffer.End);
            }
        }
        finally
        {
            await reader.CompleteAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Encodes one outbound JSONL record: the serialized JSON as raw UTF-8
    /// plus a single LF. Throws if the payload itself carries a raw newline
    /// — serialized JSON never does, so one arriving here is a caller bug
    /// that would silently split into two protocol lines.
    /// </summary>
    public static byte[] EncodeLine(string json)
    {
        if (json.Contains('\n', StringComparison.Ordinal) || json.Contains('\r', StringComparison.Ordinal))
        {
            throw new ArgumentException("JSONL payload must not contain raw newline characters", nameof(json));
        }

        var payload = new byte[Encoding.UTF8.GetByteCount(json) + 1];
        var written = Encoding.UTF8.GetBytes(json, payload);
        payload[written] = (byte)'\n';
        return payload;
    }

    private static bool TryReadLine(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> line)
    {
        var position = buffer.PositionOf((byte)'\n');
        if (position is null)
        {
            line = default;
            return false;
        }

        line = buffer.Slice(0, position.Value);
        buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
        return true;
    }

    private static string DecodeLine(in ReadOnlySequence<byte> line)
    {
        var trimmed = line;
        if (trimmed.Length > 0)
        {
            var last = trimmed.Slice(trimmed.Length - 1);
            if (last.FirstSpan[0] == (byte)'\r')
            {
                trimmed = trimmed.Slice(0, trimmed.Length - 1);
            }
        }

        return Encoding.UTF8.GetString(in trimmed);
    }
}
