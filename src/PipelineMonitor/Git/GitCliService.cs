// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.PipelineMonitor.Process;

namespace Microsoft.DotNet.PipelineMonitor.Git;

/// <summary>
/// Service for interacting with Git via the command line.
/// </summary>
internal interface IGitCliService
{
    /// <summary>
    /// Gets the list of configured Git remotes.
    /// </summary>
    /// <returns>An enumerable of Git remotes.</returns>
    Task<IEnumerable<GitRemote>> GetRemotesAsync();
}

/// <summary>
/// Service for interacting with Git via the command line.
/// </summary>
internal sealed class GitCliService : IGitCliService
{
    private readonly IProcessService _processService;

    public GitCliService(IProcessService processService)
    {
        _processService = processService;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<GitRemote>> GetRemotesAsync()
    {
        ProcessResult result = await _processService.ExecuteAsync("git", "remote -v");

        List<GitRemote> remotes = [];
        foreach (string line in result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            // Format: origin  https://github.com/dotnet/docker-tools.git (fetch)
            //         origin  https://github.com/dotnet/docker-tools.git (push)
            string[] parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                continue;
            }

            string name = parts[0];
            string[] urlAndType = parts[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (urlAndType.Length < 2)
            {
                continue;
            }

            string url = urlAndType[0];
            string type = urlAndType[1].Trim('(', ')');

            remotes.Add(new GitRemote(name, url, type));
        }

        return remotes;
    }
}
