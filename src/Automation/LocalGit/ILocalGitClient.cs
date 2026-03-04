// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.DockerTools.Automation.LocalGit;

/// <summary>
/// Local git operations via the git CLI.
/// All methods assume git is available on PATH.
/// </summary>
public interface ILocalGitClient
{
    /// <summary>
    /// Gets the commit SHA that last modified a file.
    /// </summary>
    /// <param name="filePath">Path to the file (relative to the repository root).</param>
    /// <param name="useFullHash">If true, returns the full 40-character SHA; otherwise returns the abbreviated hash.</param>
    Task<string> GetCommitShaAsync(string filePath, bool useFullHash = false);

    /// <summary>
    /// Clones a repository to a local directory.
    /// </summary>
    /// <param name="repoUrl">The repository URL to clone.</param>
    /// <param name="targetDirectory">The directory to clone into.</param>
    /// <param name="branch">Optional branch to check out after cloning.</param>
    /// <param name="depth">Optional shallow clone depth (e.g., 1 for shallowest).</param>
    /// <param name="credentials">Optional credentials for authenticated cloning.</param>
    /// <returns>The path to the cloned repository working directory.</returns>
    Task<string> CloneAsync(
        string repoUrl,
        string targetDirectory,
        string? branch = null,
        int? depth = null,
        GitCredentials? credentials = null);

    /// <summary>
    /// Stages a file in a local repository working directory.
    /// </summary>
    /// <param name="repoDirectory">Path to the repository working directory.</param>
    /// <param name="path">Path to the file to stage, relative to the repo root.</param>
    Task StageAsync(string repoDirectory, string path);

    /// <summary>
    /// Creates a commit in a local repository.
    /// </summary>
    /// <param name="repoDirectory">Path to the repository working directory.</param>
    /// <param name="message">Commit message.</param>
    /// <param name="authorName">Author name for the commit.</param>
    /// <param name="authorEmail">Author email for the commit.</param>
    /// <param name="allowEmpty">Whether to allow creating a commit with no changes.</param>
    /// <returns>The SHA of the created commit.</returns>
    Task<string> CommitAsync(
        string repoDirectory,
        string message,
        string authorName,
        string authorEmail,
        bool allowEmpty = false);

    /// <summary>
    /// Pushes a local branch to a remote.
    /// </summary>
    /// <param name="repoDirectory">Path to the repository working directory.</param>
    /// <param name="remote">Remote name (e.g., "origin").</param>
    /// <param name="branch">Branch name to push.</param>
    /// <param name="credentials">Optional credentials for authenticated pushing.</param>
    Task PushAsync(
        string repoDirectory,
        string remote,
        string branch,
        GitCredentials? credentials = null);
}
