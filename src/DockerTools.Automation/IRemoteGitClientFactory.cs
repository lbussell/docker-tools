// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.DockerTools.Automation;

/// <summary>
/// Creates an <see cref="IRemoteGitClient"/> for a given repository URL.
/// Dispatches to GitHub or Azure DevOps based on the URL pattern.
/// </summary>
public interface IRemoteGitClientFactory
{
    /// <summary>
    /// Creates a client scoped to the repository identified by <paramref name="repoUrl"/>.
    /// GitHub URLs produce a GitHub-backed client; Azure DevOps URLs produce an AzDo-backed client.
    /// </summary>
    /// <param name="repoUrl">Full URL of the repository (e.g., https://github.com/owner/repo or https://dev.azure.com/org/project/_git/repo).</param>
    Task<IRemoteGitClient> CreateAsync(string repoUrl);
}
