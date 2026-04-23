// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.DotNet.ImageBuilder.Models.Image.V2;

/// <summary>
/// Immutable representation of a multi-platform container image and its build artifacts.
/// </summary>
public record ImageData
{
    /// <summary>
    /// Product version (e.g., "8.0", "9.0.5"). May be null for unversioned images.
    /// </summary>
    public string? ProductVersion { get; init; }

    /// <summary>
    /// Manifest list metadata (digest, shared tags) for this image. Null if no manifest list exists.
    /// </summary>
    public ManifestData? Manifest { get; init; }

    /// <summary>
    /// Platform-specific build artifacts for this image.
    /// </summary>
    public IReadOnlyList<PlatformData> Platforms { get; init; } = [];
}
