using System.Runtime.InteropServices;
using System.Security.Cryptography;
using FingerTrap.Sidecar.Abstractions;

namespace FingerTrap.Sidecar.Vm;

internal readonly record struct VmTrustResult(bool Trusted, string? Detail)
{
    public static VmTrustResult Reject(string detail) => new(false, detail);
}

/// <summary>
/// Validates ADR-0029's residual pathname-launch trust contract. Every path
/// component is inspected without following symlinks, then the executable is
/// hashed immediately before spawn by the provider.
/// </summary>
internal sealed partial class VmExecutableTrust
{
    private const int StatBufferBytes = 256;
    private readonly uint _uid = OperatingSystem.IsWindows() ? 0 : GetUid();

    public VmTrustResult Validate(VmExecutableIdentity identity)
    {
        if (OperatingSystem.IsWindows())
        {
            return VmTrustResult.Reject("VM executables are unsupported on Windows");
        }

        if (OperatingSystem.IsLinux() && RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            return VmTrustResult.Reject("executable metadata validation is unsupported on this Linux architecture");
        }

        if (string.IsNullOrEmpty(identity.Path) || identity.Path.Length > 4096 || !Path.IsPathFullyQualified(identity.Path))
        {
            return VmTrustResult.Reject("executable path is not absolute");
        }

        var fullPath = Path.GetFullPath(identity.Path);
        if (!string.Equals(fullPath, identity.Path, StringComparison.Ordinal))
        {
            return VmTrustResult.Reject("executable path is not canonical");
        }

        var current = Path.GetPathRoot(fullPath) ?? "/";
        foreach (var component in fullPath[(Path.GetPathRoot(fullPath)?.Length ?? 1)..]
                     .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, component);
            var metadata = ReadMetadata(current);
            if (metadata is null)
            {
                return VmTrustResult.Reject("executable path metadata is unavailable");
            }

            if (metadata.Value.IsSymlink)
            {
                return VmTrustResult.Reject("executable path contains a symbolic link");
            }

            if ((metadata.Value.Mode & 0x12) != 0)
            {
                return VmTrustResult.Reject("executable path is writable by group or others");
            }

            if (metadata.Value.Uid != 0 && metadata.Value.Uid != _uid)
            {
                return VmTrustResult.Reject("executable path has an untrusted owner");
            }
        }

        var executableMetadata = ReadMetadata(fullPath);
        if (executableMetadata is null || !executableMetadata.Value.IsRegular)
        {
            return VmTrustResult.Reject("executable is not a regular file");
        }

        if (!File.Exists(fullPath))
        {
            return VmTrustResult.Reject("executable does not exist");
        }

        var mode = File.GetUnixFileMode(fullPath);
        if ((mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) == 0)
        {
            return VmTrustResult.Reject("file is not executable");
        }

        string digest;
        try
        {
            using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            digest = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return VmTrustResult.Reject("executable could not be hashed");
        }

        if (identity.Sha256.Length != 64)
        {
            return VmTrustResult.Reject("executable digest is invalid");
        }

        return CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.ASCII.GetBytes(digest),
            System.Text.Encoding.ASCII.GetBytes(identity.Sha256))
            ? new VmTrustResult(true, null)
            : VmTrustResult.Reject("executable digest changed");
    }

    private static unsafe Metadata? ReadMetadata(string path)
    {
        var buffer = stackalloc byte[StatBufferBytes];
        if (NativeLstat(path, (nint)buffer) != 0)
        {
            return null;
        }

        if (OperatingSystem.IsMacOS())
        {
            var mode = *(ushort*)(buffer + 4);
            var uid = *(uint*)(buffer + 16);
            return new Metadata(uid, mode, (mode & 0xF000) == 0xA000, (mode & 0xF000) == 0x8000);
        }

        var linuxMode = *(uint*)(buffer + 24);
        var linuxUid = *(uint*)(buffer + 28);
        return new Metadata(linuxUid, linuxMode, (linuxMode & 0xF000) == 0xA000, (linuxMode & 0xF000) == 0x8000);
    }

    private readonly record struct Metadata(uint Uid, uint Mode, bool IsSymlink, bool IsRegular);

    [LibraryImport("libc", EntryPoint = "lstat", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial int NativeLstat(string path, nint buffer);

    [LibraryImport("libc", EntryPoint = "getuid")]
    private static partial uint GetUid();
}
