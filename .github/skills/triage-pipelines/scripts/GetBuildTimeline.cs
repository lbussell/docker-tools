#!/usr/bin/env dotnet
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#:project common/TriagePipelines.csproj

using System.CommandLine;
using TriagePipelines;
using static System.Console;

Argument<int> buildIdArgument = new("buildId") { Description = "The build ID to fetch the timeline for." };
Option<int> maxDepthOption = new("--max-depth", "-d") { Description = "Maximum tree depth to display.", DefaultValueFactory = _ => 2 };
Option<bool> failingOption = new("--failing") { Description = "Always display failing tasks, even beyond max depth." };
Option<string> projectOption = new("--project", "-p")
{
    Description = "The Azure DevOps project (e.g., internal, public).",
    DefaultValueFactory = _ => "internal"
};

RootCommand rootCommand = new("Displays the build timeline as a tree.")
{
    buildIdArgument,
    maxDepthOption,
    failingOption,
    projectOption
};

ParseResult parseResult = rootCommand.Parse(args);
int buildId = parseResult.GetValue(buildIdArgument);
int maxDepth = parseResult.GetValue(maxDepthOption);
bool showFailing = parseResult.GetValue(failingOption);
string project = parseResult.GetValue(projectOption);

using AzureDevOpsClient client = AzureDevOpsClient.Create(project: project);
TimelineResponse timeline = await client.GetBuildTimelineAsync(buildId);
IReadOnlyList<TimelineNode> roots = timeline.BuildTree();

if (roots.Count == 0)
{
    WriteLine("No timeline records found.");
    return;
}

WriteLine($"Build {buildId} ({client.GetBuildResultUrl(buildId)}):");
PrintTree(roots, prefix: "", FormatNode, depth: 1, maxDepth, showFailing);

static void PrintTree(
    IReadOnlyList<TimelineNode> nodes,
    string prefix,
    Func<TimelineNode, string> formatLine,
    int depth,
    int maxDepth,
    bool showFailing)
{
    for (int i = 0; i < nodes.Count; i++)
    {
        TimelineNode node = nodes[i];
        bool hasFailing = showFailing && HasFailure(node);
        if (depth > maxDepth && !hasFailing) continue;

        bool isLast = i == nodes.Count - 1;
        string connector = isLast ? "└── " : "├── ";
        string childPrefix = isLast ? "    " : "│   ";
        WriteLine($"{prefix}{connector}{formatLine(node)}");
        PrintTree(node.Children, prefix + childPrefix, formatLine, depth + 1, maxDepth, showFailing);
    }
}

static bool HasFailure(TimelineNode node) =>
    node.Record.Result is "failed" || node.Children.Any(HasFailure);

static string FormatNode(TimelineNode node)
{
    string result = node.Record.Result switch
    {
        "succeeded" => "OK",
        "failed" => "FAIL",
        "skipped" => "SKIP",
        "canceled" => "CANCEL",
        "succeededWithIssues" => "WARN",
        _ => node.Record.State switch
        {
            "inProgress" => "RUNNING",
            "pending" => "PENDING",
            _ => "-"
        }
    };

    string typeAndLogId = node.Record.Log is not null
        ? $"{node.Record.Type} #{node.Record.Log.Id}"
        : node.Record.Type;

    string status = node.Children.Count > 1
        ? $"{result} ({node.Children.Count} {node.Children[0].Record.Type}s)"
        : result;

    return $"{typeAndLogId}: {node.Record.Name} | {status}";
}
