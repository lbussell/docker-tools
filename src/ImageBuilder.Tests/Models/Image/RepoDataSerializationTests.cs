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
/// Serialization and deserialization tests for <see cref="RepoData"/> model.
/// These tests ensure that serialization behavior does not change unexpectedly.
/// </summary>
public class RepoDataSerializationTests
{
    [Fact]
    public void DefaultRepoData_Bidirectional()
    {
        RepoData repo = new();

        // STJ serializes empty lists
        string json = """
            {
              "repo": "",
              "images": []
            }
            """;

        AssertBidirectional(repo, json, AssertRepoDataEqual);
    }

    [Fact]
    public void FullyPopulatedRepoData_Bidirectional()
    {
        RepoData repo = new()
        {
            Repo = "dotnet/runtime",
            Images =
            [
                new ImageData
                {
                    ProductVersion = "8.0.5",
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
                            CommitUrl = "https://github.com/dotnet/dotnet-docker/commit/abc123"
                        }
                    ]
                },
                new ImageData
                {
                    ProductVersion = "9.0.0-preview.1",
                    Platforms =
                    [
                        new PlatformData
                        {
                            Dockerfile = "src/runtime/9.0/jammy/amd64/Dockerfile",
                            SimpleTags = ["9.0-preview-jammy-amd64"],
                            Digest = "sha256:platform2",
                            OsType = "Linux",
                            OsVersion = "jammy",
                            Architecture = "amd64",
                            CommitUrl = "https://github.com/dotnet/dotnet-docker/commit/def456"
                        }
                    ]
                }
            ]
        };

        string json = """
            {
              "repo": "dotnet/runtime",
              "images": [
                {
                  "productVersion": "8.0.5",
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
                      "created": "0001-01-01T00:00:00",
                      "commitUrl": "https://github.com/dotnet/dotnet-docker/commit/abc123",
                      "layers": []
                    }
                  ]
                },
                {
                  "productVersion": "9.0.0-preview.1",
                  "platforms": [
                    {
                      "dockerfile": "src/runtime/9.0/jammy/amd64/Dockerfile",
                      "simpleTags": [
                        "9.0-preview-jammy-amd64"
                      ],
                      "digest": "sha256:platform2",
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
            """;

        AssertBidirectional(repo, json, AssertRepoDataEqual);
    }

    [Fact]
    public void FullyPopulatedRepoData_RoundTrip()
    {
        RepoData repo = new()
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
                            Dockerfile = "src/aspnet/Dockerfile",
                            Digest = "sha256:test",
                            OsType = "Linux",
                            OsVersion = "alpine3.18",
                            Architecture = "amd64",
                            CommitUrl = "https://github.com/example/commit"
                        }
                    ]
                }
            ]
        };

        AssertRoundTrip(repo, AssertRepoDataEqual);
    }

    [Fact]
    public void MinimalRepoData_Bidirectional()
    {
        RepoData repo = new()
        {
            Repo = "dotnet/sdk"
        };

        // STJ serializes empty lists
        string json = """
            {
              "repo": "dotnet/sdk",
              "images": []
            }
            """;

        AssertBidirectional(repo, json, AssertRepoDataEqual);
    }

    [Fact]
    public void Deserialization_RepoIsRequired_Missing()
    {
        string json = """
            {
              "images": []
            }
            """;

        AssertDeserializationFails<RepoData>(json, nameof(RepoData.Repo));
    }

    [Fact]
    public void Deserialization_RepoIsRequired_Null()
    {
        // STJ's [JsonRequired] only ensures the property is present in JSON,
        // it does NOT throw when the value is explicitly null.
        string json = """
            {
              "repo": null,
              "images": []
            }
            """;

        RepoData expected = new()
        {
            Repo = null!,  // STJ allows null for JsonRequired properties
            Images = []
        };

        AssertDeserialization(json, expected, AssertRepoDataEqual);
    }

    private static void AssertRepoDataEqual(RepoData expected, RepoData actual)
    {
        Assert.Equal(expected.Repo, actual.Repo);
        Assert.Equal(expected.Images.Count, actual.Images.Count);
        for (int i = 0; i < expected.Images.Count; i++)
        {
            Assert.Equal(expected.Images[i].ProductVersion, actual.Images[i].ProductVersion);
            Assert.Equal(expected.Images[i].Platforms.Count, actual.Images[i].Platforms.Count);
        }
    }
}
