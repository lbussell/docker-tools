// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.DockerTools.Automation.Models;

/// <summary>
/// Authentication options for GitHub API access.
/// Supports both personal access token (PAT) and GitHub App authentication.
/// </summary>
/// <param name="AuthToken">A personal access token, or null if using GitHub App auth.</param>
/// <param name="PrivateKey">Base64-encoded private key for GitHub App auth, or null if using a PAT.</param>
/// <param name="ClientId">The GitHub App's client ID, or null if using a PAT.</param>
/// <param name="InstallationId">The GitHub App installation ID, or null if using a PAT.</param>
public record GitHubAuthOptions(
    string? AuthToken = null,
    string? PrivateKey = null,
    string? ClientId = null,
    long? InstallationId = null)
{
    /// <summary>
    /// Whether GitHub App authentication is configured.
    /// </summary>
    public bool IsGitHubAppAuth => !string.IsNullOrEmpty(PrivateKey) && !string.IsNullOrEmpty(ClientId);

    /// <summary>
    /// Whether any credentials are available (PAT or GitHub App).
    /// </summary>
    public bool HasCredentials => !string.IsNullOrEmpty(AuthToken) || IsGitHubAppAuth;
}
