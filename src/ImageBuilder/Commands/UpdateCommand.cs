// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.DockerTools.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.ImageBuilder.Commands;

/// <summary>
/// Writes the <c>eng/docker-tools</c> infrastructure files (pipeline templates, scripts, and docs)
/// that are embedded in this ImageBuilder build to disk. This keeps the pipeline content in a
/// consuming repo coupled to the ImageBuilder version that uses it.
/// </summary>
/// <remarks>
/// The command must be run from the root of a git repository and always targets
/// <c>eng/docker-tools</c> relative to that root. It performs a full mirror: files under the
/// target directory that ImageBuilder no longer ships are removed so the output exactly matches
/// what is embedded.
/// </remarks>
public class UpdateCommand : Command<UpdateOptions>
{
    private const string OutputDirectoryName = "docker-tools";
    private const string EngDirectoryName = "eng";
    private const string GitDirectoryName = ".git";

    private readonly IFileSystem _fileSystem;
    private readonly ILogger<UpdateCommand> _logger;

    public UpdateCommand(IFileSystem fileSystem, ILogger<UpdateCommand> logger)
    {
        _fileSystem = fileSystem;
        _logger = logger;
    }

    protected override string Description => "Writes ImageBuilder's bundled docker-tools infrastructure files to disk";

    public override Task ExecuteAsync()
    {
        string currentDirectory = _fileSystem.GetCurrentDirectory();
        EnsureGitRepositoryRoot(currentDirectory);

        string outputPath = Path.Combine(currentDirectory, EngDirectoryName, OutputDirectoryName);
        EnsureOutputDirectory(outputPath);

        IReadOnlyDictionary<string, byte[]> filesToWrite = GetEmbeddedFilesByDestination(outputPath);

        DeleteStaleFiles(outputPath, filesToWrite.Keys);
        WriteFiles(filesToWrite);
        PruneEmptyDirectories(outputPath);

        return Task.CompletedTask;
    }

    private void EnsureGitRepositoryRoot(string currentDirectory)
    {
        // A git working tree root is the directory that contains a '.git' entry. It is a directory
        // for a normal clone, or a file for worktrees and submodules, so accept either.
        string gitPath = Path.Combine(currentDirectory, GitDirectoryName);
        if (!_fileSystem.DirectoryExists(gitPath) && !_fileSystem.FileExists(gitPath))
        {
            throw new InvalidOperationException(
                $"The 'update' command must be run from the root of a git repository. " +
                $"No '{GitDirectoryName}' entry was found in '{currentDirectory}'.");
        }
    }

    private void EnsureOutputDirectory(string outputPath)
    {
        if (_fileSystem.DirectoryExists(outputPath))
        {
            return;
        }

        if (!Options.Init)
        {
            throw new InvalidOperationException(
                $"The output directory '{outputPath}' does not exist. " +
                $"Pass --init to create it (use this only when onboarding a repo to docker-tools).");
        }

        if (Options.IsDryRun)
        {
            _logger.LogInformation("[Dry run] Would create directory '{OutputPath}'", outputPath);
            return;
        }

        _fileSystem.CreateDirectory(outputPath);
        _logger.LogInformation("Created directory '{OutputPath}'", outputPath);
    }

    private static IReadOnlyDictionary<string, byte[]> GetEmbeddedFilesByDestination(string outputPath) =>
        InfrastructureContent.GetRelativePaths()
            .ToDictionary(
                relativePath => Path.Combine(outputPath, NormalizeSeparators(relativePath)),
                InfrastructureContent.ReadAllBytes);

    private void DeleteStaleFiles(string outputPath, IEnumerable<string> filesToWrite)
    {
        if (!_fileSystem.DirectoryExists(outputPath))
        {
            return;
        }

        HashSet<string> destinationFiles = new(filesToWrite, StringComparer.Ordinal);
        IEnumerable<string> staleFiles = _fileSystem.EnumerateFiles(outputPath)
            .Where(existingFile => !destinationFiles.Contains(existingFile));

        foreach (string staleFile in staleFiles)
        {
            if (Options.IsDryRun)
            {
                _logger.LogInformation("[Dry run] Would delete stale file '{StaleFile}'", staleFile);
                continue;
            }

            _fileSystem.DeleteFile(staleFile);
            _logger.LogInformation("Deleted stale file '{StaleFile}'", staleFile);
        }
    }

    private void WriteFiles(IReadOnlyDictionary<string, byte[]> filesToWrite)
    {
        foreach ((string destinationPath, byte[] contents) in filesToWrite)
        {
            if (Options.IsDryRun)
            {
                _logger.LogInformation("[Dry run] Would write '{DestinationPath}'", destinationPath);
                continue;
            }

            string? destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationDirectory))
            {
                _fileSystem.CreateDirectory(destinationDirectory);
            }

            _fileSystem.WriteAllBytes(destinationPath, contents);
            _logger.LogInformation("Wrote '{DestinationPath}'", destinationPath);
        }
    }

    private void PruneEmptyDirectories(string outputPath)
    {
        if (Options.IsDryRun || !_fileSystem.DirectoryExists(outputPath))
        {
            return;
        }

        // Delete deepest directories first so that parents that become empty are pruned in turn.
        IEnumerable<string> directories = _fileSystem.EnumerateDirectories(outputPath)
            .OrderByDescending(directory => directory.Length);

        foreach (string directory in directories)
        {
            if (_fileSystem.EnumerateFiles(directory).Any() || _fileSystem.EnumerateDirectories(directory).Any())
            {
                continue;
            }

            _fileSystem.DeleteDirectory(directory);
            _logger.LogInformation("Pruned empty directory '{Directory}'", directory);
        }
    }

    private static string NormalizeSeparators(string relativePath) =>
        relativePath.Replace('/', Path.DirectorySeparatorChar);
}
