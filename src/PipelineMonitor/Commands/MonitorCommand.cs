// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ConsoleAppFramework;

namespace Microsoft.DotNet.PipelineMonitor.Commands;

internal sealed class MonitorCommand
{
    [Command("monitor")]
    public int Execute(string organizationUrl, string projectName, int pipelineId)
    {
        Console.WriteLine($"Monitoring pipeline {pipelineId} in project {projectName} at {organizationUrl}...");
        return 0;
    }
}
