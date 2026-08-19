using FingerTrap.Sidecar.Abstractions;
using FingerTrap.Sidecar.Ipc;
using FingerTrap.Sidecar.Settings;
using FingerTrap.Sidecar.Status;
using FingerTrap.Sidecar.Text;
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

    [Theory]
    [InlineData("completed", "succeeded", "success")]
    [InlineData("completed", "failed", "failure")]
    [InlineData("completed", "canceled", "cancelled")]
    [InlineData("completed", "partiallySucceeded", "failure")]
    [InlineData("inProgress", null, "in_progress")]
    [InlineData("cancelling", null, "in_progress")]
    [InlineData("notStarted", null, "queued")]
    [InlineData("postponed", null, "queued")]
    [InlineData("completed", null, "unknown")]
    [InlineData("completed", "brandNewResult", "unknown")]
    public void DerivesAdoVocabulary(string status, string? result, string expected)
    {
        // #72: ADO's status/result vocabulary through the same single
        // collapse point; partiallySucceeded is a failure on purpose, and
        // unrecognized values degrade to unknown, same as Derive.
        Assert.Equal(expected, RunOutcomes.DeriveAdo(status, result));
    }
}

public sealed class StatusUrlsTests
{
    [Theory]
    [InlineData("https://github.com/psmfd/FingerTrap/actions/runs/1")]
    [InlineData("https://GITHUB.com/psmfd/FingerTrap/pull/73")]
    public void AllowsHttpsOnAllowlistedHost(string url)
    {
        Assert.NotNull(StatusUrls.Validate(url, "github.com"));
    }

    [Theory]
    [InlineData("http://github.com/psmfd")]
    [InlineData("https://evil.example/github.com")]
    [InlineData("https://github.com.evil.example/x")]
    [InlineData("https://user@github.com/x")]
    [InlineData("https://github.com@evil.example/x")]
    [InlineData("file:///etc/passwd")]
    [InlineData("javascript:alert(1)")]
    [InlineData("not a url")]
    [InlineData("")]
    [InlineData(null)]
    public void RejectsEverythingElse(string? url)
    {
        Assert.Null(StatusUrls.Validate(url, "github.com"));
    }

    [Fact]
    public void HostMatchIsExactPerAllowlistEntry()
    {
        Assert.NotNull(StatusUrls.Validate("https://dev.azure.com/org/proj", "dev.azure.com"));
        Assert.Null(StatusUrls.Validate("https://dev.azure.com/org/proj", "github.com"));
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
