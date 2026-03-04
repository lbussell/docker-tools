// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.DockerTools.Automation.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Core.WebApi;

namespace Microsoft.DotNet.DockerTools.Automation.AzureDevOps;

internal class AzdoProjectClient(ProjectHttpClient inner, ILogger<AzdoProjectClient> logger) : IAzdoProjectClient
{
    public Task<TeamProject> GetProjectAsync(string projectId) =>
        RetryHelper.GetWaitAndRetryPolicy<Exception>(logger)
            .ExecuteAsync(() => inner.GetProject(projectId));
}
