// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder.Models.Services;

/// <summary>
/// Default implementation of <see cref="IImageArtifactDetailsLoader"/> using Newtonsoft.Json.
/// </summary>
public class ImageArtifactDetailsLoader : IImageArtifactDetailsLoader
{
    /// <inheritdoc />
    public ImageArtifactDetails Load(string filePath)
    {
        string json = File.ReadAllText(filePath);
        return LoadFromJson(json);
    }

    /// <inheritdoc />
    public ImageArtifactDetails LoadFromJson(string json)
    {
        return JsonOptions.DeserializeOrThrow<ImageArtifactDetails>(
            json,
            JsonOptions.ImageArtifactDetailsDeserializerSettings);
    }

    /// <inheritdoc />
    public void Save(ImageArtifactDetails details, string filePath)
    {
        string json = ToJson(details);
        File.WriteAllText(filePath, json);
    }

    /// <inheritdoc />
    public string ToJson(ImageArtifactDetails details)
    {
        return JsonConvert.SerializeObject(details, JsonOptions.SerializerSettings);
    }
}
