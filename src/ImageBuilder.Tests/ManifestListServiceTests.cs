// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Moq;
using Shouldly;
using Xunit;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.DockerfileHelper;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ImageInfoHelper;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ManifestHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests;

public class ManifestListServiceTests
{
    /// <summary>
    /// Verifies that a manifest list is created containing all built platforms.
    /// </summary>
    [Fact]
    public void CreateManifestLists_BasicMultiPlatform()
    {
        using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

        string dockerfile1 = CreateDockerfile("1.0/repo/linux-amd64", tempFolderContext);
        string dockerfile2 = CreateDockerfile("1.0/repo/linux-arm64", tempFolderContext);

        Manifest manifest = CreateManifest(
            CreateRepo("repo",
                CreateImage(
                    ["sharedtag"],
                    CreatePlatform(dockerfile1, ["tag-amd64"]),
                    CreatePlatform(dockerfile2, ["tag-arm64"], architecture: Architecture.ARM64))));

        ImageArtifactDetails imageArtifactDetails = CreateImageArtifactDetails(
            CreateRepoData("repo",
                CreateImageData(
                    ["sharedtag"],
                    CreatePlatform(dockerfile1, simpleTags: ["tag-amd64"]),
                    CreatePlatform(dockerfile2, simpleTags: ["tag-arm64"], architecture: "arm64"))));

        Mock<IDockerService> dockerServiceMock = new();
        ManifestListService service = CreateService(dockerServiceMock);
        ManifestInfo manifestInfo = LoadManifest(manifest, tempFolderContext);
        ImageArtifactDetails linkedImageInfo = LoadImageInfo(imageArtifactDetails, manifestInfo, tempFolderContext);

        IReadOnlyList<string> createdTags = service.CreateManifestLists(
            manifestInfo, linkedImageInfo, repoPrefix: null, isDryRun: false);

        createdTags.Count.ShouldBe(1);
        createdTags.ShouldContain("repo:sharedtag");

        dockerServiceMock.Verify(o => o.CreateManifestList(
            "repo:sharedtag",
            It.Is<IEnumerable<string>>(images =>
                images.Contains("repo:tag-amd64") && images.Contains("repo:tag-arm64")),
            false));
    }

    /// <summary>
    /// Verifies that only platforms present in image-info are included in the manifest list.
    /// Platforms defined in the manifest but not built should be excluded.
    /// </summary>
    [Fact]
    public void CreateManifestLists_OnlyIncludesBuiltPlatforms()
    {
        using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

        string dockerfileAmd64 = CreateDockerfile("1.0/repo/linux-amd64", tempFolderContext);
        string dockerfileArm64 = CreateDockerfile("1.0/repo/linux-arm64", tempFolderContext);
        string dockerfileWindows = CreateDockerfile("1.0/repo/windows", tempFolderContext);

        // Manifest defines 3 platforms
        Manifest manifest = CreateManifest(
            CreateRepo("repo",
                CreateImage(
                    ["sharedtag"],
                    CreatePlatform(dockerfileAmd64, ["tag-amd64"]),
                    CreatePlatform(dockerfileArm64, ["tag-arm64"], architecture: Architecture.ARM64),
                    CreatePlatform(dockerfileWindows, ["tag-windows"], os: OS.Windows, osVersion: "ltsc2022"))));

        // But image-info only has 2 platforms (Windows wasn't built)
        ImageArtifactDetails imageArtifactDetails = CreateImageArtifactDetails(
            CreateRepoData("repo",
                CreateImageData(
                    ["sharedtag"],
                    CreatePlatform(dockerfileAmd64, simpleTags: ["tag-amd64"]),
                    CreatePlatform(dockerfileArm64, simpleTags: ["tag-arm64"], architecture: "arm64"))));

        Mock<IDockerService> dockerServiceMock = new();
        ManifestListService service = CreateService(dockerServiceMock);
        ManifestInfo manifestInfo = LoadManifest(manifest, tempFolderContext);
        ImageArtifactDetails linkedImageInfo = LoadImageInfo(imageArtifactDetails, manifestInfo, tempFolderContext);

        IReadOnlyList<string> createdTags = service.CreateManifestLists(
            manifestInfo, linkedImageInfo, repoPrefix: null, isDryRun: false);

        createdTags.Count.ShouldBe(1);

        // Manifest list should only contain the 2 built platforms, not Windows
        dockerServiceMock.Verify(o => o.CreateManifestList(
            "repo:sharedtag",
            It.Is<IEnumerable<string>>(images =>
                images.Count() == 2 &&
                images.Contains("repo:tag-amd64") &&
                images.Contains("repo:tag-arm64")),
            false));
    }

