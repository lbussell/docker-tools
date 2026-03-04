// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.DockerTools.Automation.Models;

namespace Microsoft.DotNet.DockerTools.Automation;

/// <summary>
/// Platform-neutral interface for remote git repository operations.
/// Implementations exist for GitHub (Octokit) and Azure DevOps (TeamFoundation SDK).
/// Each instance is scoped to a single repository.
/// </summary>
public interface IRemoteGitClient
{
    // --- File Operations ---

    /// <summary>
    /// Gets the content of a file at a specific branch or commit.
    /// </summary>
    /// <param name="path">Path to the file within the repository.</param>
    /// <param name="ref">Branch name or commit SHA to read from.</param>
    /// <returns>The file content as a string.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    Task<string> GetFileContentAsync(string path, string @ref);

    /// <summary>
    /// Checks whether a file exists at a specific branch.
    /// </summary>
    /// <param name="path">Path to the file within the repository.</param>
    /// <param name="branch">Branch name to check.</param>
    Task<bool> FileExistsAsync(string path, string branch);

    // --- Commit Operations ---

    /// <summary>
    /// Atomically pushes a set of file changes as a single commit to a branch.
    /// On GitHub, uses the Git Data API (tree → commit → ref update).
    /// On AzDo, uses the pushes API.
    /// </summary>
    /// <param name="branch">Target branch name.</param>
    /// <param name="commitMessage">Commit message.</param>
    /// <param name="files">Map of file path → content to commit.</param>
    /// <param name="force">Whether to force-update the branch ref (default: false).</param>
    /// <returns>The SHA of the new commit.</returns>
    Task<string> PushFilesAsync(
        string branch,
        string commitMessage,
        IReadOnlyDictionary<string, string> files,
        bool force = false);

    /// <summary>
    /// Gets the SHA of the latest commit on a branch.
    /// </summary>
    /// <param name="branch">Branch name.</param>
    Task<string> GetLatestCommitShaAsync(string branch);

    /// <summary>
    /// Gets commit metadata by SHA.
    /// </summary>
    /// <param name="sha">Commit SHA.</param>
    Task<RemoteCommit> GetCommitAsync(string sha);

    // --- Branch Operations ---

    /// <summary>
    /// Checks whether a branch exists.
    /// </summary>
    /// <param name="branch">Branch name.</param>
    Task<bool> BranchExistsAsync(string branch);

    /// <summary>
    /// Creates a new branch from a base branch. If the branch already exists,
    /// updates it to the head of the base branch.
    /// </summary>
    /// <param name="newBranch">Name of the branch to create or update.</param>
    /// <param name="baseBranch">Name of the branch to base it on.</param>
    Task CreateOrUpdateBranchAsync(string newBranch, string baseBranch);

    // --- Pull Request Operations ---

    /// <summary>
    /// Creates a pull request.
    /// </summary>
    /// <param name="title">PR title.</param>
    /// <param name="description">PR description/body.</param>
    /// <param name="headBranch">Source branch containing the changes.</param>
    /// <param name="baseBranch">Target branch to merge into.</param>
    /// <returns>Metadata about the created pull request.</returns>
    Task<RemotePullRequest> CreatePullRequestAsync(
        string title,
        string description,
        string headBranch,
        string baseBranch);

    /// <summary>
    /// Searches for an existing pull request by head branch prefix and optional author.
    /// Returns null if no matching PR is found.
    /// </summary>
    /// <param name="headBranchPrefix">Prefix of the source branch to search for.</param>
    /// <param name="author">Optional author filter.</param>
    Task<RemotePullRequest?> FindPullRequestAsync(string headBranchPrefix, string? author = null);
}
