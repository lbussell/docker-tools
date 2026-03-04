// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.DockerTools.Automation.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.WebApi;

namespace Microsoft.DotNet.DockerTools.Automation.AzureDevOps;

internal class AzdoBuildClient(BuildHttpClient inner, ILogger<AzdoBuildClient> logger) : IAzdoBuildClient
{
    public Task<Build> QueueBuildAsync(Build build) =>
        inner.QueueBuildAsync(build);

    public Task<Build> GetBuildAsync(Guid projectId, int buildId) =>
        inner.GetBuildAsync(projectId, buildId);

    public Task<IPagedList<Build>> GetBuildsAsync(
        Guid projectId,
        IEnumerable<int>? definitions = null,
        BuildStatus? statusFilter = null) =>
        RetryHelper.GetWaitAndRetryPolicy<Exception>(logger)
            .ExecuteAsync(() => inner.GetBuildsAsync2(projectId, definitions: definitions, statusFilter: statusFilter));

    public Task<Timeline> GetBuildTimelineAsync(Guid projectId, int buildId) =>
        inner.GetBuildTimelineAsync(projectId, buildId);

    public Task<List<string>> AddBuildTagAsync(Guid projectId, int buildId, string tag) =>
        RetryHelper.GetWaitAndRetryPolicy<Exception>(logger)
            .ExecuteAsync(() => inner.AddBuildTagAsync(projectId, buildId, tag));
}
