// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Spectre.Console;

namespace Microsoft.DotNet.DockerTools.Skills;

/// <summary>
/// Extension methods for building and rendering Azure DevOps build timeline trees.
/// </summary>
public static class BuildTimelineRendering
{
    /// <summary>
    /// Renders the timeline nodes as a Spectre.Console <see cref="Tree"/>.
    /// When <paramref name="isVisible"/> is provided, only nodes satisfying the predicate are shown.
    /// </summary>
    public static Tree RenderTree(
        IEnumerable<TimelineNode> timelineRoots,
        string label,
        Func<TimelineNode, bool>? isVisible = null)
    {
        Tree tree = new(label);
        foreach (TimelineNode root in timelineRoots)
            tree.AddTimelineNode(root, FormatNode, isVisible);

        return tree;
    }

    /// <summary>
    /// Builds a list of root <see cref="TimelineNode"/>s from the flat timeline API response.
    /// Groups records into stages, jobs, and tasks, walking past intermediate types
    /// like Phase and Checkpoint to find the correct parent.
    /// </summary>
    public static IReadOnlyList<TimelineNode> BuildTree(this TimelineResponse timeline)
    {
        Dictionary<string, TimelineRecord> allById = timeline.Records.ToDictionary(record => record.Id);

        List<TimelineRecord> stageRecords = timeline.Records.Where(record => record.Type is "Stage").ToList();
        List<TimelineRecord> jobRecords = timeline.Records.Where(record => record.Type is "Job").ToList();
        List<TimelineRecord> taskRecords = timeline.Records.Where(record => record.Type is "Task").ToList();

        HashSet<string> stageIds = [.. stageRecords.Select(record => record.Id)];
        HashSet<string> jobIds = [.. jobRecords.Select(record => record.Id)];

        // Group tasks by parent job, walking past intermediate types like Checkpoint
        ILookup<string, TimelineRecord> tasksByJob = taskRecords
            .ToLookup(task => FindDisplayParent(task.ParentId, jobIds, allById) ?? "");

        Dictionary<string, TimelineNode> jobNodesById = jobRecords.ToDictionary(
            job => job.Id,
            job => new TimelineNode(job, tasksByJob[job.Id]
                .OrderBy(task => task.Order)
                .Select(task => new TimelineNode(task, []))
                .ToList()));

        // Group jobs by parent stage, walking past intermediate types like Phase
        ILookup<string, TimelineRecord> jobsByStage = jobRecords
            .ToLookup(job => FindDisplayParent(job.ParentId, stageIds, allById) ?? "");

        List<TimelineNode> stages = stageRecords
            .OrderBy(stage => stage.Order)
            .Select(stage => new TimelineNode(stage, jobsByStage[stage.Id]
                .OrderBy(job => job.Order)
                .Select(job => jobNodesById[job.Id])
                .ToList()))
            .ToList();

        List<TimelineNode> rootJobs = jobsByStage[""]
            .OrderBy(job => job.Order)
            .Select(job => jobNodesById[job.Id])
            .ToList();

        return [.. stages, .. rootJobs];
    }

    private static void AddTimelineNode(
        this IHasTreeNodes treeNode,
        TimelineNode timelineNode,
        Func<TimelineNode, string> formatNode,
        Func<TimelineNode, bool>? filter = null)
    {
        IEnumerable<TimelineNode> visibleChildren = filter is not null
            ? timelineNode.Children.Where(filter)
            : timelineNode.Children;

        TreeNode newTreeNode = treeNode.AddNode(formatNode(timelineNode));
        foreach (TimelineNode child in visibleChildren)
            newTreeNode.AddTimelineNode(child, formatNode, filter);
    }

    private static string FormatNode(TimelineNode node)
    {
        string logId = node.Record.Log is not null ? $" #{node.Record.Log.Id}" : "";
        string result = FormatResult(node.Record);
        string childCount = node.Children.Count > 1
            ? $" ({node.Children.Count} {node.Children[0].Record.Type}s)"
            : "";
        return $"{node.Record.Type}{logId}: {node.Record.Name} | {result}{childCount}";
    }

    private static string FormatResult(TimelineRecord record) =>
        record.Result switch
        {
            "succeeded" => "Succeeded",
            "failed" => "Failed",
            "skipped" => "Skipped",
            "canceled" => "Canceled",
            "succeededWithIssues" => "SucceededWithIssues",
            _ => record.State switch
            {
                "inProgress" => "InProgress",
                "pending" => "Pending",
                _ => "-"
            }
        };

    /// <summary>
    /// Walks up the parent chain to find the nearest ancestor present in the target set.
    /// </summary>
    private static string? FindDisplayParent(
        string? parentId,
        HashSet<string> targetIds,
        Dictionary<string, TimelineRecord> allById)
    {
        while (parentId is not null)
        {
            if (targetIds.Contains(parentId))
            {
                return parentId;
            }
            parentId = allById.TryGetValue(parentId, out TimelineRecord? parent)
                ? parent.ParentId
                : null;
        }
        return null;
    }
}
