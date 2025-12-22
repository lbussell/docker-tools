// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace Microsoft.DotNet.PipelineMonitor.Process;

/// <summary>
/// Service for executing external processes.
/// </summary>
internal interface IProcessService
{
    /// <summary>
    /// Executes a process and returns the result.
    /// </summary>
    /// <param name="fileName">The name of the executable to run.</param>
    /// <param name="arguments">The arguments to pass to the executable.</param>
    /// <returns>The result from the process execution.</returns>
    Task<ProcessResult> ExecuteAsync(string fileName, string arguments);
}

/// <inheritdoc/>
internal sealed class ProcessService : IProcessService
{
    /// <inheritdoc/>
    public async Task<ProcessResult> ExecuteAsync(string fileName, string arguments)
    {
        using var process = new System.Diagnostics.Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        process.Start();

        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        string output = await outputTask;
        string error = await errorTask;

        return new ProcessResult(process.ExitCode, output, error);
    }
}
