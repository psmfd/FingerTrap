using System.Text.Json;
using FingerTrap.Sidecar.Runtime;
using FingerTrap.Sidecar.Settings;

namespace FingerTrap.Sidecar.Qualification;

/// <summary>Test-only host inputs. This source is absent from the product assembly.</summary>
internal static class Selection
{
    internal static PiLaunchContextResolver Load(string[] args, PiSettings? settings)
    {
        if (args.Length != 7 || settings is not { Mode: "managed", ProfileId: not null, RuntimeId: not null })
        {
            throw new ArgumentException("qualification requires app-root, artifact-root, descriptor, digest, revision, tools-root, settings fixture and managed settings");
        }

        using var definition = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            version = 1, id = settings.ProfileId, runtimeId = settings.RuntimeId, credentialRefs = Array.Empty<object>(),
        }));
        var profile = RuntimeProfile.Parse(definition.RootElement, args[0]);
        return new PiLaunchContextResolver(new ManagedLaunchSelection(
            profile, args[0], args[1], args[2], args[3], args[4], [args[5]]));
    }
}
