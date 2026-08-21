using System.Runtime.CompilerServices;

namespace FingerTrap.Sidecar.Tests;

internal static class TestModuleInitializer
{
    /// <summary>
    /// Redirect the RPC-child reap registry (#124) to a scratch file for the
    /// whole test run, so services default-constructed in the relay tests
    /// never write the operator's real app-data registry. Runs once at
    /// assembly load, before any test.
    /// </summary>
    [ModuleInitializer]
    public static void Init()
    {
        var scratch = Path.Combine(
            Path.GetTempPath(),
            $"fingertrap-test-rpc-children-{Guid.NewGuid():N}.json");
        Environment.SetEnvironmentVariable("FINGERTRAP_RPC_CHILD_REGISTRY_PATH", scratch);
    }
}
