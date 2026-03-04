// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.DockerTools.Automation.Models;

namespace Microsoft.DotNet.DockerTools.Automation.GitHub;

/// <summary>
/// GitHub Issues API operations. Covers issue creation, updates, search, and commenting.
/// </summary>
public interface IGitHubIssuesClient
{
    /// <summary>
    /// Creates a new GitHub issue.
    /// </summary>
    /// <param name="owner">Repository owner.</param>
    /// <param name="repo">Repository name.</param>
    /// <param name="title">Issue title.</param>
    /// <param name="body">Issue body (markdown).</param>
    /// <param name="labels">Labels to apply to the issue.</param>
    Task<GitHubIssue> CreateIssueAsync(
        string owner,
        string repo,
        string title,
        string body,
        IReadOnlyList<string> labels);

    /// <summary>
    /// Updates an existing GitHub issue (e.g., to close it).
    /// </summary>
    /// <param name="owner">Repository owner.</param>
    /// <param name="repo">Repository name.</param>
    /// <param name="issueNumber">The issue number to update.</param>
    /// <param name="update">The fields to update.</param>
    Task UpdateIssueAsync(string owner, string repo, int issueNumber, GitHubIssueUpdate update);

    /// <summary>
    /// Searches for issues in a repository matching the given criteria.
    /// </summary>
    /// <param name="owner">Repository owner.</param>
    /// <param name="repo">Repository name.</param>
    /// <param name="criteria">Search criteria (labels, date range, filter type).</param>
    Task<IReadOnlyList<GitHubIssue>> SearchIssuesAsync(
        string owner,
        string repo,
        GitHubIssueSearch criteria);

    /// <summary>
    /// Posts a comment on a GitHub issue or pull request.
    /// </summary>
    /// <param name="owner">Repository owner.</param>
    /// <param name="repo">Repository name.</param>
    /// <param name="issueNumber">The issue or PR number to comment on.</param>
    /// <param name="body">Comment body (markdown).</param>
    Task CreateCommentAsync(string owner, string repo, int issueNumber, string body);
}
