// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.DockerTools.Automation.Helpers;
using Microsoft.DotNet.DockerTools.Automation.Models;
using Microsoft.Extensions.Logging;
using Octokit;

namespace Microsoft.DotNet.DockerTools.Automation.GitHub;

internal class GitHubIssuesClient(IGitHubClient client, ILogger logger) : IGitHubIssuesClient
{
    private readonly IGitHubClient _client = client;
    private readonly ILogger _logger = logger;

    public async Task<Models.GitHubIssue> CreateIssueAsync(
        string owner,
        string repo,
        string title,
        string body,
        IReadOnlyList<string> labels)
    {
        var newIssue = new NewIssue(title) { Body = body };
        foreach (var label in labels)
        {
            newIssue.Labels.Add(label);
        }

        var issue = await RetryHelper.GetWaitAndRetryPolicy<ApiException>(_logger)
            .ExecuteAsync(() => _client.Issue.Create(owner, repo, newIssue));
        return MapIssue(issue);
    }

    public async Task UpdateIssueAsync(string owner, string repo, int issueNumber, GitHubIssueUpdate update)
    {
        var issueUpdate = new IssueUpdate();
        if (update.State is not null)
        {
            issueUpdate.State = update.State switch
            {
                GitHubIssueState.Open => ItemState.Open,
                GitHubIssueState.Closed => ItemState.Closed,
                _ => throw new ArgumentOutOfRangeException(nameof(update))
            };
        }

        await RetryHelper.GetWaitAndRetryPolicy<ApiException>(_logger)
            .ExecuteAsync(() => _client.Issue.Update(owner, repo, issueNumber, issueUpdate));
    }

    public async Task<IReadOnlyList<Models.GitHubIssue>> SearchIssuesAsync(
        string owner,
        string repo,
        GitHubIssueSearch criteria)
    {
        var request = new RepositoryIssueRequest();
        if (criteria.Since is not null)
        {
            request.Since = criteria.Since;
        }

        if (criteria.Filter is not null)
        {
            request.Filter = Enum.Parse<IssueFilter>(criteria.Filter, ignoreCase: true);
        }

        if (criteria.Labels is not null)
        {
            foreach (var label in criteria.Labels)
            {
                request.Labels.Add(label);
            }
        }

        var issues = await RetryHelper.GetWaitAndRetryPolicy<ApiException>(_logger)
            .ExecuteAsync(() => _client.Issue.GetAllForRepository(owner, repo, request));
        return issues.Select(MapIssue).ToList();
    }

    public async Task CreateCommentAsync(string owner, string repo, int issueNumber, string body)
    {
        await RetryHelper.GetWaitAndRetryPolicy<ApiException>(_logger)
            .ExecuteAsync(() => _client.Issue.Comment.Create(owner, repo, issueNumber, body));
    }

    private static Models.GitHubIssue MapIssue(Issue issue) =>
        new(issue.Number, issue.Title, issue.Body ?? string.Empty,
            issue.Labels.Select(l => l.Name).ToList());
}
