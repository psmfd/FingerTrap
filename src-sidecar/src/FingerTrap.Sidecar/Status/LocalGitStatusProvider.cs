using System.Diagnostics;
using System.Text;
using FingerTrap.Sidecar.Abstractions;
using FingerTrap.Sidecar.Settings;

namespace FingerTrap.Sidecar.Status;

/// <summary>Result of one <c>git</c> invocation, for the injectable runner.</summary>
internal sealed record GitResult(int ExitCode, string Stdout, string Stderr);

/// <summary>
/// Local-git status provider (ADR-0022): shells out to the <c>git</c> CLI —
/// never LibGit2Sharp, whose native binaries recreate the companion-library
/// bundling problem ADR-0008/0010 already paid for once. Its whole surface
/// is one line (branch, ahead/behind, dirty count), published as the
/// snapshot's <c>Detail</c> on an <c>ok</c> state rather than a row list —
/// none of the per-item row shapes fit, and inventing one for a single
/// sentence would be shape for shape's sake.
///
/// LOAD-BEARING: every child process redirects stdout and stderr. An
/// unredirected child inherits the sidecar's stdout — the ADR-0002 JSON-RPC
/// framing — and corrupts the channel.
/// </summary>
internal sealed class LocalGitStatusProvider : IStatusProvider
{
    private static readonly TimeSpan GitTimeout = TimeSpan.FromSeconds(10);

    private readonly LocalGitStatusSettings? _settings;
    private readonly Func<IReadOnlyList<string>, string, CancellationToken, Task<GitResult>> _runner;

    public LocalGitStatusProvider(
        LocalGitStatusSettings? settings,
        Func<IReadOnlyList<string>, string, CancellationToken, Task<GitResult>>? runner = null)
    {
        _settings = settings;
        _runner = runner ?? RunGitAsync;
    }

    public string Name => "git";

    public async Task<ProviderSnapshot> FetchAsync(CancellationToken cancellationToken)
    {
        var path = _settings?.Path;
        if (string.IsNullOrWhiteSpace(path))
        {
            return ProviderSnapshot.Empty(Name, ProviderStates.NotConfigured,
                "set status.git.path in settings.json to watch a working tree");
        }

        if (!Directory.Exists(path))
        {
            return ProviderSnapshot.Empty(Name, ProviderStates.Error,
                StatusText.Sanitize($"status.git.path does not exist: {path}", 200));
        }

        GitResult result;
        try
        {
            // One call carries the whole surface: `--branch` emits
            // `# branch.head`, `# branch.upstream`, and `# branch.ab +A -B`
            // headers ahead of the porcelain-v2 change records.
            result = await _runner(
                ["status", "--porcelain=v2", "--branch"], path, cancellationToken).ConfigureAwait(false);
        }
        catch (GitUnavailableException e)
        {
            return ProviderSnapshot.Empty(Name, ProviderStates.Error, e.Message);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ProviderSnapshot.Empty(Name, ProviderStates.Error, "git status timed out");
        }

        if (result.ExitCode != 0)
        {
            // 128 with "not a git repository" is the expected shape; anything
            // else still reads better verbatim (sanitized) than a guess.
            return ProviderSnapshot.Empty(Name, ProviderStates.Error,
                StatusText.Sanitize($"git status failed: {FirstLine(result.Stderr)}", 200));
        }

        return new ProviderSnapshot(Name, ProviderStates.Ok, Summarize(result.Stdout), [], [], []);
    }

    /// <summary>Pure porcelain-v2 → one-line summary, e.g.
    /// <c>"main ↑2 ↓0 · 3 dirty · upstream origin/main"</c>. Internal for tests.</summary>
    internal static string Summarize(string porcelainV2)
    {
        string branch = "(unknown)";
        string? upstream = null;
        int? ahead = null, behind = null;
        var dirty = 0;

        foreach (var raw in porcelainV2.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith("# branch.head ", StringComparison.Ordinal))
            {
                branch = line["# branch.head ".Length..];
            }
            else if (line.StartsWith("# branch.upstream ", StringComparison.Ordinal))
            {
                upstream = line["# branch.upstream ".Length..];
            }
            else if (line.StartsWith("# branch.ab ", StringComparison.Ordinal))
            {
                // "# branch.ab +<ahead> -<behind>"
                var parts = line["# branch.ab ".Length..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2
                    && int.TryParse(parts[0].TrimStart('+'), out var a)
                    && int.TryParse(parts[1].TrimStart('-'), out var b))
                {
                    (ahead, behind) = (a, b);
                }
            }
            else if (!line.StartsWith('#'))
            {
                // 1/2/u/? records are all "something needs attention".
                dirty++;
            }
        }

        var summary = new StringBuilder();
        summary.Append(branch == "(detached)" ? "detached HEAD" : branch);
        if (ahead is not null && behind is not null)
        {
            summary.Append($" ↑{ahead} ↓{behind}");
        }

        summary.Append(dirty == 0 ? " · clean" : $" · {dirty} dirty");
        if (upstream is not null)
        {
            summary.Append($" · upstream {upstream}");
        }
        else if (branch != "(detached)")
        {
            summary.Append(" · no upstream");
        }

        return StatusText.Sanitize(summary.ToString(), 200);
    }

    private static async Task<GitResult> RunGitAsync(
        IReadOnlyList<string> args, string workingDirectory, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
        };
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        // Read-only probes must never take optional locks (index refresh)
        // or hang on an auth prompt.
        startInfo.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                throw new GitUnavailableException("git could not be started");
            }
        }
        catch (System.ComponentModel.Win32Exception)
        {
            throw new GitUnavailableException("git not found on PATH");
        }

        process.StandardInput.Close();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(GitTimeout);

        var stdoutTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Already gone.
            }

            throw;
        }

        return new GitResult(
            process.ExitCode,
            await stdoutTask.ConfigureAwait(false),
            await stderrTask.ConfigureAwait(false));
    }

    private static string FirstLine(string text)
    {
        var index = text.IndexOfAny(['\r', '\n']);
        return (index >= 0 ? text[..index] : text).Trim();
    }
}

/// <summary>The git executable itself is unavailable — distinct from a
/// repository-level failure, which arrives as a nonzero exit code.</summary>
internal sealed class GitUnavailableException(string message) : Exception(message);
