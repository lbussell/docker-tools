// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using CsCheck;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Services;
using Microsoft.DotNet.ImageBuilder.Tests.Generators;
using Shouldly;
using Xunit;
using V2 = Microsoft.DotNet.ImageBuilder.Models.Image.V2;

namespace Microsoft.DotNet.ImageBuilder.Tests.PropertyTests;

/// <summary>
/// Metamorphic tests verifying that the new <see cref="ImageInfoSerializer"/>
/// produces output equivalent to the old serialization path.
/// </summary>
public class SerializerMigrationPropertyTests
{
    /// <summary>
    /// For any generated data, converting old models to V2 records and serializing
    /// with the new serializer produces the same JSON as the old serializer.
    /// </summary>
    [Fact]
    public void Serialize_V2Records_MatchesOldSerialization()
    {
        ImageInfoGenerators.ImageArtifactDetails.Sample(oldDetails =>
        {
            string oldJson = JsonHelper.SerializeObject(oldDetails);
            V2.ImageArtifactDetails v2Details = ConvertToV2(oldDetails);
            string newJson = ImageInfoSerializer.Serialize(v2Details);

            newJson.ShouldBe(oldJson);
        });
    }

    /// <summary>
    /// Deserializing JSON produced by the old serializer into V2 records, then
    /// re-serializing with the new serializer, produces the same JSON.
    /// </summary>
    [Fact]
    public void RoundTrip_OldJsonThroughNewSerializer_ProducesSameJson()
    {
        ImageInfoGenerators.ImageArtifactDetails.Sample(oldDetails =>
        {
            string oldJson = JsonHelper.SerializeObject(oldDetails);
            V2.ImageArtifactDetails deserialized = ImageInfoSerializer.Deserialize(oldJson);
            string reserializedJson = ImageInfoSerializer.Serialize(deserialized);

            reserializedJson.ShouldBe(oldJson);
        });
    }

    /// <summary>
    /// Deserializing JSON into V2 records preserves all data fields.
    /// </summary>
    [Fact]
    public void Deserialize_PreservesAllFields()
    {
        ImageInfoGenerators.ImageArtifactDetails.Sample(oldDetails =>
        {
            string json = JsonHelper.SerializeObject(oldDetails);
            V2.ImageArtifactDetails v2Details = ImageInfoSerializer.Deserialize(json);

            v2Details.SchemaVersion.ShouldBe("2.0");
            v2Details.Repos.Count.ShouldBe(oldDetails.Repos.Count);

            for (int repoIdx = 0; repoIdx < oldDetails.Repos.Count; repoIdx++)
            {
                v2Details.Repos[repoIdx].Repo.ShouldBe(oldDetails.Repos[repoIdx].Repo);
                v2Details.Repos[repoIdx].Images.Count.ShouldBe(oldDetails.Repos[repoIdx].Images.Count);
            }
        });
    }

    /// <summary>
    /// The new serializer produces identical output for the same V2 data
    /// when called multiple times (deterministic).
    /// </summary>
    [Fact]
    public void Serialize_IsDeterministic()
    {
        ImageInfoGenerators.ImageArtifactDetails.Sample(oldDetails =>
        {
            V2.ImageArtifactDetails v2Details = ConvertToV2(oldDetails);
            string json1 = ImageInfoSerializer.Serialize(v2Details);
            string json2 = ImageInfoSerializer.Serialize(v2Details);

            json2.ShouldBe(json1);
        });
    }

    /// <summary>
    /// Converts old mutable ImageArtifactDetails to new V2 immutable records.
    /// This is a faithful conversion used only for testing equivalence.
    /// </summary>
    private static V2.ImageArtifactDetails ConvertToV2(ImageArtifactDetails old) =>
        new()
        {
            Repos = old.Repos.Select(ConvertRepo).ToList(),
        };

    private static V2.RepoData ConvertRepo(RepoData old) =>
        new()
        {
            Repo = old.Repo,
            Images = old.Images.Select(ConvertImage).ToList(),
        };

    private static V2.ImageData ConvertImage(ImageData old) =>
        new()
        {
            ProductVersion = old.ProductVersion,
            Manifest = old.Manifest is not null ? ConvertManifest(old.Manifest) : null,
            Platforms = old.Platforms.Select(ConvertPlatform).ToList(),
        };

    private static V2.ManifestData ConvertManifest(ManifestData old) =>
        new()
        {
            Digest = old.Digest,
            Created = old.Created,
            SharedTags = old.SharedTags?.ToList() ?? [],
            SyndicatedDigests = old.SyndicatedDigests?.ToList() ?? [],
        };

    private static V2.PlatformData ConvertPlatform(PlatformData old) =>
        new()
        {
            Dockerfile = old.Dockerfile,
            SimpleTags = old.SimpleTags?.ToList() ?? [],
            Digest = old.Digest,
            BaseImageDigest = old.BaseImageDigest,
            OsType = old.OsType,
            OsVersion = old.OsVersion,
            Architecture = old.Architecture,
            Created = old.Created,
            CommitUrl = old.CommitUrl,
            Layers = old.Layers?.Select(layer => new V2.Layer(layer.Digest, layer.Size)).ToList() ?? [],
            IsUnchanged = old.IsUnchanged,
        };
}
