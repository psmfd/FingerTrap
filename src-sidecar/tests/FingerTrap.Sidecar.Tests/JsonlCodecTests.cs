using System.Text;
using FingerTrap.Sidecar.PiRpc;
using Xunit;

namespace FingerTrap.Sidecar.Tests;

public sealed class JsonlCodecTests
{
    [Fact]
    public async Task ReadLines_LfSeparated_YieldsEachLine()
    {
        var lines = await ReadAllAsync("{\"a\":1}\n{\"b\":2}\n");

        Assert.Equal(["{\"a\":1}", "{\"b\":2}"], lines);
    }

    [Fact]
    public async Task ReadLines_CrlfInput_TrimsExactlyOneTrailingCr()
    {
        var lines = await ReadAllAsync("{\"a\":1}\r\n{\"b\":2}\r\r\n");

        // CRLF-tolerant in: one \r stripped per line, never more.
        Assert.Equal(["{\"a\":1}", "{\"b\":2}\r"], lines);
    }

    [Fact]
    public async Task ReadLines_LoneCrInsideLine_IsNotATerminator()
    {
        var lines = await ReadAllAsync("a\rb\n");

        Assert.Equal(["a\rb"], lines);
    }

    [Fact]
    public async Task ReadLines_UnicodeLineSeparatorsInsidePayload_DoNotSplit()
    {
        // U+2028/U+2029 are legal inside JSON strings and must ride through
        // as payload bytes — the reason pi avoids readline-splitting.
        var payload = "{\"text\":\"a\u2028b\u2029c\"}";

        var lines = await ReadAllAsync(payload + "\n");

        Assert.Equal([payload], lines);
    }

    [Fact]
    public async Task ReadLines_UnterminatedFinalLine_FlushedAtEof()
    {
        var lines = await ReadAllAsync("{\"a\":1}\n{\"tail\":true}");

        Assert.Equal(["{\"a\":1}", "{\"tail\":true}"], lines);
    }

    [Fact]
    public async Task ReadLines_EmptyStream_YieldsNothing()
    {
        var lines = await ReadAllAsync(string.Empty);

        Assert.Empty(lines);
    }

    [Fact]
    public async Task ReadLines_LineSplitAcrossSingleByteReads_Reassembles()
    {
        // Pins the AdvanceTo(consumed: buffer.Start, examined: buffer.End)
        // pairing: any other combination either loses the partial line or
        // spins without waiting for more bytes.
        var payload = "{\"long\":\"" + new string('x', 512) + "\"}\n{\"b\":2}\n";
        var trickle = new TrickleStream(Encoding.UTF8.GetBytes(payload));

        var lines = new List<string>();
        await foreach (var line in JsonlCodec.ReadLinesAsync(
            trickle, cancellationToken: TestContext.Current.CancellationToken))
        {
            lines.Add(line);
        }

        Assert.Equal(2, lines.Count);
        Assert.Equal("{\"b\":2}", lines[1]);
    }

    [Fact]
    public async Task ReadLines_LineOverCeilingWithoutTerminator_Throws()
    {
        var hostile = new byte[64 * 1024]; // no \n anywhere
        Array.Fill(hostile, (byte)'x');
        using var stream = new MemoryStream(hostile);

        await Assert.ThrowsAsync<IOException>(async () =>
        {
            await foreach (var _ in JsonlCodec.ReadLinesAsync(
                stream, maxLineBytes: 16 * 1024, TestContext.Current.CancellationToken))
            {
            }
        });
    }

    [Fact]
    public void EncodeLine_AppendsSingleLf_NoCr()
    {
        var bytes = JsonlCodec.EncodeLine("{\"a\":1}");

        // LF-only out on every platform — a StreamWriter would have written
        // \r\n on Windows.
        Assert.Equal((byte)'\n', bytes[^1]);
        Assert.DoesNotContain((byte)'\r', bytes);
        Assert.Equal("{\"a\":1}\n", Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public void EncodeLine_PayloadWithRawNewline_Throws()
    {
        Assert.Throws<ArgumentException>(() => JsonlCodec.EncodeLine("{\"a\":\n1}"));
    }

    private static async Task<List<string>> ReadAllAsync(string input)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(input));
        var lines = new List<string>();
        await foreach (var line in JsonlCodec.ReadLinesAsync(
            stream, cancellationToken: TestContext.Current.CancellationToken))
        {
            lines.Add(line);
        }

        return lines;
    }

    /// <summary>
    /// Returns at most one byte per read, forcing every line to reassemble
    /// across many <c>ReadAsync</c> results.
    /// </summary>
    private sealed class TrickleStream : Stream
    {
        private readonly byte[] _data;
        private int _position;

        public TrickleStream(byte[] data)
        {
            _data = data;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => _data.Length;

        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position >= _data.Length || count == 0)
            {
                return 0;
            }

            buffer[offset] = _data[_position++];
            return 1;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_position >= _data.Length || buffer.IsEmpty)
            {
                return ValueTask.FromResult(0);
            }

            buffer.Span[0] = _data[_position++];
            return ValueTask.FromResult(1);
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
