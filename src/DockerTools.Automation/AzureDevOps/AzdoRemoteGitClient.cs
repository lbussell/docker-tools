// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.DockerTools.Automation.Helpers;
using Microsoft.DotNet.DockerTools.Automation.Models;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;

namespace Microsoft.DotNet.DockerTools.Automation.AzureDevOps;

/// <summary>
/// <see cref="IRemoteGitClient"/> implementation backed by the Azure DevOps TeamFoundation SourceControl SDK.
/// </summary>
internal class AzdoRemoteGitClient(GitHttpClient gitClient, Guid repositoryId, ILogger<AzdoRemoteGitClient> logger)
    : IRemoteGitClient
{
    private readonly GitHttpClient _gitClient = gitClient;
    private readonly Guid _repositoryId = repositoryId;
    private readonly ILogger<AzdoRemoteGitClient> _logger = logger;

    public async Task<string> GetFileContentAsync(string path, string @ref)
    {
        var item = await RetryHelper.GetWaitAndRetryPolicy<Exception>(_logger)
            .ExecuteAsync(() => _gitClient.GetItemAsync(
                _repositoryId,
                path,
                includeContent: true,
                versionDescriptor: new GitVersionDescriptor
                {
                    Version = @ref,
                    VersionType = GitVersionType.Branch
                }));

        return item.Content;
    }

    public async Task<bool> FileExistsAsync(string path, string branch)
    {
        try
        {
            await RetryHelper.GetWaitAndRetryPolicy<Exception>(_logger)
                .ExecuteAsync(() => _gitClient.GetItemAsync(
                    _repositoryId,
                    path,
                    versionDescriptor: new GitVersionDescriptor
                    {
                        Version = branch,
                        VersionType = GitVersionType.Branch
                    }));
            return true;
        }
        catch (VssServiceException)
        {
            return false;
        }
    }

    public async Task<string> PushFilesAsync(
        string branch,
        string commitMessage,
        IReadOnlyDictionary<string, string> files,
        bool force = false)
    {
        var refs = await GetBranchRefsAsync();
        var branchRef = FindBranchRef(refs, branch);

        var fileInfos = files
            .Select(kvp => new
            {
                Exists = FileExistsAsync(kvp.Key, branch),
                Path = kvp.Key,
                Content = kvp.Value
            })
            .ToArray();

        await Task.WhenAll(fileInfos.Select(f => f.Exists));

        var changes = fileInfos
            .Select(f => new GitChange
            {
                ChangeType = f.Exists.Result ? VersionControlChangeType.Edit : VersionControlChangeType.Add,
                Item = new GitItem { Path = f.Path },
                NewContent = new ItemContent
                {
                    Content = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(f.Content)),
                    ContentType = ItemContentType.Base64Encoded
                }
            })
            .ToArray();

        var commit = new GitCommitRef
        {
            Comment = commitMessage,
            Changes = changes
        };

        var push = new GitPush
        {
            RefUpdates = [new GitRefUpdate { Name = branchRef.Name, OldObjectId = branchRef.ObjectId }],
            Commits = [commit]
        };

        var result = await RetryHelper.GetWaitAndRetryPolicy<Exception>(_logger)
            .ExecuteAsync(() => _gitClient.CreatePushAsync(push, _repositoryId));

        return result.Commits.First().CommitId;
    }

    public async Task<string> GetLatestCommitShaAsync(string branch)
    {
        var refs = await GetBranchRefsAsync();
        var branchRef = FindBranchRef(refs, branch);
        return branchRef.ObjectId;
    }

    public async Task<RemoteCommit> GetCommitAsync(string sha)
    {
        var commit = await RetryHelper.GetWaitAndRetryPolicy<Exception>(_logger)
            .ExecuteAsync(() => _gitClient.GetCommitAsync(sha, _repositoryId));

        return new RemoteCommit(commit.CommitId, commit.Author.Name, commit.Comment);
    }

    public async Task<bool> BranchExistsAsync(string branch)
    {
        var refs = await GetBranchRefsAsync();
        return refs.Any(r => string.Equals(r.Name, $"refs/heads/{branch}", StringComparison.OrdinalIgnoreCase));
    }

    public async Task CreateOrUpdateBranchAsync(string newBranch, string baseBranch)
    {
        var baseSha = await GetLatestCommitShaAsync(baseBranch);
        var refs = await GetBranchRefsAsync();
        var existingRef = refs.FirstOrDefault(r =>
            string.Equals(r.Name, $"refs/heads/{newBranch}", StringComparison.OrdinalIgnoreCase));

        var refUpdate = new GitRefUpdate
        {
            Name = $"refs/heads/{newBranch}",
            NewObjectId = baseSha,
            OldObjectId = existingRef?.ObjectId ?? "0000000000000000000000000000000000000000"
        };

        await RetryHelper.GetWaitAndRetryPolicy<Exception>(_logger)
            .ExecuteAsync(() => _gitClient.UpdateRefsAsync(
                [refUpdate],
                repositoryId: _repositoryId));
    }

    public async Task<RemotePullRequest> CreatePullRequestAsync(
        string title,
        string description,
        string headBranch,
        string baseBranch)
    {
        var pr = await RetryHelper.GetWaitAndRetryPolicy<Exception>(_logger)
            .ExecuteAsync(() => _gitClient.CreatePullRequestAsync(
                new GitPullRequest
                {
                    Title = title,
                    Description = description,
                    SourceRefName = $"refs/heads/{headBranch}",
                    TargetRefName = $"refs/heads/{baseBranch}"
                },
                _repositoryId));

        var url = pr.Url ?? string.Empty;
        return new RemotePullRequest(url, pr.PullRequestId, pr.Title, headBranch, baseBranch);
    }

    public async Task<RemotePullRequest?> FindPullRequestAsync(string headBranchPrefix, string? author = null)
    {
        var searchCriteria = new GitPullRequestSearchCriteria
        {
            Status = PullRequestStatus.Active,
            SourceRefName = $"refs/heads/{headBranchPrefix}"
        };

        var prs = await RetryHelper.GetWaitAndRetryPolicy<Exception>(_logger)
            .ExecuteAsync(() => _gitClient.GetPullRequestsAsync(_repositoryId, searchCriteria));

        var match = prs.FirstOrDefault(pr =>
            author is null || string.Equals(pr.CreatedBy?.DisplayName, author, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            return null;
        }

        return new RemotePullRequest(
            match.Url ?? string.Empty,
            match.PullRequestId,
            match.Title,
            match.SourceRefName.Replace("refs/heads/", ""),
            match.TargetRefName.Replace("refs/heads/", ""));
    }

    private Task<List<GitRef>> GetBranchRefsAsync() =>
        RetryHelper.GetWaitAndRetryPolicy<Exception>(_logger)
            .ExecuteAsync(() => _gitClient.GetBranchRefsAsync(_repositoryId));

    private static GitRef FindBranchRef(List<GitRef> refs, string branch) =>
        refs.FirstOrDefault(r => string.Equals(r.Name, $"refs/heads/{branch}", StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"Branch '{branch}' not found in repository.");
}
