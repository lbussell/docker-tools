// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using V2 = Microsoft.DotNet.ImageBuilder.Models.Image.V2;

namespace Microsoft.DotNet.ImageBuilder.Services;

/// <summary>
/// Query operations on V2 image-info data. Replaces the extension methods
/// on the old <see cref="ImageInfoHelper"/> class.
/// </summary>
public static class ImageInfoQueryService
{
    /// <summary>
    /// Gets all digests (platform + manifest list) from the image-info data.
    /// </summary>
    public static List<string> GetAllDigests(V2.ImageArtifactDetails details) =>
        details.Repos
            .SelectMany(repo => repo.Images)
            .SelectMany(GetAllDigests)
            .ToList();

    /// <summary>
    /// Gets all digests from a single image.
    /// </summary>
    public static List<string> GetAllDigests(V2.ImageData imageData)
    {
        IEnumerable<string> digests = imageData.Platforms.Select(platform => platform.Digest);

        if (imageData.Manifest is not null && imageData.Manifest.Digest is not null)
        {
            digests = [..digests, imageData.Manifest.Digest];
        }

        return digests.ToList();
    }

    /// <summary>
    /// Gets all digest+tags+isManifestList tuples from the image-info data.
    /// </summary>
    public static List<ImageDigestInfo> GetAllImageDigestInfos(V2.ImageArtifactDetails details) =>
        details.Repos
            .SelectMany(repo => repo.Images)
            .SelectMany(GetAllImageDigestInfos)
            .ToList();

    /// <summary>
    /// Gets all digest info tuples from a single image.
    /// </summary>
    public static List<ImageDigestInfo> GetAllImageDigestInfos(V2.ImageData imageData)
    {
        List<ImageDigestInfo> imageDigestInfos = imageData.Platforms
            .Select(platform => new ImageDigestInfo(
                Digest: platform.Digest,
                Tags: platform.SimpleTags.ToList(),
                IsManifestList: false))
            .ToList();

        if (imageData.Manifest is not null && imageData.Manifest.Digest is not null)
        {
            imageDigestInfos.Add(new ImageDigestInfo(
                Digest: imageData.Manifest.Digest,
                Tags: imageData.Manifest.SharedTags.ToList(),
                IsManifestList: true));
        }

        return imageDigestInfos;
    }

    /// <summary>
    /// Applies a registry override to all digests, returning a new instance.
    /// </summary>
    public static V2.ImageArtifactDetails ApplyRegistryOverride(
        V2.ImageArtifactDetails details,
        RegistryOptions overrideOptions)
    {
        if (string.IsNullOrEmpty(overrideOptions.Registry)
            && string.IsNullOrEmpty(overrideOptions.RepoPrefix))
        {
            return details;
        }

        return details with
        {
            Repos = details.Repos.Select(repo => repo with
            {
                Images = repo.Images.Select(image => image with
                {
                    Manifest = image.Manifest is not null && !string.IsNullOrEmpty(image.Manifest.Digest)
                        ? image.Manifest with
                        {
                            Digest = overrideOptions.ApplyOverrideToDigest(image.Manifest.Digest, repoName: repo.Repo)
                        }
                        : image.Manifest,
                    Platforms = image.Platforms.Select(platform => platform with
                    {
                        Digest = !string.IsNullOrEmpty(platform.Digest)
                            ? overrideOptions.ApplyOverrideToDigest(platform.Digest, repoName: repo.Repo)
                            : platform.Digest,
                    }).ToList(),
                }).ToList(),
            }).ToList(),
        };
    }

    /// <summary>
    /// Finds the V2 PlatformData that matches the given PlatformInfo within the image-info data.
    /// </summary>
    /// <param name="platformKey">Platform key from ImageInfoIdentity.GetPlatformKey.</param>
    /// <param name="repoName">Repo name to search within.</param>
    /// <param name="details">Image-info data to search.</param>
    /// <param name="linkIndex">Manifest link index for platform matching.</param>
    public static (V2.PlatformData Platform, V2.ImageData Image)? GetMatchingPlatformData(
        PlatformInfo platformInfo,
        RepoInfo repo,
        V2.ImageArtifactDetails details,
        ManifestLinkIndex linkIndex)
    {
        V2.RepoData? repoData = details.Repos.FirstOrDefault(repoData => repoData.Repo == repo.Name);
        if (repoData is null)
        {
            return null;
        }

        foreach (V2.ImageData imageData in repoData.Images)
        {
            foreach (V2.PlatformData platformData in imageData.Platforms)
            {
                PlatformInfo? linkedPlatform = linkIndex.GetPlatformInfo(repo.Name, platformData, imageData.ProductVersion);
                if (linkedPlatform == platformInfo)
                {
                    return (platformData, imageData);
                }
            }
        }

        return null;
    }
}
