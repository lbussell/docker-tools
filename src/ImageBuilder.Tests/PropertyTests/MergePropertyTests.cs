// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using CsCheck;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Tests.Generators;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Moq;
using Shouldly;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests.PropertyTests;

/// <summary>
/// Property-based tests that lock down current merge behavior.
/// These serve as a safety net before refactoring the merge logic.
/// </summary>
public class MergePropertyTests
{
    /// <summary>
    /// Merging a source into an empty target, then merging the result into
    /// another empty target produces identical output — i.e., merge-into-empty
    /// is idempotent (the sort/normalization applied by merge stabilizes).
    /// </summary>
    [Fact]
    public void Merge_IntoEmptyTarget_IsIdempotent()
    {
        ImageInfoGenerators.ImageArtifactDetails.Sample(source =>
        {
            ImageArtifactDetails target1 = new();
            ImageInfoHelper.MergeImageArtifactDetails(source, target1);
            string firstMergeJson = JsonHelper.SerializeObject(target1);

            ImageArtifactDetails target1Clone = CloneViaJson(target1);
            ImageArtifactDetails target2 = new();
            ImageInfoHelper.MergeImageArtifactDetails(target1Clone, target2);
            string secondMergeJson = JsonHelper.SerializeObject(target2);

            secondMergeJson.ShouldBe(firstMergeJson);
        });
    }

    /// <summary>
    /// Merging an empty source into any target leaves the target unchanged.
    /// </summary>
    [Fact]
    public void Merge_EmptySource_LeavesTargetUnchanged()
    {
        ImageInfoGenerators.ImageArtifactDetails.Sample(original =>
        {
            ImageArtifactDetails target = CloneViaJson(original);
            string beforeJson = JsonHelper.SerializeObject(target);

            ImageInfoHelper.MergeImageArtifactDetails(new ImageArtifactDetails(), target);

            string afterJson = JsonHelper.SerializeObject(target);
            afterJson.ShouldBe(beforeJson);
        });
    }

    /// <summary>
    /// Merging non-overlapping repos (different names) is commutative —
    /// order of merge doesn't affect the final result.
    /// </summary>
    [Fact]
    public void Merge_NonOverlappingRepos_IsCommutative()
    {
        Gen.Select(
            ImageInfoGenerators.ImageArtifactDetails,
            ImageInfoGenerators.ImageArtifactDetails)
        .Sample((detailsA, detailsB) =>
        {
            // Ensure repos don't overlap by prefixing names
            foreach (RepoData repo in detailsA.Repos)
            {
                repo.Repo = $"a/{repo.Repo}";
            }
            foreach (RepoData repo in detailsB.Repos)
            {
                repo.Repo = $"b/{repo.Repo}";
            }

            // Clone before merge since merge mutates
            ImageArtifactDetails detailsAClone = CloneViaJson(detailsA);
            ImageArtifactDetails detailsBClone = CloneViaJson(detailsB);

            // Merge A then B
            ImageArtifactDetails targetAB = new();
            ImageInfoHelper.MergeImageArtifactDetails(detailsA, targetAB);
            ImageInfoHelper.MergeImageArtifactDetails(detailsB, targetAB);

            // Merge B then A
            ImageArtifactDetails targetBA = new();
            ImageInfoHelper.MergeImageArtifactDetails(detailsBClone, targetBA);
            ImageInfoHelper.MergeImageArtifactDetails(detailsAClone, targetBA);

            string jsonAB = JsonHelper.SerializeObject(targetAB);
            string jsonBA = JsonHelper.SerializeObject(targetBA);
            jsonAB.ShouldBe(jsonBA);
        });
    }

    /// <summary>
    /// Repos in merged output are always sorted alphabetically by name.
    /// </summary>
    [Fact]
    public void Merge_IntoEmptyTarget_ReposAreSorted()
    {
        ImageInfoGenerators.ImageArtifactDetails.Sample(source =>
        {
            ImageArtifactDetails target = new();
            ImageInfoHelper.MergeImageArtifactDetails(source, target);

            List<string> repoNames = target.Repos.Select(repo => repo.Repo).ToList();
            List<string> sortedNames = [.. repoNames.OrderBy(name => name)];
            repoNames.ShouldBe(sortedNames);
        });
    }

    /// <summary>
    /// Layers are always replaced, never merged — the source layers fully
    /// replace the target layers for matching platforms.
    /// </summary>
    [Fact]
    public void Merge_Layers_AreAlwaysReplaced()
    {
        ImageInfo manifestImage = CreateImageInfo();

        Layer targetLayer = new("sha256:target", 100);
        Layer sourceLayer = new("sha256:source", 200);

        ImageArtifactDetails target = CreateSinglePlatformDetails(
            "repo", "1.0", "path/Dockerfile", "amd64", "Linux", "noble",
            manifestImage, [targetLayer]);

        ImageArtifactDetails source = CreateSinglePlatformDetails(
            "repo", "1.0", "path/Dockerfile", "amd64", "Linux", "noble",
            manifestImage, [sourceLayer]);

        ImageInfoHelper.MergeImageArtifactDetails(source, target);

        PlatformData resultPlatform = target.Repos[0].Images[0].Platforms[0];
        resultPlatform.Layers.ShouldHaveSingleItem();
        resultPlatform.Layers[0].ShouldBe(sourceLayer);
    }

