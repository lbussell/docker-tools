// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.ViewModel;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder;

/// <summary>
/// Provides context for associating image artifact data with manifest view models.
/// This allows the data models to remain clean (no ViewModel dependencies) while
/// still tracking the associations needed during processing.
/// </summary>
public class ImageArtifactContext
{
    private readonly Dictionary<PlatformData, PlatformInfo> _platformInfoLookup = new();
    private readonly Dictionary<PlatformData, ImageInfo> _imageInfoByPlatform = new();
    private readonly Dictionary<ImageData, ImageInfo> _imageInfoLookup = new();
    private readonly Dictionary<ImageData, RepoInfo> _repoInfoLookup = new();

    /// <summary>
    /// Gets the ImageArtifactDetails this context is associated with.
    /// </summary>
    public ImageArtifactDetails Details { get; }

    public ImageArtifactContext(ImageArtifactDetails details)
    {
        Details = details;
    }

    /// <summary>
    /// Associates a PlatformData with its corresponding PlatformInfo and ImageInfo from the manifest.
    /// </summary>
    public void SetPlatformContext(PlatformData platform, PlatformInfo platformInfo, ImageInfo imageInfo)
    {
        _platformInfoLookup[platform] = platformInfo;
        _imageInfoByPlatform[platform] = imageInfo;
    }

    /// <summary>
    /// Associates an ImageData with its corresponding ImageInfo and RepoInfo from the manifest.
    /// </summary>
    public void SetImageContext(ImageData image, ImageInfo imageInfo, RepoInfo repoInfo)
    {
        _imageInfoLookup[image] = imageInfo;
        _repoInfoLookup[image] = repoInfo;
    }

    /// <summary>
    /// Gets the PlatformInfo associated with the given PlatformData.
    /// </summary>
    /// <returns>The associated PlatformInfo, or null if not found.</returns>
    public PlatformInfo? GetPlatformInfo(PlatformData platform)
    {
        _platformInfoLookup.TryGetValue(platform, out PlatformInfo? result);
        return result;
    }

    /// <summary>
    /// Gets the ImageInfo associated with the given PlatformData.
    /// </summary>
    /// <returns>The associated ImageInfo, or null if not found.</returns>
    public ImageInfo? GetImageInfoForPlatform(PlatformData platform)
    {
        _imageInfoByPlatform.TryGetValue(platform, out ImageInfo? result);
        return result;
    }

    /// <summary>
    /// Gets the ImageInfo associated with the given ImageData.
    /// </summary>
    /// <returns>The associated ImageInfo, or null if not found.</returns>
    public ImageInfo? GetImageInfo(ImageData image)
    {
        _imageInfoLookup.TryGetValue(image, out ImageInfo? result);
        return result;
    }

    /// <summary>
    /// Gets the RepoInfo associated with the given ImageData.
    /// </summary>
    /// <returns>The associated RepoInfo, or null if not found.</returns>
    public RepoInfo? GetRepoInfo(ImageData image)
    {
        _repoInfoLookup.TryGetValue(image, out RepoInfo? result);
        return result;
    }

    /// <summary>
    /// Finds the PlatformData that matches the given PlatformInfo.
    /// </summary>
    public PlatformData? FindPlatformData(PlatformInfo platformInfo)
    {
        foreach (var kvp in _platformInfoLookup)
        {
            if (kvp.Value == platformInfo)
            {
                return kvp.Key;
            }
        }
        return null;
    }

    /// <summary>
    /// Finds the ImageData that contains the given PlatformData.
    /// </summary>
    public ImageData? FindImageDataForPlatform(PlatformData platform)
    {
        foreach (RepoData repo in Details.Repos)
        {
            foreach (ImageData image in repo.Images)
            {
                if (image.Platforms.Contains(platform))
                {
                    return image;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Gets all tags (shared + platform-specific) for a platform.
    /// </summary>
    public IEnumerable<TagInfo> GetAllTags(PlatformData platform)
    {
        ImageInfo? imageInfo = GetImageInfoForPlatform(platform);
        PlatformInfo? platformInfo = GetPlatformInfo(platform);

        if (imageInfo == null)
        {
            return Enumerable.Empty<TagInfo>();
        }

        return imageInfo.SharedTags.Union(platformInfo?.Tags ?? Enumerable.Empty<TagInfo>());
    }

    /// <summary>
    /// Gets the major.minor version for a platform's product version.
    /// </summary>
    public string? GetMajorMinorVersion(PlatformData platform)
    {
        ImageInfo? imageInfo = GetImageInfoForPlatform(platform);
        if (imageInfo == null)
        {
            return null;
        }

        string? fullVersion = imageInfo.ProductVersion;
        if (string.IsNullOrEmpty(fullVersion))
        {
            return null;
        }

        // Remove any version suffix (like "-preview")
        int separatorIndex = fullVersion.IndexOf("-");
        if (separatorIndex >= 0)
        {
            fullVersion = fullVersion.Substring(0, separatorIndex);
        }

        return new System.Version(fullVersion).ToString(2);
    }

    /// <summary>
    /// Gets a platform identifier that includes the product version from the context.
    /// </summary>
    public string GetPlatformIdentifier(PlatformData platform, bool excludeProductVersion = false)
    {
        string baseIdentifier = $"{platform.Dockerfile}-{platform.Architecture}-{platform.OsType}-{platform.OsVersion}";
        if (excludeProductVersion)
        {
            return baseIdentifier;
        }

        string? majorMinorVersion = GetMajorMinorVersion(platform);
        return $"{baseIdentifier}{(majorMinorVersion != null ? "-" + majorMinorVersion : "")}";
    }
}
#nullable disable
