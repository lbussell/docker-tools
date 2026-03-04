// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.DockerTools.Automation.Models;

namespace Microsoft.DotNet.DockerTools.Automation.GitHub;

/// <summary>
/// Factory for creating GitHub-specific clients that go beyond <see cref="IRemoteGitClient"/>.
/// </summary>
public interface IGitHubClientFactory
{
    /// <summary>
    /// Creates a GitHub Issues client for the given auth options.
    /// </summary>
    Task<IGitHubIssuesClient> CreateIssuesClientAsync(GitHubAuthOptions auth);

    /// <summary>
    /// Creates an authentication token from the given auth options.
    /// For PAT auth, returns the token directly.
    /// For GitHub App auth, creates a JWT and exchanges it for an installation token.
    /// </summary>
    Task<string> CreateTokenAsync(GitHubAuthOptions auth);
}
