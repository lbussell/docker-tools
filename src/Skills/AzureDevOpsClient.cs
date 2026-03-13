// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;

namespace Microsoft.DotNet.DockerTools.Skills;

/// <summary>
/// A lightweight client for calling Azure DevOps REST APIs.
/// Authenticates via <see cref="AzureDeveloperCliCredential"/> (requires prior <c>azd auth login</c>).
/// </summary>
public sealed class AzureDevOpsClient : IDisposable
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
    /// Sends a GET request to the specified API path and returns the response body as a string.
    /// The <c>api-version</c> query parameter is appended automatically.
    /// </summary>
    /// <param name="path">API path relative to the project URL.</param>
    /// <param name="queryParams">Optional query parameters to append to the URL.</param>
    public async Task<string> GetAsTextAsync(string path, Dictionary<string, string>? queryParams = null)
    {
        string query = BuildQueryString(queryParams);
        string url = $"{path}?{query}";
        return await _http.GetStringAsync(url);
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

    /// <summary>
    /// Returns the build definitions in the specified folder, with their latest completed builds.
    /// </summary>
    /// <param name="folder">The pipeline folder path (e.g., <c>\dotnet\docker-tools</c>).</param>
    public async Task<DefinitionsResponse> GetBuildDefinitionsAsync(string folder) =>
        await GetAsJsonAsync(
            "_apis/build/definitions",
            AzureDevOpsJsonContext.Default.DefinitionsResponse,
            new() { ["path"] = folder, ["includeLatestBuilds"] = "true" });

    /// <summary>
    /// Returns the details of a specific build, including its pipeline definition name.
    /// </summary>
    /// <param name="buildId">The build ID to retrieve.</param>
    public Task<BuildDetail> GetBuildAsync(int buildId) =>
        GetAsJsonAsync(
            $"_apis/build/builds/{buildId}",
            AzureDevOpsJsonContext.Default.BuildDetail);

    /// <summary>
    /// Returns the timeline for a build, containing stage, job, and task records with their results.
    /// </summary>
    /// <param name="buildId">The build ID to retrieve the timeline for.</param>
    public Task<TimelineResponse> GetBuildTimelineAsync(int buildId) =>
        GetAsJsonAsync(
            $"_apis/build/builds/{buildId}/timeline",
            AzureDevOpsJsonContext.Default.TimelineResponse);

    /// <summary>
    /// Returns the plain-text log content for a specific log in a build.
    /// </summary>
    /// <param name="buildId">The build ID that owns the log.</param>
    /// <param name="logId">The log ID to retrieve (found in <see cref="TimelineRecord.Log"/>).</param>
    public Task<string> GetBuildLogContentAsync(int buildId, int logId) =>
        GetAsTextAsync($"_apis/build/builds/{buildId}/logs/{logId}");

    /// <inheritdoc/>
    public void Dispose() => _http.Dispose();
}
