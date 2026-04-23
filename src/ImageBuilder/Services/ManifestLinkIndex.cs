// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using V2 = Microsoft.DotNet.ImageBuilder.Models.Image.V2;

namespace Microsoft.DotNet.ImageBuilder.Services;

/// <summary>
/// Key-based lookup index that maps V2 image-info data to their corresponding
/// manifest ViewModel objects. Replaces the mutable [JsonIgnore] cross-references
/// on the old model classes.
/// </summary>
public class ManifestLinkIndex
{
    private readonly Dictionary<string, RepoInfo> _repoLinks = new();
    private readonly Dictionary<string, PlatformLink> _platformLinks = new();

    /// <summary>
    /// Gets the <see cref="RepoInfo"/> for the given repo name, or null if not found.
    /// </summary>
    public RepoInfo? GetRepoInfo(string repoName) =>
        _repoLinks.GetValueOrDefault(repoName);

    /// <summary>
    /// Gets the <see cref="ImageInfo"/> that corresponds to the given platform in the given repo.
    /// Returns null if no matching manifest image was found.
    /// </summary>
    public ImageInfo? GetImageInfo(string repoName, V2.PlatformData platform, string? productVersion) =>
        GetPlatformLink(repoName, platform, productVersion)?.ImageInfo;

    /// <summary>
    /// Gets the <see cref="PlatformInfo"/> that corresponds to the given platform data.
    /// Returns null if no matching manifest platform was found.
    /// </summary>
    public PlatformInfo? GetPlatformInfo(string repoName, V2.PlatformData platform, string? productVersion) =>
        GetPlatformLink(repoName, platform, productVersion)?.PlatformInfo;

    /// <summary>
    /// Creates a <see cref="ManifestLinkIndex"/> by matching image-info data to manifest definitions.
    /// </summary>
    /// <param name="details">The image-info data to link.</param>
    /// <param name="manifest">The manifest containing repo/image/platform definitions.</param>
    /// <param name="useFilteredManifest">Whether to use filtered content for lookups.</param>
    /// <param name="skipManifestValidation">Whether to skip validation when no match is found.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a platform cannot be matched to the manifest and validation is not skipped.
    /// </exception>
    public static ManifestLinkIndex Create(
        V2.ImageArtifactDetails details,
        ManifestInfo manifest,
        bool useFilteredManifest = false,
        bool skipManifestValidation = false)
    {
        ManifestLinkIndex index = new();

        IEnumerable<RepoInfo> manifestRepos = useFilteredManifest
            ? manifest.FilteredRepos
            : manifest.AllRepos;

        foreach (V2.RepoData repoData in details.Repos)
        {
            RepoInfo? manifestRepo = manifestRepos.FirstOrDefault(repo => repo.Name == repoData.Repo);
            if (manifestRepo is null)
            {
                Console.WriteLine($"Image info repo not loaded: {repoData.Repo}");
                continue;
            }

            index._repoLinks[repoData.Repo] = manifestRepo;

            foreach (V2.ImageData imageData in repoData.Images)
            {
                foreach (V2.PlatformData platformData in imageData.Platforms)
                {
                    bool matched = TryMatchPlatform(
                        platformData, imageData, manifestRepo, useFilteredManifest, index);

                    if (!matched && !skipManifestValidation)
                    {
                        string platformKey = ImageInfoIdentity.GetPlatformKey(platformData);
                        throw new InvalidOperationException(
                            $"Unable to find matching platform in manifest for platform '{platformKey}'.");
                    }
                }
            }
        }

        return index;
    }

    private PlatformLink? GetPlatformLink(string repoName, V2.PlatformData platform, string? productVersion)
    {
        string key = BuildLookupKey(repoName, platform, productVersion);
        return _platformLinks.GetValueOrDefault(key);
    }

    private static bool TryMatchPlatform(
        V2.PlatformData platformData,
        V2.ImageData imageData,
        RepoInfo manifestRepo,
        bool useFilteredManifest,
        ManifestLinkIndex index)
    {
        IEnumerable<ImageInfo> manifestImages = useFilteredManifest
            ? manifestRepo.FilteredImages
            : manifestRepo.AllImages;

        string platformKeyNoVersion = ImageInfoIdentity.GetPlatformKey(platformData);

        foreach (ImageInfo manifestImage in manifestImages)
        {
            IEnumerable<PlatformInfo> manifestPlatforms = useFilteredManifest
                ? manifestImage.FilteredPlatforms
                : manifestImage.AllPlatforms;

            foreach (PlatformInfo manifestPlatform in manifestPlatforms)
            {
                if (ArePlatformsEqual(platformData, imageData, manifestPlatform, manifestImage))
                {
                    string key = BuildLookupKey(manifestRepo.Name, platformData, imageData.ProductVersion);
                    index._platformLinks[key] = new PlatformLink(manifestPlatform, manifestImage);
                    return true;
                }
            }
        }

        return false;
    }

    private static bool ArePlatformsEqual(
        V2.PlatformData platformData,
        V2.ImageData imageData,
        PlatformInfo manifestPlatform,
        ImageInfo manifestImage)
    {
        // Build the manifest platform's identity
        string manifestPlatformKey = ImageInfoIdentity.GetPlatformKey(
            manifestPlatform.DockerfilePathRelativeToManifest,
            manifestPlatform.Model.Architecture.GetDisplayName(),
            manifestPlatform.Model.OS.ToString(),
            manifestPlatform.Model.OsVersion);

        string imagePlatformKey = ImageInfoIdentity.GetPlatformKey(platformData);

        // Build tag lists for tag-state comparison
        IReadOnlyList<string> manifestTags = manifestPlatform.Tags
            .Select(tag => tag.Name)
            .ToList();

        return imagePlatformKey == manifestPlatformKey &&
               !ImageInfoIdentity.HasDifferentTagState(platformData.SimpleTags, manifestTags) &&
               ImageInfoIdentity.AreProductVersionsEquivalent(imageData.ProductVersion, manifestImage.ProductVersion);
    }

    private static string BuildLookupKey(string repoName, V2.PlatformData platform, string? productVersion) =>
        $"{repoName}|{ImageInfoIdentity.GetPlatformKey(platform)}|{productVersion}";

    private sealed record PlatformLink(PlatformInfo PlatformInfo, ImageInfo ImageInfo);
}
