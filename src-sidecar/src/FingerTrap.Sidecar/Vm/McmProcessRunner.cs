using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace FingerTrap.Sidecar.Vm;

/// <summary>
/// Bounded shell-free POSIX runner. The child enters a dedicated process
/// group in posix_spawn itself, removing the post-spawn setpgid race.
/// </summary>
internal sealed partial class McmProcessRunner : IMcmProcessRunner
{
    private const short PosixSpawnSetProcessGroup = 0x0002;
    private const short DarwinSpawnCloseOnExecDefault = 0x4000;
    private const int AddressFamilyUnix = 1;
    private const int SocketStream = 1;
    private const int FileDescriptorSetFlags = 2;
    private const int FileDescriptorCloseOnExec = 1;
    private const int LinuxSocketCloseOnExec = 0x00080000;
    private const int Sigterm = 15;
    private const int Sigkill = 9;
    private const int NoSuchProcess = 3;
    private const int Interrupted = 4;
    private const int OpaqueBufferBytes = 1024;

    public async Task<McmProcessResult> RunAsync(
        McmProcessRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (OperatingSystem.IsWindows())
        {
            return Failure(McmProcessOutcome.SpawnFailure, "POSIX process containment is unavailable");
        }

        if (!ValidRequest(request))
        {
            return Failure(McmProcessOutcome.InvalidRequest, "subprocess limits or arguments are invalid");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Failure(McmProcessOutcome.Canceled, "operation was canceled before spawn");
        }

        SpawnedProcess spawned;
        try
        {
            spawned = Spawn(request);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return Failure(McmProcessOutcome.SpawnFailure, "client process could not be spawned");
        }

        await using var stdout = new FileStream(
            new SafeFileHandle(spawned.StdoutFileDescriptor, ownsHandle: true),
            FileAccess.Read,
            4096,
            isAsync: false);
        await using var stderr = new FileStream(
            new SafeFileHandle(spawned.StderrFileDescriptor, ownsHandle: true),
            FileAccess.Read,
            4096,
            isAsync: false);

        var overflow = new TaskCompletionSource<McmProcessOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stdoutTask = DrainAsync(stdout, request.Limits.MaxStdoutBytes, McmProcessOutcome.StdoutOverflow, overflow);
        var stderrTask = DrainAsync(stderr, request.Limits.MaxStderrBytes, McmProcessOutcome.StderrOverflow, overflow);
        var exitTask = Task.Run(() => WaitForExit(spawned.ProcessId));
        var timeoutTask = Task.Delay(request.Limits.Timeout, CancellationToken.None);
        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellationToken.Register(static state =>
            ((TaskCompletionSource)state!).TrySetResult(), canceled);

        var winner = await Task.WhenAny(exitTask, timeoutTask, canceled.Task, overflow.Task).ConfigureAwait(false);
        McmProcessOutcome outcome;
        ExitStatus? exit = null;
        if (winner == exitTask)
        {
            exit = await exitTask.ConfigureAwait(false);
            outcome = exit.Value.Signal is null ? McmProcessOutcome.Exited : McmProcessOutcome.Signaled;
        }
        else if (winner == timeoutTask)
        {
            outcome = McmProcessOutcome.TimedOut;
        }
        else if (winner == canceled.Task)
        {
            outcome = McmProcessOutcome.Canceled;
        }
        else
        {
            outcome = await overflow.Task.ConfigureAwait(false);
        }

        var groupAlive = ProcessGroupExists(spawned.ProcessId);
        var cleanupConfirmed = !groupAlive;
        if (winner != exitTask || groupAlive)
        {
            cleanupConfirmed = await TerminateGroupAsync(spawned.ProcessId, request.Limits).ConfigureAwait(false);
        }

        try
        {
            exit ??= await exitTask.WaitAsync(request.Limits.KillGrace, CancellationToken.None).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            cleanupConfirmed = false;
        }

        byte[] stdoutBytes;
        byte[] stderrBytes;
        try
        {
            stdoutBytes = await stdoutTask.WaitAsync(request.Limits.KillGrace, CancellationToken.None).ConfigureAwait(false);
            stderrBytes = await stderrTask.WaitAsync(request.Limits.KillGrace, CancellationToken.None).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            stdoutBytes = [];
            stderrBytes = [];
            cleanupConfirmed = false;
        }

        // The leader can exit before the drain observes its final oversized
        // pipe segment. Overflow remains authoritative after both drains end.
        if (winner == exitTask && overflow.Task.IsCompletedSuccessfully)
        {
            outcome = overflow.Task.Result;
        }

        if (!cleanupConfirmed)
        {
            return new McmProcessResult(
                McmProcessOutcome.CleanupFailed,
                stdoutBytes,
                stderrBytes,
                false,
                exit?.ExitCode,
                exit?.Signal,
                $"cleanup failed after {outcome}");
        }

        return new McmProcessResult(
            outcome,
            stdoutBytes,
            stderrBytes,
            true,
            exit?.ExitCode,
            exit?.Signal);
    }

