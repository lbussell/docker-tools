// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CsCheck;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Services;
using Microsoft.DotNet.ImageBuilder.Tests.Generators;
using Shouldly;
using Xunit;
using V2 = Microsoft.DotNet.ImageBuilder.Models.Image.V2;

namespace Microsoft.DotNet.ImageBuilder.Tests.PropertyTests;

/// <summary>
/// Differential characterization tests verifying that the new <see cref="ImageInfoSerializer"/>
/// produces output equivalent to the old serialization path.
/// </summary>
public class SerializerMigrationPropertyTests
{
    /// <summary>
    /// For any generated V2 data, serializing with the new serializer produces
    /// the same JSON as deserializing that JSON into old models and re-serializing.
    /// </summary>
    [Fact]
    public void Serialize_V2Records_MatchesOldSerialization()
    {
        ImageInfoGenerators.ImageArtifactDetails.Sample(v2Details =>
        {
            string newJson = ImageInfoSerializer.Serialize(v2Details);
            ImageArtifactDetails oldDetails = v2Details.ConvertToV1();
            string oldJson = JsonHelper.SerializeObject(oldDetails);

            newJson.ShouldBe(oldJson);
        });
    }

    /// <summary>
    /// Deserializing JSON produced by the new serializer into V2 records, then
    /// re-serializing with the new serializer, produces the same JSON.
    /// </summary>
    [Fact]
    public void RoundTrip_OldJsonThroughNewSerializer_ProducesSameJson()
    {
        ImageInfoGenerators.ImageArtifactDetails.Sample(v2Details =>
        {
            string originalJson = ImageInfoSerializer.Serialize(v2Details);
            V2.ImageArtifactDetails deserialized = ImageInfoSerializer.Deserialize(originalJson);
            string reserializedJson = ImageInfoSerializer.Serialize(deserialized);

            reserializedJson.ShouldBe(originalJson);
        });
    }

    /// <summary>
    /// Deserializing JSON into V2 records preserves all data fields.
    /// </summary>
    [Fact]
    public void Deserialize_PreservesAllFields()
    {
        ImageInfoGenerators.ImageArtifactDetails.Sample(original =>
        {
            string json = ImageInfoSerializer.Serialize(original);
            V2.ImageArtifactDetails v2Details = ImageInfoSerializer.Deserialize(json);

            v2Details.SchemaVersion.ShouldBe("2.0");
            v2Details.Repos.Count.ShouldBe(original.Repos.Count);

            for (int repoIdx = 0; repoIdx < original.Repos.Count; repoIdx++)
            {
                v2Details.Repos[repoIdx].Repo.ShouldBe(original.Repos[repoIdx].Repo);
                v2Details.Repos[repoIdx].Images.Count.ShouldBe(original.Repos[repoIdx].Images.Count);
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
        ImageInfoGenerators.ImageArtifactDetails.Sample(v2Details =>
        {
            string json1 = ImageInfoSerializer.Serialize(v2Details);
            string json2 = ImageInfoSerializer.Serialize(v2Details);

            json2.ShouldBe(json1);
        });
    }

}
