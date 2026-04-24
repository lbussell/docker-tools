// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using V2 = Microsoft.DotNet.ImageBuilder.Models.Image.V2;

namespace Microsoft.DotNet.ImageBuilder.Services;

/// <summary>
/// Explicit, type-safe merge logic for V2 image-info records.
/// Replaces the old reflection-based <c>ImageInfoHelper.MergeImageArtifactDetails</c>.
/// Returns new immutable instances — does not mutate inputs.
/// </summary>
public static class ImageInfoMerger
{
    /// <summary>
    /// Merges source image-info data into target, returning the merged result.
    /// </summary>
    /// <param name="source">Source data to merge from.</param>
    /// <param name="target">Target data to merge into.</param>
    /// <param name="options">Merge options (build vs publish mode).</param>
    /// <returns>A new <see cref="V2.ImageArtifactDetails"/> containing the merged data.</returns>
    public static V2.ImageArtifactDetails Merge(
        V2.ImageArtifactDetails source,
        V2.ImageArtifactDetails target,
        ImageInfoMergeOptions? options = null)
    {
        options ??= new ImageInfoMergeOptions();

        List<V2.RepoData> mergedRepos = MergeRepos(source.Repos, target.Repos, options);

        return new V2.ImageArtifactDetails
        {
            Repos = mergedRepos,
        };
    }

    private static List<V2.RepoData> MergeRepos(
        IReadOnlyList<V2.RepoData> sourceRepos,
        IReadOnlyList<V2.RepoData> targetRepos,
        ImageInfoMergeOptions options)
    {
        List<V2.RepoData> result = targetRepos.Select(CloneRepo).ToList();

        foreach (V2.RepoData srcRepo in sourceRepos)
        {
            V2.RepoData? matchingTarget = result.FirstOrDefault(
                targetRepo => targetRepo.Repo == srcRepo.Repo);

            if (matchingTarget is not null)
            {
                int index = result.IndexOf(matchingTarget);
                result[index] = MergeRepo(srcRepo, matchingTarget, options);
            }
            else
            {
                result.Add(CloneRepo(srcRepo));
            }
        }

        result.Sort((a, b) => string.Compare(a.Repo, b.Repo, StringComparison.Ordinal));
        return result;
    }

    private static V2.RepoData MergeRepo(
        V2.RepoData source,
        V2.RepoData target,
        ImageInfoMergeOptions options) =>
        new()
        {
            Repo = source.Repo,
            Images = MergeImages(source.Images, target.Images, options),
        };

    private static List<V2.ImageData> MergeImages(
        IReadOnlyList<V2.ImageData> sourceImages,
        IReadOnlyList<V2.ImageData> targetImages,
        ImageInfoMergeOptions options)
    {
        List<V2.ImageData> result = targetImages.Select(CloneImage).ToList();

        foreach (V2.ImageData srcImage in sourceImages)
        {
            V2.ImageData? matchingTarget = FindMatchingImage(srcImage, result);

            if (matchingTarget is not null)
            {
                int index = result.IndexOf(matchingTarget);
                result[index] = MergeImage(srcImage, matchingTarget, options);
            }
            else
            {
                result.Add(CloneImage(srcImage));
            }
        }

        result.Sort(CompareImages);
        return result;
    }

    private static V2.ImageData MergeImage(
        V2.ImageData source,
        V2.ImageData target,
        ImageInfoMergeOptions options) =>
        new()
        {
            ProductVersion = source.ProductVersion,
            Manifest = MergeManifestData(source.Manifest, target.Manifest, options),
            Platforms = MergePlatforms(source.Platforms, target.Platforms, options),
        };

    private static V2.ManifestData? MergeManifestData(
        V2.ManifestData? source,
        V2.ManifestData? target,
        ImageInfoMergeOptions options)
    {
        if (source is null && target is not null)
        {
            return null;
        }

        if (source is not null && target is null)
        {
            return CloneManifest(source);
        }

        if (source is null)
        {
            return null;
        }

        return new V2.ManifestData
        {
            Digest = source.Digest,
            Created = source.Created,
            SharedTags = MergeStringList(source.SharedTags, target!.SharedTags, options.IsPublish, isReplaceable: true),
            SyndicatedDigests = MergeStringList(source.SyndicatedDigests, target.SyndicatedDigests, options.IsPublish, isReplaceable: true),
        };
    }

    private static List<V2.PlatformData> MergePlatforms(
        IReadOnlyList<V2.PlatformData> sourcePlatforms,
        IReadOnlyList<V2.PlatformData> targetPlatforms,
        ImageInfoMergeOptions options)
    {
        List<V2.PlatformData> result = targetPlatforms.Select(ClonePlatform).ToList();

        foreach (V2.PlatformData srcPlatform in sourcePlatforms)
        {
            V2.PlatformData? matchingTarget = FindMatchingPlatform(srcPlatform, result);

            if (matchingTarget is not null)
            {
                int index = result.IndexOf(matchingTarget);
                result[index] = MergePlatform(srcPlatform, matchingTarget, options);
            }
            else
            {
                result.Add(ClonePlatform(srcPlatform));
            }
        }

        result.Sort(ComparePlatforms);
        return result;
    }

