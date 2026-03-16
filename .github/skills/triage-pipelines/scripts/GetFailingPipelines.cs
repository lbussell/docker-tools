#!/usr/bin/env dotnet
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#:project ../../../../src/Skills/Skills.csproj

using System.CommandLine;
using Microsoft.DotNet.DockerTools.Skills;
using static System.Console;

Argument<string> folderArgument = new("folder")
{
    Description = "The Azure DevOps pipeline folder path (e.g., dotnet/docker-tools)."
};
Option<string> projectOption = new("--project", "-p")
{
    Description = "The Azure DevOps project (e.g., internal, public).",
    DefaultValueFactory = _ => "internal"
};

RootCommand rootCommand = new("Lists failing pipelines in an Azure DevOps pipeline folder.")
{
    folderArgument,
    projectOption
};

ParseResult parseResult = rootCommand.Parse(args);
string folder = parseResult.GetValue(folderArgument) ?? "";
string project = parseResult.GetValue(projectOption) ?? "internal";

// Normalize to the backslash-prefixed format the API expects (e.g., \dotnet\docker-tools)
folder = @"\" + folder.Trim('\\', '/').Replace('/', '\\');

using AzureDevOpsClient client = AzureDevOpsClient.Create(project: project);

DefinitionsResponse buildDefinitions = await client.GetBuildDefinitionsAsync(folder);
List<BuildDefinitionReference> unhealthyPipelines = buildDefinitions
    .Value.Where(definition => definition.LatestCompletedBuild is { Result: "failed" or "partiallySucceeded" })
    .ToList();

if (unhealthyPipelines.Count == 0)
{
    WriteLine("No failing or warning pipelines found.");
    return;
}

WriteLine($"Found {unhealthyPipelines.Count} unhealthy pipeline(s):\n");

foreach (BuildDefinitionReference def in unhealthyPipelines)
{
    ApiBuild build = def.LatestCompletedBuild!;
    string result = build.Result switch
    {
        "failed" => "Failed",
        "partiallySucceeded" => "SucceededWithIssues",
        _ => build.Result ?? "unknown",
    };
    WriteLine($"Pipeline: {def.Name}");
    WriteLine($"  Result: {result}");
    WriteLine($"  Commit: {build.SourceVersion ?? "unknown"}");
    WriteLine($"  Link:   {client.GetBuildResultUrl(build.Id)}");
    WriteLine();
}
