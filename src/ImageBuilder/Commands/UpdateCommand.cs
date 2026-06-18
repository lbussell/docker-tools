// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Cottle;
using Microsoft.DotNet.DockerTools.Infrastructure;
using Microsoft.DotNet.ImageBuilder.Templating;

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
    private static readonly string s_outputRelativePath = Path.Combine("eng", "docker-tools");
    private static readonly string s_dockerImagesRelativePath = Path.Combine("templates", "variables", "docker-images.yml");
    private const string GitDirectoryName = ".git";
    private const string ImageBuilderTagTemplateVariableName = "IMAGE_BUILDER_TAG";
    private const string UniqueIdMetadataKey = "UniqueId";

    private static readonly DocumentConfiguration s_templateConfiguration = CottleDocumentConfiguration.Create();

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
        string gitPath = Path.Combine(currentDirectory, GitDirectoryName);
        if (!_fileSystem.DirectoryExists(gitPath) && !_fileSystem.FileExists(gitPath))
        {
            throw new InvalidOperationException(
                $"The 'update' command must be run from the root of a git repository. " +
                $"No '{GitDirectoryName}' entry was found in '{currentDirectory}'.");
        }

        string outputPath = Path.Combine(currentDirectory, s_outputRelativePath);
        if (!Options.Init && !_fileSystem.DirectoryExists(outputPath))
        {
            throw new InvalidOperationException(
                $"The output directory '{outputPath}' does not exist. " +
                $"Pass --init to create it (use this only when onboarding a repo to docker-tools).");
        }

        if (Options.Init)
        {
            if (!Options.IsDryRun)
            {
                _fileSystem.CreateDirectory(outputPath);
            }
        }

        string imageBuilderTag;
        if (GetImageBuilderTag() is { } resolvedImageBuilderTag && !string.IsNullOrWhiteSpace(resolvedImageBuilderTag))
        {
            imageBuilderTag = resolvedImageBuilderTag;
        }
        else
        {
            _logger.LogWarning(
                "This build of ImageBuilder was not built with the \"IMAGEBUILDER_TAG\" MSBuild property set. " +
                "ImageBuilder tag will fall back to \"latest\".");
            imageBuilderTag = "latest";
        }

        IReadOnlyList<string> embeddedRelativePaths = InfrastructureContent.GetRelativePaths();
        IReadOnlyDictionary<string, byte[]> filesToWrite = embeddedRelativePaths
            .ToDictionary(
                relativePath => Path.Combine(outputPath, relativePath.Replace('/', Path.DirectorySeparatorChar)),
                relativePath =>
                {
                    byte[] content = InfrastructureContent.ReadAllBytes(relativePath);
                    if (relativePath.Replace('/', Path.DirectorySeparatorChar) != s_dockerImagesRelativePath)
                    {
                        return content;
                    }

                    string template = Encoding.UTF8.GetString(content);
                    IDocument document = Document.CreateDefault(template, s_templateConfiguration).DocumentOrThrow;
                    Dictionary<Value, Value> symbols = new()
                    {
                        [ImageBuilderTagTemplateVariableName] = imageBuilderTag
                    };

                    return Encoding.UTF8.GetBytes(document.Render(Context.CreateBuiltin(symbols)));
                });

        if (_fileSystem.DirectoryExists(outputPath))
        {
            HashSet<string> destinationFiles = new(filesToWrite.Keys, StringComparer.Ordinal);
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

        if (!Options.IsDryRun && _fileSystem.DirectoryExists(outputPath))
        {
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

        return Task.CompletedTask;
    }

    protected virtual string? GetImageBuilderTag() =>
        typeof(UpdateCommand).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == UniqueIdMetadataKey)?.Value;
}
