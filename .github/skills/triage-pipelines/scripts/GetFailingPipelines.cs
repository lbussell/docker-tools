#!/usr/bin/env dotnet
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#:package Azure.Identity@1.13.2

using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using Azure.Identity;

const string org = "dnceng";
const string project = "internal";
const string folder = @"\dotnet\docker-tools";
const string scope = "499b84ac-1321-427f-aa17-267ca6975798/.default";

// Authenticate using Azure Developer CLI credential (same as the monitor project).
// Requires prior `azd auth login`.
AzureDeveloperCliCredential credential = new();
AccessToken token = credential.GetToken(new([scope]), CancellationToken.None);

using HttpClient http = new();
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

string url = $"https://dev.azure.com/{org}/{project}/_apis/build/definitions"
    + $"?path={Uri.EscapeDataString(folder)}&includeLatestBuilds=true&api-version=7.1";

string json = await http.GetStringAsync(url);
DefinitionsResponse response =
    JsonSerializer.Deserialize(json, AzureDevOpsJsonContext.Default.DefinitionsResponse)
    ?? throw new InvalidOperationException("Failed to deserialize API response.");

List<BuildDefinitionReference> failing = response
    .Value.Where(def => def.LatestCompletedBuild is { Result: "failed" })
    .ToList();

if (failing.Count == 0)
{
    Console.WriteLine("No failing pipelines found.");
    return;
}

Console.WriteLine($"Found {failing.Count} failing pipeline(s):\n");

foreach (BuildDefinitionReference def in failing)
{
    ApiBuild build = def.LatestCompletedBuild!;
    Console.WriteLine($"Pipeline: {def.Name}");
    Console.WriteLine($"  Commit: {build.SourceVersion ?? "unknown"}");
    Console.WriteLine($"  Link:   https://dev.azure.com/{org}/{project}/_build/results?buildId={build.Id}");
    Console.WriteLine();
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
