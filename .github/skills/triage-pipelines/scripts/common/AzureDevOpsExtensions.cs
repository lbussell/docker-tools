// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TriagePipelines;

/// <summary>
/// Convenience extension methods for common Azure DevOps API calls.
/// </summary>
public static class AzureDevOpsExtensions
{
    /// <summary>
    /// Returns the build definitions in the specified folder, with their latest completed builds.
    /// </summary>
    /// <param name="client">The Azure DevOps client.</param>
    /// <param name="folder">The pipeline folder path (e.g., <c>\dotnet\docker-tools</c>).</param>
    public static async Task<DefinitionsResponse> GetBuildDefinitionsAsync(
        this AzureDevOpsClient client, string folder) =>
        await client.GetAsJsonAsync(
            "_apis/build/definitions",
            AzureDevOpsJsonContext.Default.DefinitionsResponse,
            new() { ["path"] = folder, ["includeLatestBuilds"] = "true" });

    /// <summary>
    /// Returns the timeline for a build, containing stage, job, and task records with their results.
    /// </summary>
    /// <param name="client">The Azure DevOps client.</param>
    /// <param name="buildId">The build ID to retrieve the timeline for.</param>
    public static Task<TimelineResponse> GetBuildTimelineAsync(
        this AzureDevOpsClient client, int buildId) =>
        client.GetAsJsonAsync(
            $"_apis/build/builds/{buildId}/timeline",
            AzureDevOpsJsonContext.Default.TimelineResponse);

    /// <summary>
    /// Returns the plain-text log content for a specific log in a build.
    /// </summary>
    /// <param name="client">The Azure DevOps client.</param>
    /// <param name="buildId">The build ID that owns the log.</param>
    /// <param name="logId">The log ID to retrieve (found in <see cref="TimelineRecord.Log"/>).</param>
    public static Task<string> GetBuildLogContentAsync(
        this AzureDevOpsClient client, int buildId, int logId) =>
        client.GetAsTextAsync($"_apis/build/builds/{buildId}/logs/{logId}");

    /// <summary>
    /// Builds a tree of <see cref="TimelineNode"/> from the flat timeline records.
    /// Filters to Stage, Job, and Task types, skipping intermediate types like Phase
    /// by walking parent chains to find the nearest displayable ancestor.
    /// </summary>
    public static IReadOnlyList<TimelineNode> BuildTree(this TimelineResponse timeline)
    {
        List<TimelineRecord> displayRecords = timeline.Records
            .Where(record => record.Type is "Stage" or "Job" or "Task")
            .ToList();

        Dictionary<string, TimelineRecord> allRecordsById = timeline.Records.ToDictionary(record => record.Id);
        HashSet<string> displayIds = [.. displayRecords.Select(record => record.Id)];

        ILookup<string, TimelineRecord> childrenByParent = displayRecords
            .ToLookup(record => FindDisplayParent(record.ParentId, displayIds, allRecordsById) ?? "");

        return BuildNodes(childrenByParent, "");
    }

    private static IReadOnlyList<TimelineNode> BuildNodes(
        ILookup<string, TimelineRecord> childrenByParent, string parentKey) =>
        childrenByParent[parentKey]
            .OrderBy(record => record.Order)
            .Select(record => new TimelineNode(record, BuildNodes(childrenByParent, record.Id)))
            .ToList();

    /// <summary>
    /// Walks up the parent chain to find the nearest ancestor present in the display set.
    /// </summary>
    private static string? FindDisplayParent(
        string? parentId,
        HashSet<string> displayIds,
        Dictionary<string, TimelineRecord> allRecordsById)
    {
        while (parentId is not null)
        {
            if (displayIds.Contains(parentId))
            {
                return parentId;
            }
            parentId = allRecordsById.TryGetValue(parentId, out TimelineRecord? parent)
                ? parent.ParentId
                : null;
        }
        return null;
    }
}
