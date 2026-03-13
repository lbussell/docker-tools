// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Microsoft.DotNet.DockerTools.Skills;

/// <summary>
/// Lightweight helper for running external processes and capturing their output.
/// </summary>
public static class ProcessHelper
{
    /// <summary>
    /// Runs a process with the specified file name and arguments, returning its standard output.
    /// </summary>
    /// <param name="fileName">The executable to run (e.g., <c>"gh"</c>, <c>"git"</c>).</param>
    /// <param name="args">Command-line arguments passed to the process.</param>
    /// <returns>The process's standard output as a string.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the process exits with a non-zero exit code.</exception>
    public static async Task<string> RunAsync(string fileName, params string[] args)
    {
        ProcessStartInfo startInfo = new(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (string arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start process '{fileName}'.");

        // Read stdout and stderr concurrently to avoid deadlocks
        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            string stderr = await stderrTask;
            throw new InvalidOperationException(
                $"'{fileName}' exited with code {process.ExitCode}: {stderr}");
        }

        return await stdoutTask;
    }
}
