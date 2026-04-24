// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.ImageBuilder.Models.Image.V2;

/// <summary>
/// Immutable representation of manifest list metadata for a multi-platform image.
/// </summary>
public record ManifestData
{
    /// <summary>
    /// Manifest list digest.
    /// </summary>
    public string? Digest { get; init; }

    /// <summary>
    /// Additional digests for syndicated content.
    /// </summary>
    public IReadOnlyList<string> SyndicatedDigests { get; init; } = [];

    /// <summary>
    /// Timestamp when the manifest list was created.
    /// </summary>
    public DateTime Created { get; init; }

    /// <summary>
    /// Tags shared across all platforms for this image (e.g., "8.0", "latest").
    /// </summary>
    public IReadOnlyList<string> SharedTags { get; init; } = [];
}
