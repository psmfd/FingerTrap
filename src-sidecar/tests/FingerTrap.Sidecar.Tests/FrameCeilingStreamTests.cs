using System.Text;
using FingerTrap.Sidecar.Ipc;
using Xunit;

namespace FingerTrap.Sidecar.Tests;

public sealed class FrameCeilingStreamTests
{
    private static byte[] Frame(int bodyLength, long? declaredLength = null)
    {
        var body = new string('x', bodyLength);
        var header = $"Content-Length: {declaredLength ?? bodyLength}\r\n\r\n";
        return Encoding.ASCII.GetBytes(header + body);
    }

    private static async Task<byte[]> DrainAsync(FrameCeilingStream stream, int readSize = 4096)
    {
        using var collected = new MemoryStream();
        var buffer = new byte[readSize];
        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(), TestContext.Current.CancellationToken);
            if (read == 0)
            {
                return collected.ToArray();
            }

            collected.Write(buffer, 0, read);
        }
    }

    [Fact]
    public async Task PassesFramesThroughUnchanged()
    {
        var input = Frame(64).Concat(Frame(128)).ToArray();
        using var stream = new FrameCeilingStream(new MemoryStream(input), maxFrameBytes: 1024);

        var output = await DrainAsync(stream);

        Assert.Equal(input, output);
    }

    [Fact]
    public async Task FrameExactlyAtCeilingPasses()
    {
        var input = Frame(256);
        using var stream = new FrameCeilingStream(new MemoryStream(input), maxFrameBytes: 256);

        var output = await DrainAsync(stream);

        Assert.Equal(input, output);
    }

    [Fact]
    public async Task DeclaredLengthOverCeilingThrows()
    {
        // The body never needs to arrive — the DECLARED length is the attack
        // surface (the reader would allocate it), so the guard fires on the
        // header alone.
        var input = Frame(bodyLength: 0, declaredLength: 1025);
        using var stream = new FrameCeilingStream(new MemoryStream(input), maxFrameBytes: 1024);

        await Assert.ThrowsAsync<IOException>(() => DrainAsync(stream));
    }

    [Fact]
    public async Task SecondFrameOverCeilingThrowsAfterFirstPasses()
    {
        var input = Frame(16).Concat(Frame(bodyLength: 0, declaredLength: 9999)).ToArray();
        using var stream = new FrameCeilingStream(new MemoryStream(input), maxFrameBytes: 1024);

        await Assert.ThrowsAsync<IOException>(() => DrainAsync(stream));
    }

    [Fact]
    public async Task HeaderSplitAcrossReadsIsReassembled()
    {
        // One-byte reads force every boundary: the terminator and the
        // Content-Length digits all arrive in separate Inspect() calls.
        var input = Frame(32);
        using var stream = new FrameCeilingStream(new MemoryStream(input), maxFrameBytes: 1024);

        var output = await DrainAsync(stream, readSize: 1);

        Assert.Equal(input, output);
    }

    [Fact]
    public async Task OversizedFrameCaughtEvenWithOneByteReads()
    {
        var input = Frame(bodyLength: 0, declaredLength: 2048);
        using var stream = new FrameCeilingStream(new MemoryStream(input), maxFrameBytes: 1024);

        await Assert.ThrowsAsync<IOException>(() => DrainAsync(stream, readSize: 1));
    }

    [Fact]
    public async Task MissingContentLengthThrows()
    {
        var input = Encoding.ASCII.GetBytes("X-Whatever: 1\r\n\r\n{}");
        using var stream = new FrameCeilingStream(new MemoryStream(input), maxFrameBytes: 1024);

        await Assert.ThrowsAsync<IOException>(() => DrainAsync(stream));
    }

    [Fact]
    public async Task UnterminatedHeaderBeyondHeaderCapThrows()
    {
        var input = Encoding.ASCII.GetBytes("Content-Length: " + new string('9', 20_000));
        using var stream = new FrameCeilingStream(new MemoryStream(input), maxFrameBytes: 1024);

        await Assert.ThrowsAsync<IOException>(() => DrainAsync(stream));
    }

    [Fact]
    public async Task BodyBytesAreNeverParsedAsHeaders()
    {
        // A body that itself contains a huge Content-Length header must not
        // trip the guard — only real headers count.
        var body = "Content-Length: 999999999\r\n\r\n";
        var header = $"Content-Length: {body.Length}\r\n\r\n";
        var input = Encoding.ASCII.GetBytes(header + body).Concat(Frame(8)).ToArray();
        using var stream = new FrameCeilingStream(new MemoryStream(input), maxFrameBytes: 1024);

        var output = await DrainAsync(stream);

        Assert.Equal(input, output);
    }
}
