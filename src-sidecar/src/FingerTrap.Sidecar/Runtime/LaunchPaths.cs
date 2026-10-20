namespace FingerTrap.Sidecar.Runtime;

/// <summary>Host roots may have platform aliases; paths beneath them may not contain links.</summary>
internal static class LaunchPaths
{
    public static string ExistingDirectory(string path)
    {
        if (!Path.IsPathFullyQualified(path) || path.Contains('\0', StringComparison.Ordinal))
        {
            throw new ArgumentException("launch directory must be absolute", nameof(path));
        }

        var full = Path.GetFullPath(path);
        var current = Path.GetPathRoot(full)!;
        foreach (var part in full[current.Length..].Split(Path.DirectorySeparatorChar,
                     StringSplitOptions.RemoveEmptyEntries))
        {
            var entry = new DirectoryInfo(Path.Combine(current, part));
            if (!entry.Exists) throw new IOException("launch directory does not exist");
            current = (entry.ResolveLinkTarget(returnFinalTarget: true) ?? entry).FullName;
        }

        return Path.TrimEndingDirectorySeparator(current);
    }

    public static string RelativeFile(string root, string relative)
    {
        if (string.IsNullOrEmpty(relative) || relative.Length > 512 || relative.Contains('\\', StringComparison.Ordinal)
            || relative.Contains(':', StringComparison.Ordinal) || relative.Contains('\0', StringComparison.Ordinal)
            || relative.Split('/').Any(part => part is "" or "." or ".." || part.EndsWith(' ') || part.EndsWith('.')))
        {
            throw new InvalidDataException("invalid artifact relative path");
        }

        var current = root;
        foreach (var part in relative.Split('/'))
        {
            current = Path.Combine(current, part);
            RejectLink(current);
        }

        if (!File.Exists(current)) throw new FileNotFoundException("required runtime file is missing");
        return current;
    }

    public static void RejectLink(string path)
    {
        // File.Exists follows links and reports false for dangling links; attributes do not.
        var info = new FileInfo(path);
        if (info.LinkTarget is not null
            || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("links are not permitted beneath a managed launch root");
        }
    }

    public static string CreateProfileDirectory(string root, string relative)
    {
        RejectLink(root);
        if (!OperatingSystem.IsWindows() && (File.GetUnixFileMode(root)
                & (UnixFileMode.GroupWrite | UnixFileMode.OtherWrite)) != 0)
        {
            throw new IOException("managed application/profile root is writable by other users");
        }

        var current = root;
        foreach (var part in relative.Split('/'))
        {
            current = Path.Combine(current, part);
            if (new DirectoryInfo(current).LinkTarget is not null)
            {
                throw new IOException("profile directory must not be a link");
            }

            if (OperatingSystem.IsWindows())
            {
                Directory.CreateDirectory(current);
            }
            else
            {
                Directory.CreateDirectory(current, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                var mode = File.GetUnixFileMode(current);
                if ((mode & (UnixFileMode.GroupWrite | UnixFileMode.OtherWrite)) != 0)
                {
                    throw new IOException("profile directory is writable by other users");
                }
            }

            RejectLink(current);
        }

        return current;
    }
}
