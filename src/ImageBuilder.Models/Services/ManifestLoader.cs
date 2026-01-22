// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder.Models.Services;

/// <summary>
/// Default implementation of <see cref="IManifestLoader"/> using Newtonsoft.Json.
/// </summary>
public class ManifestLoader : IManifestLoader
{
    /// <inheritdoc />
    public Manifest.Manifest Load(string filePath)
    {
        string json = File.ReadAllText(filePath);
        return LoadFromJson(json);
    }

    /// <inheritdoc />
    public Manifest.Manifest LoadFromJson(string json)
    {
        return JsonOptions.DeserializeOrThrow<Manifest.Manifest>(json);
    }

    /// <inheritdoc />
    public void Save(Manifest.Manifest manifest, string filePath)
    {
        string json = ToJson(manifest);
        File.WriteAllText(filePath, json);
    }

    /// <inheritdoc />
    public string ToJson(Manifest.Manifest manifest)
    {
        return JsonConvert.SerializeObject(manifest, JsonOptions.SerializerSettings);
    }
}
