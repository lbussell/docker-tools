// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Models.Image;

namespace Microsoft.DotNet.ImageBuilder.Models.Services;

/// <summary>
/// Interface for loading and saving ImageArtifactDetails JSON files (image-info.json).
/// </summary>
public interface IImageArtifactDetailsLoader
{
    /// <summary>
    /// Loads an ImageArtifactDetails from a JSON file.
    /// </summary>
    /// <param name="filePath">Path to the image-info.json file.</param>
    /// <returns>The deserialized ImageArtifactDetails object.</returns>
    ImageArtifactDetails Load(string filePath);

    /// <summary>
    /// Loads an ImageArtifactDetails from a JSON string.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>The deserialized ImageArtifactDetails object.</returns>
    ImageArtifactDetails LoadFromJson(string json);

    /// <summary>
    /// Saves an ImageArtifactDetails to a JSON file.
    /// </summary>
    /// <param name="details">The ImageArtifactDetails to serialize.</param>
    /// <param name="filePath">Path to save the image-info.json file.</param>
    void Save(ImageArtifactDetails details, string filePath);

    /// <summary>
    /// Serializes an ImageArtifactDetails to a JSON string.
    /// </summary>
    /// <param name="details">The ImageArtifactDetails to serialize.</param>
    /// <returns>The JSON string representation.</returns>
    string ToJson(ImageArtifactDetails details);
}