    private static unsafe SpawnedProcess Spawn(McmProcessRequest request)
    {
        if (!Path.IsPathFullyQualified(request.ExecutablePath))
        {
            throw new InvalidOperationException("executable path must be absolute");
        }

        int* stdoutPipe = stackalloc int[2];
        int* stderrPipe = stackalloc int[2];
        if (CreateChannel(stdoutPipe) != 0)
        {
            throw new IOException("stdout pipe creation failed");
        }

        if (CreateChannel(stderrPipe) != 0)
        {
            _ = NativeClose(stdoutPipe[0]);
            _ = NativeClose(stdoutPipe[1]);
            throw new IOException("stderr pipe creation failed");
        }

        var actions = Marshal.AllocHGlobal(OpaqueBufferBytes);
        var attributes = Marshal.AllocHGlobal(OpaqueBufferBytes);
        var actionsInitialized = false;
        var attributesInitialized = false;
        var spawned = false;
        try
        {
            if (NativeFileActionsInit(actions) != 0)
            {
                throw new IOException("spawn file actions could not be initialized");
            }

            actionsInitialized = true;
            CheckSpawnCall(NativeFileActionsAddDup2(actions, stdoutPipe[1], 1));
            CheckSpawnCall(NativeFileActionsAddDup2(actions, stderrPipe[1], 2));
            // Binding fd 0 to /dev/null gives deterministic EOF and prevents
            // the runtime loader from reusing a merely-closed descriptor.
            CheckSpawnCall(NativeFileActionsAddOpen(actions, 0, "/dev/null", 0, 0));
            if (OperatingSystem.IsLinux())
            {
                CheckSpawnCall(NativeFileActionsAddCloseFrom(actions, 3));
            }
            else
            {
                CheckSpawnCall(NativeFileActionsAddClose(actions, stdoutPipe[0]));
                CheckSpawnCall(NativeFileActionsAddClose(actions, stdoutPipe[1]));
                CheckSpawnCall(NativeFileActionsAddClose(actions, stderrPipe[0]));
                CheckSpawnCall(NativeFileActionsAddClose(actions, stderrPipe[1]));
            }

            CheckSpawnCall(NativeSpawnAttributesInit(attributes));
            attributesInitialized = true;
            var spawnFlags = (short)(PosixSpawnSetProcessGroup
                | (OperatingSystem.IsMacOS() ? DarwinSpawnCloseOnExecDefault : 0));
            CheckSpawnCall(NativeSpawnAttributesSetFlags(attributes, spawnFlags));
            CheckSpawnCall(NativeSpawnAttributesSetProcessGroup(attributes, 0));

            using var arguments = new NativeStringArray([request.ExecutablePath, .. request.Arguments]);
            using var environment = new NativeStringArray(request.Environment
                .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                .Select(static pair => $"{pair.Key}={pair.Value}")
                .ToArray());
            var result = NativeSpawn(
                out var pid,
                request.ExecutablePath,
                actions,
                attributes,
                arguments.Pointer,
                environment.Pointer);
            CheckSpawnCall(result);
            spawned = true;
            return new SpawnedProcess(pid, stdoutPipe[0], stderrPipe[0]);
        }
        finally
        {
            if (actionsInitialized)
            {
                _ = NativeFileActionsDestroy(actions);
            }

            if (attributesInitialized)
            {
                _ = NativeSpawnAttributesDestroy(attributes);
            }

            Marshal.FreeHGlobal(actions);
            Marshal.FreeHGlobal(attributes);
            _ = NativeClose(stdoutPipe[1]);
            _ = NativeClose(stderrPipe[1]);
            if (!spawned)
            {
                _ = NativeClose(stdoutPipe[0]);
                _ = NativeClose(stderrPipe[0]);
            }
        }
    }

