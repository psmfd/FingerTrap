using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using FingerTrap.Sidecar.Runtime;
using FingerTrap.Sidecar.Settings;

namespace FingerTrap.Sidecar.Tests;

internal sealed class ManagedLaunchFixture : IDisposable
{
    public string Root { get; } = LaunchPaths.ExistingDirectory(Directory.CreateTempSubdirectory("ft-launch-").FullName);
    public string ArtifactRoot => Path.Combine(Root, "runtime");
    public string DescriptorPath => Path.Combine(Root, "runtime.json");
    public string AppRoot => Path.Combine(Root, "app");
    public string Cwd => Path.Combine(Root, "project");
    public string Digest { get; private set; } = "";
    public JsonObject Descriptor { get; }
    // Synthetic FakePi source identity; never a production Pi pin.
    public const string CandidateRevision = "1111111111111111111111111111111111111111";
    public const string RuntimeId = "pi-0.85.1-psmfd.1";
    public string Executable => Path.Combine(ArtifactRoot, OperatingSystem.IsWindows() ? "FakePi.exe" : "FakePi");

    public ManagedLaunchFixture(bool includeRuntime = false)
    {
        Directory.CreateDirectory(ArtifactRoot);
        Directory.CreateDirectory(AppRoot);
        Directory.CreateDirectory(Cwd);
        var source = Path.GetDirectoryName(FakePiShim.FakePiDllPath())!;
        foreach (var file in Directory.EnumerateFiles(source))
        {
            if (Path.GetFileName(file).StartsWith("FakePi", StringComparison.Ordinal))
            {
                File.Copy(file, Path.Combine(ArtifactRoot, Path.GetFileName(file)));
            }
        }

        if (includeRuntime) IncludeLocalRuntime();

        var files = new JsonArray();
        foreach (var file in Directory.EnumerateFiles(ArtifactRoot).Order(StringComparer.Ordinal))
        {
            files.Add(new JsonObject
            {
                ["path"] = Path.GetFileName(file),
                ["sha256"] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant(),
            });
        }

        Descriptor = new JsonObject
        {
            ["version"] = 2, ["runtimeId"] = RuntimeId, ["forkRevision"] = CandidateRevision,
            ["startupPolicy"] = RuntimeArtifact.StartupPolicy,
            ["platform"] = RuntimeArtifact.Platform, ["architecture"] = RuntimeArtifact.Architecture,
            ["executable"] = Path.GetFileName(Executable), ["files"] = files, ["extensions"] = new JsonArray(),
        };
        SaveDescriptor();
    }

    private void IncludeLocalRuntime()
    {
        // A framework-dependent apphost needs ambient DOTNET_ROOT on some machines.
        // Assemble a test-only app-local closure from the running test runtime instead;
        // replacement tests must also work with a genuinely empty environment.
        var runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        foreach (var file in Directory.EnumerateFiles(runtime))
        {
            File.Copy(file, Path.Combine(ArtifactRoot, Path.GetFileName(file)), overwrite: true);
        }

        var hostLibrary = OperatingSystem.IsWindows() ? "hostfxr.dll"
            : OperatingSystem.IsMacOS() ? "libhostfxr.dylib" : "libhostfxr.so";
        var host = Path.GetFullPath(Path.Combine(runtime, "..", "..", "..", "host", "fxr",
            Path.GetFileName(runtime), hostLibrary));
        File.Copy(host, Path.Combine(ArtifactRoot, hostLibrary));
        var configPath = Path.Combine(ArtifactRoot, "FakePi.runtimeconfig.json");
        var config = JsonNode.Parse(File.ReadAllText(configPath))!;
        var options = config["runtimeOptions"]!;
        options["includedFrameworks"] = new JsonArray(options["framework"]!.DeepClone());
        options.AsObject().Remove("framework");
        File.WriteAllText(configPath, config.ToJsonString());

        var depsPath = Path.Combine(ArtifactRoot, "FakePi.deps.json");
        var app = JsonNode.Parse(File.ReadAllText(depsPath))!;
        var framework = JsonNode.Parse(File.ReadAllText(Path.Combine(runtime, "Microsoft.NETCore.App.deps.json")))!;
        var target = framework["targets"]!.AsObject().First().Value!.AsObject();
        foreach (var entry in app["targets"]!.AsObject().First().Value!.AsObject())
        {
            target[entry.Key] = entry.Value!.DeepClone();
        }

        foreach (var entry in app["libraries"]!.AsObject())
        {
            framework["libraries"]![entry.Key] = entry.Value!.DeepClone();
        }

        File.WriteAllText(depsPath, framework.ToJsonString());
    }

    public void SaveDescriptor()
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(Descriptor);
        File.WriteAllBytes(DescriptorPath, bytes);
        Digest = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    public RuntimeProfile Profile(string id)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            version = 1, id, runtimeId = RuntimeId, credentialRefs = Array.Empty<object>(),
        }));
        return RuntimeProfile.Parse(document.RootElement, AppRoot);
    }

    public ManagedLaunchSelection Selection(string id) =>
        new(Profile(id), AppRoot, ArtifactRoot, DescriptorPath, Digest, CandidateRevision, []);

    public static PiSettings Settings(string id) => new() { Mode = "managed", ProfileId = id, RuntimeId = RuntimeId };

    public PiLaunchContext Resolve(string id = "alpha", bool rpc = true) =>
        new PiLaunchContextResolver(Selection(id)).Resolve(Settings(id), rpc, null, Cwd, null, null, CancellationToken.None);

    public void Dispose() => Directory.Delete(Root, recursive: true);
}
