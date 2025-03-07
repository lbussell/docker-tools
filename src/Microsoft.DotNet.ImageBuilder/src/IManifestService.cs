// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Oci = Microsoft.DotNet.ImageBuilder.Models.Oci;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder;

public interface IManifestService
{
    Task<ManifestQueryResult> GetManifestAsync(string image, bool isDryRun);

    public async Task<IEnumerable<string>> GetImageLayerDigestsAsync(string tag, bool isDryRun)
    {
        var layers = await GetImageLayersAsync(tag, isDryRun);
        return layers.Select(layer => layer.Digest);
    }

    public async Task<List<Oci.Descriptor>> GetImageLayersAsync(string tag, bool isDryRun)
    {
        if (isDryRun)
        {
            return [];
        }

        ManifestQueryResult manifestResult = await GetManifestAsync(tag, isDryRun);
        var manifest = Oci.Manifest.FromJson(manifestResult.Manifest);

        if (manifest.Layers is null)
        {
            JsonArray manifests = (JsonArray)(manifestResult.Manifest["manifests"] ??
                throw new InvalidOperationException("Expected manifests property"));

            throw new InvalidOperationException(
                $"'{tag}' is expected to be a concrete tag with 1 manifest. It has '{manifests.Count}' manifests.");
        }

        return manifest.Layers;
    }

    public async Task<string?> GetLocalImageDigestAsync(string image, bool isDryRun)
    {
        IEnumerable<string> digests = DockerHelper.GetImageDigests(image, isDryRun);

        // A digest will not exist for images that have been built locally or have been manually installed
        if (!digests.Any())
        {
            return null;
        }

        string digestSha = await GetManifestDigestShaAsync(image, isDryRun);
        if (digestSha is null)
        {
            return null;
        }

        string digest = DockerHelper.GetDigestString(DockerHelper.GetRepo(image), digestSha);
        if (!digests.Contains(digest))
        {
            throw new InvalidOperationException(
                $"Found published digest '{digestSha}' for tag '{image}' but could not find a matching digest value from " +
                $"the set of locally pulled digests for this tag: { string.Join(", ", digests) }. This most likely means that " +
                "this tag has been updated since it was last pulled.");
        }

        return digest;
    }

    public async Task<string> GetManifestDigestShaAsync(string tag, bool isDryRun)
    {
        ManifestQueryResult manifestResult = await GetManifestAsync(tag, isDryRun);
        return manifestResult.ContentDigest;
    }
}
