// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.DotNet.DockerTools.Infrastructure;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Microsoft.DotNet.ImageBuilder.Tests;

[TestClass]
public class UpdateCommandTests
{
    private const string RepoRoot = "/repo";

    private static readonly string OutputPath = Path.Combine(RepoRoot, "eng", "docker-tools");

    [TestMethod]
    public async Task UpdateCommand_WritesAllEmbeddedFiles()
    {
        InMemoryFileSystem fileSystem = CreateRepoFileSystem();
        fileSystem.AddDirectory(OutputPath);
        UpdateCommand command = CreateCommand(fileSystem);

        await command.ExecuteAsync();

        IReadOnlyList<string> expectedPaths = InfrastructureContent.GetRelativePaths();
        expectedPaths.ShouldNotBeEmpty();

        foreach (string relativePath in expectedPaths)
        {
            string expectedDestination = Path.Combine(OutputPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            fileSystem.FileExists(expectedDestination).ShouldBeTrue();
            fileSystem.GetFileBytes(expectedDestination).ShouldBe(InfrastructureContent.ReadAllBytes(relativePath));
        }
    }

    [TestMethod]
    public async Task UpdateCommand_DeletesStaleFiles()
    {
        InMemoryFileSystem fileSystem = CreateRepoFileSystem();
        fileSystem.AddDirectory(OutputPath);
        string staleFile = Path.Combine(OutputPath, "templates", "removed-template.yml");
        fileSystem.AddFile(staleFile, "stale");
        UpdateCommand command = CreateCommand(fileSystem);

        await command.ExecuteAsync();

        fileSystem.FileExists(staleFile).ShouldBeFalse();
        fileSystem.FilesDeleted.ShouldContain(staleFile);
    }

    [TestMethod]
    public async Task UpdateCommand_PrunesEmptyDirectories()
    {
        InMemoryFileSystem fileSystem = CreateRepoFileSystem();
        fileSystem.AddDirectory(OutputPath);
        string staleDirectory = Path.Combine(OutputPath, "obsolete");
        string staleFile = Path.Combine(staleDirectory, "old.yml");
        fileSystem.AddFile(staleFile, "stale");
        UpdateCommand command = CreateCommand(fileSystem);

        await command.ExecuteAsync();

        fileSystem.DirectoriesDeleted.ShouldContain(staleDirectory);
    }

    [TestMethod]
    public async Task UpdateCommand_NotGitRoot_Throws()
    {
        InMemoryFileSystem fileSystem = new() { CurrentDirectory = RepoRoot };
        fileSystem.AddDirectory(OutputPath);
        UpdateCommand command = CreateCommand(fileSystem);

        InvalidOperationException exception =
            await Should.ThrowAsync<InvalidOperationException>(() => command.ExecuteAsync());
        exception.Message.ShouldContain("root of a git repository");
    }

    [TestMethod]
    public async Task UpdateCommand_OutputMissingWithoutInit_Throws()
    {
        InMemoryFileSystem fileSystem = CreateRepoFileSystem();
        UpdateCommand command = CreateCommand(fileSystem);

        InvalidOperationException exception =
            await Should.ThrowAsync<InvalidOperationException>(() => command.ExecuteAsync());
        exception.Message.ShouldContain("--init");
        fileSystem.FilesWritten.ShouldBeEmpty();
    }

    [TestMethod]
    public async Task UpdateCommand_OutputMissingWithInit_CreatesAndWrites()
    {
        InMemoryFileSystem fileSystem = CreateRepoFileSystem();
        UpdateCommand command = CreateCommand(fileSystem);
        command.Options.Init = true;

        await command.ExecuteAsync();

        fileSystem.DirectoriesCreated.ShouldContain(OutputPath);
        fileSystem.FilesWritten.ShouldNotBeEmpty();
    }

    [TestMethod]
    public async Task UpdateCommand_DryRun_MakesNoChanges()
    {
        InMemoryFileSystem fileSystem = CreateRepoFileSystem();
        fileSystem.AddDirectory(OutputPath);
        string staleFile = Path.Combine(OutputPath, "templates", "removed-template.yml");
        fileSystem.AddFile(staleFile, "stale");
        UpdateCommand command = CreateCommand(fileSystem);
        command.Options.IsDryRun = true;

        await command.ExecuteAsync();

        fileSystem.FilesWritten.ShouldBeEmpty();
        fileSystem.FilesDeleted.ShouldBeEmpty();
        fileSystem.DirectoriesDeleted.ShouldBeEmpty();
        fileSystem.FileExists(staleFile).ShouldBeTrue();
    }

    private static InMemoryFileSystem CreateRepoFileSystem()
    {
        InMemoryFileSystem fileSystem = new() { CurrentDirectory = RepoRoot };
        fileSystem.AddDirectory(Path.Combine(RepoRoot, ".git"));
        return fileSystem;
    }

    private static UpdateCommand CreateCommand(IFileSystem fileSystem) =>
        new(fileSystem, Mock.Of<ILogger<UpdateCommand>>());
}
