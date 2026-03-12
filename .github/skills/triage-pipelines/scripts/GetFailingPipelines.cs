#!/usr/bin/env dotnet
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#:package Azure.Identity@1.13.2

using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Azure.Core;
using Azure.Identity;
using static System.Console;

using AzureDevOpsClient client = AzureDevOpsClient.Create(org: "dnceng", project: "internal");

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

/// <summary>
/// Convenience extension methods for common Azure DevOps API calls.
/// </summary>
static class AzureDevOpsExtensions
{
    /// <summary>
    /// Returns the build definitions in the specified folder, with their latest completed builds.
    /// </summary>
    /// <param name="folder">The pipeline folder path (e.g., <c>\dotnet\docker-tools</c>).</param>
    public static async Task<DefinitionsResponse> GetBuildDefinitionsAsync(
        this AzureDevOpsClient client, string folder) =>
        await client.GetAsJsonAsync(
            "_apis/build/definitions",
            AzureDevOpsJsonContext.Default.DefinitionsResponse,
            new() { ["path"] = folder, ["includeLatestBuilds"] = "true" });
}

/// <summary>
/// A lightweight client for calling Azure DevOps REST APIs.
/// Authenticates via <see cref="AzureDeveloperCliCredential"/> (requires prior <c>azd auth login</c>).
/// </summary>
sealed class AzureDevOpsClient : IDisposable
{
    private const string Scope = "499b84ac-1321-427f-aa17-267ca6975798/.default";
    private const string ApiVersion = "7.1";

    private readonly HttpClient _http;

    /// <summary>The Azure DevOps organization name.</summary>
    public string Org { get; }

    /// <summary>The Azure DevOps project name.</summary>
    public string Project { get; }

    private AzureDevOpsClient(HttpClient http, string org, string project)
    {
        _http = http;
        Org = org;
        Project = project;
    }

    /// <summary>
    /// Creates an authenticated client for the specified Azure DevOps organization and project.
    /// </summary>
    public static AzureDevOpsClient Create(string org = "dnceng", string project = "internal")
    {
        AzureDeveloperCliCredential credential = new();
        AccessToken token = credential.GetToken(new([Scope]), CancellationToken.None);

        HttpClient http = new()
        {
            BaseAddress = new Uri($"https://dev.azure.com/{org}/{project}/")
        };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

        return new AzureDevOpsClient(http, org, project);
    }

    /// <summary>
    /// Sends a GET request to the specified API path and deserializes the JSON response.
    /// The <c>api-version</c> query parameter is appended automatically.
    /// </summary>
    /// <param name="path">API path relative to the project URL (e.g., <c>_apis/build/definitions</c>).</param>
    /// <param name="jsonTypeInfo">Source-generated JSON type info for deserialization.</param>
    /// <param name="queryParams">Optional query parameters to append to the URL.</param>
    public async Task<T> GetAsJsonAsync<T>(
        string path,
        JsonTypeInfo<T> jsonTypeInfo,
        Dictionary<string, string>? queryParams = null)
    {
        string query = BuildQueryString(queryParams);
        string url = $"{path}?{query}";
        string json = await _http.GetStringAsync(url);
        return JsonSerializer.Deserialize(json, jsonTypeInfo)
            ?? throw new InvalidOperationException($"Failed to deserialize response from {path}.");
    }

    /// <summary>
    /// Returns the web UI URL for a specific build result.
    /// </summary>
    public string GetBuildResultUrl(int buildId) =>
        $"https://dev.azure.com/{Org}/{Project}/_build/results?buildId={buildId}";

    private static string BuildQueryString(Dictionary<string, string>? queryParams)
    {
        List<string> parts = [$"api-version={ApiVersion}"];
        if (queryParams is not null)
        {
            foreach ((string key, string value) in queryParams)
            {
                parts.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}");
            }
        }
        return string.Join("&", parts);
    }

    /// <inheritdoc/>
    public void Dispose() => _http.Dispose();
}

/// <summary>
/// Response from the Azure DevOps Build Definitions - List API.
/// </summary>
/// <param name="Value">The array of build definition references.</param>
/// <seealso href="https://learn.microsoft.com/en-us/rest/api/azure/devops/build/definitions/list?view=azure-devops-rest-7.1" />
record DefinitionsResponse(BuildDefinitionReference[] Value);

/// <summary>
/// A reference to a build definition, including its latest completed build.
/// </summary>
/// <param name="Name">The display name of the pipeline.</param>
/// <param name="LatestCompletedBuild">The most recent completed build for this definition, if any.</param>
/// <seealso href="https://learn.microsoft.com/en-us/rest/api/azure/devops/build/definitions/list?view=azure-devops-rest-7.1#builddefinitionreference" />
record BuildDefinitionReference(string Name, ApiBuild? LatestCompletedBuild);

/// <summary>
/// A completed build from Azure DevOps.
/// </summary>
/// <param name="Id">The unique ID of the build.</param>
/// <param name="Result">The result of the build (e.g., "succeeded", "failed", "canceled").</param>
/// <param name="SourceVersion">The commit SHA that the build ran against.</param>
/// <seealso href="https://learn.microsoft.com/en-us/rest/api/azure/devops/build/builds/get?view=azure-devops-rest-7.1#build" />
record ApiBuild(int Id, string? Result, string? SourceVersion);

/// <summary>
/// Source-generated JSON serializer context for the Azure DevOps API response types.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(DefinitionsResponse))]
partial class AzureDevOpsJsonContext : JsonSerializerContext;
