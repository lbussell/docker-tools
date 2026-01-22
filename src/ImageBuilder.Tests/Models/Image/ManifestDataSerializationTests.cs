// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Xunit;
using static Microsoft.DotNet.ImageBuilder.Tests.Models.SerializationHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests.Models.Image;

/// <summary>
/// Serialization and deserialization tests for <see cref="ManifestData"/> model.
/// These tests ensure that serialization behavior does not change unexpectedly.
/// </summary>
public class ManifestDataSerializationTests
{
    [Fact]
    public void DefaultManifestData_Bidirectional()
    {
        ManifestData manifestData = new();

        // DateTime defaults to MinValue which is omitted due to DefaultValueHandling.Ignore
        // Empty lists are omitted for non-required properties
        string json = """
            {
              "digest": ""
            }
            """;

        AssertBidirectional(manifestData, json, AssertManifestDataEqual);
    }

    [Fact]
    public void FullyPopulatedManifestData_Bidirectional()
    {
        ManifestData manifestData = new()
        {
            Digest = "sha256:manifest123abc",
            SyndicatedDigests = ["sha256:syndicated1", "sha256:syndicated2"],
            Created = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc),
            SharedTags = ["8.0", "latest", "8.0-jammy"]
        };

        string json = """
            {
              "digest": "sha256:manifest123abc",
              "syndicatedDigests": [
                "sha256:syndicated1",
                "sha256:syndicated2"
              ],
              "created": "2024-06-15T10:30:00Z",
              "sharedTags": [
                "8.0",
                "latest",
                "8.0-jammy"
              ]
            }
            """;

        AssertBidirectional(manifestData, json, AssertManifestDataEqual);
    }

    [Fact]
    public void FullyPopulatedManifestData_RoundTrip()
    {
        ManifestData manifestData = new()
        {
            Digest = "sha256:roundtrip",
            SyndicatedDigests = ["sha256:syn1"],
            Created = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            SharedTags = ["tag1", "tag2"]
        };

        AssertRoundTrip(manifestData, AssertManifestDataEqual);
    }

    [Fact]
    public void MinimalManifestData_Bidirectional()
    {
        ManifestData manifestData = new()
        {
            Digest = "sha256:minimal"
        };

        // Empty lists are omitted, DateTime default is omitted
        string json = """
            {
              "digest": "sha256:minimal"
            }
            """;

        AssertBidirectional(manifestData, json, AssertManifestDataEqual);
    }

    private static void AssertManifestDataEqual(ManifestData expected, ManifestData actual)
    {
        Assert.Equal(expected.Digest, actual.Digest);
        Assert.Equal(expected.SyndicatedDigests, actual.SyndicatedDigests);
        Assert.Equal(expected.Created, actual.Created);
        Assert.Equal(expected.SharedTags, actual.SharedTags);
    }
}
