using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace FingerTrap.Sidecar.Runtime;

/// <summary>Verified immutable artifact selection; the descriptor digest comes from the host.</summary>
internal sealed record RuntimeArtifact(string Executable, IReadOnlyList<string> Extensions)
{
    public const string PinnedRevision = "e7d09c9dc5fca409f46a5476669e3c0404054d69";
    public const int MaxDescriptorBytes = 1024 * 1024;
    public const int MaxFiles = 8192;
    private const long MaxFileBytes = 512L * 1024 * 1024;
    private const long MaxTotalBytes = 4L * 1024 * 1024 * 1024;

    public static string Platform => OperatingSystem.IsWindows() ? "windows"
        : OperatingSystem.IsMacOS() ? "darwin" : OperatingSystem.IsLinux() ? "linux"
        : throw new PlatformNotSupportedException();

    public static string Architecture => RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();

    public static RuntimeArtifact Verify(string artifactRoot, string descriptorPath, string expectedSha256,
        string runtimeId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var root = LaunchPaths.ExistingDirectory(artifactRoot);
        using var descriptorStream = File.OpenRead(descriptorPath);
        if (descriptorStream.Length is <= 0 or > MaxDescriptorBytes)
        {
            throw new InvalidDataException("runtime descriptor exceeds size limit");
        }

        var bytes = new byte[checked((int)descriptorStream.Length)];
        descriptorStream.ReadExactly(bytes);
        if (descriptorStream.ReadByte() != -1 || !DigestMatches(SHA256.HashData(bytes), expectedSha256))
        {
            throw new InvalidDataException("runtime descriptor digest mismatch");
        }

        using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 8 });
        var definition = document.RootElement;
        RequireFields(definition, "version", "runtimeId", "forkRevision", "platform", "architecture",
            "executable", "files", "extensions");
        if (definition.GetProperty("version").GetInt32() != 1
            || definition.GetProperty("runtimeId").GetString() != runtimeId
            || definition.GetProperty("forkRevision").GetString() != PinnedRevision
            || definition.GetProperty("platform").GetString() != Platform
            || definition.GetProperty("architecture").GetString() != Architecture)
        {
            throw new InvalidDataException("runtime descriptor identity or platform mismatch");
        }

        var inventory = definition.GetProperty("files");
        if (inventory.GetArrayLength() is < 1 or > MaxFiles) throw new InvalidDataException("invalid runtime inventory size");
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long totalBytes = 0;
        foreach (var item in inventory.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequireFields(item, "path", "sha256");
            var relative = item.GetProperty("path").GetString()!;
            if (relative is null || !aliases.Add(relative))
            {
                throw new InvalidDataException("duplicate runtime inventory path");
            }

            var path = LaunchPaths.RelativeFile(root, relative);
            using var file = File.OpenRead(path);
            totalBytes += file.Length;
            if (file.Length > MaxFileBytes || totalBytes > MaxTotalBytes)
            {
                throw new InvalidDataException("runtime inventory exceeds byte limit");
            }

            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[81920];
            long readBytes = 0;
            int count;
            while ((count = file.Read(buffer)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                readBytes += count;
                if (readBytes > MaxFileBytes) throw new InvalidDataException("runtime file exceeds byte limit");
                hash.AppendData(buffer, 0, count);
            }

            if (!DigestMatches(hash.GetHashAndReset(), item.GetProperty("sha256").GetString()))
            {
                throw new InvalidDataException("runtime file digest mismatch");
            }

            files.Add(relative, path);
        }

        // Complete closure, including dependencies: no unlisted executable code/resources.
        var pending = new Stack<string>();
        pending.Push(root);
        var entryCount = 0;
        while (pending.TryPop(out var directory))
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (++entryCount > MaxFiles * 2) throw new InvalidDataException("runtime tree exceeds entry limit");
                LaunchPaths.RejectLink(entry);
                if (Directory.Exists(entry)) pending.Push(entry);
                else if (!files.ContainsKey(Path.GetRelativePath(root, entry).Replace('\\', '/')))
                {
                    throw new InvalidDataException("unlisted runtime file");
                }
            }
        }

        var executable = definition.GetProperty("executable").GetString()!;
        if (executable is null || !files.TryGetValue(executable, out var executablePath))
        {
            throw new InvalidDataException("runtime executable is not in the inventory");
        }

        if (!OperatingSystem.IsWindows() && (File.GetUnixFileMode(executablePath)
            & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) == 0)
        {
            throw new InvalidDataException("runtime executable has no execute permission");
        }

        var resources = definition.GetProperty("extensions");
        if (resources.GetArrayLength() > 64) throw new InvalidDataException("too many explicit extensions");
        var extensions = new List<string>();
        foreach (var resource in resources.EnumerateArray())
        {
            var name = resource.GetString();
            if (name is null || !files.TryGetValue(name, out var path) || extensions.Contains(path, StringComparer.Ordinal))
            {
                throw new InvalidDataException("invalid explicit extension selection");
            }

            extensions.Add(path);
        }

        return new RuntimeArtifact(executablePath, extensions.AsReadOnly());
    }

    private static bool DigestMatches(byte[] actual, string? expected) =>
        expected is { Length: 64 } && expected.All(char.IsAsciiHexDigit)
        && Convert.ToHexString(actual).Equals(expected, StringComparison.OrdinalIgnoreCase);

    private static void RequireFields(JsonElement value, params string[] fields)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new InvalidDataException("expected descriptor object");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            if (!fields.Contains(property.Name, StringComparer.Ordinal) || !seen.Add(property.Name))
            {
                throw new InvalidDataException("unknown or duplicate descriptor field");
            }
        }

        if (seen.Count != fields.Length) throw new InvalidDataException("missing descriptor field");
    }
}
