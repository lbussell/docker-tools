#!/usr/bin/env dotnet
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#:project common/TriagePipelines.csproj

using TriagePipelines;
using static System.Console;

using AzureDevOpsClient client = AzureDevOpsClient.Create();

DefinitionsResponse buildDefinitions = await client.GetBuildDefinitionsAsync(@"\dotnet\docker-tools");
List<BuildDefinitionReference> failingBuilds = buildDefinitions
    .Value.Where(definition => definition.LatestCompletedBuild is { Result: "failed" })
    .ToList();

if (failingBuilds.Count == 0)
{
    WriteLine("No failing pipelines found.");
    return;
}

WriteLine($"Found {failingBuilds.Count} failing pipeline(s):\n");

foreach (BuildDefinitionReference def in failingBuilds)
{
    ApiBuild build = def.LatestCompletedBuild!;
    WriteLine($"Pipeline: {def.Name}");
    WriteLine($"  Commit: {build.SourceVersion ?? "unknown"}");
    WriteLine($"  Link:   {client.GetBuildResultUrl(build.Id)}");
    WriteLine();
}
