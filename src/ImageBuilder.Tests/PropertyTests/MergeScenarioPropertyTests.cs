// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CsCheck;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Services;
using Microsoft.DotNet.ImageBuilder.Tests.Generators;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Shouldly;
using Xunit;
using V2 = Microsoft.DotNet.ImageBuilder.Models.Image.V2;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.DockerfileHelper;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ManifestHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests.PropertyTests;

/// <summary>
/// Property-based tests that exercise production-shaped merge scenarios.
/// Tests either differentially compare old and new merge implementations
/// or verify V2 merger structural properties directly.
/// </summary>
public class MergeScenarioPropertyTests : IDisposable
{
    private readonly TempFolderContext _tempFolder = new();

    public void Dispose() => _tempFolder.Dispose();

    #region Golden regression fixture

    /// <summary>
    /// Deterministic golden test based on the real pipeline shape from IMAGE_INFO_MERGE_REPRO_SUMMARY.md.
    /// Two repos × two product versions × three architectures, merged from six per-leg source files.
    /// Verifies the V2 merger produces the correct structural output.
    /// </summary>
    [Fact]
    public void Merge_GoldenRepro_MultiLegAggregation_ProducesCorrectStructure()
    {
        string[] repos = ["dotnet/nightly/runtime-deps", "dotnet/nightly/runtime"];
        string[] versions = ["8.0.26", "9.0.15"];
        string[] architectures = ["amd64", "arm32", "arm64"];

        List<string> sourceJsons = BuildGoldenSourceJsons(repos, versions, architectures);

        V2.ImageArtifactDetails result = MergeV2Sources(sourceJsons);

        result.Repos.Count.ShouldBe(2, "Expected 2 repos");

        foreach (V2.RepoData repo in result.Repos)
        {
            repo.Images.Count.ShouldBe(2, $"Repo {repo.Repo} should have 2 images (one per version)");

            foreach (V2.ImageData image in repo.Images)
            {
                image.Platforms.Count.ShouldBe(3,
                    $"Image {image.ProductVersion} in {repo.Repo} should have 3 platforms");

                HashSet<string> archSet = image.Platforms.Select(p => p.Architecture).ToHashSet();
                archSet.ShouldBe(architectures.ToHashSet(),
                    $"Image {image.ProductVersion} should have all architectures");
            }
        }

        int totalPlatforms = result.Repos.Sum(r => r.Images.Sum(i => i.Platforms.Count));
        totalPlatforms.ShouldBe(12, "Expected 12 total platforms (2 repos × 2 versions × 3 archs)");

        // Verify round-trip through serialization
        string mergedJson = ImageInfoSerializer.Serialize(result);
        V2.ImageArtifactDetails roundTripped = ImageInfoSerializer.Deserialize(mergedJson);
        ImageInfoSerializer.Serialize(roundTripped).ShouldBe(mergedJson);
    }

    /// <summary>
    /// Differential golden test: the old manifest-linked merger and the new V2 merger
    /// produce the same output for a production-shaped multi-leg scenario.
    /// </summary>
    [Fact]
    public void Merge_GoldenRepro_MatchesOldBehavior()
    {
        string[] repos = ["dotnet/nightly/runtime-deps", "dotnet/nightly/runtime"];
        string[] versions = ["8.0.26", "9.0.15"];
        string[] architectures = ["amd64", "arm32", "arm64"];

        // Create Dockerfiles and manifest — each repo gets its own image objects
        List<Repo> manifestRepos = [];
        foreach (string repo in repos)
        {
            List<Image> repoImages = [];
            foreach (string version in versions)
            {
                List<Platform> imagePlatforms = [];
                foreach (string arch in architectures)
                {
                    string dockerfilePath = CreateDockerfile(
                        $"src/{version}/bookworm-slim/{arch}", _tempFolder);
                    (Architecture archEnum, string? variant) = ParseArchitecture(arch);
                    imagePlatforms.Add(CreatePlatform(
                        dockerfilePath,
                        [$"{version}-bookworm-slim-{arch}"],
                        osVersion: "bookworm-slim",
                        architecture: archEnum,
                        variant: variant));
                }
                repoImages.Add(CreateImage(imagePlatforms, productVersion: version));
            }
            manifestRepos.Add(CreateRepo(repo, repoImages));
        }

        Manifest manifest = CreateManifest(manifestRepos.ToArray());
        string manifestPath = Path.Combine(_tempFolder.Path, "manifest.json");
        File.WriteAllText(manifestPath, JsonHelper.SerializeObject(manifest));
        ManifestInfo manifestInfo = TestHelper.CreateManifestJsonService()
            .Load(GetManifestOptions(manifestPath));

        List<string> sourceJsons = BuildGoldenSourceJsons(repos, versions, architectures);

        // Old merge
        ImageArtifactDetails oldTarget = new();
        foreach (string sourceJson in sourceJsons)
        {
            ImageArtifactDetails oldSource = ImageInfoHelper.LoadFromContent(
                sourceJson, manifestInfo, skipManifestValidation: true);
            ImageInfoHelper.MergeImageArtifactDetails(oldSource, oldTarget);
        }
        string oldJson = JsonHelper.SerializeObject(oldTarget);

        // New merge
        V2.ImageArtifactDetails newResult = MergeV2Sources(sourceJsons);
        string newJson = ImageInfoSerializer.Serialize(newResult);

        newJson.ShouldBe(oldJson);
    }

