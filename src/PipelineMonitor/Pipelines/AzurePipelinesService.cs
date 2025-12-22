// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.PipelineMonitor.Pipelines;

/// <summary>
/// Service for monitoring Azure DevOps pipelines.
/// </summary>
internal interface IAzurePipelinesService
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

/// <summary>
/// Implementation of <see cref="IAzurePipelinesService"/> for monitoring Azure DevOps pipelines.
/// </summary>
internal sealed class AzurePipelinesService(ILogger<AzurePipelinesService> logger) : IAzurePipelinesService
{
    private readonly ILogger<AzurePipelinesService> _logger = logger;

    /// <inheritdoc/>
    public async Task MonitorAsync(string organization, string project, int? pipelineId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting pipeline monitoring for {Organization}/{Project}...", organization, project);

        // TODO: Implement pipeline monitoring logic
        await Task.Delay(1000, cancellationToken);

        _logger.LogInformation("Pipeline monitoring completed.");
    }
}
