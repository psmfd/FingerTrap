using Xunit;

namespace FingerTrap.Sidecar.Tests.Goldens;

public sealed class GoldenNormalizerTests
{
    [Fact]
    public void DurationNormalizationPreservesOtherPayload()
    {
        // Latest Pi qualification (#210): tool/message timings vary between recordings.
        const string wire = "{\"type\":\"message_end\",\"durationMs\":14,\"tokens\":14,\"text\":\"durationMs:14\"}";
        var normalizer = new GoldenNormalizer([]);
        var normalized = normalizer.NormalizeText(wire);
        Assert.Equal(wire.Replace("\"durationMs\":14", "\"durationMs\":\"@TIME:duration@\""), normalized);
        Assert.Equal(normalized, normalizer.NormalizeText(wire.Replace("\"durationMs\":14", "\"durationMs\":10")));
        Assert.Equal(normalized, normalizer.NormalizeText(normalized));
        Assert.NotEqual(normalized, normalizer.NormalizeText(wire.Replace("\"tokens\":14", "\"tokens\":10")));
        Assert.NotEqual(normalized, normalizer.NormalizeText(wire.Replace("durationMs:14", "durationMs:10")));
    }

    [Fact]
    public void EpochTimestampCollisionsDoNotChangeTheTranscript()
    {
        // Maintained Pi qualification (#202): adjacent events can share a millisecond.
        var sameTick = new GoldenNormalizer([]).Normalize([
            GoldenRecord.Wire(false, "{\"type\":\"message_start\",\"timestamp\":1800000000000}"),
            GoldenRecord.Wire(false, "{\"type\":\"message_end\",\"timestamp\":1800000000000}"),
        ]);
        var differentTicks = new GoldenNormalizer([]).Normalize([
            GoldenRecord.Wire(false, "{\"type\":\"message_start\",\"timestamp\":1800000000001}"),
            GoldenRecord.Wire(false, "{\"type\":\"message_end\",\"timestamp\":1800000000002}"),
        ]);
        Assert.Equal(sameTick.Select(GoldenStore.Serialize), differentTicks.Select(GoldenStore.Serialize));
    }

    [Fact]
    public void NormalizationPreservesNonvolatilePayloadAndIsIdempotent()
    {
        const string wire = "{\"type\":\"message_end\",\"id\":\"req_7\",\"timestamp\":1800000000000,\"tokens\":1800000000000,\"text\":\"1800000000000\"}";
        var normalizer = new GoldenNormalizer([]);
        var normalized = normalizer.NormalizeText(wire);
        Assert.Equal(wire.Replace("\"timestamp\":1800000000000", "\"timestamp\":\"@TIME:epoch@\""), normalized);
        Assert.Equal(normalized, normalizer.NormalizeText(normalized));
        Assert.NotEqual(normalized, normalizer.NormalizeText(wire.Replace("message_end", "message_start")));
        Assert.NotEqual(normalized, normalizer.NormalizeText(wire.Replace("req_7", "req_8")));
    }
}
