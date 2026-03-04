// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder;

/// <summary>
/// Pairs deserialized <see cref="ImageArtifactDetails"/> with ViewModel association lookups,
/// providing O(1) navigation from image-info model objects to their corresponding manifest definitions.
/// </summary>
/// <remarks>
/// Instances are created by <see cref="IImageInfoService.CreateContext"/> or
/// <see cref="IImageInfoService.LoadContext"/>. Because the context is a stateless result object,
/// callers can mutate the underlying <see cref="Details"/> and call
/// <see cref="IImageInfoService.CreateContext"/> again to rebuild associations.
/// </remarks>
public class ImageArtifactContext
{
    private readonly Dictionary<PlatformData, PlatformInfo> _platformInfoMap;
    private readonly Dictionary<PlatformData, ImageInfo> _platformImageInfoMap;
    private readonly Dictionary<ImageData, ImageInfo> _manifestImageMap;
    private readonly Dictionary<ImageData, RepoInfo> _manifestRepoMap;

    /// <summary>
    /// The deserialized image artifact details that this context wraps.
    /// </summary>
    public ImageArtifactDetails Details { get; }

    internal ImageArtifactContext(
        ImageArtifactDetails details,
        Dictionary<PlatformData, PlatformInfo> platformInfoMap,
        Dictionary<PlatformData, ImageInfo> platformImageInfoMap,
        Dictionary<ImageData, ImageInfo> manifestImageMap,
        Dictionary<ImageData, RepoInfo> manifestRepoMap)
    {
        Details = details;
        _platformInfoMap = platformInfoMap;
        _platformImageInfoMap = platformImageInfoMap;
        _manifestImageMap = manifestImageMap;
        _manifestRepoMap = manifestRepoMap;
    }

    /// <summary>
    /// Gets the manifest platform definition associated with the given platform data,
    /// or <c>null</c> if no match was found.
    /// </summary>
    public PlatformInfo? GetPlatformInfo(PlatformData platform) =>
        _platformInfoMap.GetValueOrDefault(platform);

    /// <summary>
    /// Gets the manifest image definition that contains the given platform,
    /// or <c>null</c> if no match was found.
    /// </summary>
    public ImageInfo? GetImageInfo(PlatformData platform) =>
        _platformImageInfoMap.GetValueOrDefault(platform);

    /// <summary>
    /// Gets the manifest image definition associated with the given image data,
    /// or <c>null</c> if no match was found.
    /// </summary>
    public ImageInfo? GetManifestImage(ImageData image) =>
        _manifestImageMap.GetValueOrDefault(image);

    /// <summary>
    /// Gets the manifest repo definition associated with the given image data,
    /// or <c>null</c> if no match was found.
    /// </summary>
    public RepoInfo? GetManifestRepo(ImageData image) =>
        _manifestRepoMap.GetValueOrDefault(image);
}
