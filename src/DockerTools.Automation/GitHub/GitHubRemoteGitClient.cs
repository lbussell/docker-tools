// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.DockerTools.Automation.Helpers;
using Microsoft.DotNet.DockerTools.Automation.Models;
using Microsoft.Extensions.Logging;
using Octokit;

namespace Microsoft.DotNet.DockerTools.Automation.GitHub;

internal class GitHubRemoteGitClient(
    IGitHubClient client,
    IBlobsClient blobsClient,
    ITreesClient treesClient,
    string owner,
    string repo,
    ILogger<GitHubRemoteGitClient> logger) : IRemoteGitClient
{
    private readonly IGitHubClient _client = client;
    private readonly IBlobsClient _blobsClient = blobsClient;
    private readonly ITreesClient _treesClient = treesClient;
    private readonly string _owner = owner;
    private readonly string _repo = repo;
    private readonly ILogger<GitHubRemoteGitClient> _logger = logger;

    public async Task<string> GetFileContentAsync(string path, string @ref)
    {
        var contents = await RetryHelper.GetWaitAndRetryPolicy<ApiException>(_logger)
            .ExecuteAsync(() => _client.Repository.Content.GetAllContentsByRef(_owner, _repo, path, @ref));
        return contents[0].Content;
    }

    public async Task<bool> FileExistsAsync(string path, string branch)
    {
        try
        {
            await GetFileContentAsync(path, branch);
            return true;
        }
        catch (NotFoundException)
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
        var currentRef = await RetryHelper.GetWaitAndRetryPolicy<ApiException>(_logger)
            .ExecuteAsync(() => _client.Git.Reference.Get(_owner, _repo, $"heads/{branch}"));
        var currentCommitSha = currentRef.Object.Sha;

        var currentCommit = await RetryHelper.GetWaitAndRetryPolicy<ApiException>(_logger)
            .ExecuteAsync(() => _client.Git.Commit.Get(_owner, _repo, currentCommitSha));

        var newTree = new NewTree { BaseTree = currentCommit.Tree.Sha };

        foreach (var (path, content) in files)
        {
            var blob = await RetryHelper.GetWaitAndRetryPolicy<ApiException>(_logger)
                .ExecuteAsync(() => _blobsClient.Create(_owner, _repo, new NewBlob { Content = content, Encoding = EncodingType.Utf8 }));

            newTree.Tree.Add(new NewTreeItem
            {
                Path = path,
                Mode = "100644",
                Type = TreeType.Blob,
                Sha = blob.Sha
            });
        }

        var tree = await RetryHelper.GetWaitAndRetryPolicy<ApiException>(_logger)
            .ExecuteAsync(() => _treesClient.Create(_owner, _repo, newTree));

        var commit = await RetryHelper.GetWaitAndRetryPolicy<ApiException>(_logger)
            .ExecuteAsync(() => _client.Git.Commit.Create(_owner, _repo, new NewCommit(commitMessage, tree.Sha, currentCommitSha)));

        await RetryHelper.GetWaitAndRetryPolicy<ApiException>(_logger)
            .ExecuteAsync(() => _client.Git.Reference.Update(_owner, _repo, $"heads/{branch}", new ReferenceUpdate(commit.Sha, force)));

        return commit.Sha;
    }

    public async Task<string> GetLatestCommitShaAsync(string branch)
    {
        var reference = await RetryHelper.GetWaitAndRetryPolicy<ApiException>(_logger)
            .ExecuteAsync(() => _client.Git.Reference.Get(_owner, _repo, $"heads/{branch}"));
        return reference.Object.Sha;
    }

    public async Task<RemoteCommit> GetCommitAsync(string sha)
    {
        var commit = await RetryHelper.GetWaitAndRetryPolicy<ApiException>(_logger)
            .ExecuteAsync(() => _client.Git.Commit.Get(_owner, _repo, sha));
        return new RemoteCommit(commit.Sha, commit.Author.Name, commit.Message);
    }

    public async Task<bool> BranchExistsAsync(string branch)
    {
        try
        {
            await RetryHelper.GetWaitAndRetryPolicy<ApiException>(_logger)
                .ExecuteAsync(() => _client.Git.Reference.Get(_owner, _repo, $"heads/{branch}"));
            return true;
        }
        catch (NotFoundException)
        {
            return false;
        }
    }

    public async Task CreateOrUpdateBranchAsync(string newBranch, string baseBranch)
    {
        var baseSha = await GetLatestCommitShaAsync(baseBranch);

        try
        {
            await RetryHelper.GetWaitAndRetryPolicy<ApiException>(_logger)
                .ExecuteAsync(() => _client.Git.Reference.Update(_owner, _repo, $"heads/{newBranch}", new ReferenceUpdate(baseSha)));
        }
        catch (NotFoundException)
        {
            await RetryHelper.GetWaitAndRetryPolicy<ApiException>(_logger)
                .ExecuteAsync(() => _client.Git.Reference.Create(_owner, _repo, new NewReference($"refs/heads/{newBranch}", baseSha)));
        }
    }

    public async Task<RemotePullRequest> CreatePullRequestAsync(
        string title,
        string description,
        string headBranch,
        string baseBranch)
    {
        var pr = await RetryHelper.GetWaitAndRetryPolicy<ApiException>(_logger)
            .ExecuteAsync(() => _client.PullRequest.Create(_owner, _repo, new NewPullRequest(title, headBranch, baseBranch) { Body = description }));
        return new RemotePullRequest(pr.HtmlUrl, pr.Number, pr.Title, pr.Head.Ref, pr.Base.Ref);
    }

    public async Task<RemotePullRequest?> FindPullRequestAsync(string headBranchPrefix, string? author = null)
    {
        var request = new PullRequestRequest { State = ItemStateFilter.Open };
        var prs = await RetryHelper.GetWaitAndRetryPolicy<ApiException>(_logger)
            .ExecuteAsync(() => _client.PullRequest.GetAllForRepository(_owner, _repo, request));

        var match = prs.FirstOrDefault(pr =>
            pr.Head.Ref.StartsWith(headBranchPrefix, StringComparison.OrdinalIgnoreCase) &&
            (author is null || string.Equals(pr.User.Login, author, StringComparison.OrdinalIgnoreCase)));

        return match is null
            ? null
            : new RemotePullRequest(match.HtmlUrl, match.Number, match.Title, match.Head.Ref, match.Base.Ref);
    }
}
