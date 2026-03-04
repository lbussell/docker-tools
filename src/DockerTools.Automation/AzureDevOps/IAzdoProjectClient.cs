// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.TeamFoundation.Core.WebApi;

namespace Microsoft.DotNet.DockerTools.Automation.AzureDevOps;

/// <summary>
/// Azure DevOps Project API operations.
/// </summary>
public interface IAzdoProjectClient
{
    /// <summary>
    /// Gets project metadata by project name or ID.
    /// </summary>
    /// <param name="projectId">The project name or GUID string.</param>
    Task<TeamProject> GetProjectAsync(string projectId);
}
