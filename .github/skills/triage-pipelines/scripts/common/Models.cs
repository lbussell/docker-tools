// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace TriagePipelines;

/// <summary>
/// Response from the Azure DevOps Build Definitions - List API.
/// </summary>
/// <param name="Value">The array of build definition references.</param>
/// <seealso href="https://learn.microsoft.com/en-us/rest/api/azure/devops/build/definitions/list?view=azure-devops-rest-7.1" />
public record DefinitionsResponse(BuildDefinitionReference[] Value);

/// <summary>
/// A reference to a build definition, including its latest completed build.
/// </summary>
/// <param name="Name">The display name of the pipeline.</param>
/// <param name="LatestCompletedBuild">The most recent completed build for this definition, if any.</param>
/// <seealso href="https://learn.microsoft.com/en-us/rest/api/azure/devops/build/definitions/list?view=azure-devops-rest-7.1#builddefinitionreference" />
public record BuildDefinitionReference(string Name, ApiBuild? LatestCompletedBuild);

/// <summary>
/// A completed build from Azure DevOps.
/// </summary>
/// <param name="Id">The unique ID of the build.</param>
/// <param name="Result">The result of the build (e.g., "succeeded", "failed", "canceled").</param>
/// <param name="SourceVersion">The commit SHA that the build ran against.</param>
/// <seealso href="https://learn.microsoft.com/en-us/rest/api/azure/devops/build/builds/get?view=azure-devops-rest-7.1#build" />
public record ApiBuild(int Id, string? Result, string? SourceVersion);

/// <summary>
/// Source-generated JSON serializer context for the Azure DevOps API response types.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(DefinitionsResponse))]
internal partial class AzureDevOpsJsonContext : JsonSerializerContext;
