// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Models.Services;
using Shouldly;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests.Models.Services;

public class ImageArtifactDetailsLoaderTests
{
    private readonly ImageArtifactDetailsLoader _loader = new();

    [Fact]
    public void LoadFromJson_MinimalImageArtifactDetails_Succeeds()
    {
        string json = """
            {
              "repos": []
            }
            """;

        ImageArtifactDetails details = _loader.LoadFromJson(json);

        details.ShouldNotBeNull();
        details.Repos.ShouldNotBeNull();
        details.Repos.Count.ShouldBe(0);
    }

    [Fact]
    public void LoadFromJson_FullImageArtifactDetails_Succeeds()
    {
        string json = """
            {
              "repos": [
                {
                  "repo": "dotnet/runtime",
                  "images": [
                    {
                      "productVersion": "8.0",
                      "platforms": [
                        {
                          "dockerfile": "8.0/runtime/linux/amd64/Dockerfile",
                          "simpleTags": ["8.0-bookworm-slim"],
                          "digest": "sha256:abc123",
                          "baseImageDigest": "sha256:base123",
                          "osType": "Linux",
                          "osVersion": "bookworm-slim",
                          "architecture": "amd64",
                          "commitUrl": "https://github.com/dotnet/dotnet-docker/commit/abc123",
                          "layers": [
                            { "digest": "sha256:layer1", "size": 1024 },
                            { "digest": "sha256:layer2", "size": 2048 }
                          ]
                        }
                      ],
                      "manifest": {
                        "digest": "sha256:manifest123",
                        "sharedTags": ["8.0", "latest"]
                      }
                    }
                  ]
                }
              ]
            }
            """;

        ImageArtifactDetails details = _loader.LoadFromJson(json);

        details.ShouldNotBeNull();
        details.Repos.Count.ShouldBe(1);
        details.Repos[0].Repo.ShouldBe("dotnet/runtime");
        details.Repos[0].Images.Count.ShouldBe(1);

        ImageData image = details.Repos[0].Images[0];
        image.ProductVersion.ShouldBe("8.0");
        image.Platforms.Count.ShouldBe(1);
        image.Manifest.ShouldNotBeNull();
        image.Manifest!.Digest.ShouldBe("sha256:manifest123");

        PlatformData platform = image.Platforms[0];
        platform.Dockerfile.ShouldBe("8.0/runtime/linux/amd64/Dockerfile");
        platform.Digest.ShouldBe("sha256:abc123");
        platform.BaseImageDigest.ShouldBe("sha256:base123");
        platform.SimpleTags.ShouldContain("8.0-bookworm-slim");
        platform.Layers.Count.ShouldBe(2);
        platform.Layers[0].Digest.ShouldBe("sha256:layer1");
        platform.Layers[0].Size.ShouldBe(1024);
    }

    [Fact]
    public void LoadFromJson_SchemaVersion1Layers_ConvertsToVersion2()
    {
        // Schema version 1 stored layers as just digest strings
        string json = """
            {
              "repos": [
                {
                  "repo": "dotnet/runtime",
                  "images": [
                    {
                      "platforms": [
                        {
                          "dockerfile": "Dockerfile",
                          "simpleTags": [],
                          "digest": "sha256:abc123",
                          "osType": "Linux",
                          "osVersion": "bookworm-slim",
                          "architecture": "amd64",
                          "commitUrl": "https://github.com/dotnet/dotnet-docker/commit/abc123",
                          "layers": [
                            "sha256:oldlayer1",
                            "sha256:oldlayer2"
                          ]
                        }
                      ]
                    }
                  ]
                }
              ]
            }
            """;

        ImageArtifactDetails details = _loader.LoadFromJson(json);

        PlatformData platform = details.Repos[0].Images[0].Platforms[0];
        platform.Layers.Count.ShouldBe(2);

        // Schema version 1 layers should be converted to Layer objects with size 0
        platform.Layers[0].Digest.ShouldBe("sha256:oldlayer1");
        platform.Layers[0].Size.ShouldBe(0);
        platform.Layers[1].Digest.ShouldBe("sha256:oldlayer2");
        platform.Layers[1].Size.ShouldBe(0);
    }

