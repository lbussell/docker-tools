// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using CsCheck;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Tests.Generators;
using Shouldly;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests.PropertyTests;

/// <summary>
/// Property-based tests that lock down current serialization round-trip behavior.
/// These serve as a safety net before any refactoring of the image-info models.
/// </summary>
public class SerializationPropertyTests
{
    /// <summary>
    /// For any generated ImageArtifactDetails, serializing then deserializing
    /// produces output that serializes identically to the original.
    /// </summary>
    [Fact]
    public void RoundTrip_SerializeThenDeserialize_ProducesSemanticallyIdenticalOutput()
    {
        ImageInfoGenerators.ImageArtifactDetails.Sample(original =>
        {
            string json = JsonHelper.SerializeObject(original);
            ImageArtifactDetails deserialized = ImageArtifactDetails.FromJson(json);
            string roundTrippedJson = JsonHelper.SerializeObject(deserialized);

            roundTrippedJson.ShouldBe(json);
        });
    }

    /// <summary>
    /// Serializing the same object twice produces identical JSON output.
    /// </summary>
    [Fact]
    public void Serialization_IsDeterministic()
    {
        ImageInfoGenerators.ImageArtifactDetails.Sample(details =>
        {
            string json1 = JsonHelper.SerializeObject(details);
            string json2 = JsonHelper.SerializeObject(details);

            json2.ShouldBe(json1);
        });
    }

    /// <summary>
    /// SchemaVersion is always "2.0" in the serialized output.
    /// </summary>
    [Fact]
    public void Serialization_AlwaysOutputsSchemaVersion2()
    {
        ImageInfoGenerators.ImageArtifactDetails.Sample(details =>
        {
            string json = JsonHelper.SerializeObject(details);
            json.ShouldContain("\"schemaVersion\": \"2.0\"");
        });
    }

    /// <summary>
    /// Required fields on PlatformData are always present in serialized output,
    /// even when their values are empty strings.
    /// </summary>
    [Fact]
    public void Serialization_RequiredFieldsAlwaysPresent()
    {
        ImageInfoGenerators.PlatformData.Sample(platform =>
        {
            string json = JsonHelper.SerializeObject(platform);

            json.ShouldContain("\"dockerfile\":");
            json.ShouldContain("\"digest\":");
            json.ShouldContain("\"osType\":");
            json.ShouldContain("\"osVersion\":");
            json.ShouldContain("\"architecture\":");
            json.ShouldContain("\"commitUrl\":");
        });
    }

    /// <summary>
    /// Optional null fields are omitted from serialized output.
    /// </summary>
    [Fact]
    public void Serialization_NullOptionalFieldsAreOmitted()
    {
        PlatformData platform = new()
        {
            Dockerfile = "src/Dockerfile",
            Digest = "dotnet/runtime@sha256:abc123",
            OsType = "Linux",
            OsVersion = "noble",
            Architecture = "amd64",
            CommitUrl = "https://example.com/commit/1",
            BaseImageDigest = null,
        };

        string json = JsonHelper.SerializeObject(platform);
        json.ShouldNotContain("\"baseImageDigest\":");
    }

    /// <summary>
    /// Empty lists on non-required properties are omitted from serialized output.
    /// </summary>
    [Fact]
    public void Serialization_EmptyListsAreOmitted()
    {
        PlatformData platform = new()
        {
            Dockerfile = "src/Dockerfile",
            Digest = "dotnet/runtime@sha256:abc123",
            OsType = "Linux",
            OsVersion = "noble",
            Architecture = "amd64",
            CommitUrl = "https://example.com/commit/1",
            SimpleTags = [],
            Layers = [],
        };

        string json = JsonHelper.SerializeObject(platform);
        json.ShouldNotContain("\"simpleTags\":");
        json.ShouldNotContain("\"layers\":");
    }

    /// <summary>
    /// Properties use camelCase naming in the serialized JSON output.
    /// </summary>
    [Fact]
    public void Serialization_UsesCamelCasePropertyNames()
    {
        ImageInfoGenerators.ImageArtifactDetails.Sample(details =>
        {
            string json = JsonHelper.SerializeObject(details);

            // camelCase properties should be present
            json.ShouldContain("\"schemaVersion\":");
            json.ShouldContain("\"repos\":");

            // PascalCase should not appear (case-sensitive check)
            json.Contains("\"SchemaVersion\":", StringComparison.Ordinal).ShouldBeFalse();
            json.Contains("\"Repos\":", StringComparison.Ordinal).ShouldBeFalse();
        });
    }

    /// <summary>
    /// IsUnchanged defaults to false and is omitted from serialized output when false.
    /// </summary>
    [Fact]
    public void Serialization_IsUnchangedFalse_IsOmitted()
    {
        PlatformData platform = new()
        {
            Dockerfile = "src/Dockerfile",
            Digest = "dotnet/runtime@sha256:abc123",
            OsType = "Linux",
            OsVersion = "noble",
            Architecture = "amd64",
            CommitUrl = "https://example.com/commit/1",
            IsUnchanged = false,
        };

        string json = JsonHelper.SerializeObject(platform);
        json.ShouldNotContain("\"isUnchanged\":");
    }

    /// <summary>
    /// IsUnchanged is included in serialized output when true.
    /// </summary>
    [Fact]
    public void Serialization_IsUnchangedTrue_IsIncluded()
    {
        PlatformData platform = new()
        {
            Dockerfile = "src/Dockerfile",
            Digest = "dotnet/runtime@sha256:abc123",
            OsType = "Linux",
            OsVersion = "noble",
            Architecture = "amd64",
            CommitUrl = "https://example.com/commit/1",
            IsUnchanged = true,
        };

        string json = JsonHelper.SerializeObject(platform);
        json.ShouldContain("\"isUnchanged\": true");
    }
}
