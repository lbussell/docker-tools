// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.PipelineMonitor;

/// <summary>
/// Service for monitoring Azure DevOps pipelines.
/// </summary>
public interface IPipelineService
{
    /// <summary>
    /// Monitors pipelines for the specified organization and project.
    /// </summary>
    /// <param name="organization">The Azure DevOps organization name.</param>
    /// <param name="project">The Azure DevOps project name.</param>
    /// <param name="pipelineId">Optional pipeline ID to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task MonitorAsync(string organization, string project, int? pipelineId, CancellationToken cancellationToken = default);
}
