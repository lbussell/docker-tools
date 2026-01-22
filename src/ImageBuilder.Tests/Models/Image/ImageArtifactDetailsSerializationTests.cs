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
/// Serialization and deserialization tests for <see cref="ImageArtifactDetails"/> model.
/// These tests ensure that serialization behavior does not change unexpectedly.
/// </summary>
public class ImageArtifactDetailsSerializationTests
{
    [Fact]
    public void DefaultImageArtifactDetails_Bidirectional()
    {
        ImageArtifactDetails details = new();

        // SchemaVersion is always "2.0" (read-only)
        // STJ serializes empty lists
        string json = """
            {
              "schemaVersion": "2.0",
              "repos": []
            }
            """;

        AssertBidirectional(details, json, AssertImageArtifactDetailsEqual);
    }

    [Fact]
    public void FullyPopulatedImageArtifactDetails_Bidirectional()
    {
        ImageArtifactDetails details = new()
        {
            Repos =
            [
                new RepoData
                {
                    Repo = "dotnet/runtime",
                    Images =
                    [
                        new ImageData
                        {
                            ProductVersion = "8.0.5",
                            Manifest = new ManifestData
                            {
                                Digest = "sha256:manifest1",
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
                                    CommitUrl = "https://github.com/dotnet/dotnet-docker/commit/abc123",
                                    Layers =
                                    [
                                        new Layer("sha256:layer1", 1000),
                                        new Layer("sha256:layer2", 2000)
                                    ]
                                }
                            ]
                        }
                    ]
                },
                new RepoData
                {
                    Repo = "dotnet/aspnet",
                    Images =
                    [
                        new ImageData
                        {
                            ProductVersion = "8.0.5",
                            Platforms =
                            [
                                new PlatformData
                                {
                                    Dockerfile = "src/aspnet/8.0/jammy/amd64/Dockerfile",
                                    SimpleTags = ["8.0-jammy-amd64"],
                                    Digest = "sha256:aspnet1",
                                    OsType = "Linux",
                                    OsVersion = "jammy",
                                    Architecture = "amd64",
                                    CommitUrl = "https://github.com/dotnet/dotnet-docker/commit/def456"
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        string json = """
            {
              "schemaVersion": "2.0",
              "repos": [
                {
                  "repo": "dotnet/runtime",
                  "images": [
                    {
                      "productVersion": "8.0.5",
                      "manifest": {
                        "digest": "sha256:manifest1",
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
                          "layers": [
                            {
                              "digest": "sha256:layer1",
                              "size": 1000
                            },
                            {
                              "digest": "sha256:layer2",
                              "size": 2000
                            }
                          ]
                        }
                      ]
                    }
                  ]
                },
                {
                  "repo": "dotnet/aspnet",
                  "images": [
                    {
                      "productVersion": "8.0.5",
                      "platforms": [
                        {
                          "dockerfile": "src/aspnet/8.0/jammy/amd64/Dockerfile",
                          "simpleTags": [
                            "8.0-jammy-amd64"
                          ],
                          "digest": "sha256:aspnet1",
                          "osType": "Linux",
                          "osVersion": "jammy",
                          "architecture": "amd64",
                          "created": "0001-01-01T00:00:00",
                          "commitUrl": "https://github.com/dotnet/dotnet-docker/commit/def456",
                          "layers": []
                        }
                      ]
                    }
                  ]
                }
              ]
            }
            """;

        AssertBidirectional(details, json, AssertImageArtifactDetailsEqual);
    }

    [Fact]
    public void FullyPopulatedImageArtifactDetails_RoundTrip()
    {
        ImageArtifactDetails details = new()
        {
            Repos =
            [
                new RepoData
                {
                    Repo = "dotnet/sdk",
                    Images =
                    [
                        new ImageData
                        {
                            ProductVersion = "9.0.0-preview.1",
                            Platforms =
                            [
                                new PlatformData
                                {
                                    Dockerfile = "src/sdk/Dockerfile",
                                    Digest = "sha256:sdk",
                                    OsType = "Linux",
                                    OsVersion = "alpine3.19",
                                    Architecture = "amd64",
                                    CommitUrl = "https://github.com/example/commit"
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        AssertRoundTrip(details, AssertImageArtifactDetailsEqual);
    }

    [Fact]
    public void SchemaVersion_AlwaysSerializedAs2Point0()
    {
        ImageArtifactDetails details = new();

        // SchemaVersion should always be "2.0" regardless of input
        // STJ serializes empty lists
        string json = """
            {
              "schemaVersion": "2.0",
              "repos": []
            }
            """;

        AssertSerialization(details, json);
    }

    [Fact]
    public void Deserialization_SchemaVersionIsIgnored()
    {
        // Even if JSON has a different schema version, it should deserialize
        // (SchemaVersion is read-only with a constant value)
        string json = """
            {
              "schemaVersion": "1.0",
              "repos": []
            }
            """;

        ImageArtifactDetails expected = new();
        AssertDeserialization(json, expected, AssertImageArtifactDetailsEqual);
    }

    private static void AssertImageArtifactDetailsEqual(ImageArtifactDetails expected, ImageArtifactDetails actual)
    {
        Assert.Equal(expected.SchemaVersion, actual.SchemaVersion);
        Assert.Equal("2.0", actual.SchemaVersion); // Always "2.0"
        Assert.Equal(expected.Repos.Count, actual.Repos.Count);
        for (int i = 0; i < expected.Repos.Count; i++)
        {
            Assert.Equal(expected.Repos[i].Repo, actual.Repos[i].Repo);
            Assert.Equal(expected.Repos[i].Images.Count, actual.Repos[i].Images.Count);
        }
    }
}
