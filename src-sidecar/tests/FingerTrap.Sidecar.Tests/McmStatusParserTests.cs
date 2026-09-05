using FingerTrap.Sidecar.Abstractions;
using FingerTrap.Sidecar.Vm;
using Xunit;

namespace FingerTrap.Sidecar.Tests;

public sealed class McmStatusParserTests
{
    private const string Valid = """
        {"schema":1,"name":"devbox","exists":true,"state":"running","reachable":true,"stamp_present":true,"provisioned_profile":"personal","configured_profile":"personal","drift":false,"needs_provision":false,"expertise_write_configured":false,"expertise_token":{"present":true,"scope":"read","detail":"token healthy"},"errors":0,"warnings":0,"result":"PASS"}
        """;

    [Fact]
    public void Parse_ValidSchema1_ReturnsOwnedSnapshot()
    {
        var result = McmStatusParser.Parse(System.Text.Encoding.UTF8.GetBytes(Valid));

        Assert.Equal(VmStatusOutcome.Ok, result.Outcome);
        Assert.Equal("devbox", result.Snapshot?.Name);
        Assert.Equal("token healthy", result.Snapshot?.ExpertiseToken?.Detail);
    }

    [Theory]
    [InlineData("not json", VmStatusOutcome.MalformedOutput)]
    [InlineData("{\"schema\":2}", VmStatusOutcome.UnsupportedSchema)]
    [InlineData("{\"schema\":1,\"schema\":1}", VmStatusOutcome.InvalidOutput)]
    [InlineData("{\"schema\":1} trailing", VmStatusOutcome.MalformedOutput)]
    public void Parse_InvalidDocuments_FailClosed(string json, VmStatusOutcome expected)
    {
        var result = McmStatusParser.Parse(System.Text.Encoding.UTF8.GetBytes(json));
        Assert.Equal(expected, result.Outcome);
        Assert.Null(result.Snapshot);
    }

    [Fact]
    public void Parse_UnknownSchema1Property_IsAdditive()
    {
        var json = Valid.Replace("\"result\":\"PASS\"", "\"future\":true,\"result\":\"PASS\"", StringComparison.Ordinal);
        Assert.Equal(VmStatusOutcome.Ok, McmStatusParser.Parse(System.Text.Encoding.UTF8.GetBytes(json)).Outcome);
    }

    [Fact]
    public void Parse_UnrecognizedClosedState_IsRejected()
    {
        var json = Valid.Replace("\"running\"", "\"suspended\"", StringComparison.Ordinal);
        Assert.Equal(VmStatusOutcome.InvalidOutput, McmStatusParser.Parse(System.Text.Encoding.UTF8.GetBytes(json)).Outcome);
    }

    [Fact]
    public void Parse_ValidJsonWithWrongPropertyType_IsInvalidNotMalformed()
    {
        var json = Valid.Replace("\"exists\":true", "\"exists\":\"true\"", StringComparison.Ordinal);
        Assert.Equal(VmStatusOutcome.InvalidOutput, McmStatusParser.Parse(System.Text.Encoding.UTF8.GetBytes(json)).Outcome);
    }

    [Theory]
    [InlineData("\"reachable\":true", "\"reachable\":null")]
    [InlineData("\"stamp_present\":true", "\"stamp_present\":false")]
    [InlineData("\"scope\":\"read\"", "\"scope\":null")]
    public void Parse_IncoherentSchemaFields_AreRejected(string original, string replacement)
    {
        var json = Valid.Replace(original, replacement, StringComparison.Ordinal);
        Assert.Equal(VmStatusOutcome.InvalidOutput, McmStatusParser.Parse(System.Text.Encoding.UTF8.GetBytes(json)).Outcome);
    }

    [Fact]
    public void Parse_AdditiveDataStillObeysStringAndCollectionBounds()
    {
        var longString = Valid.Replace(
            "\"result\":\"PASS\"",
            $"\"future\":\"{new string('x', 1201)}\",\"result\":\"PASS\"",
            StringComparison.Ordinal);
        Assert.Equal(VmStatusOutcome.InvalidOutput, McmStatusParser.Parse(System.Text.Encoding.UTF8.GetBytes(longString)).Outcome);

        var array = string.Join(',', Enumerable.Repeat("0", 65));
        var longArray = Valid.Replace(
            "\"result\":\"PASS\"", $"\"future\":[{array}],\"result\":\"PASS\"", StringComparison.Ordinal);
        Assert.Equal(VmStatusOutcome.InvalidOutput, McmStatusParser.Parse(System.Text.Encoding.UTF8.GetBytes(longArray)).Outcome);
    }
}
