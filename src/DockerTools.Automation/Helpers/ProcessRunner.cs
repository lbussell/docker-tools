// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.DockerTools.Automation.Helpers;

/// <summary>
/// Provides async process execution for running CLI commands.
/// </summary>
internal static class ProcessRunner
{
    internal static async Task<string> RunAsync(
        string fileName,
        string arguments,
        string? workingDirectory,
        ILogger logger,
        string? logArgumentsOverride = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Executing: {FileName} {Arguments}", fileName, logArgumentsOverride ?? arguments);

        var startInfo = new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        if (workingDirectory is not null)
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        var process = Process.Start(startInfo)!;

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            var logArgs = logArgumentsOverride ?? arguments;
            throw new InvalidOperationException(
                $"Command '{fileName} {logArgs}' failed with exit code {process.ExitCode}: {stderr}");
        }

        return stdout.Trim();
    }
}