    /// <summary>
    /// In build mode (IsPublish=false), tags from source are unioned with
    /// tags from target.
    /// </summary>
    [Fact]
    public void Merge_BuildMode_TagsAreUnioned()
    {
        ImageInfo manifestImage = CreateImageInfo();

        ImageArtifactDetails target = CreateSinglePlatformDetails(
            "repo", "1.0", "path/Dockerfile", "amd64", "Linux", "noble",
            manifestImage, [], simpleTags: ["tag-a", "tag-b"]);

        ImageArtifactDetails source = CreateSinglePlatformDetails(
            "repo", "1.0", "path/Dockerfile", "amd64", "Linux", "noble",
            manifestImage, [], simpleTags: ["tag-b", "tag-c"]);

        ImageInfoHelper.MergeImageArtifactDetails(source, target);

        PlatformData resultPlatform = target.Repos[0].Images[0].Platforms[0];
        resultPlatform.SimpleTags.ShouldBe(["tag-a", "tag-b", "tag-c"], ignoreOrder: false);
    }

    /// <summary>
    /// In publish mode (IsPublish=true), tags from source replace tags in target.
    /// </summary>
    [Fact]
    public void Merge_PublishMode_TagsAreReplaced()
    {
        ImageInfo manifestImage = CreateImageInfo();
        ImageInfoMergeOptions options = new() { IsPublish = true };

        ImageArtifactDetails target = CreateSinglePlatformDetails(
            "repo", "1.0", "path/Dockerfile", "amd64", "Linux", "noble",
            manifestImage, [], simpleTags: ["tag-a", "tag-b"]);

        ImageArtifactDetails source = CreateSinglePlatformDetails(
            "repo", "1.0", "path/Dockerfile", "amd64", "Linux", "noble",
            manifestImage, [], simpleTags: ["tag-c"]);

        ImageInfoHelper.MergeImageArtifactDetails(source, target, options);

        PlatformData resultPlatform = target.Repos[0].Images[0].Platforms[0];
        resultPlatform.SimpleTags.ShouldBe(["tag-c"]);
    }

    /// <summary>
    /// Platform digest from source overwrites target digest after merge.
    /// </summary>
    [Fact]
    public void Merge_Digest_IsOverwritten()
    {
        ImageInfo manifestImage = CreateImageInfo();

        ImageArtifactDetails target = CreateSinglePlatformDetails(
            "repo", "1.0", "path/Dockerfile", "amd64", "Linux", "noble",
            manifestImage, [], digest: "dotnet/runtime@sha256:olddigest");

        ImageArtifactDetails source = CreateSinglePlatformDetails(
            "repo", "1.0", "path/Dockerfile", "amd64", "Linux", "noble",
            manifestImage, [], digest: "dotnet/runtime@sha256:newdigest");

        ImageInfoHelper.MergeImageArtifactDetails(source, target);

        PlatformData resultPlatform = target.Repos[0].Images[0].Platforms[0];
        resultPlatform.Digest.ShouldBe("dotnet/runtime@sha256:newdigest");
    }

    private static ImageArtifactDetails CreateSinglePlatformDetails(
        string repo,
        string productVersion,
        string dockerfile,
        string architecture,
        string osType,
        string osVersion,
        ImageInfo manifestImage,
        List<Layer> layers,
        List<string>? simpleTags = null,
        string digest = "dotnet/runtime@sha256:abc123")
    {
        return new ImageArtifactDetails
        {
            Repos =
            [
                new RepoData
                {
                    Repo = repo,
                    Images =
                    [
                        new ImageData
                        {
                            ManifestImage = manifestImage,
                            ProductVersion = productVersion,
                            Platforms =
                            [
                                new PlatformData
                                {
                                    Dockerfile = dockerfile,
                                    Architecture = architecture,
                                    OsType = osType,
                                    OsVersion = osVersion,
                                    Digest = digest,
                                    Layers = layers,
                                    SimpleTags = simpleTags ?? [],
                                    ImageInfo = manifestImage,
                                    CommitUrl = "https://github.com/dotnet/dotnet-docker/commit/abc123",
                                }
                            ]
                        }
                    ]
                }
            ]
        };
    }

    private static ImageInfo CreateImageInfo() =>
        ImageInfo.Create(
            new Image
            {
                Platforms = Array.Empty<Platform>(),
                ProductVersion = "1.0"
            },
            "fullrepo",
            "repo",
            new ManifestFilter(Enumerable.Empty<string>()),
            new VariableHelper(new Manifest(), Mock.Of<IManifestOptionsInfo>(), null!),
            "base");

    private static ImageArtifactDetails CloneViaJson(ImageArtifactDetails original)
    {
        string json = JsonHelper.SerializeObject(original);
        return ImageArtifactDetails.FromJson(json);
    }
}