    /// <summary>
    /// Verifies that no manifest list is created when an image has shared tags
    /// but no platforms exist in image-info.
    /// </summary>
    [Fact]
    public void CreateManifestLists_SkipsImageWithNoBuiltPlatforms()
    {
        using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

        string dockerfile1 = CreateDockerfile("1.0/repo/os1", tempFolderContext);
        string dockerfile2 = CreateDockerfile("1.0/repo/os2", tempFolderContext);

        // Manifest defines two images with shared tags
        Manifest manifest = CreateManifest(
            CreateRepo("repo",
                CreateImage(
                    ["sharedtag"],
                    CreatePlatform(dockerfile1, ["tag1"])),
                CreateImage(
                    ["built-sharedtag"],
                    CreatePlatform(dockerfile2, ["tag2"]))));

        // image-info only has the second image's platform, not the first
        ImageArtifactDetails imageArtifactDetails = CreateImageArtifactDetails(
            CreateRepoData("repo",
                CreateImageData(
                    ["built-sharedtag"],
                    CreatePlatform(dockerfile2, simpleTags: ["tag2"]))));

        Mock<IDockerService> dockerServiceMock = new();
        ManifestListService service = CreateService(dockerServiceMock);
        ManifestInfo manifestInfo = LoadManifest(manifest, tempFolderContext);
        ImageArtifactDetails linkedImageInfo = LoadImageInfo(imageArtifactDetails, manifestInfo, tempFolderContext);

        IReadOnlyList<string> createdTags = service.CreateManifestLists(
            manifestInfo, linkedImageInfo, repoPrefix: null, isDryRun: false);

        // Only the second image's manifest list should be created
        createdTags.Count.ShouldBe(1);
        createdTags.ShouldContain("repo:built-sharedtag");

        dockerServiceMock.Verify(o => o.CreateManifestList(
            "repo:sharedtag", It.IsAny<IEnumerable<string>>(), It.IsAny<bool>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that manifest lists are not created for images where all platforms are unchanged.
    /// </summary>
    [Fact]
    public void CreateManifestLists_SkipsUnchangedImages()
    {
        using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

        string dockerfile = CreateDockerfile("1.0/repo/os", tempFolderContext);

        Manifest manifest = CreateManifest(
            CreateRepo("repo",
                CreateImage(
                    ["sharedtag"],
                    CreatePlatform(dockerfile, ["tag1"]))));

        ImageArtifactDetails imageArtifactDetails = CreateImageArtifactDetails(
            CreateRepoData("repo",
                CreateImageData(
                    ["sharedtag"],
                    CreatePlatform(dockerfile, simpleTags: ["tag1"], isUnchanged: true))));

        Mock<IDockerService> dockerServiceMock = new();
        ManifestListService service = CreateService(dockerServiceMock);
        ManifestInfo manifestInfo = LoadManifest(manifest, tempFolderContext);
        ImageArtifactDetails linkedImageInfo = LoadImageInfo(imageArtifactDetails, manifestInfo, tempFolderContext);

        IReadOnlyList<string> createdTags = service.CreateManifestLists(
            manifestInfo, linkedImageInfo, repoPrefix: null, isDryRun: false);

        createdTags.ShouldBeEmpty();

        dockerServiceMock.Verify(o => o.CreateManifestList(
            It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<bool>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that manifest lists are created when at least one platform has changed,
    /// including both changed and unchanged platforms.
    /// </summary>
    [Fact]
    public void CreateManifestLists_IncludesPartiallyChangedImages()
    {
        using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

        string dockerfileChanged = CreateDockerfile("1.0/repo/changed", tempFolderContext);
        string dockerfileUnchanged = CreateDockerfile("1.0/repo/unchanged", tempFolderContext);

        Manifest manifest = CreateManifest(
            CreateRepo("repo",
                CreateImage(
                    ["sharedtag"],
                    CreatePlatform(dockerfileChanged, ["changed-tag"]),
                    CreatePlatform(dockerfileUnchanged, ["unchanged-tag"], architecture: Architecture.ARM64))));

        ImageArtifactDetails imageArtifactDetails = CreateImageArtifactDetails(
            CreateRepoData("repo",
                CreateImageData(
                    ["sharedtag"],
                    CreatePlatform(dockerfileChanged, simpleTags: ["changed-tag"]),
                    CreatePlatform(dockerfileUnchanged, simpleTags: ["unchanged-tag"],
                        architecture: "arm64", isUnchanged: true))));

        Mock<IDockerService> dockerServiceMock = new();
        ManifestListService service = CreateService(dockerServiceMock);
        ManifestInfo manifestInfo = LoadManifest(manifest, tempFolderContext);
        ImageArtifactDetails linkedImageInfo = LoadImageInfo(imageArtifactDetails, manifestInfo, tempFolderContext);

        IReadOnlyList<string> createdTags = service.CreateManifestLists(
            manifestInfo, linkedImageInfo, repoPrefix: null, isDryRun: false);

        createdTags.Count.ShouldBe(1);

        // Both platforms should be in the manifest list
        dockerServiceMock.Verify(o => o.CreateManifestList(
            "repo:sharedtag",
            It.Is<IEnumerable<string>>(images =>
                images.Count() == 2 &&
                images.Contains("repo:changed-tag") &&
                images.Contains("repo:unchanged-tag")),
            false));
    }

    /// <summary>
    /// Verifies that images without shared tags do not produce manifest lists.
    /// </summary>
    [Fact]
    public void CreateManifestLists_SkipsImagesWithNoSharedTags()
    {
        using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

        string dockerfile = CreateDockerfile("1.0/repo/os", tempFolderContext);

        Manifest manifest = CreateManifest(
            CreateRepo("repo",
                CreateImage(
                    CreatePlatform(dockerfile, ["tag1"]))));

        ImageArtifactDetails imageArtifactDetails = CreateImageArtifactDetails(
            CreateRepoData("repo",
                CreateImageData(
                    CreatePlatform(dockerfile, simpleTags: ["tag1"]))));

        Mock<IDockerService> dockerServiceMock = new();
        ManifestListService service = CreateService(dockerServiceMock);
        ManifestInfo manifestInfo = LoadManifest(manifest, tempFolderContext);
        ImageArtifactDetails linkedImageInfo = LoadImageInfo(imageArtifactDetails, manifestInfo, tempFolderContext);

        IReadOnlyList<string> createdTags = service.CreateManifestLists(
            manifestInfo, linkedImageInfo, repoPrefix: null, isDryRun: false);

        createdTags.ShouldBeEmpty();

        dockerServiceMock.Verify(o => o.CreateManifestList(
            It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<bool>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that when a platform has no tags of its own, it borrows a tag
    /// from a matching platform in another image within the same repo.
    /// </summary>
    [Fact]
    public void CreateManifestLists_DuplicatePlatformCrossReference()
    {
        using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

        string dockerfile = CreateDockerfile("1.0/repo/os", tempFolderContext);

        // Image 1 has tags, Image 2 has same platform but no tags
        Manifest manifest = CreateManifest(
            CreateRepo("repo",
                CreateImage(
                    ["sharedtag1"],
                    CreatePlatform(dockerfile, ["tag1", "tag2"])),
                CreateImage(
                    ["sharedtag2"],
                    CreatePlatform(dockerfile, Array.Empty<string>()))));

        manifest.Registry = "mcr.microsoft.com";

        ImageArtifactDetails imageArtifactDetails = CreateImageArtifactDetails(
            CreateRepoData("repo",
                CreateImageData(
                    ["sharedtag1"],
                    CreatePlatform(dockerfile, simpleTags: ["tag1", "tag2"])),
                CreateImageData(
                    ["sharedtag2"],
                    CreatePlatform(dockerfile))));

        Mock<IDockerService> dockerServiceMock = new();
        ManifestListService service = CreateService(dockerServiceMock);
        ManifestInfo manifestInfo = LoadManifest(manifest, tempFolderContext);
        ImageArtifactDetails linkedImageInfo = LoadImageInfo(imageArtifactDetails, manifestInfo, tempFolderContext);

        IReadOnlyList<string> createdTags = service.CreateManifestLists(
            manifestInfo, linkedImageInfo, repoPrefix: null, isDryRun: false);

        createdTags.Count.ShouldBe(2);

        // sharedtag2's platform has no tags, so it should use tag1 from the matching platform in Image 1
        dockerServiceMock.Verify(o => o.CreateManifestList(
            "mcr.microsoft.com/repo:sharedtag2",
            new[] { "mcr.microsoft.com/repo:tag1" },
            false));
    }

    /// <summary>
    /// Verifies that manifest lists are created for syndicated repos with correct tags.
    /// </summary>
    [Fact]
    public void CreateManifestLists_SyndicatedTags()
    {
        using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

        string dockerfile = CreateDockerfile("1.0/repo/os", tempFolderContext);

        Platform platform;
        Manifest manifest = CreateManifest(
            CreateRepo("repo",
                CreateImage(
                    [platform = CreatePlatform(dockerfile, Array.Empty<string>())],
                    new Dictionary<string, Tag>
                    {
                        {
                            "sharedtag",
                            new Tag
                            {
                                Syndication = new TagSyndication
                                {
                                    Repo = "syndicated-repo",
                                    DestinationTags = ["syn-sharedtag1", "syn-sharedtag2"]
                                }
                            }
                        }
                    })));

        manifest.Registry = "mcr.microsoft.com";
        platform.Tags = new Dictionary<string, Tag>
        {
            {
                "tag1",
                new Tag
                {
                    Syndication = new TagSyndication
                    {
                        Repo = "syndicated-repo",
                        DestinationTags = ["syn-tag1"]
                    }
                }
            }
        };

        ImageArtifactDetails imageArtifactDetails = CreateImageArtifactDetails(
            CreateRepoData("repo",
                CreateImageData(
                    ["sharedtag"],
                    CreatePlatform(dockerfile, simpleTags: ["tag1"]))));

        Mock<IDockerService> dockerServiceMock = new();
        ManifestListService service = CreateService(dockerServiceMock);
        ManifestInfo manifestInfo = LoadManifest(manifest, tempFolderContext);
        ImageArtifactDetails linkedImageInfo = LoadImageInfo(imageArtifactDetails, manifestInfo, tempFolderContext);

        IReadOnlyList<string> createdTags = service.CreateManifestLists(
            manifestInfo, linkedImageInfo, repoPrefix: null, isDryRun: false);

        // Should create manifest for primary repo AND syndicated repo
        createdTags.ShouldContain("mcr.microsoft.com/repo:sharedtag");
        createdTags.ShouldContain("mcr.microsoft.com/syndicated-repo:syn-sharedtag1");
        createdTags.ShouldContain("mcr.microsoft.com/syndicated-repo:syn-sharedtag2");

        dockerServiceMock.Verify(o => o.CreateManifestList(
            "mcr.microsoft.com/repo:sharedtag",
            new[] { "mcr.microsoft.com/repo:tag1" },
            false));

        dockerServiceMock.Verify(o => o.CreateManifestList(
            "mcr.microsoft.com/syndicated-repo:syn-sharedtag1",
            new[] { "mcr.microsoft.com/syndicated-repo:tag1" },
            false));

        dockerServiceMock.Verify(o => o.CreateManifestList(
            "mcr.microsoft.com/syndicated-repo:syn-sharedtag2",
            new[] { "mcr.microsoft.com/syndicated-repo:tag1" },
            false));
    }

    /// <summary>
    /// Verifies that syndicated manifest lists only include platforms that have
    /// matching syndicated tags (not all platforms).
    /// </summary>
    [Fact]
    public void CreateManifestLists_SyndicatedOnlyIncludesMatchingPlatforms()
    {
        using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

        string dockerfile1 = CreateDockerfile("1.0/repo/os1", tempFolderContext);
        string dockerfile2 = CreateDockerfile("1.0/repo/os2", tempFolderContext);

        Platform platform1;
        Platform platform2;
        Manifest manifest = CreateManifest(
            CreateRepo("repo",
                CreateImage(
                    [
                        platform1 = CreatePlatform(dockerfile1, Array.Empty<string>()),
                        platform2 = CreatePlatform(dockerfile2, Array.Empty<string>())
                    ],
                    new Dictionary<string, Tag>
                    {
                        {
                            "sharedtag",
                            new Tag
                            {
                                Syndication = new TagSyndication
                                {
                                    Repo = "syndicated-repo",
                                    DestinationTags = ["syn-sharedtag"]
                                }
                            }
                        }
                    })));

        manifest.Registry = "mcr.microsoft.com";

        // Only platform1 has syndicated tags, platform2 does not
        platform1.Tags = new Dictionary<string, Tag>
        {
            { "tag1", new Tag() },
            {
                "tag1-syndicated",
                new Tag
                {
                    Syndication = new TagSyndication
                    {
                        Repo = "syndicated-repo",
                        DestinationTags = ["syn-tag1"]
                    }
                }
            }
        };
        platform2.Tags = new Dictionary<string, Tag>
        {
            { "tag2", new Tag() }
        };

        ImageArtifactDetails imageArtifactDetails = CreateImageArtifactDetails(
            CreateRepoData("repo",
                CreateImageData(
                    ["sharedtag"],
                    CreatePlatform(dockerfile1, simpleTags: ["tag1", "tag1-syndicated"]),
                    CreatePlatform(dockerfile2, simpleTags: ["tag2"]))));

        Mock<IDockerService> dockerServiceMock = new();
        ManifestListService service = CreateService(dockerServiceMock);
        ManifestInfo manifestInfo = LoadManifest(manifest, tempFolderContext);
        ImageArtifactDetails linkedImageInfo = LoadImageInfo(imageArtifactDetails, manifestInfo, tempFolderContext);

        service.CreateManifestLists(
            manifestInfo, linkedImageInfo, repoPrefix: null, isDryRun: false);

        // Primary manifest list should include both platforms
        dockerServiceMock.Verify(o => o.CreateManifestList(
            "mcr.microsoft.com/repo:sharedtag",
            It.Is<IEnumerable<string>>(images =>
                images.Contains("mcr.microsoft.com/repo:tag1") &&
                images.Contains("mcr.microsoft.com/repo:tag2")),
            false));

        // Syndicated manifest list should only include platform1 (the one with syndicated tags)
        dockerServiceMock.Verify(o => o.CreateManifestList(
            "mcr.microsoft.com/syndicated-repo:syn-sharedtag",
            new[] { "mcr.microsoft.com/syndicated-repo:tag1-syndicated" },
            false));
    }

    /// <summary>
    /// Verifies that repo prefix is correctly applied to image names in manifest lists.
    /// </summary>
    [Fact]
    public void CreateManifestLists_RepoPrefix()
    {
        using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

        string dockerfile = CreateDockerfile("1.0/repo/os", tempFolderContext);

        Manifest manifest = CreateManifest(
            CreateRepo("repo",
                CreateImage(
                    ["sharedtag"],
                    CreatePlatform(dockerfile, ["tag1"]))));

        manifest.Registry = "myregistry.azurecr.io";

        ImageArtifactDetails imageArtifactDetails = CreateImageArtifactDetails(
            CreateRepoData("repo",
                CreateImageData(
                    ["sharedtag"],
                    CreatePlatform(dockerfile, simpleTags: ["tag1"]))));

        Mock<IDockerService> dockerServiceMock = new();
        ManifestListService service = CreateService(dockerServiceMock);
        ManifestInfo manifestInfo = LoadManifest(manifest, tempFolderContext);
        ImageArtifactDetails linkedImageInfo = LoadImageInfo(imageArtifactDetails, manifestInfo, tempFolderContext);

        IReadOnlyList<string> createdTags = service.CreateManifestLists(
            manifestInfo, linkedImageInfo, repoPrefix: "build/", isDryRun: false);

        createdTags.ShouldContain("myregistry.azurecr.io/build/repo:sharedtag");

        dockerServiceMock.Verify(o => o.CreateManifestList(
            "myregistry.azurecr.io/build/repo:sharedtag",
            new[] { "myregistry.azurecr.io/build/repo:tag1" },
            false));
    }

    /// <summary>
    /// Verifies that the registry is included in manifest list tag and platform image names.
    /// </summary>
    [Fact]
    public void CreateManifestLists_RegistryInImageNames()
    {
        using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

        string dockerfile = CreateDockerfile("1.0/repo/os", tempFolderContext);

        Manifest manifest = CreateManifest(
            CreateRepo("repo",
                CreateImage(
                    ["sharedtag"],
                    CreatePlatform(dockerfile, ["tag1"]))));

        manifest.Registry = "mcr.microsoft.com";

        ImageArtifactDetails imageArtifactDetails = CreateImageArtifactDetails(
            CreateRepoData("repo",
                CreateImageData(
                    ["sharedtag"],
                    CreatePlatform(dockerfile, simpleTags: ["tag1"]))));

        Mock<IDockerService> dockerServiceMock = new();
        ManifestListService service = CreateService(dockerServiceMock);
        ManifestInfo manifestInfo = LoadManifest(manifest, tempFolderContext);
        ImageArtifactDetails linkedImageInfo = LoadImageInfo(imageArtifactDetails, manifestInfo, tempFolderContext);

        IReadOnlyList<string> createdTags = service.CreateManifestLists(
            manifestInfo, linkedImageInfo, repoPrefix: null, isDryRun: false);

        createdTags.ShouldContain("mcr.microsoft.com/repo:sharedtag");

        dockerServiceMock.Verify(o => o.CreateManifestList(
            "mcr.microsoft.com/repo:sharedtag",
            new[] { "mcr.microsoft.com/repo:tag1" },
            false));
    }

    private static ManifestListService CreateService(Mock<IDockerService> dockerServiceMock) =>
        new(dockerServiceMock.Object);

    private static ManifestInfo LoadManifest(Manifest manifest, TempFolderContext tempFolderContext)
    {
        string manifestPath = Path.Combine(tempFolderContext.Path, "manifest.json");
        File.WriteAllText(manifestPath, JsonHelper.SerializeObject(manifest));

        IManifestOptionsInfo manifestOptions = ManifestHelper.GetManifestOptions(manifestPath);
        IManifestJsonService manifestJsonService = TestHelper.CreateManifestJsonService();
        return manifestJsonService.Load(manifestOptions);
    }

    /// <summary>
    /// Writes image-info to disk and loads it back with manifest linkage,
    /// so that PlatformData.PlatformInfo references are set correctly for GetMatchingPlatformData.
    /// </summary>
    private static ImageArtifactDetails LoadImageInfo(
        ImageArtifactDetails imageArtifactDetails,
        ManifestInfo manifestInfo,
        TempFolderContext tempFolderContext)
    {
        string imageInfoPath = Path.Combine(tempFolderContext.Path, "image-info.json");
        File.WriteAllText(imageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));
        return ImageInfoHelper.LoadFromFile(imageInfoPath, manifestInfo);
    }
}
