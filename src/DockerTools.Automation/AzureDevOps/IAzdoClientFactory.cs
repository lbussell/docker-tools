// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.Services.Common;

namespace Microsoft.DotNet.DockerTools.Automation.AzureDevOps;

/// <summary>
/// Factory for creating Azure DevOps-specific clients that go beyond <see cref="IRemoteGitClient"/>.
/// </summary>
public interface IAzdoClientFactory
{
    /// <summary>
    /// Creates a Build API client for the given AzDo account.
    /// </summary>
    /// <param name="baseUrl">The Azure DevOps organization URL (e.g., https://dev.azure.com/org).</param>
    /// <param name="credentials">VSS credentials for authentication.</param>
    IAzdoBuildClient CreateBuildClient(Uri baseUrl, VssCredentials credentials);

    /// <summary>
    /// Creates a Project API client for the given AzDo account.
    /// </summary>
    /// <param name="baseUrl">The Azure DevOps organization URL.</param>
    /// <param name="credentials">VSS credentials for authentication.</param>
    IAzdoProjectClient CreateProjectClient(Uri baseUrl, VssCredentials credentials);
}