    private static async Task<byte[]> DrainAsync(
        Stream stream,
        int limit,
        McmProcessOutcome overflowOutcome,
        TaskCompletionSource<McmProcessOutcome> overflow)
    {
        using var content = new MemoryStream(Math.Min(limit, 8192));
        var buffer = new byte[4096];
        while (true)
        {
            var read = await stream.ReadAsync(buffer).ConfigureAwait(false);
            if (read == 0)
            {
                return content.ToArray();
            }

            var available = limit - checked((int)content.Length);
            if (read > available)
            {
                if (available > 0)
                {
                    content.Write(buffer, 0, available);
                }

                overflow.TrySetResult(overflowOutcome);
                return content.ToArray();
            }

            content.Write(buffer, 0, read);
        }
    }

    private static ExitStatus WaitForExit(int pid)
    {
        while (true)
        {
            var waited = NativeWaitPid(pid, out var status, 0);
            if (waited == pid)
            {
                var signal = status & 0x7f;
                return signal == 0
                    ? new ExitStatus((status >> 8) & 0xff, null)
                    : new ExitStatus(null, signal);
            }

            if (waited < 0 && Marshal.GetLastPInvokeError() == Interrupted)
            {
                continue;
            }

            throw new IOException("waitpid failed");
        }
    }

    private static async Task<bool> TerminateGroupAsync(int processGroup, McmProcessLimits limits)
    {
        SignalGroup(processGroup, Sigterm);
        if (await WaitForGroupEmptyAsync(processGroup, limits.TerminateGrace).ConfigureAwait(false))
        {
            return true;
        }

        SignalGroup(processGroup, Sigkill);
        return await WaitForGroupEmptyAsync(processGroup, limits.KillGrace).ConfigureAwait(false);
    }

    private static async Task<bool> WaitForGroupEmptyAsync(int processGroup, TimeSpan budget)
    {
        var deadline = DateTimeOffset.UtcNow + budget;
        do
        {
            if (!ProcessGroupExists(processGroup))
            {
                return true;
            }

            await Task.Delay(20).ConfigureAwait(false);
        }
        while (DateTimeOffset.UtcNow < deadline);

        return !ProcessGroupExists(processGroup);
    }

    private static bool ProcessGroupExists(int processGroup)
    {
        if (NativeKill(-processGroup, 0) == 0)
        {
            return true;
        }

        return Marshal.GetLastPInvokeError() != NoSuchProcess;
    }

    private static void SignalGroup(int processGroup, int signal)
    {
        if (NativeKill(-processGroup, signal) != 0 && Marshal.GetLastPInvokeError() != NoSuchProcess)
        {
            // Emptiness verification below converts an unhandled signal failure
            // into CleanupFailed without exposing errno or process identity.
        }
    }

    private static void CheckSpawnCall(int result)
    {
        if (result != 0)
        {
            throw new IOException("posix_spawn operation failed");
        }
    }

    private static McmProcessResult Failure(McmProcessOutcome outcome, string detail) =>
        new(outcome, [], [], true, Detail: detail);

    private readonly record struct SpawnedProcess(int ProcessId, int StdoutFileDescriptor, int StderrFileDescriptor);
    private readonly record struct ExitStatus(int? ExitCode, int? Signal);

    private sealed class NativeStringArray : IDisposable
    {
        private readonly nint[] _strings;

        public NativeStringArray(IReadOnlyList<string> values)
        {
            _strings = new nint[values.Count];
            Pointer = Marshal.AllocHGlobal((values.Count + 1) * IntPtr.Size);
            for (var index = 0; index < values.Count; index++)
            {
                _strings[index] = Marshal.StringToCoTaskMemUTF8(values[index]);
                Marshal.WriteIntPtr(Pointer, index * IntPtr.Size, _strings[index]);
            }

            Marshal.WriteIntPtr(Pointer, values.Count * IntPtr.Size, IntPtr.Zero);
        }

        public nint Pointer { get; }

        public void Dispose()
        {
            foreach (var value in _strings)
            {
                Marshal.FreeCoTaskMem(value);
            }

            Marshal.FreeHGlobal(Pointer);
        }
    }

    private static bool ValidRequest(McmProcessRequest request) =>
        request.ExecutablePath.Length is > 0 and <= 4096
        && request.Arguments.Count <= 16
        && request.Arguments.All(static argument => argument is { Length: <= 4096 })
        && request.Environment.Count <= 16
        && request.Environment.All(static pair =>
            pair.Key is { Length: > 0 and <= 128 }
            && !pair.Key.Contains('=')
            && pair.Value.Length <= 4096)
        && request.Limits.Timeout > TimeSpan.Zero
        && request.Limits.Timeout <= TimeSpan.FromMinutes(1)
        && request.Limits.TerminateGrace > TimeSpan.Zero
        && request.Limits.TerminateGrace <= TimeSpan.FromSeconds(10)
        && request.Limits.KillGrace > TimeSpan.Zero
        && request.Limits.KillGrace <= TimeSpan.FromSeconds(10)
        && request.Limits.MaxStdoutBytes is > 0 and <= 4 * 1024 * 1024
        && request.Limits.MaxStderrBytes is > 0 and <= 4 * 1024 * 1024;

