// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder;

/// <summary>
/// Loads and deserializes image info JSON files from disk.
/// </summary>
public class ImageInfoService(IFileSystem fileSystem) : IImageInfoService
{
    private readonly IFileSystem _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

    /// <inheritdoc />
    public ImageArtifactDetails DeserializeImageArtifactDetails(string path)
    {
        var imageInfoText = _fileSystem.ReadAllText(path);
        return ImageArtifactDetails.FromJson(imageInfoText) ??
            throw new InvalidDataException($"Unable to deserialize image info file {path}");
    }

    /// <inheritdoc />
    public ImageArtifactDetails LoadFromFile(
        string path,
        ManifestInfo manifest,
        bool skipManifestValidation = false,
        bool useFilteredManifest = false) =>
        ImageInfoHelper.LoadFromContent(
            _fileSystem.ReadAllText(path),
            manifest,
            skipManifestValidation,
            useFilteredManifest);
}
