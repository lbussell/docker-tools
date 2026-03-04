// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;
using Microsoft.DotNet.DockerTools.Automation.Helpers;
using Microsoft.DotNet.DockerTools.Automation.Models;
using Microsoft.Extensions.Logging;
using Octokit;

namespace Microsoft.DotNet.DockerTools.Automation.GitHub;

internal class GitHubClientFactory(ILogger<GitHubClientFactory> logger) : IGitHubClientFactory
{
    private static readonly ProductHeaderValue s_productHeaderValue = new("DockerTools-Automation");

    private readonly ILogger<GitHubClientFactory> _logger = logger;

    /// <inheritdoc/>
    public async Task<IGitHubIssuesClient> CreateIssuesClientAsync(GitHubAuthOptions auth)
    {
        var client = await CreateGitHubClientAsync(auth);
        return new GitHubIssuesClient(client, _logger);
    }

    /// <inheritdoc/>
    public async Task<string> CreateTokenAsync(GitHubAuthOptions auth)
    {
        if (auth.IsGitHubAppAuth)
        {
            var jwt = JwtHelper.CreateJwt(auth.ClientId!, auth.PrivateKey!, TimeSpan.FromMinutes(9));
            var appCredentials = new Credentials(jwt, AuthenticationType.Bearer);
            var appClient = new GitHubClient(s_productHeaderValue) { Credentials = appCredentials };

            var installations = await appClient.GitHubApps.GetAllInstallationsForCurrent();

            Installation installation;
            if (auth.InstallationId is long providedId)
            {
                installation = installations
                    .FirstOrDefault(i => i.Id == providedId)
                        ?? throw new InvalidOperationException($"No installation found with ID {providedId}.");
            }
            else
            {
                installation = installations.SingleOrDefault()
                    ?? throw new InvalidOperationException(
                        "Expected exactly one installation for GitHub App but found none or multiple. "
                        + "Provide an installation ID explicitly.");
            }

            var installationToken = await appClient.GitHubApps.CreateInstallationToken(installation.Id);

            _logger.LogInformation(
                "GitHub App token created for App ID {AppId} and installation {InstallationId} with expiration {Expiration}",
                installation.AppId,
                installation.Id,
                installationToken.ExpiresAt);

            return installationToken.Token;
        }

        return auth.AuthToken ?? throw new InvalidOperationException("No auth credentials provided.");
    }

    internal async Task<IGitHubClient> CreateGitHubClientAsync(GitHubAuthOptions auth)
    {
        var token = await CreateTokenAsync(auth);
        return new GitHubClient(s_productHeaderValue)
        {
            Credentials = new Credentials(token, AuthenticationType.Bearer)
        };
    }

    internal async Task<ApiConnection> CreateApiConnectionAsync(GitHubAuthOptions auth)
    {
        var token = await CreateTokenAsync(auth);
        var connection = new Connection(s_productHeaderValue)
        {
            Credentials = new Credentials(token, AuthenticationType.Bearer)
        };
        return new ApiConnection(connection);
    }

    internal async Task<IBlobsClient> CreateBlobsClientAsync(GitHubAuthOptions auth)
    {
        var apiConnection = await CreateApiConnectionAsync(auth);
        return new BlobsClient(apiConnection);
    }

    internal async Task<ITreesClient> CreateTreesClientAsync(GitHubAuthOptions auth)
    {
        var apiConnection = await CreateApiConnectionAsync(auth);
        return new TreesClient(apiConnection);
    }
}
