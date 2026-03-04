// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.DockerTools.Automation.AzureDevOps;
using Microsoft.DotNet.DockerTools.Automation.GitHub;
using Microsoft.DotNet.DockerTools.Automation.Models;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;

namespace Microsoft.DotNet.DockerTools.Automation;

internal class RemoteGitClientFactory(
    GitHubClientFactory gitHubClientFactory,
    GitHubAuthOptions gitHubAuthOptions,
    string? azdoAccessToken,
    ILoggerFactory loggerFactory) : IRemoteGitClientFactory
{
    private readonly GitHubClientFactory _gitHubClientFactory = gitHubClientFactory;
    private readonly GitHubAuthOptions _gitHubAuthOptions = gitHubAuthOptions;
    private readonly string? _azdoAccessToken = azdoAccessToken;
    private readonly ILoggerFactory _loggerFactory = loggerFactory;

    public async Task<IRemoteGitClient> CreateAsync(string repoUrl)
    {
        var uri = new Uri(repoUrl);

        if (IsGitHubUrl(uri))
        {
            return await CreateGitHubClientAsync(uri);
        }

        if (IsAzdoUrl(uri))
        {
            return await CreateAzdoClientAsync(uri);
        }

        throw new ArgumentException($"Unsupported repository URL: {repoUrl}. Expected a GitHub or Azure DevOps URL.", nameof(repoUrl));
    }

    private static bool IsGitHubUrl(Uri uri) =>
        uri.Host.Contains("github.com", StringComparison.OrdinalIgnoreCase);

    private static bool IsAzdoUrl(Uri uri) =>
        uri.Host.Contains("dev.azure.com", StringComparison.OrdinalIgnoreCase) ||
        uri.Host.Contains("visualstudio.com", StringComparison.OrdinalIgnoreCase);

    private async Task<IRemoteGitClient> CreateGitHubClientAsync(Uri uri)
    {
        var owner = uri.Segments[1].TrimEnd('/');
        var repo = uri.Segments[2].TrimEnd('/');

        var client = await _gitHubClientFactory.CreateGitHubClientAsync(_gitHubAuthOptions);
        var blobsClient = await _gitHubClientFactory.CreateBlobsClientAsync(_gitHubAuthOptions);
        var treesClient = await _gitHubClientFactory.CreateTreesClientAsync(_gitHubAuthOptions);

        return new GitHubRemoteGitClient(client, blobsClient, treesClient, owner, repo, _loggerFactory.CreateLogger<GitHubRemoteGitClient>());
    }

    private async Task<IRemoteGitClient> CreateAzdoClientAsync(Uri uri)
    {
        // URL format: https://dev.azure.com/{org}/{project}/_git/{repo}
        var org = uri.Segments[1].TrimEnd('/');
        var project = uri.Segments[2].TrimEnd('/');
        var repo = uri.Segments[4].TrimEnd('/');

        var baseUrl = $"https://dev.azure.com/{org}";
        var credentials = new VssBasicCredential(string.Empty, _azdoAccessToken);
        var gitClient = new GitHttpClient(new Uri(baseUrl), credentials);

        var repositories = await gitClient.GetRepositoriesAsync(project);
        var repository = repositories.First(r => string.Equals(r.Name, repo, StringComparison.OrdinalIgnoreCase));

        return new AzdoRemoteGitClient(gitClient, repository.Id, _loggerFactory.CreateLogger<AzdoRemoteGitClient>());
    }
}
