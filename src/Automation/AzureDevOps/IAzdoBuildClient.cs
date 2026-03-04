// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.WebApi;

namespace Microsoft.DotNet.DockerTools.Automation.AzureDevOps;

/// <summary>
/// Azure DevOps Build API operations.
/// Wraps <see cref="Microsoft.TeamFoundation.Build.WebApi"/> with retry policies.
/// </summary>
public interface IAzdoBuildClient
{
    /// <summary>
    /// Queues a new build.
    /// </summary>
    Task<Build> QueueBuildAsync(Build build);

    /// <summary>
    /// Gets a build by ID.
    /// </summary>
    /// <param name="projectId">The project GUID.</param>
    /// <param name="buildId">The build ID.</param>
    Task<Build> GetBuildAsync(Guid projectId, int buildId);

    /// <summary>
    /// Gets builds matching the specified filters.
    /// </summary>
    /// <param name="projectId">The project GUID.</param>
    /// <param name="definitions">Optional build definition IDs to filter by.</param>
    /// <param name="statusFilter">Optional build status filter.</param>
    Task<IPagedList<Build>> GetBuildsAsync(
        Guid projectId,
        IEnumerable<int>? definitions = null,
        BuildStatus? statusFilter = null);

    /// <summary>
    /// Gets the timeline for a build, including task results.
    /// </summary>
    /// <param name="projectId">The project GUID.</param>
    /// <param name="buildId">The build ID.</param>
    Task<Timeline> GetBuildTimelineAsync(Guid projectId, int buildId);

    /// <summary>
    /// Adds a tag to a build.
    /// </summary>
    /// <param name="projectId">The project GUID.</param>
    /// <param name="buildId">The build ID.</param>
    /// <param name="tag">The tag to add.</param>
    /// <returns>The complete list of tags on the build after the addition.</returns>
    Task<List<string>> AddBuildTagAsync(Guid projectId, int buildId, string tag);
}
