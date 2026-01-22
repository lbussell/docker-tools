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
/// Serialization and deserialization tests for <see cref="PlatformData"/> model.
/// These tests ensure that serialization behavior does not change unexpectedly.
/// </summary>
public class PlatformDataSerializationTests
{
    [Fact]
    public void DefaultPlatformData_Bidirectional()
    {
        PlatformData platform = new();

        // Default values for required string properties are empty strings
        // Empty lists are omitted for non-required properties
        // BaseImageDigest is null and omitted due to NullValueHandling.Ignore
        // IsUnchanged is false (default) and omitted due to DefaultValueHandling.Ignore
        // Created is DateTime.MinValue and omitted due to DefaultValueHandling.Ignore
        string json = """
            {
              "dockerfile": "",
              "digest": "",
              "osType": "",
              "osVersion": "",
              "architecture": "",
              "commitUrl": ""
            }
            """;

        AssertBidirectional(platform, json, AssertPlatformDataEqual);
    }

    [Fact]
    public void FullyPopulatedPlatformData_Bidirectional()
    {
        PlatformData platform = new()
        {
            Dockerfile = "src/runtime/8.0/jammy/amd64/Dockerfile",
            SimpleTags = ["8.0-jammy-amd64", "8.0-jammy"],
            Digest = "sha256:abc123def456789",
            BaseImageDigest = "sha256:baseimage123",
            OsType = "Linux",
            OsVersion = "jammy",
            Architecture = "amd64",
            Created = new DateTime(2024, 6, 15, 14, 30, 0, DateTimeKind.Utc),
            CommitUrl = "https://github.com/dotnet/dotnet-docker/commit/abc123",
            Layers =
            [
                new Layer("sha256:layer1", 1000),
                new Layer("sha256:layer2", 2000)
            ],
            IsUnchanged = true
        };

        string json = """
            {
              "dockerfile": "src/runtime/8.0/jammy/amd64/Dockerfile",
              "simpleTags": [
                "8.0-jammy-amd64",
                "8.0-jammy"
              ],
              "digest": "sha256:abc123def456789",
              "baseImageDigest": "sha256:baseimage123",
              "osType": "Linux",
              "osVersion": "jammy",
              "architecture": "amd64",
              "created": "2024-06-15T14:30:00Z",
              "commitUrl": "https://github.com/dotnet/dotnet-docker/commit/abc123",
              "layers": [
                {
                  "digest": "sha256:layer1",
                  "size": 1000
                },
                {
                  "digest": "sha256:layer2",
                  "size": 2000
                }
              ],
              "isUnchanged": true
            }
            """;

        AssertBidirectional(platform, json, AssertPlatformDataEqual);
    }

    [Fact]
    public void FullyPopulatedPlatformData_RoundTrip()
    {
        PlatformData platform = new()
        {
            Dockerfile = "src/Dockerfile",
            SimpleTags = ["latest"],
            Digest = "sha256:roundtrip",
            BaseImageDigest = "sha256:base",
            OsType = "Linux",
            OsVersion = "alpine3.18",
            Architecture = "arm64",
            Created = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CommitUrl = "https://github.com/example/repo/commit/123",
            Layers = [new Layer("sha256:single", 500)],
            IsUnchanged = false
        };

        AssertRoundTrip(platform, AssertPlatformDataEqual);
    }

    [Fact]
    public void MinimalPlatformData_Bidirectional()
    {
        PlatformData platform = new()
        {
            Dockerfile = "src/Dockerfile",
            Digest = "sha256:minimal",
            OsType = "Linux",
            OsVersion = "jammy",
            Architecture = "amd64",
            CommitUrl = "https://github.com/example/commit"
        };

        // BaseImageDigest is null and omitted
        // IsUnchanged is false (default) and omitted
        // Empty SimpleTags and Layers lists are omitted
        // Created is DateTime.MinValue and omitted
        string json = """
            {
              "dockerfile": "src/Dockerfile",
              "digest": "sha256:minimal",
              "osType": "Linux",
              "osVersion": "jammy",
              "architecture": "amd64",
              "commitUrl": "https://github.com/example/commit"
            }
            """;

        AssertBidirectional(platform, json, AssertPlatformDataEqual);
    }

    [Fact]
    public void WindowsPlatformData_Bidirectional()
    {
        PlatformData platform = new()
        {
            Dockerfile = "src/runtime/8.0/nanoserver-ltsc2022/amd64/Dockerfile",
            SimpleTags = ["8.0-nanoserver-ltsc2022"],
            Digest = "sha256:windows123",
            OsType = "Windows",
            OsVersion = "nanoserver-ltsc2022",
            Architecture = "amd64",
            Created = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc),
            CommitUrl = "https://github.com/dotnet/dotnet-docker/commit/win123"
        };

        // Empty Layers list is omitted
        string json = """
            {
              "dockerfile": "src/runtime/8.0/nanoserver-ltsc2022/amd64/Dockerfile",
              "simpleTags": [
                "8.0-nanoserver-ltsc2022"
              ],
              "digest": "sha256:windows123",
              "osType": "Windows",
              "osVersion": "nanoserver-ltsc2022",
              "architecture": "amd64",
              "created": "2024-06-15T12:00:00Z",
              "commitUrl": "https://github.com/dotnet/dotnet-docker/commit/win123"
            }
            """;

