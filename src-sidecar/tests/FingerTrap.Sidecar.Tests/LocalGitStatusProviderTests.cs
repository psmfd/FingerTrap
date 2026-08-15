using System.Diagnostics;
using FingerTrap.Sidecar.Abstractions;
using FingerTrap.Sidecar.Settings;
using FingerTrap.Sidecar.Status;
using Xunit;

namespace FingerTrap.Sidecar.Tests;

public sealed class LocalGitStatusProviderTests
{
    [Fact]
    public async Task NoPathIsNotConfigured()
    {
        var provider = new LocalGitStatusProvider(null);

        var snapshot = await provider.FetchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ProviderStates.NotConfigured, snapshot.State);
        Assert.Contains("status.git.path", snapshot.Detail);
    }

    [Fact]
    public async Task MissingDirectoryIsErrorWithoutRunningGit()
    {
        var ran = false;
        var provider = new LocalGitStatusProvider(
            new LocalGitStatusSettings { Path = "/no/such/dir/fingertrap-test" },
            (_, _, _) => { ran = true; return Task.FromResult(new GitResult(0, "", "")); });

        var snapshot = await provider.FetchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ProviderStates.Error, snapshot.State);
        Assert.False(ran);
    }

    [Fact]
    public async Task NonRepoExitCodeBecomesErrorWithFirstStderrLine()
    {
        var provider = new LocalGitStatusProvider(
            new LocalGitStatusSettings { Path = Path.GetTempPath() },
            (_, _, _) => Task.FromResult(new GitResult(
                128, "", "fatal: not a git repository (or any of the parent directories): .git\nmore")));

        var snapshot = await provider.FetchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ProviderStates.Error, snapshot.State);
        Assert.Contains("not a git repository", snapshot.Detail);
        Assert.DoesNotContain("more", snapshot.Detail);
    }

    [Fact]
    public async Task GitUnavailableBecomesErrorState()
    {
        var provider = new LocalGitStatusProvider(
            new LocalGitStatusSettings { Path = Path.GetTempPath() },
            (_, _, _) => throw new GitUnavailableException("git not found on PATH"));

        var snapshot = await provider.FetchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ProviderStates.Error, snapshot.State);
        Assert.Equal("git not found on PATH", snapshot.Detail);
    }

    [Fact]
    public async Task OkSnapshotCarriesSummaryDetailAndNoRows()
    {
        var porcelain = "# branch.oid deadbeef\n# branch.head main\n" +
            "# branch.upstream origin/main\n# branch.ab +2 -1\n" +
            "1 .M N... 100644 100644 100644 abc def src/a.ts\n";
        var provider = new LocalGitStatusProvider(
            new LocalGitStatusSettings { Path = Path.GetTempPath() },
            (_, _, _) => Task.FromResult(new GitResult(0, porcelain, "")));

        var snapshot = await provider.FetchAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ProviderStates.Ok, snapshot.State);
        Assert.Equal("main ↑2 ↓1 · 1 dirty · upstream origin/main", snapshot.Detail);
        Assert.Empty(snapshot.Issues);
        Assert.Empty(snapshot.PullRequests);
        Assert.Empty(snapshot.Runs);
    }

    [Fact]
    public void SummarizeCleanBranchWithoutUpstream()
    {
        var porcelain = "# branch.oid deadbeef\n# branch.head feat/x\n";

        Assert.Equal("feat/x · clean · no upstream", LocalGitStatusProvider.Summarize(porcelain));
    }

    [Fact]
    public void SummarizeDetachedHead()
    {
        var porcelain = "# branch.oid deadbeef\n# branch.head (detached)\n";

        Assert.Equal("detached HEAD · clean", LocalGitStatusProvider.Summarize(porcelain));
    }

    [Fact]
    public void SummarizeCountsUntrackedAndConflictsAsDirty()
    {
        var porcelain = "# branch.head main\n" +
            "? new-file.txt\n" +
            "u UU N... 100644 100644 100644 100644 a b c both-modified.txt\n" +
            "2 R. N... 100644 100644 100644 abc def R100 new\told\n";

        Assert.StartsWith("main · 3 dirty", LocalGitStatusProvider.Summarize(porcelain));
    }

    [Fact]
    public void SummarizeSanitizesHostileBranchNames()
    {
        // A branch name is attacker-influenceable text (git allows almost
        // anything); the summary passes through StatusText like every other
        // provider string.
        var porcelain = "# branch.head evil[2Jname\n";

        Assert.StartsWith("evil[2Jname", LocalGitStatusProvider.Summarize(porcelain));
    }

    /// <summary>
    /// End-to-end against the real git CLI in a throwaway repo — the fake
    /// runner cannot prove the ProcessStartInfo redirection contract or the
    /// porcelain flag spelling. git is present on every CI runner OS.
    /// </summary>
    [Fact]
    public async Task RealGitEndToEnd()
    {
        var repo = Directory.CreateTempSubdirectory("ft-git-test").FullName;
        try
        {
            RunGit(repo, "init", "--initial-branch=main");
            RunGit(repo, "config", "user.email", "test@example.invalid");
            RunGit(repo, "config", "user.name", "Test");
            await File.WriteAllTextAsync(
                Path.Combine(repo, "a.txt"), "hello", TestContext.Current.CancellationToken);
            RunGit(repo, "add", "a.txt");
            RunGit(repo, "commit", "-m", "initial");
            await File.WriteAllTextAsync(
                Path.Combine(repo, "b.txt"), "dirty", TestContext.Current.CancellationToken);

            var provider = new LocalGitStatusProvider(new LocalGitStatusSettings { Path = repo });
            var snapshot = await provider.FetchAsync(TestContext.Current.CancellationToken);

            Assert.Equal(ProviderStates.Ok, snapshot.State);
            Assert.StartsWith("main · 1 dirty · no upstream", snapshot.Detail);
        }
        finally
        {
            TryDeleteRepo(repo);
        }
    }

    /// <summary>Best-effort temp cleanup. On Windows, .git/objects files
    /// are read-only and Directory.Delete throws UnauthorizedAccessException
    /// until the attribute is cleared.</summary>
    private static void TryDeleteRepo(string path)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(path, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void RunGit(string repo, params string[] args)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = repo,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo)!;
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);
    }
}