    [Fact]
    public void ToJson_FullImageArtifactDetails_ProducesValidJson()
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
                            ProductVersion = "8.0",
                            Platforms =
                            [
                                new PlatformData
                                {
                                    Dockerfile = "8.0/runtime/linux/amd64/Dockerfile",
                                    SimpleTags = ["8.0-bookworm-slim"],
                                    Digest = "sha256:abc123",
                                    BaseImageDigest = "sha256:base123",
                                    OsType = "Linux",
                                    OsVersion = "bookworm-slim",
                                    Architecture = "amd64",
                                    CommitUrl = "https://github.com/dotnet/dotnet-docker/commit/abc123",
                                    Layers =
                                    [
                                        new Layer("sha256:layer1", 1024),
                                        new Layer("sha256:layer2", 2048)
                                    ]
                                }
                            ],
                            Manifest = new ManifestData
                            {
                                Digest = "sha256:manifest123",
                                SharedTags = ["8.0", "latest"]
                            }
                        }
                    ]
                }
            ]
        };

        string json = _loader.ToJson(details);

        // Verify round-trip
        ImageArtifactDetails roundTripped = _loader.LoadFromJson(json);
        roundTripped.Repos.Count.ShouldBe(1);
        roundTripped.Repos[0].Repo.ShouldBe("dotnet/runtime");
        roundTripped.Repos[0].Images[0].Platforms[0].Layers.Count.ShouldBe(2);
    }

    [Fact]
    public void RoundTrip_PreservesData()
    {
        ImageArtifactDetails original = new()
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
                            ProductVersion = "8.0",
                            Platforms =
                            [
                                new PlatformData
                                {
                                    Dockerfile = "8.0/runtime/linux/amd64/Dockerfile",
                                    SimpleTags = ["8.0-bookworm-slim", "8.0"],
                                    Digest = "sha256:abc123",
                                    BaseImageDigest = "sha256:base123",
                                    OsType = "Linux",
                                    OsVersion = "bookworm-slim",
                                    Architecture = "amd64",
                                    CommitUrl = "https://github.com/dotnet/dotnet-docker/commit/abc123",
                                    Layers =
                                    [
                                        new Layer("sha256:layer1", 1024)
                                    ]
                                }
                            ],
                            Manifest = new ManifestData
                            {
                                Digest = "sha256:manifest123",
                                SharedTags = ["8.0"],
                                SyndicatedDigests =
                                [
                                    "sha256:syndicated1"
                                ]
                            }
                        }
                    ]
                }
            ]
        };

        string json = _loader.ToJson(original);
        ImageArtifactDetails roundTripped = _loader.LoadFromJson(json);

        roundTripped.Repos.Count.ShouldBe(1);
        RepoData repo = roundTripped.Repos[0];
        repo.Repo.ShouldBe("dotnet/runtime");

        ImageData image = repo.Images[0];
        image.ProductVersion.ShouldBe("8.0");
        image.Manifest.ShouldNotBeNull();
        image.Manifest!.Digest.ShouldBe("sha256:manifest123");
        image.Manifest.SyndicatedDigests.ShouldContain("sha256:syndicated1");

        PlatformData platform = image.Platforms[0];
        platform.SimpleTags.Count.ShouldBe(2);
        platform.SimpleTags.ShouldContain("8.0-bookworm-slim");
        platform.SimpleTags.ShouldContain("8.0");
        platform.Layers.Count.ShouldBe(1);
        platform.Layers[0].Digest.ShouldBe("sha256:layer1");
        platform.Layers[0].Size.ShouldBe(1024);
    }
}