    #endregion

    #region Differential property tests (same-platform scenarios)

    /// <summary>
    /// Same-platform replacement: matching platforms should have scalar fields replaced.
    /// </summary>
    [Fact]
    public void Merge_SamePlatformReplacement_MatchesOldBehavior()
    {
        ImageInfoMergeScenarioGenerators.SamePlatformReplacement.Sample(scenario =>
        {
            AssertOldNewMatch(scenario);
        }, iter: 200);
    }

    /// <summary>
    /// Build mode tag union: overlapping tags should be unioned and sorted.
    /// </summary>
    [Fact]
    public void Merge_BuildModeTagUnion_MatchesOldBehavior()
    {
        ImageInfoMergeScenarioGenerators.BuildModeTagUnion.Sample(scenario =>
        {
            AssertOldNewMatch(scenario);
        }, iter: 200);
    }

    /// <summary>
    /// Publish mode tag replacement: tags should be replaced, not unioned.
    /// </summary>
    [Fact]
    public void Merge_PublishModeTagReplacement_MatchesOldBehavior()
    {
        ImageInfoMergeScenarioGenerators.PublishModeTagReplacement.Sample(scenario =>
        {
            AssertOldNewMatch(scenario);
        }, iter: 200);
    }

    /// <summary>
    /// Manifest null/non-null replacement: null source manifest should clear target manifest.
    /// </summary>
    [Fact]
    public void Merge_ManifestNullReplacement_MatchesOldBehavior()
    {
        ImageInfoMergeScenarioGenerators.ManifestNullReplacement.Sample(scenario =>
        {
            AssertOldNewMatch(scenario);
        }, iter: 200);
    }

    #endregion

    #region V2 merger structural property tests

    /// <summary>
    /// Multi-leg platform aggregation: fragments for the same repo/image from different
    /// build legs should merge into one image with multiple platforms.
    /// </summary>
    [Fact]
    public void Merge_MultiLegPlatformAggregation_CombinesPlatforms()
    {
        ImageInfoMergeScenarioGenerators.MultiLegPlatformAggregation.Sample(scenario =>
        {
            V2.ImageArtifactDetails result = MergeV2(scenario);

            V2.RepoData repo = result.Repos.ShouldHaveSingleItem();
            V2.ImageData image = repo.Images.ShouldHaveSingleItem();
            image.Platforms.Count.ShouldBe(scenario.SourceJsons.Count,
                $"Scenario: {scenario.Description}");
        }, iter: 200);
    }

    /// <summary>
    /// Tag-state near miss: platforms with different tag presence should not match.
    /// </summary>
    [Fact]
    public void Merge_TagStateNearMiss_PreservesBothPlatforms()
    {
        ImageInfoMergeScenarioGenerators.TagStateNearMiss.Sample(scenario =>
        {
            V2.ImageArtifactDetails result = MergeV2(scenario);

            V2.RepoData repo = result.Repos.ShouldHaveSingleItem();
            int totalPlatforms = repo.Images.Sum(img => img.Platforms.Count);
            totalPlatforms.ShouldBe(2,
                $"Tag-state mismatch should preserve both platforms. Scenario: {scenario.Description}");
        }, iter: 200);
    }

