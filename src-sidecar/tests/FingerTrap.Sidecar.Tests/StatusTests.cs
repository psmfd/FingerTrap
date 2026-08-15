using FingerTrap.Sidecar.Abstractions;
using FingerTrap.Sidecar.Ipc;
using FingerTrap.Sidecar.Settings;
using FingerTrap.Sidecar.Status;
using Xunit;

namespace FingerTrap.Sidecar.Tests;

public sealed class StatusTextTests
{
    [Fact]
    public void StripsC0ControlsIncludingEscapeSequenceIntroducers()
    {
        // \u escapes, not \x: C# \x is variable-length and eats following
        // hex digits. Only the control BYTES are stripped — the printable
        // "[2J" remnant stays, defanged without its ESC introducer.
        var hostile = "evil[2Jtitle\r\nwithbell";

        Assert.Equal("evil[2Jtitlewithbell", StatusText.Sanitize(hostile));
    }

    [Fact]
    public void StripsC1Controls()
    {
        // 0x9B is a bare CSI — one byte away from a full escape sequence.
        Assert.Equal("ab", StatusText.Sanitize("ab"));
    }

    [Fact]
    public void StripsBidiControls()
    {
        // Trojan-Source class: RLO makes display order diverge from bytes.
        var spoofed = "release‮0.2v/";

        Assert.Equal("release0.2v/", StatusText.Sanitize(spoofed));
    }

    [Fact]
    public void KeepsOrdinaryUnicode()
    {
        Assert.Equal("héllo — ☂ 日本語", StatusText.Sanitize("héllo — ☂ 日本語"));
    }

    [Fact]
    public void CapsLengthWithEllipsis()
    {
        var result = StatusText.Sanitize(new string('x', 500), maxLength: 10);

        Assert.Equal(11, result.Length);
        Assert.EndsWith("…", result, StringComparison.Ordinal);
    }

    [Fact]
    public void NullAndEmptyAreEmpty()
    {
        Assert.Equal(string.Empty, StatusText.Sanitize(null));
        Assert.Equal(string.Empty, StatusText.Sanitize(string.Empty));
    }
}

public sealed class RunOutcomesTests
{
    [Theory]
    [InlineData("completed", "success", "success")]
    [InlineData("completed", "failure", "failure")]
    [InlineData("completed", "cancelled", "cancelled")]
    [InlineData("completed", "startup_failure", "startup_failure")]
    [InlineData("in_progress", null, "in_progress")]
    [InlineData("queued", null, "queued")]
    [InlineData("waiting", null, "queued")]
    [InlineData("pending", null, "queued")]
    public void DerivesDocumentedPairs(string status, string? conclusion, string expected)
    {
        Assert.Equal(expected, RunOutcomes.Derive(status, conclusion));
    }

    [Fact]
    public void UnrecognizedConclusionDegradesToUnknown_NeverPassesThrough()
    {
        // The API has grown conclusions before; a value this module has
        // never seen must not render as though it were understood.
        Assert.Equal("unknown", RunOutcomes.Derive("completed", "brand_new_conclusion"));
    }

    [Fact]
    public void CompletedWithoutConclusionIsUnknown()
    {
        Assert.Equal("unknown", RunOutcomes.Derive("completed", null));
    }
}

public sealed class GitHubStatusProviderTests
{
    [Fact]
    public async Task NoRepoConfigured_ReportsNotConfigured()
    {
        var provider = new GitHubStatusProvider(new CredentialCache(), null);

        var snapshot = await provider.FetchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ProviderStates.NotConfigured, snapshot.State);
        Assert.Contains("status.github.repo", snapshot.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MalformedRepo_ReportsError()
    {
        var provider = new GitHubStatusProvider(
            new CredentialCache(),
            new GitHubStatusSettings { Repo = "not-owner-slash-name" });

        var snapshot = await provider.FetchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ProviderStates.Error, snapshot.State);
    }

    [Fact]
    public async Task NoToken_ReportsNotConfigured_AndNeverBuildsAClient()
    {
        var factoryCalls = 0;
        var provider = new GitHubStatusProvider(
            new CredentialCache(),
            new GitHubStatusSettings { Repo = "psmfd/FingerTrap" },
            _ =>
            {
                factoryCalls++;
                throw new InvalidOperationException("must not be reached");
            });

        var snapshot = await provider.FetchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ProviderStates.NotConfigured, snapshot.State);
        Assert.Equal(0, factoryCalls);
    }
}
