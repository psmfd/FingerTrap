using FingerTrap.Sidecar.Executables;
using FingerTrap.Sidecar.Processes;
using FingerTrap.Sidecar.Pty;
using FingerTrap.Sidecar.Settings;

namespace FingerTrap.Sidecar.Runtime;

internal sealed record PiLaunchContext(
    string Executable, IReadOnlyList<string> Arguments, string? WorkingDirectory,
    ChildEnvironmentMode EnvironmentMode, IReadOnlyDictionary<string, string> Environment,
    string? ProfileId = null, string? RuntimeId = null);

/// <summary>Internal host inputs, never deserialized from settings or RPC requests.</summary>
internal sealed record ManagedLaunchSelection(
    RuntimeProfile Profile, string ApplicationRoot, string ArtifactRoot,
    string DescriptorPath, string DescriptorSha256, string CandidateRevision, IReadOnlyList<string> ToolDirectories);

internal sealed class PiLaunchContextResolver
{
    private readonly ManagedLaunchSelection? _managed;

    public PiLaunchContextResolver(ManagedLaunchSelection? managed = null)
    {
        _managed = managed is null ? null : managed with
        {
            ToolDirectories = Array.AsReadOnly(managed.ToolDirectories.ToArray()),
        };
    }

    public PiLaunchContext Resolve(PiSettings? settings, bool rpc, string? requestedPath,
        string? cwd, string? sessionPath, IReadOnlyDictionary<string, string>? environment,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var managed = _managed;
        if (settings?.Mode != "managed")
        {
            var args = new List<string>();
            if (rpc) args.AddRange(["--mode", "rpc"]);
            if (!string.IsNullOrEmpty(sessionPath)) args.AddRange(["--session", sessionPath]);
            return new(PiExecutableResolver.ResolvePi(requestedPath, settings), args.AsReadOnly(),
                rpc ? cwd : PtyService.ResolveCwd(cwd), ChildEnvironmentMode.Inherit,
                ChildEnvironment.Snapshot(environment));
        }

        if (managed is null)
        {
            // The production composition never supplies managed host inputs.
            _ = PiExecutableResolver.ResolvePi(requestedPath, settings);
            throw new InvalidOperationException("managed production gate unexpectedly returned");
        }

        if (requestedPath is not null || settings.Path is not null || environment is not null || sessionPath is not null)
        {
            throw new InvalidOperationException("managed executable/environment overrides and resume are unavailable");
        }

        var profile = managed.Profile;
        if (profile.Id != settings.ProfileId || profile.RuntimeId != settings.RuntimeId
            || profile.CredentialReferences.Count != 0)
        {
            throw new InvalidOperationException("managed selection mismatch or unresolved credential reference");
        }

        var appRoot = LaunchPaths.ExistingDirectory(managed.ApplicationRoot);
        var expectedProfileRoot = Path.Combine(Path.GetFullPath(managed.ApplicationRoot), "profiles", profile.Id);
        if (!string.Equals(profile.Root, expectedProfileRoot, OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            throw new InvalidOperationException("profile does not belong to the selected application root");
        }

        var workingDirectory = LaunchPaths.ExistingDirectory(cwd
            ?? throw new ArgumentException("managed launch requires an explicit working directory", nameof(cwd)));
        var artifact = RuntimeArtifact.Verify(managed.ArtifactRoot, managed.DescriptorPath,
            managed.DescriptorSha256, profile.RuntimeId, managed.CandidateRevision, cancellationToken);
        var artifactRoot = LaunchPaths.ExistingDirectory(managed.ArtifactRoot);
        var root = LaunchPaths.CreateProfileDirectory(appRoot, $"profiles/{profile.Id}");
        if (IsWithin(root, artifactRoot) || IsWithin(artifactRoot, root))
        {
            throw new InvalidOperationException("runtime artifact and profile state must be separate");
        }

        foreach (var directory in new[] { "agent", "agent/sessions", "agent/extensions", "cache", "tmp",
                     "home", "home/config", "home/data", "home/state", "home/local", "home/roaming" })
        {
            LaunchPaths.CreateProfileDirectory(root, directory);
        }

        var agentDir = Path.Combine(root, "agent");
        foreach (var name in new[] { "settings.json", "auth.json", "oauth.json", "models.json",
                     "SYSTEM.md", "APPEND_SYSTEM.md", "keybindings.json" })
        {
            var path = Path.Combine(agentDir, name);
            if (Path.Exists(path) || new FileInfo(path).LinkTarget is not null)
            {
                throw new InvalidOperationException("profile startup configuration is not qualified for managed launch");
            }
        }

        var toolDirectories = managed.ToolDirectories.Select(LaunchPaths.ExistingDirectory).ToArray();
        if (toolDirectories.Any(path => path.Contains(Path.PathSeparator)))
        {
            throw new InvalidOperationException("invalid managed tool directory");
        }

        var home = Path.Combine(root, "home");
        var temp = Path.Combine(root, "tmp");
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["HOME"] = home, ["USERPROFILE"] = home,
            ["APPDATA"] = Path.Combine(home, "roaming"), ["LOCALAPPDATA"] = Path.Combine(home, "local"),
            ["XDG_CONFIG_HOME"] = Path.Combine(home, "config"), ["XDG_DATA_HOME"] = Path.Combine(home, "data"),
            ["XDG_STATE_HOME"] = Path.Combine(home, "state"), ["XDG_CACHE_HOME"] = Path.Combine(root, "cache"),
            ["TMPDIR"] = temp, ["TMP"] = temp, ["TEMP"] = temp,
            ["PI_CODING_AGENT_DIR"] = agentDir, ["PI_CODING_AGENT_SESSION_DIR"] = Path.Combine(agentDir, "sessions"),
            ["FINGERTRAP_PROFILE_ID"] = profile.Id, ["FINGERTRAP_RUNTIME_ID"] = profile.RuntimeId,
            ["LANG"] = "en_US.UTF-8",
        };
        // An empty PATH means "search cwd" on Unix. Omit it when the host supplies no tools.
        if (toolDirectories.Length > 0) values["PATH"] = string.Join(Path.PathSeparator, toolDirectories);
        if (!rpc) values["TERM"] = "xterm-256color";
        if (OperatingSystem.IsWindows())
        {
            var windows = LaunchPaths.ExistingDirectory(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
            values["SystemRoot"] = windows;
            values["WINDIR"] = windows;
        }

        var arguments = new List<string> { "--startup-policy", RuntimeArtifact.StartupPolicy };
        if (rpc) arguments.AddRange(["--mode", "rpc"]);
        arguments.AddRange(["--no-extensions", "--no-skills", "--no-prompt-templates", "--no-themes",
            "--no-context-files", "--no-approve", "--offline", "--session-dir", values["PI_CODING_AGENT_SESSION_DIR"],
            "--system-prompt", "You are a coding assistant."]);
        foreach (var extension in artifact.Extensions) arguments.AddRange(["--extension", extension]);
        return new(artifact.Executable, arguments.AsReadOnly(), workingDirectory,
            ChildEnvironmentMode.Replace, ChildEnvironment.Snapshot(values), profile.Id, profile.RuntimeId);
    }

    private static bool IsWithin(string path, string root)
    {
        var relative = Path.GetRelativePath(root, path);
        return relative == "." || (!Path.IsPathRooted(relative) && relative != ".."
            && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal));
    }
}