    private static V2.PlatformData MergePlatform(
        V2.PlatformData source,
        V2.PlatformData target,
        ImageInfoMergeOptions options) =>
        new()
        {
            Dockerfile = source.Dockerfile,
            Digest = source.Digest,
            BaseImageDigest = source.BaseImageDigest,
            OsType = source.OsType,
            OsVersion = source.OsVersion,
            Architecture = source.Architecture,
            Created = source.Created,
            CommitUrl = source.CommitUrl,
            IsUnchanged = source.IsUnchanged,
            // Layers are always replaced, never merged; order is preserved
            Layers = source.Layers.ToList(),
            // Tags: merge or replace depending on mode
            SimpleTags = MergeStringList(source.SimpleTags, target.SimpleTags, options.IsPublish, isReplaceable: true),
        };

    private static List<string> MergeStringList(
        IReadOnlyList<string> source,
        IReadOnlyList<string> target,
        bool isPublish,
        bool isReplaceable)
    {
        if (isPublish && isReplaceable)
        {
            return source.OrderBy(item => item).ToList();
        }

        if (source.Count == 0)
        {
            return target.ToList();
        }

        return target
            .Union(source)
            .OrderBy(item => item)
            .ToList();
    }

    private static V2.ImageData? FindMatchingImage(V2.ImageData source, List<V2.ImageData> targets) =>
        targets.FirstOrDefault(target => CompareImages(source, target) == 0);

    private static V2.PlatformData? FindMatchingPlatform(V2.PlatformData source, List<V2.PlatformData> targets) =>
        targets.FirstOrDefault(target => ComparePlatforms(source, target) == 0);

    /// <summary>
    /// Compares images for matching. Two images match if they have equivalent product versions
    /// and (when versions match) the same first platform identity.
    /// </summary>
    private static int CompareImages(V2.ImageData a, V2.ImageData b)
    {
        if (ImageInfoIdentity.AreProductVersionsEquivalent(a.ProductVersion, b.ProductVersion) &&
            HaveSameFirstPlatformKey(a, b))
        {
            return 0;
        }

        int versionCompare = string.Compare(a.ProductVersion, b.ProductVersion, StringComparison.Ordinal);
        if (versionCompare != 0)
        {
            return versionCompare;
        }

        return CompareFirstPlatforms(a, b);
    }

    private static bool HaveSameFirstPlatformKey(V2.ImageData a, V2.ImageData b)
    {
        V2.PlatformData? firstA = a.Platforms.OrderBy(p => p, Comparer<V2.PlatformData>.Create(ComparePlatforms)).FirstOrDefault();
        V2.PlatformData? firstB = b.Platforms.OrderBy(p => p, Comparer<V2.PlatformData>.Create(ComparePlatforms)).FirstOrDefault();

        if (firstA is null || firstB is null)
        {
            return firstA is null && firstB is null;
        }

        return ComparePlatforms(firstA, firstB) == 0;
    }

    private static int CompareFirstPlatforms(V2.ImageData a, V2.ImageData b)
    {
        V2.PlatformData? firstA = a.Platforms.OrderBy(p => p, Comparer<V2.PlatformData>.Create(ComparePlatforms)).FirstOrDefault();
        V2.PlatformData? firstB = b.Platforms.OrderBy(p => p, Comparer<V2.PlatformData>.Create(ComparePlatforms)).FirstOrDefault();

        if (firstA is null)
        {
            return firstB is null ? 0 : 1;
        }

        return firstB is null ? 1 : ComparePlatforms(firstA, firstB);
    }

    /// <summary>
    /// Compares platforms for matching. Two platforms match if they have the same
    /// structural identity (dockerfile, arch, os, osVersion) and the same tag state.
    /// </summary>
    private static int ComparePlatforms(V2.PlatformData a, V2.PlatformData b)
    {
        if (ImageInfoIdentity.HasDifferentTagState(a.SimpleTags, b.SimpleTags))
        {
            return 1;
        }

        string keyA = ImageInfoIdentity.GetPlatformKey(a);
        string keyB = ImageInfoIdentity.GetPlatformKey(b);
        return string.Compare(keyA, keyB, StringComparison.Ordinal);
    }

    private static V2.RepoData CloneRepo(V2.RepoData repo) =>
        new()
        {
            Repo = repo.Repo,
            Images = repo.Images.Select(CloneImage).ToList(),
        };

    private static V2.ImageData CloneImage(V2.ImageData image) =>
        new()
        {
            ProductVersion = image.ProductVersion,
            Manifest = image.Manifest is not null ? CloneManifest(image.Manifest) : null,
            Platforms = image.Platforms.Select(ClonePlatform).ToList(),
        };

    private static V2.ManifestData CloneManifest(V2.ManifestData manifest) =>
        new()
        {
            Digest = manifest.Digest,
            Created = manifest.Created,
            SharedTags = manifest.SharedTags.ToList(),
            SyndicatedDigests = manifest.SyndicatedDigests.ToList(),
        };

    private static V2.PlatformData ClonePlatform(V2.PlatformData platform) =>
        new()
        {
            Dockerfile = platform.Dockerfile,
            SimpleTags = platform.SimpleTags.ToList(),
            Digest = platform.Digest,
            BaseImageDigest = platform.BaseImageDigest,
            OsType = platform.OsType,
            OsVersion = platform.OsVersion,
            Architecture = platform.Architecture,
            Created = platform.Created,
            CommitUrl = platform.CommitUrl,
            Layers = platform.Layers.Select(layer => new V2.Layer(layer.Digest, layer.Size)).ToList(),
            IsUnchanged = platform.IsUnchanged,
        };
}
