// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder.Models.Image;

public class ImageData : IComparable<ImageData>
{
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? ProductVersion { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ManifestData? Manifest { get; set; }

    public List<PlatformData> Platforms { get; set; } = [];

    public int CompareTo([AllowNull] ImageData other)
    {
        if (other is null)
        {
            return 1;
        }

        // Compare by ProductVersion first
        if (ProductVersion != other.ProductVersion)
        {
            return string.Compare(ProductVersion, other.ProductVersion, StringComparison.Ordinal);
        }

        // If ProductVersions match, compare by the first Platform to provide deterministic ordering
        // and distinguish between different images with the same ProductVersion
        PlatformData? thisFirstPlatform = Platforms
            .OrderBy(platform => platform)
            .FirstOrDefault();
        PlatformData? otherFirstPlatform = other.Platforms
            .OrderBy(platform => platform)
            .FirstOrDefault();
        return thisFirstPlatform?.CompareTo(otherFirstPlatform) ?? 1;
    }
}