    /// <summary>
    /// Product version equivalence: same major.minor versions should merge into one image.
    /// </summary>
    [Fact]
    public void Merge_ProductVersionEquivalence_MergesSameMajorMinor()
    {
        ImageInfoMergeScenarioGenerators.ProductVersionEquivalence.Sample(scenario =>
        {
            V2.ImageArtifactDetails result = MergeV2(scenario);

            V2.RepoData repo = result.Repos.ShouldHaveSingleItem();
            repo.Images.Count.ShouldBe(1,
                $"Same major.minor versions should merge. Scenario: {scenario.Description}");
        }, iter: 200);
    }

    #endregion

    #region Helpers

    private static List<string> BuildGoldenSourceJsons(
        string[] repos, string[] versions, string[] architectures)
    {
        List<string> sourceJsons = [];
        foreach (string version in versions)
        {
            foreach (string arch in architectures)
            {
                string dockerfile = $"src/{version}/bookworm-slim/{arch}/Dockerfile";
                List<V2.RepoData> legRepos = repos.Select(repo => new V2.RepoData
                {
                    Repo = repo,
                    Images =
                    [
                        new V2.ImageData
                        {
                            ProductVersion = version,
                            Manifest = new V2.ManifestData
                            {
                                SharedTags = [$"{version}-bookworm-slim"],
                            },
                            Platforms =
                            [
                                new V2.PlatformData
                                {
                                    Dockerfile = dockerfile,
                                    Architecture = arch,
                                    OsType = "Linux",
                                    OsVersion = "bookworm-slim",
                                    Digest = $"{repo}@sha256:{arch.PadRight(64, '0')[..64]}",
                                    SimpleTags = [$"{version}-bookworm-slim-{arch}"],
                                    CommitUrl = "https://github.com/dotnet/dotnet-docker/commit/abc123",
                                }
                            ],
                        }
                    ],
                }).ToList();

                V2.ImageArtifactDetails legDetails = new() { Repos = legRepos };
                sourceJsons.Add(ImageInfoSerializer.Serialize(legDetails));
            }
        }
        return sourceJsons;
    }

    private static V2.ImageArtifactDetails MergeV2Sources(List<string> sourceJsons)
    {
        V2.ImageArtifactDetails target = new();
        foreach (string sourceJson in sourceJsons)
        {
            V2.ImageArtifactDetails source = ImageInfoSerializer.Deserialize(sourceJson);
            target = ImageInfoMerger.Merge(source, target);
        }
        return target;
    }

    private void AssertOldNewMatch(ImageInfoMergeScenario scenario)
    {
        string oldJson = MergeOldJson(scenario);
        string newJson = MergeNewJson(scenario);

        newJson.ShouldBe(oldJson, $"Scenario: {scenario.Description}");
    }

    private string MergeOldJson(ImageInfoMergeScenario scenario)
    {
        ManifestInfo manifestInfo = BuildManifestForScenario(scenario);

        ImageArtifactDetails target = scenario.InitialTargetJson is not null
            ? ImageInfoHelper.LoadFromContent(
                scenario.InitialTargetJson, manifestInfo, skipManifestValidation: true)
            : new ImageArtifactDetails();

        foreach (string sourceJson in scenario.SourceJsons)
        {
            ImageArtifactDetails source = ImageInfoHelper.LoadFromContent(
                sourceJson, manifestInfo, skipManifestValidation: true);
            ImageInfoHelper.MergeImageArtifactDetails(source, target, scenario.Options);
        }

        return JsonHelper.SerializeObject(target);
    }

    private static string MergeNewJson(ImageInfoMergeScenario scenario)
    {
        V2.ImageArtifactDetails result = MergeV2(scenario);
        return ImageInfoSerializer.Serialize(result);
    }

