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
/// Serialization and deserialization tests for <see cref="ImageData"/> model.
/// These tests ensure that serialization behavior does not change unexpectedly.
/// </summary>
public class ImageDataSerializationTests
{
    [Fact]
    public void DefaultImageData_Bidirectional()
    {
        ImageData image = new();

        // STJ serializes empty lists
        string json = """
            {
              "platforms": []
            }
            """;

        AssertBidirectional(image, json, AssertImageDataEqual);
    }

    [Fact]
    public void FullyPopulatedImageData_Bidirectional()
    {
        ImageData image = new()
        {
            ProductVersion = "8.0.5",
            Manifest = new ManifestData
            {
                Digest = "sha256:manifest123",
                SharedTags = ["8.0", "latest"],
                Created = new DateTime(2024, 6, 15, 10, 0, 0, DateTimeKind.Utc)
            },
            Platforms =
            [
                new PlatformData
                {
                    Dockerfile = "src/runtime/8.0/jammy/amd64/Dockerfile",
                    SimpleTags = ["8.0-jammy-amd64"],
                    Digest = "sha256:platform1",
                    OsType = "Linux",
                    OsVersion = "jammy",
                    Architecture = "amd64",
                    Created = new DateTime(2024, 6, 15, 9, 0, 0, DateTimeKind.Utc),
                    CommitUrl = "https://github.com/dotnet/dotnet-docker/commit/abc123"
                },
                new PlatformData
                {
                    Dockerfile = "src/runtime/8.0/jammy/arm64v8/Dockerfile",
                    SimpleTags = ["8.0-jammy-arm64v8"],
                    Digest = "sha256:platform2",
                    OsType = "Linux",
                    OsVersion = "jammy",
                    Architecture = "arm64",
                    Created = new DateTime(2024, 6, 15, 9, 30, 0, DateTimeKind.Utc),
                    CommitUrl = "https://github.com/dotnet/dotnet-docker/commit/def456"
                }
            ]
        };

        string json = """
            {
              "productVersion": "8.0.5",
              "manifest": {
                "digest": "sha256:manifest123",
                "syndicatedDigests": [],
                "created": "2024-06-15T10:00:00Z",
                "sharedTags": [
                  "8.0",
                  "latest"
                ]
              },
              "platforms": [
                {
                  "dockerfile": "src/runtime/8.0/jammy/amd64/Dockerfile",
                  "simpleTags": [
                    "8.0-jammy-amd64"
                  ],
                  "digest": "sha256:platform1",
                  "osType": "Linux",
                  "osVersion": "jammy",
                  "architecture": "amd64",
                  "created": "2024-06-15T09:00:00Z",
                  "commitUrl": "https://github.com/dotnet/dotnet-docker/commit/abc123",
                  "layers": []
                },
                {
                  "dockerfile": "src/runtime/8.0/jammy/arm64v8/Dockerfile",
                  "simpleTags": [
                    "8.0-jammy-arm64v8"
                  ],
                  "digest": "sha256:platform2",
                  "osType": "Linux",
                  "osVersion": "jammy",
                  "architecture": "arm64",
                  "created": "2024-06-15T09:30:00Z",
                  "commitUrl": "https://github.com/dotnet/dotnet-docker/commit/def456",
                  "layers": []
                }
              ]
            }
            """;

        AssertBidirectional(image, json, AssertImageDataEqual);
    }

