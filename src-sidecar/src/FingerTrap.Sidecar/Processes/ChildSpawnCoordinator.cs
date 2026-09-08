using System.Diagnostics;

namespace FingerTrap.Sidecar.Processes;

/// <summary>
/// Serializes the short descriptor-creation-to-spawn window on Darwin
/// (ADR-0030). Child lifetime, I/O, waiting, and cleanup remain outside.
/// </summary>
internal static class ChildSpawnCoordinator
{
    private static readonly SemaphoreSlim DarwinGate = new(1, 1);

    public static T Run<T>(Func<T> spawn)
    {
        ArgumentNullException.ThrowIfNull(spawn);
        if (!OperatingSystem.IsMacOS())
        {
            return spawn();
        }

        DarwinGate.Wait();
        try
        {
            return spawn();
        }
        finally
        {
            DarwinGate.Release();
        }
    }

    public static async Task<T> RunAsync<T>(
        Func<Task<T>> spawn,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(spawn);
        if (!OperatingSystem.IsMacOS())
        {
            return await spawn().ConfigureAwait(false);
        }

        await DarwinGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await spawn().ConfigureAwait(false);
        }
        finally
        {
            DarwinGate.Release();
        }
    }
}

/// <summary>Starts managed child processes through the process-wide coordinator.</summary>
internal static class ChildProcessLauncher
{
    public static bool Start(Process process)
    {
        ArgumentNullException.ThrowIfNull(process);
        return ChildSpawnCoordinator.Run(process.Start);
    }

    public static Process? Start(ProcessStartInfo startInfo)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        return ChildSpawnCoordinator.Run(() => Process.Start(startInfo));
    }
}
