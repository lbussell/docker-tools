// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using CsCheck;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Services;
using Microsoft.DotNet.ImageBuilder.Tests.Generators;
using Shouldly;
using Xunit;
using V2 = Microsoft.DotNet.ImageBuilder.Models.Image.V2;

namespace Microsoft.DotNet.ImageBuilder.Tests.PropertyTests;

/// <summary>
/// Metamorphic tests verifying that the new <see cref="ImageInfoQueryService"/>
/// produces results equivalent to the old <see cref="ImageInfoHelper"/> extension methods.
/// </summary>
public class QueryMigrationPropertyTests
{
    /// <summary>
    /// GetAllDigests on V2 data returns the same digests as the old extension method.
    /// </summary>
    [Fact]
    public void GetAllDigests_MatchesOldBehavior()
    {
        ImageInfoGenerators.ImageArtifactDetails.Sample(oldDetails =>
        {
            List<string> oldDigests = oldDetails.GetAllDigests();
            V2.ImageArtifactDetails v2Details = ConvertToV2(oldDetails);
            List<string> newDigests = ImageInfoQueryService.GetAllDigests(v2Details);

            newDigests.ShouldBe(oldDigests);
        });
    }

    /// <summary>
    /// GetAllImageDigestInfos on V2 data returns equivalent digest infos.
    /// </summary>
    [Fact]
    public void GetAllImageDigestInfos_MatchesOldBehavior()
    {
        ImageInfoGenerators.ImageArtifactDetails.Sample(oldDetails =>
        {
            List<ImageDigestInfo> oldInfos = oldDetails.GetAllImageDigestInfos();
            V2.ImageArtifactDetails v2Details = ConvertToV2(oldDetails);
            List<ImageDigestInfo> newInfos = ImageInfoQueryService.GetAllImageDigestInfos(v2Details);

            newInfos.Count.ShouldBe(oldInfos.Count);
            for (int i = 0; i < oldInfos.Count; i++)
            {
                newInfos[i].Digest.ShouldBe(oldInfos[i].Digest);
                newInfos[i].IsManifestList.ShouldBe(oldInfos[i].IsManifestList);
                newInfos[i].Tags.ShouldBe(oldInfos[i].Tags);
            }
        });
    }

    /// <summary>
    /// GetAllDigests returns empty list for empty ImageArtifactDetails.
    /// </summary>
    [Fact]
    public void GetAllDigests_EmptyDetails_ReturnsEmpty()
    {
        V2.ImageArtifactDetails empty = new();
        ImageInfoQueryService.GetAllDigests(empty).ShouldBeEmpty();
    }

    /// <summary>
    /// GetAllDigests includes both platform and manifest list digests.
    /// </summary>
    [Fact]
    public void GetAllDigests_IncludesPlatformAndManifestDigests()
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
                            Manifest = new V2.ManifestData { Digest = "manifest-digest" },
                            Platforms =
                            [
                                new V2.PlatformData
                                {
                                    Dockerfile = "path",
                                    Digest = "platform-digest",
                                    OsType = "Linux",
                                    OsVersion = "noble",
                                    Architecture = "amd64",
                                    CommitUrl = "https://example.com",
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        List<string> digests = ImageInfoQueryService.GetAllDigests(details);
        digests.ShouldBe(["platform-digest", "manifest-digest"]);
    }

    private static V2.ImageArtifactDetails ConvertToV2(ImageArtifactDetails old) =>
        new()
        {
            Repos = old.Repos.Select(ConvertRepo).ToList(),
        };

    private static V2.RepoData ConvertRepo(RepoData old) =>
        new()
        {
            Repo = old.Repo,
            Images = old.Images.Select(ConvertImage).ToList(),
        };

    private static V2.ImageData ConvertImage(ImageData old) =>
        new()
        {
            ProductVersion = old.ProductVersion,
            Manifest = old.Manifest is not null ? ConvertManifest(old.Manifest) : null,
            Platforms = old.Platforms.Select(ConvertPlatform).ToList(),
        };

    private static V2.ManifestData ConvertManifest(ManifestData old) =>
        new()
        {
            Digest = old.Digest,
            Created = old.Created,
            SharedTags = old.SharedTags?.ToList() ?? [],
            SyndicatedDigests = old.SyndicatedDigests?.ToList() ?? [],
        };

    private static V2.PlatformData ConvertPlatform(PlatformData old) =>
        new()
        {
            Dockerfile = old.Dockerfile,
            SimpleTags = old.SimpleTags?.ToList() ?? [],
            Digest = old.Digest,
            BaseImageDigest = old.BaseImageDigest,
            OsType = old.OsType,
            OsVersion = old.OsVersion,
            Architecture = old.Architecture,
            Created = old.Created,
            CommitUrl = old.CommitUrl,
            Layers = old.Layers?.Select(layer => new V2.Layer(layer.Digest, layer.Size)).ToList() ?? [],
            IsUnchanged = old.IsUnchanged,
        };
}
