#!/usr/bin/env dotnet
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#:project ../../../../src/Skills/Skills.csproj

using System.CommandLine;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using static System.Console;

Argument<string> prArgument = new("pr")
{
    Description = "The pull request to look up. Accepts a GitHub PR URL or a PR number (requires --repo)."
};
Option<string> repoOption = new("--repo", "-R")
{
    Description = "The GitHub repository in owner/repo format. Required when using a PR number instead of a URL."
};

RootCommand rootCommand = new("Lists Azure Pipelines runs associated with a GitHub pull request.")
{
    prArgument,
    repoOption,
};

ParseResult parseResult = rootCommand.Parse(args);
string pr = parseResult.GetValue(prArgument)!;
string? repo = parseResult.GetValue(repoOption);

// Resolve the repo from the URL if not provided via --repo
repo ??= ParseRepoFromUrl(pr);
if (repo is null)
{
    Error.WriteLine("Error: Could not determine repository. Use --repo or pass a full PR URL.");
    return 1;
}

// Step 1: Get the head commit SHA from the PR
string headSha = (await RunGhAsync("pr", "view", pr, "--repo", repo, "--json", "headRefOid", "--jq", ".headRefOid")).Trim();

// Step 2: Get check runs for the head commit via the GitHub REST API.
// The check-runs endpoint returns details_url which points to Azure DevOps build results.
string checkRunsJson = await RunGhAsync("api", $"repos/{repo}/commits/{headSha}/check-runs", "--paginate", "--jq", ".check_runs");

// gh --paginate with --jq outputs one JSON array per page, newline-separated
List<JsonElement> allCheckRuns = [];
foreach (string line in checkRunsJson.Split('\n', StringSplitOptions.RemoveEmptyEntries))
{
    using JsonDocument page = JsonDocument.Parse(line);
    foreach (JsonElement run in page.RootElement.EnumerateArray())
    {
        allCheckRuns.Add(run.Clone());
    }
}

// Step 3: Group by build ID, keeping the top-level pipeline entry.
// Each Azure Pipelines run reports multiple check runs (one per job).
Dictionary<int, PipelineRun> pipelines = [];

foreach (JsonElement check in allCheckRuns)
{
    string detailsUrl = check.GetProperty("details_url").GetString() ?? "";
    if (!detailsUrl.StartsWith("https://dev.azure.com", StringComparison.Ordinal))
    {
        continue;
    }

    if (!TryExtractBuildId(detailsUrl, out int buildId))
    {
        continue;
    }

    string name = check.GetProperty("name").GetString() ?? "";
    string conclusion = check.GetProperty("conclusion").GetString() ?? "";
    string status = check.GetProperty("status").GetString() ?? "";

    // The top-level pipeline check run has no job suffix in parentheses
    bool isTopLevel = !name.Contains(" (");

    if (!pipelines.TryGetValue(buildId, out PipelineRun? existing) || (isTopLevel && !existing.IsTopLevel))
    {
        string buildUrl = BuildCleanUrl(detailsUrl, buildId);
        pipelines[buildId] = new PipelineRun(name, buildId, status, conclusion, buildUrl, isTopLevel);
    }
}

if (pipelines.Count == 0)
{
    WriteLine("No Azure Pipelines runs found for this pull request.");
    return 0;
}

WriteLine($"Found {pipelines.Count} Azure Pipelines run(s):\n");

foreach (PipelineRun pipeline in pipelines.Values.OrderBy(p => p.Name))
{
    string resultText = pipeline.Conclusion.Length > 0 ? pipeline.Conclusion : pipeline.Status;
    WriteLine($"Pipeline: {pipeline.Name}");
    WriteLine($"  Build:  {pipeline.BuildId}");
    WriteLine($"  Result: {resultText}");
    WriteLine($"  Link:   {pipeline.Url}");
    WriteLine();
}

return 0;

/// <summary>
/// Runs the gh CLI with the specified arguments and returns its standard output.
/// Throws if the process exits with a non-zero exit code.
/// </summary>
static async Task<string> RunGhAsync(params string[] arguments)
{
    ProcessStartInfo startInfo = new("gh")
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    };
    foreach (string arg in arguments)
    {
        startInfo.ArgumentList.Add(arg);
    }

    using Process process = Process.Start(startInfo)
        ?? throw new InvalidOperationException("Failed to start gh process.");

    // Read stdout and stderr concurrently to avoid deadlocks
    Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
    Task<string> stderrTask = process.StandardError.ReadToEndAsync();

    await process.WaitForExitAsync();

    if (process.ExitCode != 0)
    {
        string stderr = await stderrTask;
        throw new InvalidOperationException($"gh exited with code {process.ExitCode}: {stderr}");
    }

    return await stdoutTask;
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

static bool TryExtractBuildId(string url, out int buildId)
{
    buildId = 0;
    try
    {
        Uri uri = new(url);
        foreach (string param in uri.Query.TrimStart('?').Split('&'))
        {
            string[] parts = param.Split('=', 2);
            if (parts.Length == 2 && parts[0] == "buildId")
            {
                return int.TryParse(parts[1], out buildId);
            }
        }
    }
    catch (UriFormatException) { }
    return false;
}

static string BuildCleanUrl(string detailsUrl, int buildId)
{
    int queryStart = detailsUrl.IndexOf('?');
    string basePath = queryStart >= 0 ? detailsUrl[..queryStart] : detailsUrl;
    return $"{basePath}?buildId={buildId}";
}

record PipelineRun(string Name, int BuildId, string Status, string Conclusion, string Url, bool IsTopLevel);
