// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using CsCheck;
using Microsoft.DotNet.ImageBuilder.Services;
using Microsoft.DotNet.ImageBuilder.Tests.Generators;
using Shouldly;
using Xunit;
using V2 = Microsoft.DotNet.ImageBuilder.Models.Image.V2;

namespace Microsoft.DotNet.ImageBuilder.Tests.PropertyTests;

/// <summary>
/// Property-based tests that lock down V2 image-info merge behavior.
/// </summary>
public class ImageInfoMergerPropertyTests
{
    /// <summary>
    /// Empty-platform images have no platform identity, so major.minor product-version
    /// equivalence is not enough to merge them.
    /// </summary>
    [Fact]
    public void Merge_EmptyPlatformImages_MatchesOnlyExactProductVersion()
    {
        ImageInfoGenerators.SameMajorMinorProductVersionPair
            .Where(pair => pair.Version1 != pair.Version2)
            .Sample(pair =>
        {
            string targetJson = CreateDetailsJson(pair.Version1, sharedTags: ["target-tag"]);
            string sourceJson = CreateDetailsJson(pair.Version2, sharedTags: ["source-tag"]);

            V2.ImageArtifactDetails result = ImageInfoSerializer.Deserialize(
                MergeWithNewSerializer(sourceJson, targetJson));

            V2.RepoData repo = result.Repos.ShouldHaveSingleItem();
            repo.Images.Count.ShouldBe(2);
            repo.Images.Select(image => image.ProductVersion).ShouldBe([pair.Version1, pair.Version2], ignoreOrder: true);
        });
    }

    /// <summary>
    /// Empty-platform images with the same product version are the same logical image.
    /// </summary>
    [Fact]
    public void Merge_EmptyPlatformImages_WithExactProductVersion_Merges()
    {
        string targetJson = CreateDetailsJson("8.0", sharedTags: ["target-tag"]);
        string sourceJson = CreateDetailsJson("8.0", sharedTags: ["source-tag"]);

        V2.ImageArtifactDetails result = ImageInfoSerializer.Deserialize(
            MergeWithNewSerializer(sourceJson, targetJson));

        V2.RepoData repo = result.Repos.ShouldHaveSingleItem();
        V2.ImageData image = repo.Images.ShouldHaveSingleItem();
        image.ProductVersion.ShouldBe("8.0");
        image.Platforms.ShouldBeEmpty();
        image.Manifest.ShouldNotBeNull();
        image.Manifest.SharedTags.ShouldBe(["source-tag", "target-tag"], ignoreOrder: false);
    }

    /// <summary>
    /// An empty-platform image does not match a populated image because it has no
    /// representative platform identity.
    /// </summary>
    [Fact]
    public void Merge_EmptyPlatformImage_DoesNotMatchPopulatedImage()
    {
        ImageInfoGenerators.PlatformData.Sample(platform =>
        {
            string targetJson = CreateDetailsJson("8.0", [platform], sharedTags: ["target-tag"]);
            string sourceJson = CreateDetailsJson("8.0", sharedTags: ["source-tag"]);

            V2.ImageArtifactDetails result = ImageInfoSerializer.Deserialize(
                MergeWithNewSerializer(sourceJson, targetJson));

            V2.RepoData repo = result.Repos.ShouldHaveSingleItem();
            repo.Images.Count.ShouldBe(2);
            repo.Images.Count(image => image.Platforms.Count == 0).ShouldBe(1);
            repo.Images.Count(image => image.Platforms.Count == 1).ShouldBe(1);
        });
    }

    private static string MergeWithNewSerializer(string sourceJson, string targetJson)
    {
        V2.ImageArtifactDetails source = ImageInfoSerializer.Deserialize(sourceJson);
        V2.ImageArtifactDetails target = ImageInfoSerializer.Deserialize(targetJson);
        V2.ImageArtifactDetails result = ImageInfoMerger.Merge(source, target);
        return ImageInfoSerializer.Serialize(result);
    }

    private static string CreateDetailsJson(
        string? productVersion,
        IReadOnlyList<V2.PlatformData>? platforms = null,
        IReadOnlyList<string>? sharedTags = null)
    {
        V2.ImageArtifactDetails details = new()
        {
            Repos =
            [
                new V2.RepoData
                {
                    Repo = "repo",
                    Images =
                    [
                        new V2.ImageData
                        {
                            ProductVersion = productVersion,
                            Manifest = new V2.ManifestData
                            {
                                SharedTags = sharedTags?.ToList() ?? [],
                            },
                            Platforms = platforms?.ToList() ?? [],
                        }
                    ],
                }
            ],
        };

        return ImageInfoSerializer.Serialize(details);
    }
}
