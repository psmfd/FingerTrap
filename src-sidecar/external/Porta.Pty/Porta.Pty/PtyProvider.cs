// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Porta.Pty
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides the ability to spawn new processes under a pseudoterminal.
    /// </summary>
    public static class PtyProvider
    {
        private static readonly TraceSource Trace = new TraceSource(nameof(PtyProvider));

        /// <summary>
        /// Spawn a new process connected to a pseudoterminal.
        /// </summary>
        /// <param name="options">The set of options for creating the pseudoterminal.</param>
        /// <param name="cancellationToken">The token to cancel process creation early.</param>
        /// <returns>A <see cref="Task{IPtyConnection}"/> that completes once the process has spawned.</returns>
        public static Task<IPtyConnection> SpawnAsync(
            PtyOptions options,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(options.App))
            {
                throw new ArgumentNullException(nameof(options.App));
            }

            if (string.IsNullOrEmpty(options.Cwd))
            {
                throw new ArgumentNullException(nameof(options.Cwd));
            }

            if (options.CommandLine == null)
            {
                throw new ArgumentNullException(nameof(options.CommandLine));
            }

            if (options.Environment == null)
            {
                throw new ArgumentNullException(nameof(options.Environment));
            }

            if (!Enum.IsDefined(typeof(EnvironmentMode), options.EnvironmentMode))
            {
                throw new ArgumentException("Invalid environment mode.", nameof(options));
            }

            var supplied = new Dictionary<string, string>(PlatformServices.EnvironmentVariableComparer);
            foreach (var entry in options.Environment)
            {
                if (string.IsNullOrEmpty(entry.Key) || entry.Key.IndexOfAny(new[] { '=', '\0' }) >= 0
                    || entry.Value == null || entry.Value.IndexOf('\0') >= 0 || supplied.ContainsKey(entry.Key))
                {
                    throw new ArgumentException("Invalid or duplicate environment entry.", nameof(options));
                }

                supplied.Add(entry.Key, entry.Value);
            }

            IDictionary<string, string> environment;
            if (options.EnvironmentMode == EnvironmentMode.Replace)
            {
                if (!Path.IsPathRooted(options.App) || Path.GetFullPath(options.App) != options.App
                    || (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && Path.GetPathRoot(options.App).Length <= 1))
                {
                    throw new ArgumentException("Replacement requires an absolute executable.", nameof(options));
                }

                environment = supplied;
            }
            else
            {
                environment = MergeEnvironment(PlatformServices.PtyEnvironment, null);
                environment = MergeEnvironment(supplied, environment);
            }

            // Never mutate the caller's options or dictionaries: callers may spawn concurrently.
            var snapshot = new PtyOptions
            {
                App = options.App, Cwd = options.Cwd, Name = options.Name,
                Rows = options.Rows, Cols = options.Cols,
                CommandLine = (string[])options.CommandLine.Clone(),
                VerbatimCommandLine = options.VerbatimCommandLine,
                Environment = environment, EnvironmentMode = options.EnvironmentMode,
            };
            return StartSnapshotAsync(snapshot, cancellationToken);
        }

        private static async Task<IPtyConnection> StartSnapshotAsync(PtyOptions snapshot, CancellationToken cancellationToken)
        {
            var connection = await PlatformServices.PtyProvider.StartTerminalAsync(snapshot, Trace, cancellationToken)
                .ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
            {
                try
                {
                    connection.Kill();
                    connection.WaitForExit(5000);
                }
                finally
                {
                    connection.Dispose();
                }

                cancellationToken.ThrowIfCancellationRequested();
            }

            return connection;
        }

        private static IDictionary<string, string> MergeEnvironment(IDictionary<string, string> enviromentToMerge, IDictionary<string, string>? environment)
        {
            if (environment == null)
            {
                environment = new Dictionary<string, string>(PlatformServices.EnvironmentVariableComparer);
                foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
                {
                    environment[entry.Key.ToString()] = entry.Value.ToString();
                }
            }

            foreach (var kvp in enviromentToMerge)
            {
                if (string.IsNullOrEmpty(kvp.Value))
                {
                    environment.Remove(kvp.Key);
                }
                else
                {
                    environment[kvp.Key] = kvp.Value;
                }
            }

            return environment;
        }
    }
}
