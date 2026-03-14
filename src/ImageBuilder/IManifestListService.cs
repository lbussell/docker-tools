// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder;

/// <summary>
/// Describes a Docker manifest list to be created - a multi-arch tag
/// that references one or more platform-specific image tags.
/// </summary>
/// <param name="Tag">The fully-qualified manifest list tag (e.g., "mcr.microsoft.com/dotnet/aspnet:8.0").</param>
/// <param name="PlatformTags">The fully-qualified platform image tags included in this manifest list.</param>
public record ManifestListInfo(string Tag, IReadOnlyList<string> PlatformTags);

/// <summary>
/// Determines which Docker manifest lists should be created based on
/// the manifest definition and which platforms were actually built.
/// </summary>
public interface IManifestListService
{
    /// <summary>
    /// Returns the manifest lists that should be created for images that have
    /// shared tags and at least one changed (non-cached) platform in
    /// <paramref name="imageArtifactDetails"/>. Only platforms present in
    /// <paramref name="imageArtifactDetails"/> are included in the results.
    /// </summary>
    /// <param name="manifest">The loaded manifest definition.</param>
    /// <param name="imageArtifactDetails">Image info describing which platforms were actually built.</param>
    /// <param name="repoPrefix">Optional prefix to prepend to repository names.</param>
    /// <returns>Manifest list descriptors, each containing a tag and its platform image tags.</returns>
    IReadOnlyList<ManifestListInfo> GetManifestListsForChangedImages(
        ManifestInfo manifest,
        ImageArtifactDetails imageArtifactDetails,
        string? repoPrefix);
}