        AssertBidirectional(platform, json, AssertPlatformDataEqual);
    }

    [Fact]
    public void Deserialization_DockerfileIsRequired_Missing()
    {
        string json = """
            {
              "digest": "sha256:test",
              "osType": "Linux",
              "osVersion": "jammy",
              "architecture": "amd64",
              "commitUrl": "https://example.com"
            }
            """;

        AssertDeserializationFails<PlatformData>(json, nameof(PlatformData.Dockerfile));
    }

    [Fact]
    public void Deserialization_DigestIsRequired_Missing()
    {
        string json = """
            {
              "dockerfile": "src/Dockerfile",
              "osType": "Linux",
              "osVersion": "jammy",
              "architecture": "amd64",
              "commitUrl": "https://example.com"
            }
            """;

        AssertDeserializationFails<PlatformData>(json, nameof(PlatformData.Digest));
    }

    [Fact]
    public void Deserialization_OsTypeIsRequired_Missing()
    {
        string json = """
            {
              "dockerfile": "src/Dockerfile",
              "digest": "sha256:test",
              "osVersion": "jammy",
              "architecture": "amd64",
              "commitUrl": "https://example.com"
            }
            """;

        AssertDeserializationFails<PlatformData>(json, nameof(PlatformData.OsType));
    }

    [Fact]
    public void Deserialization_OsVersionIsRequired_Missing()
    {
        string json = """
            {
              "dockerfile": "src/Dockerfile",
              "digest": "sha256:test",
              "osType": "Linux",
              "architecture": "amd64",
              "commitUrl": "https://example.com"
            }
            """;

        AssertDeserializationFails<PlatformData>(json, nameof(PlatformData.OsVersion));
    }

    [Fact]
    public void Deserialization_ArchitectureIsRequired_Missing()
    {
        string json = """
            {
              "dockerfile": "src/Dockerfile",
              "digest": "sha256:test",
              "osType": "Linux",
              "osVersion": "jammy",
              "commitUrl": "https://example.com"
            }
            """;

        AssertDeserializationFails<PlatformData>(json, nameof(PlatformData.Architecture));
    }

    [Fact]
    public void Deserialization_CommitUrlIsRequired_Missing()
    {
        string json = """
            {
              "dockerfile": "src/Dockerfile",
              "digest": "sha256:test",
              "osType": "Linux",
              "osVersion": "jammy",
              "architecture": "amd64"
            }
            """;

        AssertDeserializationFails<PlatformData>(json, nameof(PlatformData.CommitUrl));
    }

    [Fact]
    public void IsUnchanged_OmittedWhenFalse()
    {
        PlatformData platform = new()
        {
            Dockerfile = "src/Dockerfile",
            Digest = "sha256:test",
            OsType = "Linux",
            OsVersion = "jammy",
            Architecture = "amd64",
            CommitUrl = "https://example.com",
            IsUnchanged = false
        };

        // IsUnchanged should NOT appear in serialized output when false
        // Empty SimpleTags and Layers lists are also omitted
        // Created is DateTime.MinValue and omitted
        string json = """
            {
              "dockerfile": "src/Dockerfile",
              "digest": "sha256:test",
              "osType": "Linux",
              "osVersion": "jammy",
              "architecture": "amd64",
              "commitUrl": "https://example.com"
            }
            """;

        AssertBidirectional(platform, json, AssertPlatformDataEqual);
    }

    [Fact]
    public void IsUnchanged_IncludedWhenTrue()
    {
        PlatformData platform = new()
        {
            Dockerfile = "src/Dockerfile",
            Digest = "sha256:test",
            OsType = "Linux",
            OsVersion = "jammy",
            Architecture = "amd64",
            CommitUrl = "https://example.com",
            IsUnchanged = true
        };

        // IsUnchanged SHOULD appear in serialized output when true
        // Empty SimpleTags and Layers lists are omitted
        // Created is DateTime.MinValue and omitted
        string json = """
            {
              "dockerfile": "src/Dockerfile",
              "digest": "sha256:test",
              "osType": "Linux",
              "osVersion": "jammy",
              "architecture": "amd64",
              "commitUrl": "https://example.com",
              "isUnchanged": true
            }
            """;

        AssertBidirectional(platform, json, AssertPlatformDataEqual);
    }

    private static void AssertPlatformDataEqual(PlatformData expected, PlatformData actual)
    {
        Assert.Equal(expected.Dockerfile, actual.Dockerfile);
        Assert.Equal(expected.SimpleTags, actual.SimpleTags);
        Assert.Equal(expected.Digest, actual.Digest);
        Assert.Equal(expected.BaseImageDigest, actual.BaseImageDigest);
        Assert.Equal(expected.OsType, actual.OsType);
        Assert.Equal(expected.OsVersion, actual.OsVersion);
        Assert.Equal(expected.Architecture, actual.Architecture);
        Assert.Equal(expected.Created, actual.Created);
        Assert.Equal(expected.CommitUrl, actual.CommitUrl);
        Assert.Equal(expected.Layers.Count, actual.Layers.Count);
        for (int i = 0; i < expected.Layers.Count; i++)
        {
            Assert.Equal(expected.Layers[i].Digest, actual.Layers[i].Digest);
            Assert.Equal(expected.Layers[i].Size, actual.Layers[i].Size);
        }
        Assert.Equal(expected.IsUnchanged, actual.IsUnchanged);
    }
}
