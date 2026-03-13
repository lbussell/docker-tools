#!/usr/bin/env dotnet
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#:project ../../../../src/Skills/Skills.csproj

using System.CommandLine;
using System.Text.Json;
using System.Text.RegularExpressions;
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

// Resolve the repo from the URL if explicitly provided
repo ??= ParseRepoFromUrl(pr);

// Get the head SHA and repo from a single gh pr view call.
// When --repo isn't set, gh auto-detects from the current git remote.
List<string> prViewArgs = ["pr", "view", pr, "--json", "headRefOid,url"];
if (repo is not null)
    prViewArgs.AddRange(["--repo", repo]);

using JsonDocument prInfo = await RunGitHubCliJsonAsync([.. prViewArgs]);
string headSha = prInfo.RootElement.GetProperty("headRefOid").GetString()?.Trim()
    ?? throw new InvalidOperationException("Could not get head SHA from PR.");
repo ??= ParseRepoFromUrl(prInfo.RootElement.GetProperty("url").GetString() ?? "");
if (repo is null)
{
    Write("Error: Could not determine repository from PR data.");
    return 1;
}

// Get check runs for the head commit via the GitHub REST API.
// The check-runs endpoint returns details_url which points to Azure DevOps build results.
List<JsonElement> allCheckRuns = await GetCheckRunsAsync(repo, headSha);

// Step 3: Group by build ID, keeping the top-level pipeline entry.
// Each Azure Pipelines run reports multiple check runs (one per job).
Dictionary<int, PipelineRun> pipelines = [];

foreach (JsonElement check in allCheckRuns)
{
    string detailsUrl = check.GetProperty("details_url").GetString() ?? "";

    if (!TryParseAzureDevOpsUrl(detailsUrl, out string? org, out string? project, out int buildId)
        || org is null || project is null)
    {
        continue;
    }

    string name = check.GetProperty("name").GetString() ?? "";

    // The top-level pipeline check run has no job suffix in parentheses
    bool isTopLevel = !name.Contains(" (");

    if (!pipelines.TryGetValue(buildId, out PipelineRun? existing) || (isTopLevel && !existing.IsTopLevel))
    {
        pipelines[buildId] = new PipelineRun(name, buildId, org, project, isTopLevel);
    }
}

if (pipelines.Count == 0)
{
    WriteLine("No Azure Pipelines runs found for this pull request.");
    return 0;
}

Func<TimelineNode, bool>? filter = showAll
    ? null
    : node => node.Record.Type is not "Task" || node.Record.Result is "failed";

// Group pipeline runs by (org, project) so we can share AzureDevOpsClient instances
IEnumerable<IGrouping<(string Org, string Project), PipelineRun>> groups =
    pipelines.Values.GroupBy(p => (p.Org, p.Project));

foreach (IGrouping<(string Org, string Project), PipelineRun> group in groups)
{
    using AzureDevOpsClient client = AzureDevOpsClient.Create(org: group.Key.Org, project: group.Key.Project);

    foreach (PipelineRun pipeline in group.OrderBy(p => p.Name))
    {
        BuildDetail build = await client.GetBuildAsync(pipeline.BuildId);
        TimelineResponse timeline = await client.GetBuildTimelineAsync(pipeline.BuildId);
        IReadOnlyList<TimelineNode> roots = timeline.BuildTree();

        string label = $"{build.Definition.Name} - Build {pipeline.BuildId} ({client.GetBuildResultUrl(pipeline.BuildId)}):";

        if (roots.Count == 0)
        {
            MarkupLine($"[dim]{label} (no timeline records)[/]");
        }
        else
        {
            Spectre.Console.Tree tree = BuildTimelineRendering.RenderTree(roots, label, filter);
            Write(tree);
        }

        WriteLine();
    }
}

return 0;

/// <summary>
/// Runs a GitHub CLI command and returns the standard output as a string.
/// </summary>
static Task<string> RunGitHubCliAsync(params string[] arguments) =>
    ProcessHelper.RunAsync("gh", arguments);

/// <summary>
/// Runs a GitHub CLI command and parses the output as a JSON document.
/// </summary>
static async Task<JsonDocument> RunGitHubCliJsonAsync(params string[] arguments)
{
    string response = await RunGitHubCliAsync(arguments);
    JsonDocument json = JsonDocument.Parse(response);
    return json;
}

/// <summary>
/// Fetches all check runs for a commit, handling paginated JSON output from gh.
/// </summary>
static async Task<List<JsonElement>> GetCheckRunsAsync(string repo, string headSha)
{
    string checkRunsJson = await RunGitHubCliAsync(
        "api", $"repos/{repo}/commits/{headSha}/check-runs", "--paginate", "--jq", ".check_runs");

    // gh --paginate with --jq outputs one JSON array per page, newline-separated
    List<JsonElement> results = [];
    foreach (string line in checkRunsJson.Split('\n', StringSplitOptions.RemoveEmptyEntries))
    {
        using JsonDocument page = JsonDocument.Parse(line);
        foreach (JsonElement run in page.RootElement.EnumerateArray())
        {
            results.Add(run.Clone());
        }
    }
    return results;
}

/// <summary>
/// Extracts the "owner/repo" from a GitHub PR URL.
/// Returns null if the URL doesn't match the expected pattern.
/// </summary>
static string? ParseRepoFromUrl(string url)
{
    Match match = Regex.Match(url, @"github\.com/([^/]+/[^/]+)/pull/");
    return match.Success ? match.Groups[1].Value : null;
}

/// <summary>
/// Parses an Azure DevOps build URL to extract the org, project, and build ID.
/// Expected format: <c>https://dev.azure.com/{org}/{project}/_build/results?buildId={id}</c>
/// </summary>
static bool TryParseAzureDevOpsUrl(string url, out string? org, out string? project, out int buildId)
{
    org = null;
    project = null;
    buildId = 0;

    // Match: https://dev.azure.com/{org}/{project}/_build/results?...buildId={id}...
    Match match = Regex.Match(url, @"^https://dev\.azure\.com/([^/]+)/([^/]+)/_build/results");
    if (!match.Success)
    {
        return false;
    }

    org = match.Groups[1].Value;
    project = match.Groups[2].Value;

    // Extract buildId from query string
    int queryStart = url.IndexOf('?');
    if (queryStart < 0)
    {
        return false;
    }

    foreach (string param in url[(queryStart + 1)..].Split('&'))
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
