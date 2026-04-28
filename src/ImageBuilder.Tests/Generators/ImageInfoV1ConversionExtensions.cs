// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Services;
using V2 = Microsoft.DotNet.ImageBuilder.Models.Image.V2;

namespace Microsoft.DotNet.ImageBuilder.Tests.Generators;

/// <summary>
/// Converts generated V2 image-info records to the old mutable image-info model through the JSON contract.
/// </summary>
public static class ImageInfoV1ConversionExtensions
{
    /// <summary>
    /// Converts V2 image-info details to the old mutable image-info details.
    /// </summary>
    public static ImageArtifactDetails ConvertToV1(this V2.ImageArtifactDetails details) =>
        ImageArtifactDetails.FromJson(ImageInfoSerializer.Serialize(details));

    /// <summary>
    /// Converts a V2 repo to the old mutable repo model.
    /// </summary>
    public static RepoData ConvertToV1(this V2.RepoData repo) =>
        new V2.ImageArtifactDetails
        {
            Repos = [repo],
        }.ConvertToV1().Repos[0];

    /// <summary>
    /// Converts a V2 image to the old mutable image model.
    /// </summary>
    public static ImageData ConvertToV1(this V2.ImageData image) =>
        new V2.ImageArtifactDetails
        {
            Repos =
            [
                new V2.RepoData
                {
                    Repo = "repo",
                    Images = [image],
                }
            ],
        }.ConvertToV1().Repos[0].Images[0];

    /// <summary>
    /// Converts a V2 platform to the old mutable platform model.
    /// </summary>
    public static PlatformData ConvertToV1(this V2.PlatformData platform) =>
        new V2.ImageData
        {
            Platforms = [platform],
        }.ConvertToV1().Platforms[0];

    /// <summary>
    /// Converts a V2 manifest to the old mutable manifest model.
    /// </summary>
    public static ManifestData? ConvertToV1(this V2.ManifestData? manifest) =>
        manifest is null
            ? null
            : new V2.ImageData
            {
                Manifest = manifest,
                Platforms =
                [
                    new V2.PlatformData
                    {
                        Dockerfile = "Dockerfile",
                        Digest = "sha256:0000000000000000000000000000000000000000000000000000000000000000",
                        OsType = "Linux",
                        OsVersion = "noble",
                        Architecture = "amd64",
                        CommitUrl = "https://example.com",
                    }
                ],
            }.ConvertToV1().Manifest;

    /// <summary>
    /// Converts a V2 layer to the old layer record.
    /// </summary>
    public static Layer ConvertToV1(this V2.Layer layer) =>
        new(layer.Digest, layer.Size);
}
