// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json.Serialization;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Models.Image;

public class PlatformData : IComparable<PlatformData>
{
    [JsonRequired]
    public string Dockerfile { get; set; } = string.Empty;

    public List<string> SimpleTags { get; set; } = [];

    [JsonRequired]
    public string Digest { get; set; } = string.Empty;

    public string? BaseImageDigest { get; set; }

    [JsonRequired]
    public string OsType { get; set; } = string.Empty;

    [JsonRequired]
    public string OsVersion { get; set; } = string.Empty;

    [JsonRequired]
    public string Architecture { get; set; } = string.Empty;

    public DateTime Created { get; set; }

    [JsonRequired]
    public string CommitUrl { get; set; } = string.Empty;

    public List<Layer> Layers { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the image or its associated tag names have changed since it was last published.
    /// </summary>
    /// <remarks>
    /// Items with this state should only be used internally within a build. Such items
    /// should be stripped out of the published image info content.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsUnchanged { get; set; }

    public int CompareTo([AllowNull] PlatformData other)
    {
        if (other is null)
        {
            return 1;
        }

        if (HasDifferentTagState(other))
        {
            return 1;
        }

        // Compare without product version since we don't have access to ImageInfo
        return GetIdentifier().CompareTo(other.GetIdentifier());
    }

    /// <summary>
    /// Gets a unique identifier for the platform based on its properties.
    /// </summary>
    /// <param name="productVersion">
    /// Optional product version to include in the identifier. 
    /// When null, the identifier excludes version information.
    /// </param>
    public string GetIdentifier(string? productVersion = null)
    {
        string versionSuffix = productVersion is null ? "" : "-" + GetMajorMinorVersion(productVersion);
        return $"{Dockerfile}-{Architecture}-{OsType}-{OsVersion}{versionSuffix}";
    }

    public bool HasDifferentTagState(PlatformData other) =>
        // If either of the platforms has no simple tags while the other does have simple tags, they are not equal
        (IsNullOrEmpty(SimpleTags) && !IsNullOrEmpty(other.SimpleTags)) ||
        (!IsNullOrEmpty(SimpleTags) && IsNullOrEmpty(other.SimpleTags));

    private static bool IsNullOrEmpty<T>(List<T>? list) =>
        list is null || !list.Any();

    private static string? GetMajorMinorVersion(string? fullVersion)
    {
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

        return new Version(fullVersion).ToString(2);
    }
}
#nullable disable
