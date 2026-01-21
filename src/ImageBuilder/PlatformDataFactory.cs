// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder;

/// <summary>
/// Factory methods for creating PlatformData instances from ViewModel types.
/// </summary>
public static class PlatformDataFactory
{
    /// <summary>
    /// Creates a PlatformData instance from PlatformInfo and ImageInfo.
    /// </summary>
    public static PlatformData FromPlatformInfo(PlatformInfo platform, ImageInfo image) =>
        new()
        {
            Dockerfile = platform.DockerfilePathRelativeToManifest,
            Architecture = platform.Model.Architecture.GetDisplayName(),
            OsType = platform.Model.OS.ToString(),
            OsVersion = platform.Model.OsVersion,
            SimpleTags = platform.Tags.Select(tag => tag.Name).ToList()
        };
}
