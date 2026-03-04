// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.DockerTools.Automation.Helpers;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.DockerTools.Automation.LocalGit;

internal class LocalGitClient(ILogger<LocalGitClient> logger) : ILocalGitClient
{
    private readonly ILogger<LocalGitClient> _logger = logger;

    public async Task<string> GetCommitShaAsync(string filePath, bool useFullHash = false)
    {
        var directory = new FileInfo(filePath).Directory;
        while (!directory!.GetDirectories(".git").Any())
        {
            directory = directory.Parent;

            if (directory is null)
            {
                throw new InvalidOperationException(
                    $"File '{filePath}' is not contained within a Git repository.");
            }
        }

        var repoRoot = directory.FullName;
        var relativePath = Path.GetRelativePath(repoRoot, filePath);
        var format = useFullHash ? "H" : "h";

        return await ProcessRunner.RunAsync(
            "git", $"log -1 --format=format:%{format} {relativePath}", repoRoot, _logger);
    }

    public async Task<string> CloneAsync(
        string repoUrl,
        string targetDirectory,
        string? branch = null,
        int? depth = null,
        GitCredentials? credentials = null)
    {
        var url = credentials is not null ? InjectCredentials(repoUrl, credentials) : repoUrl;
        var args = $"clone {url} {targetDirectory}";
        var logArgs = credentials is not null ? $"clone {repoUrl} {targetDirectory}" : null;

        if (branch is not null)
        {
            args += $" --branch {branch}";
            if (logArgs is not null)
            {
                logArgs += $" --branch {branch}";
            }
        }

        if (depth is not null)
        {
            args += $" --depth {depth}";
            if (logArgs is not null)
            {
                logArgs += $" --depth {depth}";
            }
        }

        await ProcessRunner.RunAsync("git", args, workingDirectory: null, _logger, logArgumentsOverride: logArgs);

        return targetDirectory;
    }

    public Task StageAsync(string repoDirectory, string path) =>
        ProcessRunner.RunAsync("git", $"add {path}", repoDirectory, _logger);

    public async Task<string> CommitAsync(
        string repoDirectory,
        string message,
        string authorName,
        string authorEmail,
        bool allowEmpty = false)
    {
        var args = $"""-c user.name="{authorName}" -c user.email="{authorEmail}" commit -m "{message}""";

        if (allowEmpty)
        {
            args += " --allow-empty";
        }

        await ProcessRunner.RunAsync("git", args, repoDirectory, _logger);

        return await ProcessRunner.RunAsync("git", "rev-parse HEAD", repoDirectory, _logger);
    }

    public async Task PushAsync(
        string repoDirectory,
        string remote,
        string branch,
        GitCredentials? credentials = null)
    {
        if (credentials is not null)
        {
            var remoteUrl = await ProcessRunner.RunAsync(
                "git", $"remote get-url {remote}", repoDirectory, _logger);
            var injectedUrl = InjectCredentials(remoteUrl, credentials);

            await ProcessRunner.RunAsync(
                "git", $"push {injectedUrl} {branch}", repoDirectory, _logger,
                logArgumentsOverride: $"push {remote} {branch}");
        }
        else
        {
            await ProcessRunner.RunAsync("git", $"push {remote} {branch}", repoDirectory, _logger);
        }
    }

    private static string InjectCredentials(string url, GitCredentials credentials) =>
        url.Replace("https://", $"https://{Uri.EscapeDataString(credentials.Username)}:{Uri.EscapeDataString(credentials.Token)}@");
}
