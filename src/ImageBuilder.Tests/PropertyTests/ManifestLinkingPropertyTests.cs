// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Services;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Shouldly;
using Xunit;
using V2 = Microsoft.DotNet.ImageBuilder.Models.Image.V2;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ManifestHelper;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.DockerfileHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests.PropertyTests;

/// <summary>
/// Tests verifying that <see cref="ManifestLinkIndex"/> correctly maps
/// V2 image-info data to manifest ViewModel objects.
/// </summary>
public class ManifestLinkingPropertyTests
{
    [Fact]
    public void Create_MatchesPlatformToManifest()
    {
        using TempFolderContext tempFolderContext = new();

        string dockerfilePath = CreateDockerfile("1.0/runtime/linux", tempFolderContext);

        Manifest manifest = CreateManifest(
            CreateRepo("runtime",
                CreateImage(
                    [
                        CreatePlatform(dockerfilePath, ["tag1"], OS.Linux, "noble")
                    ],
                    productVersion: "1.0")));

        string manifestPath = Path.Combine(tempFolderContext.Path, "manifest.json");
        File.WriteAllText(manifestPath, JsonHelper.SerializeObject(manifest));
        ManifestInfo manifestInfo = TestHelper.CreateManifestJsonService()
            .Load(new ManifestLinkingManifestOptions(manifestPath));

        V2.ImageArtifactDetails details = new()
        {
            Repos =
            [
                new V2.RepoData
                {
                    Repo = "runtime",
                    Images =
                    [
                        new V2.ImageData
                        {
                            ProductVersion = "1.0",
                            Platforms =
                            [
                                new V2.PlatformData
                                {
                                    Dockerfile = "1.0/runtime/linux/Dockerfile",
                                    Architecture = "amd64",
                                    OsType = "Linux",
                                    OsVersion = "noble",
                                    Digest = "runtime@sha256:abc",
                                    CommitUrl = "https://example.com/commit/1",
                                    SimpleTags = ["tag1"],
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        ManifestLinkIndex index = ManifestLinkIndex.Create(details, manifestInfo);

        RepoInfo? repoInfo = index.GetRepoInfo("runtime");
        repoInfo.ShouldNotBeNull();
        repoInfo.Name.ShouldBe("runtime");

        V2.PlatformData platform = details.Repos[0].Images[0].Platforms[0];
        PlatformInfo? platformInfo = index.GetPlatformInfo("runtime", platform, "1.0");
        platformInfo.ShouldNotBeNull();

        ImageInfo? imageInfo = index.GetImageInfo("runtime", platform, "1.0");
        imageInfo.ShouldNotBeNull();
        imageInfo.ProductVersion.ShouldBe("1.0");
    }

    [Fact]
    public void Create_UnmatchedPlatform_SkippedWhenValidationDisabled()
    {
        using TempFolderContext tempFolderContext = new();

        string dockerfilePath = CreateDockerfile("1.0/runtime/linux", tempFolderContext);

        Manifest manifest = CreateManifest(
            CreateRepo("runtime",
                CreateImage(
                    [
                        CreatePlatform(dockerfilePath, ["tag1"], OS.Linux, "noble")
                    ],
                    productVersion: "1.0")));

        string manifestPath = Path.Combine(tempFolderContext.Path, "manifest.json");
        File.WriteAllText(manifestPath, JsonHelper.SerializeObject(manifest));
        ManifestInfo manifestInfo = TestHelper.CreateManifestJsonService()
            .Load(new ManifestLinkingManifestOptions(manifestPath));

        V2.ImageArtifactDetails details = new()
        {
            Repos =
            [
                new V2.RepoData
                {
                    Repo = "runtime",
                    Images =
                    [
                        new V2.ImageData
                        {
                            ProductVersion = "1.0",
                            Platforms =
                            [
                                new V2.PlatformData
                                {
                                    Dockerfile = "nonexistent/path/Dockerfile",
                                    Architecture = "amd64",
                                    OsType = "Linux",
                                    OsVersion = "noble",
                                    Digest = "runtime@sha256:abc",
                                    CommitUrl = "https://example.com/commit/1",
                                    SimpleTags = ["tag1"],
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        ManifestLinkIndex index = ManifestLinkIndex.Create(
            details, manifestInfo, skipManifestValidation: true);

        V2.PlatformData platform = details.Repos[0].Images[0].Platforms[0];
        index.GetPlatformInfo("runtime", platform, "1.0").ShouldBeNull();
    }

    [Fact]
    public void Create_UnknownRepo_SkippedGracefully()
    {
        using TempFolderContext tempFolderContext = new();

        string dockerfilePath = CreateDockerfile("1.0/runtime/linux", tempFolderContext);

        Manifest manifest = CreateManifest(
            CreateRepo("runtime",
                CreateImage(
                    [
                        CreatePlatform(dockerfilePath, ["tag1"], OS.Linux, "noble")
                    ],
                    productVersion: "1.0")));

        string manifestPath = Path.Combine(tempFolderContext.Path, "manifest.json");
        File.WriteAllText(manifestPath, JsonHelper.SerializeObject(manifest));
        ManifestInfo manifestInfo = TestHelper.CreateManifestJsonService()
            .Load(new ManifestLinkingManifestOptions(manifestPath));

        V2.ImageArtifactDetails details = new()
        {
            Repos =
            [
                new V2.RepoData
                {
                    Repo = "unknown-repo",
                    Images = []
                }
            ]
        };

        ManifestLinkIndex index = ManifestLinkIndex.Create(
            details, manifestInfo, skipManifestValidation: true);

        index.GetRepoInfo("unknown-repo").ShouldBeNull();
    }

    private sealed class ManifestLinkingManifestOptions : IManifestOptionsInfo
    {
        public ManifestLinkingManifestOptions(string manifestPath)
        {
            Manifest = manifestPath;
        }

        public string Manifest { get; }
        public string? RegistryOverride => null;
        public string? RepoPrefix => null;
        public IDictionary<string, string> Variables { get; } = new Dictionary<string, string>();
        public bool IsDryRun => false;
        public bool IsVerbose => false;
        public ManifestFilter GetManifestFilter() => new(Enumerable.Empty<string>());
        public string GetOption(string name) => throw new System.NotImplementedException();
    }
}
