// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.DotNet.DockerTools.Skills;

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
/// Response from the Azure DevOps Build Timeline API.
/// </summary>
/// <param name="Records">The array of timeline records (stages, jobs, tasks, etc.).</param>
/// <seealso href="https://learn.microsoft.com/en-us/rest/api/azure/devops/build/timeline/get?view=azure-devops-rest-7.1" />
public record TimelineResponse(TimelineRecord[] Records);

/// <summary>
/// A single record in a build timeline, representing a stage, job, or task.
/// </summary>
/// <param name="Id">The unique ID of this record.</param>
/// <param name="ParentId">The ID of the parent record (e.g., a task's parent job), or null for root records.</param>
/// <param name="Type">The record type: Stage, Job, Task, Phase, Checkpoint, etc.</param>
/// <param name="Name">The display name of the record.</param>
/// <param name="State">The execution state: pending, inProgress, or completed.</param>
/// <param name="Result">The result when completed: succeeded, failed, skipped, canceled, succeededWithIssues, abandoned.</param>
/// <param name="Order">The display order among sibling records under the same parent.</param>
/// <param name="Log">A reference to the log for this record, if one exists.</param>
public record TimelineRecord(string Id, string? ParentId, string Type, string Name, string? State, string? Result, int? Order, TimelineLog? Log);

/// <summary>
/// A reference to a build log, used to fetch its content.
/// </summary>
/// <param name="Id">The log ID, used with the Build Log API to retrieve content.</param>
public record TimelineLog(int Id);

/// <summary>
/// A node in the parsed build timeline tree (stage, job, or task).
/// </summary>
/// <param name="Record">The underlying timeline record.</param>
/// <param name="Children">Child nodes in display order.</param>
public record TimelineNode(TimelineRecord Record, IReadOnlyList<TimelineNode> Children);

/// <summary>
/// Source-generated JSON serializer context for the Azure DevOps API response types.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(DefinitionsResponse))]
[JsonSerializable(typeof(TimelineResponse))]
internal partial class AzureDevOpsJsonContext : JsonSerializerContext;
