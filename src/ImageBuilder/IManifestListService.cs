// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder;

/// <summary>
/// Service for creating Docker manifest lists (multi-arch image indexes).
/// </summary>
public interface IManifestListService
{
    /// <summary>
    /// Creates Docker manifest lists for all images with shared tags
    /// that have at least one built (non-unchanged) platform in the image artifact details.
    /// Only platforms present in <paramref name="imageArtifactDetails"/> are included in the manifest lists.
    /// </summary>
    /// <param name="manifest">The loaded manifest definition.</param>
    /// <param name="imageArtifactDetails">Image info containing data about which platforms were actually built.</param>
    /// <param name="repoPrefix">Optional prefix to prepend to repository names.</param>
    /// <param name="isDryRun">When true, manifest lists are not actually created.</param>
    /// <returns>The list of manifest list tags that were created.</returns>
    IReadOnlyList<string> CreateManifestLists(
        ManifestInfo manifest,
        ImageArtifactDetails imageArtifactDetails,
        string? repoPrefix,
        bool isDryRun);
}