    private static V2.ImageArtifactDetails MergeV2(ImageInfoMergeScenario scenario)
    {
        V2.ImageArtifactDetails target = scenario.InitialTargetJson is not null
            ? ImageInfoSerializer.Deserialize(scenario.InitialTargetJson)
            : new V2.ImageArtifactDetails();

        foreach (string sourceJson in scenario.SourceJsons)
        {
            V2.ImageArtifactDetails source = ImageInfoSerializer.Deserialize(sourceJson);
            target = ImageInfoMerger.Merge(source, target, scenario.Options);
        }

        return target;
    }

    private ManifestInfo BuildManifestForScenario(ImageInfoMergeScenario scenario)
    {
        List<string> allJsons = [.. scenario.SourceJsons];
        if (scenario.InitialTargetJson is not null)
        {
            allJsons.Add(scenario.InitialTargetJson);
        }

        List<V2.ImageArtifactDetails> allDetails = allJsons
            .Select(ImageInfoSerializer.Deserialize)
            .ToList();

        Dictionary<string, Dictionary<string, List<V2.PlatformData>>> repoVersionPlatforms = [];

        foreach (V2.ImageArtifactDetails details in allDetails)
        {
            foreach (V2.RepoData repo in details.Repos)
            {
                if (!repoVersionPlatforms.TryGetValue(repo.Repo, out Dictionary<string, List<V2.PlatformData>>? versionMap))
                {
                    versionMap = [];
                    repoVersionPlatforms[repo.Repo] = versionMap;
                }

                foreach (V2.ImageData image in repo.Images)
                {
                    string versionKey = image.ProductVersion ?? "";
                    if (!versionMap.TryGetValue(versionKey, out List<V2.PlatformData>? platforms))
                    {
                        platforms = [];
                        versionMap[versionKey] = platforms;
                    }

                    foreach (V2.PlatformData platform in image.Platforms)
                    {
                        string platformKey = ImageInfoIdentity.GetPlatformKey(platform);
                        if (!platforms.Any(p => ImageInfoIdentity.GetPlatformKey(p) == platformKey))
                        {
                            platforms.Add(platform);
                        }
                    }
                }
            }
        }

        List<Repo> manifestRepos = [];
        foreach ((string repoName, Dictionary<string, List<V2.PlatformData>> versionMap) in repoVersionPlatforms)
        {
            List<Image> images = [];
            foreach ((string version, List<V2.PlatformData> platforms) in versionMap)
            {
                List<Platform> manifestPlatforms = [];
                foreach (V2.PlatformData platform in platforms)
                {
                    string dockerfilePath = CreateDockerfile(
                        Path.GetDirectoryName(platform.Dockerfile)!, _tempFolder);
                    // Deduplicate tags for the manifest (duplicate tags cause ArgumentException)
                    string[] tags = platform.SimpleTags.Distinct().ToArray();

                    (Architecture archEnum, string? variant) = ParseArchitecture(platform.Architecture);
                    manifestPlatforms.Add(CreatePlatform(
                        dockerfilePath,
                        tags,
                        os: platform.OsType == "Windows" ? OS.Windows : OS.Linux,
                        osVersion: platform.OsVersion,
                        architecture: archEnum,
                        variant: variant));
                }

                images.Add(CreateImage(
                    manifestPlatforms,
                    productVersion: string.IsNullOrEmpty(version) ? null : version));
            }

            manifestRepos.Add(CreateRepo(repoName, images));
        }

        Manifest manifest = CreateManifest(manifestRepos.ToArray());
        string manifestPath = Path.Combine(_tempFolder.Path, $"manifest-{Guid.NewGuid():N}.json");
        File.WriteAllText(manifestPath, JsonHelper.SerializeObject(manifest));

        return TestHelper.CreateManifestJsonService()
            .Load(GetManifestOptions(manifestPath));
    }

    private static (Architecture Arch, string? Variant) ParseArchitecture(string arch) =>
        arch switch
        {
            "amd64" => (Architecture.AMD64, null),
            "arm64v8" => (Architecture.ARM64, "v8"),
            "arm64" => (Architecture.ARM64, null),
            "arm32v7" => (Architecture.ARM, "v7"),
            "arm" or "arm32" => (Architecture.ARM, null),
            _ => (Architecture.AMD64, null),
        };

    #endregion
}
