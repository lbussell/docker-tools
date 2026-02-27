// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder;

/// <summary>
/// Loads and deserializes image info JSON files (<c>image-info.json</c>) into
/// <see cref="ImageArtifactDetails"/> models, optionally associating them with manifest metadata.
/// </summary>
/// <remarks>
/// Image info files are ImageBuilder's output describing which images were built — their digests,
/// tags, Dockerfile paths, and build timestamps. This service handles reading those files from disk.
/// For pure in-memory operations on image info data (merging, querying, etc.), see <see cref="ImageInfoHelper"/>.
/// </remarks>
public interface IImageInfoService
{
    /// <summary>
    /// Loads an image info file as a parsed model directly with no validation or filtering.
    /// </summary>
    /// <param name="path">Path to the image info file.</param>
    /// <returns>The deserialized <see cref="ImageArtifactDetails"/>.</returns>
    /// <exception cref="System.IO.InvalidDataException">Thrown when the file cannot be deserialized.</exception>
    ImageArtifactDetails DeserializeImageArtifactDetails(string path);

    /// <summary>
    /// Loads an image info file and associates its contents with manifest metadata.
    /// </summary>
    /// <param name="path">Path to the image info file.</param>
    /// <param name="manifest">The manifest to associate with the loaded image info.</param>
    /// <param name="skipManifestValidation">
    /// Whether to skip validation if no associated manifest model item was found for a given image info model item.
    /// </param>
    /// <param name="useFilteredManifest">Whether to use the filtered content of the manifest for lookups.</param>
    /// <returns>The deserialized and manifest-associated <see cref="ImageArtifactDetails"/>.</returns>
    ImageArtifactDetails LoadFromFile(
        string path,
        ManifestInfo manifest,
        bool skipManifestValidation = false,
        bool useFilteredManifest = false);
}
