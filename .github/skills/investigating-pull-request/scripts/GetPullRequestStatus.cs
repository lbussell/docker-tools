#!/usr/bin/env dotnet
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#:project ../../../../src/Skills/Skills.csproj

using System.CommandLine;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.DotNet.DockerTools.Skills;
using static Spectre.Console.AnsiConsole;

Argument<string> pullRequestArgument = new("pull-request")
{
    Description = "The pull request number to look up."
};
Option<string> repoOption = new("--repo", "-R")
{
    Description = "The GitHub repository in owner/repo format."
};
Option<bool> showAllOption = new("--show-all", "-a")
{
    Description = "Show all tasks in the build timeline, not just failing ones."
};

RootCommand rootCommand = new("Displays build timelines for Azure Pipelines runs associated with a GitHub pull request.")
{
    pullRequestArgument,
    repoOption,
    showAllOption,
};

ParseResult parseResult = rootCommand.Parse(args);
string pr = parseResult.GetValue(pullRequestArgument)!;
string? repo = parseResult.GetValue(repoOption);
bool showAll = parseResult.GetValue(showAllOption);

// When --repo isn't set, gh auto-detects from the current git remote.
List<string> repoArgs = repo is not null ? ["--repo", repo] : [];

// Fetch PR metadata and check runs in parallel
List<string> prViewArgs = ["pr", "view", pr, "--json", "title,headRefName,headRepositoryOwner,headRepository,author", .. repoArgs];
List<string> checksArgs = ["pr", "checks", pr, "--json", "name,link,state", .. repoArgs];

Task<JsonDocument> prInfoTask = RunGhJsonAsync([.. prViewArgs]);
Task<JsonDocument> checksTask = RunGhJsonAsync([.. checksArgs]);

using JsonDocument prInfo = await prInfoTask;
using JsonDocument checksJson = await checksTask;

string prTitle = prInfo.RootElement.GetProperty("title").GetString() ?? "";
string prBranch = prInfo.RootElement.GetProperty("headRefName").GetString() ?? "";
string prAuthor = prInfo.RootElement.GetProperty("author").GetProperty("login").GetString() ?? "";
string forkOwner = prInfo.RootElement.GetProperty("headRepositoryOwner").GetProperty("login").GetString() ?? "";
string forkRepo = prInfo.RootElement.GetProperty("headRepository").GetProperty("name").GetString() ?? "";

WriteLine($"# Pull Request: {prTitle}");
WriteLine();
WriteLine($"- Author: {prAuthor}");
WriteLine($"- Fork: {forkOwner}/{forkRepo}");
WriteLine($"- Branch: {prBranch}");
WriteLine();

// Separate Azure Pipelines check runs from other checks (e.g., GitHub Actions, CLA bots).
(Dictionary<int, PipelineRun> pipelines, List<OtherCheck> otherChecks) = ClassifyCheckRuns(checksJson);

if (pipelines.Count == 0 && otherChecks.Count == 0)
{
    WriteLine("No checks found for this pull request.");
    return 0;
}

Func<TimelineNode, bool>? filter = showAll
    ? null
    : node => node.Record.Type is not "Task" || node.Record.Result is "failed";

// Group pipeline runs by (org, project) so we can share AzureDevOpsClient instances
IEnumerable<IGrouping<(string Org, string Project), PipelineRun>> groups =
    pipelines.Values.GroupBy(p => (p.Org, p.Project));

WriteLine("## Azure Pipelines Checks");
WriteLine();
if (!groups.Any())
{
    WriteLine("No Azure Pipelines runs found for this pull request.");
    WriteLine();
}

foreach (IGrouping<(string Org, string Project), PipelineRun> group in groups)
{
    using AzureDevOpsClient client = AzureDevOpsClient.Create(org: group.Key.Org, project: group.Key.Project);

    foreach (PipelineRun pipeline in group.OrderBy(p => p.Name))
    {
        BuildDetail build = await client.GetBuildAsync(pipeline.BuildId);
        TimelineResponse timeline = await client.GetBuildTimelineAsync(pipeline.BuildId);
        IReadOnlyList<TimelineNode> roots = timeline.BuildTree();

        string buildResultUrl = client.GetBuildResultUrl(pipeline.BuildId);
        string label = $"Azure Pipelines: {build.Definition.Name} - Build {pipeline.BuildId} ({buildResultUrl}):";

        if (roots.Count == 0)
        {
            WriteLine($"{label} (no timeline records)");
        }
        else
        {
            Spectre.Console.Tree tree = BuildTimelineRendering.RenderTree(roots, label, filter);
            Write(tree);
        }

        WriteLine();
    }
}

