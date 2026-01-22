// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Models.Manifest;

namespace Microsoft.DotNet.ImageBuilder.Models.Services;

/// <summary>
/// Interface for loading and saving Manifest JSON files.
/// </summary>
public interface IManifestLoader
{
    /// <summary>
    /// Loads a Manifest from a JSON file.
    /// </summary>
    /// <param name="filePath">Path to the manifest.json file.</param>
    /// <returns>The deserialized Manifest object.</returns>
    Manifest.Manifest Load(string filePath);

    /// <summary>
    /// Loads a Manifest from a JSON string.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>The deserialized Manifest object.</returns>
    Manifest.Manifest LoadFromJson(string json);

    /// <summary>
    /// Saves a Manifest to a JSON file.
    /// </summary>
    /// <param name="manifest">The Manifest to serialize.</param>
    /// <param name="filePath">Path to save the manifest.json file.</param>
    void Save(Manifest.Manifest manifest, string filePath);

    /// <summary>
    /// Serializes a Manifest to a JSON string.
    /// </summary>
    /// <param name="manifest">The Manifest to serialize.</param>
    /// <returns>The JSON string representation.</returns>
    string ToJson(Manifest.Manifest manifest);
}
