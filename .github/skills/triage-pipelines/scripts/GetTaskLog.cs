#!/usr/bin/env dotnet
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#:project common/TriagePipelines.csproj

using System.CommandLine;
using TriagePipelines;

Argument<int> buildIdArgument = new("buildId") { Description = "The build ID to fetch the log from." };
Argument<int> logIdArgument = new("logId") { Description = "The log ID to fetch." };
Option<string> projectOption = new("--project", "-p")
{
    Description = "The Azure DevOps project (e.g., internal, public).",
    DefaultValueFactory = _ => "internal"
};

RootCommand rootCommand = new("Fetches a task log from an Azure DevOps build.")
{
    buildIdArgument,
    logIdArgument,
    projectOption
};

ParseResult parseResult = rootCommand.Parse(args);
int buildId = parseResult.GetValue(buildIdArgument);
int logId = parseResult.GetValue(logIdArgument);
string project = parseResult.GetValue(projectOption);

using AzureDevOpsClient client = AzureDevOpsClient.Create(project: project);
string logContent = await client.GetBuildLogContentAsync(buildId, logId);
Console.Write(logContent);
