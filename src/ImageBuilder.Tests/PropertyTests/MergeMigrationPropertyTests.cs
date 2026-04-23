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
/// Metamorphic tests verifying that the new <see cref="ImageInfoMerger"/>
/// produces results equivalent to the old <see cref="ImageInfoHelper.MergeImageArtifactDetails"/>.
/// </summary>
public class MergeMigrationPropertyTests
{
    /// <summary>
    /// Merging non-overlapping repos with the new merger produces the same JSON
    /// as the old merger (after converting between old and V2 types).
    /// </summary>
    [Fact]
    public void Merge_NonOverlappingRepos_MatchesOldBehavior()
    {
        Gen.Select(
            ImageInfoGenerators.ImageArtifactDetails,
            ImageInfoGenerators.ImageArtifactDetails)
        .Sample((detailsA, detailsB) =>
        {
            // Ensure repos don't overlap
            foreach (RepoData repo in detailsA.Repos)
            {
                repo.Repo = $"a/{repo.Repo}";
            }
            foreach (RepoData repo in detailsB.Repos)
            {
                repo.Repo = $"b/{repo.Repo}";
            }

            // Old merge (mutating)
            ImageArtifactDetails oldTarget = new();
            ImageInfoHelper.MergeImageArtifactDetails(detailsA, oldTarget);
            ImageInfoHelper.MergeImageArtifactDetails(detailsB, oldTarget);
            string oldJson = JsonHelper.SerializeObject(oldTarget);

            // New merge (immutable)
            V2.ImageArtifactDetails v2A = ConvertToV2(detailsA);
            V2.ImageArtifactDetails v2B = ConvertToV2(detailsB);
            V2.ImageArtifactDetails v2Result = ImageInfoMerger.Merge(v2A, new V2.ImageArtifactDetails());
            v2Result = ImageInfoMerger.Merge(v2B, v2Result);
            string newJson = ImageInfoSerializer.Serialize(v2Result);

            newJson.ShouldBe(oldJson);
        });
    }

    /// <summary>
    /// Merging into an empty target with the new merger produces the same JSON
    /// as the old merger.
    /// </summary>
    [Fact]
    public void Merge_IntoEmptyTarget_MatchesOldBehavior()
    {
        ImageInfoGenerators.ImageArtifactDetails.Sample(source =>
        {
            // Old merge
            ImageArtifactDetails oldTarget = new();
            ImageInfoHelper.MergeImageArtifactDetails(source, oldTarget);
            string oldJson = JsonHelper.SerializeObject(oldTarget);

            // New merge
            V2.ImageArtifactDetails v2Source = ConvertToV2(source);
            V2.ImageArtifactDetails v2Result = ImageInfoMerger.Merge(v2Source, new V2.ImageArtifactDetails());
            string newJson = ImageInfoSerializer.Serialize(v2Result);

            newJson.ShouldBe(oldJson);
        });
    }

    /// <summary>
    /// The new merger is idempotent: merging the result into another empty target
    /// produces the same result.
    /// </summary>
    [Fact]
    public void Merge_IsIdempotent()
    {
        ImageInfoGenerators.ImageArtifactDetails.Sample(source =>
        {
            V2.ImageArtifactDetails v2Source = ConvertToV2(source);
            V2.ImageArtifactDetails first = ImageInfoMerger.Merge(v2Source, new V2.ImageArtifactDetails());
            V2.ImageArtifactDetails second = ImageInfoMerger.Merge(first, new V2.ImageArtifactDetails());

            string firstJson = ImageInfoSerializer.Serialize(first);
            string secondJson = ImageInfoSerializer.Serialize(second);
            secondJson.ShouldBe(firstJson);
        });
    }

    /// <summary>
    /// The new merger does not mutate the source or target inputs.
    /// </summary>
    [Fact]
    public void Merge_DoesNotMutateInputs()
    {
        ImageInfoGenerators.ImageArtifactDetails.Sample(source =>
        {
            V2.ImageArtifactDetails v2Source = ConvertToV2(source);
            V2.ImageArtifactDetails v2Target = new V2.ImageArtifactDetails
            {
                Repos =
                [
                    new V2.RepoData
                    {
                        Repo = "existing-repo",
                        Images = [],
                    }
                ]
            };

            string sourceJsonBefore = ImageInfoSerializer.Serialize(v2Source);
            string targetJsonBefore = ImageInfoSerializer.Serialize(v2Target);

            ImageInfoMerger.Merge(v2Source, v2Target);

            string sourceJsonAfter = ImageInfoSerializer.Serialize(v2Source);
            string targetJsonAfter = ImageInfoSerializer.Serialize(v2Target);

            sourceJsonAfter.ShouldBe(sourceJsonBefore);
            targetJsonAfter.ShouldBe(targetJsonBefore);
        });
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