    private static unsafe int CreateChannel(int* fileDescriptors)
    {
        var socketType = SocketStream
            | (OperatingSystem.IsLinux() ? LinuxSocketCloseOnExec : 0);
        var result = NativeSocketPair(AddressFamilyUnix, socketType, 0, fileDescriptors);
        if (result != 0 || OperatingSystem.IsLinux())
        {
            return result;
        }

        // Darwin has no pipe2/SOCK_CLOEXEC. Mark both descriptors before
        // leaving this synchronous spawn section; CLOEXEC_DEFAULT below also
        // prevents concurrent Mcm spawns from inheriting unrelated handles.
        // Coordinating this unavoidable two-syscall Darwin window with every
        // non-Mcm child-launch path is tracked by #175 and blocks production
        // VM wiring; #174 keeps this runner fake-only and unreachable.
        if (NativeFcntl(fileDescriptors[0], FileDescriptorSetFlags, FileDescriptorCloseOnExec) == 0
            && NativeFcntl(fileDescriptors[1], FileDescriptorSetFlags, FileDescriptorCloseOnExec) == 0)
        {
            return 0;
        }

        _ = NativeClose(fileDescriptors[0]);
        _ = NativeClose(fileDescriptors[1]);
        return -1;
    }

    [LibraryImport("libc", EntryPoint = "socketpair", SetLastError = true)]
    private static unsafe partial int NativeSocketPair(
        int domain, int type, int protocol, int* fileDescriptors);

    [LibraryImport("libc", EntryPoint = "fcntl", SetLastError = true)]
    private static partial int NativeFcntl(int fileDescriptor, int command, int value);

    [LibraryImport("libc", EntryPoint = "close", SetLastError = true)]
    private static partial int NativeClose(int fileDescriptor);

    [LibraryImport("libc", EntryPoint = "posix_spawn_file_actions_init")]
    private static partial int NativeFileActionsInit(nint actions);

    [LibraryImport("libc", EntryPoint = "posix_spawn_file_actions_destroy")]
    private static partial int NativeFileActionsDestroy(nint actions);

    [LibraryImport("libc", EntryPoint = "posix_spawn_file_actions_adddup2")]
    private static partial int NativeFileActionsAddDup2(nint actions, int fileDescriptor, int targetFileDescriptor);

    [LibraryImport("libc", EntryPoint = "posix_spawn_file_actions_addclose")]
    private static partial int NativeFileActionsAddClose(nint actions, int fileDescriptor);

    [LibraryImport("libc", EntryPoint = "posix_spawn_file_actions_addclosefrom_np")]
    private static partial int NativeFileActionsAddCloseFrom(nint actions, int firstFileDescriptor);

    [LibraryImport("libc", EntryPoint = "posix_spawn_file_actions_addopen", StringMarshalling = StringMarshalling.Utf8)]
    private static partial int NativeFileActionsAddOpen(
        nint actions, int fileDescriptor, string path, int openFlags, uint mode);

    [LibraryImport("libc", EntryPoint = "posix_spawnattr_init")]
    private static partial int NativeSpawnAttributesInit(nint attributes);

    [LibraryImport("libc", EntryPoint = "posix_spawnattr_destroy")]
    private static partial int NativeSpawnAttributesDestroy(nint attributes);

    [LibraryImport("libc", EntryPoint = "posix_spawnattr_setflags")]
    private static partial int NativeSpawnAttributesSetFlags(nint attributes, short flags);

    [LibraryImport("libc", EntryPoint = "posix_spawnattr_setpgroup")]
    private static partial int NativeSpawnAttributesSetProcessGroup(nint attributes, int processGroup);

    [LibraryImport("libc", EntryPoint = "posix_spawn", StringMarshalling = StringMarshalling.Utf8)]
    private static partial int NativeSpawn(
        out int processId,
        string path,
        nint actions,
        nint attributes,
        nint arguments,
        nint environment);

    [LibraryImport("libc", EntryPoint = "waitpid", SetLastError = true)]
    private static partial int NativeWaitPid(int processId, out int status, int options);

    [LibraryImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static partial int NativeKill(int processId, int signal);
}