if (otherChecks.Count > 0)
{
    WriteLine("## Other Checks");
    WriteLine();
    WriteLine("| Check | Result | URL |");
    foreach (OtherCheck otherCheck in otherChecks.OrderBy(c => c.Name))
    {
        string statusText = otherCheck.State switch
        {
            "SUCCESS" => "OK",
            "FAILURE" => "FAIL",
            "PENDING" or "EXPECTED" => "PENDING",
            _ => otherCheck.State,
        };
        WriteLine($"| {otherCheck.Name} | {statusText} | {otherCheck.Link} |");
    }
    WriteLine();
}

return 0;

/// <summary>
/// Runs a gh CLI command and parses the output as a JSON document.
/// </summary>
static async Task<JsonDocument> RunGhJsonAsync(params IEnumerable<string> arguments)
{
    string response = await ProcessHelper.RunAsync("gh", arguments.ToArray());
    JsonDocument responseJson = JsonDocument.Parse(response);
    return responseJson ?? throw new InvalidOperationException("Failed to parse JSON response.");
}

/// <summary>
/// Classifies check runs into Azure Pipelines runs (grouped by build ID) and other checks.
/// Azure Pipelines reports one check run per job, so we deduplicate by build ID, preferring the
/// top-level pipeline entry (the one without a job name in parentheses).
/// </summary>
static (Dictionary<int, PipelineRun> Pipelines, List<OtherCheck> OtherChecks) ClassifyCheckRuns(
    JsonDocument checksJson)
{
    Dictionary<int, PipelineRun> pipelines = [];
    List<OtherCheck> otherChecks = [];

    foreach (JsonElement checkRun in checksJson.RootElement.EnumerateArray())
    {
        string detailsLink = checkRun.GetProperty("link").GetString() ?? "";
        string name = checkRun.GetProperty("name").GetString() ?? "";
        string state = checkRun.GetProperty("state").GetString() ?? "";

        if (!TryParseAzureDevOpsUrl(detailsLink, out string? org, out string? project, out int buildId))
        {
            otherChecks.Add(new OtherCheck(name, state, detailsLink));
            continue;
        }

        // Azure Pipelines reports a top-level check (e.g., "my-pipeline") plus per-job checks
        // (e.g., "my-pipeline (Build Linux_amd64)"). Keep the top-level entry when available.
        bool isTopLevelPipelineCheck = !name.Contains(" (");
        bool shouldReplace = !pipelines.ContainsKey(buildId)
            || (isTopLevelPipelineCheck && !pipelines[buildId].IsTopLevel);

        if (shouldReplace)
        {
            pipelines[buildId] = new PipelineRun(name, buildId, org, project, isTopLevelPipelineCheck);
        }
    }

    return (pipelines, otherChecks);
}

/// <summary>
/// Parses an Azure DevOps build URL to extract the org, project, and build ID.
/// Expected format: <c>https://dev.azure.com/{org}/{project}/_build/results?buildId={id}</c>
/// </summary>
static bool TryParseAzureDevOpsUrl(
    string url,
    [NotNullWhen(true)] out string? org,
    [NotNullWhen(true)] out string? project,
    out int buildId)
{
    org = null;
    project = null;
    buildId = 0;

    if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
        || uri.Host is not "dev.azure.com"
        || uri.Segments.Length < 4) // Expects 4 segments: ["/", "org/", "project/", "_build/", "results"]
    {
        return false;
    }

    org = uri.Segments[1].TrimEnd('/');
    project = uri.Segments[2].TrimEnd('/');

    foreach (string param in uri.Query.TrimStart('?').Split('&'))
    {
        string[] parts = param.Split('=', 2);
        if (parts.Length == 2 && parts[0] == "buildId")
        {
            return int.TryParse(parts[1], out buildId);
        }
    }

    return false;
}

record PipelineRun(string Name, int BuildId, string Org, string Project, bool IsTopLevel);
record OtherCheck(string Name, string State, string Link);
