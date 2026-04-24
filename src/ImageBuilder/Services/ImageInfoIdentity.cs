// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using V2 = Microsoft.DotNet.ImageBuilder.Models.Image.V2;

namespace Microsoft.DotNet.ImageBuilder.Services;

/// <summary>
/// Provides canonical identity key generation and comparison functions for image-info data.
/// These keys are used by the merge, linking, and query services to match items
/// without requiring mutable ViewModel cross-references.
/// </summary>
public static class ImageInfoIdentity
{
    /// <summary>
    /// Generates a canonical identity key for a platform based on its structural properties.
    /// Format: "{Dockerfile}-{Architecture}-{OsType}-{OsVersion}[-{MajorMinorVersion}]"
    /// </summary>
    /// <param name="dockerfile">Dockerfile path relative to manifest root.</param>
    /// <param name="architecture">CPU architecture (e.g., "amd64").</param>
    /// <param name="osType">Operating system type (e.g., "Linux").</param>
    /// <param name="osVersion">Operating system version (e.g., "noble").</param>
    /// <param name="productVersion">
    /// Optional product version. When provided, the major.minor portion is appended to the key.
    /// </param>
    public static string GetPlatformKey(
        string dockerfile,
        string architecture,
        string osType,
        string osVersion,
        string? productVersion = null)
    {
        string versionSuffix = productVersion is not null
            ? $"-{GetMajorMinorVersion(productVersion)}"
            : "";
        return $"{dockerfile}-{architecture}-{osType}-{osVersion}{versionSuffix}";
    }

    /// <summary>
    /// Generates a platform key from a V2 PlatformData record.
    /// </summary>
    /// <param name="platform">The platform data.</param>
    /// <param name="productVersion">
    /// Optional product version from the parent ImageData.
    /// </param>
    public static string GetPlatformKey(V2.PlatformData platform, string? productVersion = null) =>
        GetPlatformKey(platform.Dockerfile, platform.Architecture, platform.OsType, platform.OsVersion, productVersion);

    /// <summary>
    /// Gets the canonical repo identity key, which is simply the repo name.
    /// </summary>
    public static string GetRepoKey(string repoName) => repoName;

    /// <summary>
    /// Determines whether two sets of tags have different "presence" state —
    /// i.e., one has tags while the other has none.
    /// </summary>
    public static bool HasDifferentTagState(IReadOnlyList<string> tagsA, IReadOnlyList<string> tagsB) =>
        (tagsA.Count == 0 && tagsB.Count > 0) || (tagsA.Count > 0 && tagsB.Count == 0);

    /// <summary>
    /// Determines whether two product versions are considered equivalent.
    /// Product versions are equivalent if their major.minor segments match.
    /// </summary>
    public static bool AreProductVersionsEquivalent(string? version1, string? version2)
    {
        if (version1 == version2)
        {
            return true;
        }

        return Version.TryParse(StripVersionSuffix(version1), out Version? parsed1) &&
               Version.TryParse(StripVersionSuffix(version2), out Version? parsed2) &&
               parsed1.ToString(2) == parsed2.ToString(2);
    }

    /// <summary>
    /// Extracts the major.minor portion of a version string.
    /// Returns null if the version is null, empty, or cannot be parsed.
    /// </summary>
    public static string? GetMajorMinorVersion(string? fullVersion)
    {
        if (string.IsNullOrEmpty(fullVersion))
        {
            return null;
        }

        string stripped = StripVersionSuffix(fullVersion);

        return Version.TryParse(stripped, out Version? version)
            ? version.ToString(2)
            : null;
    }

    private static string StripVersionSuffix(string? version)
    {
        if (version is null)
        {
            return string.Empty;
        }

        int separatorIndex = version.IndexOf('-');
        return separatorIndex >= 0
            ? version[..separatorIndex]
            : version;
    }
}
