using FingerTrap.Sidecar.Abstractions;
using FingerTrap.Sidecar.Ipc;
using FingerTrap.Sidecar.Settings;
using Octokit;

namespace FingerTrap.Sidecar.Status;

/// <summary>
/// GitHub status provider (ADR-0022): Octokit.NET, token borrowed from the
/// shell-fed <see cref="CredentialCache"/> per fetch (never cached here),
/// every free-text field through <see cref="StatusText"/> at row
/// construction. Expected failure modes come back as states, not throws:
/// fine-grained PATs return 403 — not 404 — for out-of-grant repos, so
/// "no access" and "not found" render distinguishably.
/// </summary>
internal sealed class GitHubStatusProvider : IStatusProvider
{
    private const int PageSize = 20;

    private readonly CredentialCache _credentials;
    private readonly GitHubStatusSettings? _settings;
    private readonly Func<string, IGitHubClient> _clientFactory;

    public GitHubStatusProvider(
        CredentialCache credentials,
        GitHubStatusSettings? settings,
        Func<string, IGitHubClient>? clientFactory = null)
    {
        _credentials = credentials;
        _settings = settings;
        _clientFactory = clientFactory ?? CreateClient;
    }

    public string Name => "github";

    public async Task<ProviderSnapshot> FetchAsync(CancellationToken cancellationToken)
    {
        var repo = _settings?.Repo;
        if (string.IsNullOrWhiteSpace(repo))
        {
            return ProviderSnapshot.Empty(Name, ProviderStates.NotConfigured,
                "set status.github.repo (\"owner/name\") in settings.json");
        }

        var parts = repo.Split('/', StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
        {
            return ProviderSnapshot.Empty(Name, ProviderStates.Error,
                $"status.github.repo must be \"owner/name\", got \"{StatusText.Sanitize(repo, 80)}\"");
        }

        if (!_credentials.TryGet(Name, out var token))
        {
            return ProviderSnapshot.Empty(Name, ProviderStates.NotConfigured,
                "no GitHub token — save a fine-grained read-only PAT below to show linked PRs, issues, and CI runs");
        }

        var (owner, name) = (parts[0], parts[1]);
        try
        {
            var client = _clientFactory(token);

            // Octokit's issue list includes pull requests; filter them out so
            // the issue rows are issues (the PR list is the PR surface).
            var issuesTask = client.Issue.GetAllForRepository(owner, name,
                new RepositoryIssueRequest { State = ItemStateFilter.Open },
                new ApiOptions { PageSize = PageSize, PageCount = 1 });
            var prsTask = client.PullRequest.GetAllForRepository(owner, name,
                new PullRequestRequest { State = ItemStateFilter.Open },
                new ApiOptions { PageSize = PageSize, PageCount = 1 });
            var runsTask = client.Actions.Workflows.Runs.List(owner, name,
                new WorkflowRunsRequest(),
                new ApiOptions { PageSize = PageSize, PageCount = 1 });

            await Task.WhenAll(issuesTask, prsTask, runsTask).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            var issues = issuesTask.Result
                .Where(i => i.PullRequest is null)
                .Select(ToIssueRow)
                .ToList();
            var prs = prsTask.Result.Select(ToPrRow).ToList();
            var runs = runsTask.Result.WorkflowRuns.Select(ToRunRow).ToList();

            return new ProviderSnapshot(Name, ProviderStates.Ok, null, issues, prs, runs);
        }
        catch (AuthorizationException)
        {
            return ProviderSnapshot.Empty(Name, ProviderStates.AuthFailed,
                "GitHub rejected the token (401) — expired or revoked; save a new one");
        }
        catch (RateLimitExceededException e)
        {
            // Ordered before ForbiddenException, which it derives from.
            return ProviderSnapshot.Empty(Name, ProviderStates.Error,
                $"GitHub rate limit exceeded; resets {e.Reset:HH:mm} UTC");
        }
        catch (ForbiddenException)
        {
            // Fine-grained PAT semantics: out-of-grant is 403, not 404.
            return ProviderSnapshot.Empty(Name, ProviderStates.AuthFailed,
                $"token has no access to {owner}/{name} (403) — check the token's repository grant");
        }
        catch (NotFoundException)
        {
            return ProviderSnapshot.Empty(Name, ProviderStates.Error,
                $"{owner}/{name} not found");
        }
        catch (ApiException e)
        {
            // Sanitized: API messages can echo attacker-influenced content.
            return ProviderSnapshot.Empty(Name, ProviderStates.Error,
                StatusText.Sanitize($"GitHub API error: {e.Message}", 200));
        }
        catch (HttpRequestException)
        {
            return ProviderSnapshot.Empty(Name, ProviderStates.Error, "network error reaching GitHub");
        }
    }

    private static GitHubClient CreateClient(string token) =>
        new GitHubClient(new ProductHeaderValue("FingerTrap"))
        {
            Credentials = new Credentials(token),
        };

    private static IssueRow ToIssueRow(Issue issue) => new(
        issue.Id,
        issue.Number,
        StatusText.Sanitize(issue.Title),
        StatusText.Sanitize(issue.User?.Login, 80),
        issue.State.StringValue,
        issue.UpdatedAt?.UtcDateTime.ToString("O") ?? string.Empty,
        StatusUrls.Validate(issue.HtmlUrl, "github.com"));

    private static PrRow ToPrRow(PullRequest pr) => new(
        pr.Id,
        pr.Number,
        StatusText.Sanitize(pr.Title),
        StatusText.Sanitize(pr.User?.Login, 80),
        pr.State.StringValue,
        pr.Draft,
        StatusText.Sanitize(pr.Head?.Ref, 120),
        pr.UpdatedAt.UtcDateTime.ToString("O"),
        StatusUrls.Validate(pr.HtmlUrl, "github.com"));

    private static RunRow ToRunRow(WorkflowRun run) => new(
        run.Id,
        run.RunNumber,
        StatusText.Sanitize(run.Name, 120),
        StatusText.Sanitize(run.DisplayTitle),
        run.Status.StringValue,
        run.Conclusion?.StringValue,
        RunOutcomes.Derive(run.Status.StringValue, run.Conclusion?.StringValue),
        StatusText.Sanitize(run.HeadBranch, 120),
        run.CreatedAt.UtcDateTime.ToString("O"),
        StatusUrls.Validate(run.HtmlUrl, "github.com"));
}
