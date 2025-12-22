// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ConsoleAppFramework;
using Microsoft.DotNet.PipelineMonitor.Git;

namespace Microsoft.DotNet.PipelineMonitor.Commands;

internal sealed class RunPipelineCommand
{
    private readonly IGitCliService _gitCliService;

    public RunPipelineCommand(IGitCliService gitCliService)
    {
        _gitCliService = gitCliService;
    }

    [Command("run-pipeline")]
    public async Task<int> Execute(string organizationUrl, string projectName, int pipelineId)
    {
        Console.WriteLine($"Starting pipeline {pipelineId} in project {projectName} at {organizationUrl}...");

        // Validate GitCliService.GetRemotesAsync
        Console.WriteLine("\nTesting GitCliService.GetRemotesAsync()...");
        var remotes = await _gitCliService.GetRemotesAsync();
        foreach (var remote in remotes)
        {
            Console.WriteLine($"  Remote: {remote.Name} -> {remote.Url} ({remote.Type})");
        }

        return 0;
    }
}