    [Fact]
    public void FullyPopulatedImageData_RoundTrip()
    {
        ImageData image = new()
        {
            ProductVersion = "9.0.0-preview.1",
            Manifest = new ManifestData
            {
                Digest = "sha256:roundtrip",
                SharedTags = ["9.0-preview"],
                Created = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            Platforms =
            [
                new PlatformData
                {
                    Dockerfile = "src/Dockerfile",
                    SimpleTags = ["preview"],
                    Digest = "sha256:platform",
                    OsType = "Linux",
                    OsVersion = "alpine3.19",
                    Architecture = "amd64",
                    CommitUrl = "https://github.com/example/commit"
                }
            ]
        };

        AssertRoundTrip(image, AssertImageDataEqual);
    }

    [Fact]
    public void ImageData_WithoutManifest_Bidirectional()
    {
        ImageData image = new()
        {
            ProductVersion = "8.0.5",
            Platforms =
            [
                new PlatformData
                {
                    Dockerfile = "src/Dockerfile",
                    Digest = "sha256:single",
                    OsType = "Linux",
                    OsVersion = "jammy",
                    Architecture = "amd64",
                    CommitUrl = "https://github.com/example/commit"
                }
            ]
        };

        // STJ serializes empty lists and default DateTime values
        string json = """
            {
              "productVersion": "8.0.5",
              "platforms": [
                {
                  "dockerfile": "src/Dockerfile",
                  "simpleTags": [],
                  "digest": "sha256:single",
                  "osType": "Linux",
                  "osVersion": "jammy",
                  "architecture": "amd64",
                  "created": "0001-01-01T00:00:00",
                  "commitUrl": "https://github.com/example/commit",
                  "layers": []
                }
              ]
            }
            """;

        AssertBidirectional(image, json, AssertImageDataEqual);
    }

    [Fact]
    public void ImageData_WithoutProductVersion_Bidirectional()
    {
        ImageData image = new()
        {
            Manifest = new ManifestData
            {
                Digest = "sha256:noproductversion",
                SharedTags = ["latest"]
            },
            Platforms =
            [
                new PlatformData
                {
                    Dockerfile = "src/Dockerfile",
                    Digest = "sha256:platform",
                    OsType = "Linux",
                    OsVersion = "jammy",
                    Architecture = "amd64",
                    CommitUrl = "https://github.com/example/commit"
                }
            ]
        };

        // STJ serializes empty lists and default DateTime values
        string json = """
            {
              "manifest": {
                "digest": "sha256:noproductversion",
                "syndicatedDigests": [],
                "created": "0001-01-01T00:00:00",
                "sharedTags": [
                  "latest"
                ]
              },
              "platforms": [
                {
                  "dockerfile": "src/Dockerfile",
                  "simpleTags": [],
                  "digest": "sha256:platform",
                  "osType": "Linux",
                  "osVersion": "jammy",
                  "architecture": "amd64",
                  "created": "0001-01-01T00:00:00",
                  "commitUrl": "https://github.com/example/commit",
                  "layers": []
                }
              ]
            }
            """;

        AssertBidirectional(image, json, AssertImageDataEqual);
    }

    private static void AssertImageDataEqual(ImageData expected, ImageData actual)
    {
        Assert.Equal(expected.ProductVersion, actual.ProductVersion);

        if (expected.Manifest is null)
        {
            Assert.Null(actual.Manifest);
        }
        else
        {
            Assert.NotNull(actual.Manifest);
            Assert.Equal(expected.Manifest.Digest, actual.Manifest.Digest);
            Assert.Equal(expected.Manifest.SharedTags, actual.Manifest.SharedTags);
            Assert.Equal(expected.Manifest.SyndicatedDigests, actual.Manifest.SyndicatedDigests);
            Assert.Equal(expected.Manifest.Created, actual.Manifest.Created);
        }

        Assert.Equal(expected.Platforms.Count, actual.Platforms.Count);
        for (int i = 0; i < expected.Platforms.Count; i++)
        {
            Assert.Equal(expected.Platforms[i].Dockerfile, actual.Platforms[i].Dockerfile);
            Assert.Equal(expected.Platforms[i].Digest, actual.Platforms[i].Digest);
            Assert.Equal(expected.Platforms[i].OsType, actual.Platforms[i].OsType);
            Assert.Equal(expected.Platforms[i].OsVersion, actual.Platforms[i].OsVersion);
            Assert.Equal(expected.Platforms[i].Architecture, actual.Platforms[i].Architecture);
        }
    }
}
