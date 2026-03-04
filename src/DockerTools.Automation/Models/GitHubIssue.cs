// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.DockerTools.Automation.Models;

/// <summary>
/// GitHub issue metadata.
/// </summary>
public record GitHubIssue(int Number, string Title, string Body, IReadOnlyList<string> Labels);

/// <summary>
/// Parameters for updating a GitHub issue.
/// </summary>
public record GitHubIssueUpdate(GitHubIssueState? State = null);

/// <summary>
/// Criteria for searching GitHub issues.
/// </summary>
/// <param name="Labels">Filter by labels. All specified labels must be present.</param>
/// <param name="Since">Only return issues updated after this date.</param>
/// <param name="Filter">Issue filter type (e.g., all, created, assigned).</param>
public record GitHubIssueSearch(
    IReadOnlyList<string>? Labels = null,
    DateTimeOffset? Since = null,
    string? Filter = null);

/// <summary>
/// Represents the state of a GitHub issue.
/// </summary>
public enum GitHubIssueState
{
    Open,
    Closed
}
