// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections.Generic;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Models.Services;
using Shouldly;
using Xunit;
using ManifestImage = Microsoft.DotNet.ImageBuilder.Models.Manifest.Image;

namespace Microsoft.DotNet.ImageBuilder.Tests.Models.Services;

public class ManifestLoaderTests
{
    private readonly ManifestLoader _loader = new();

    [Fact]
    public void LoadFromJson_MinimalManifest_Succeeds()
    {
        string json = """
            {
              "repos": [
                {
                  "name": "dotnet/runtime",
                  "images": [
                    {
                      "platforms": [
                        {
                          "dockerfile": "Dockerfile",
                          "os": "Linux",
                          "osVersion": "bookworm-slim",
                          "architecture": "Amd64",
                          "tags": {}
                        }
                      ]
                    }
                  ]
                }
              ]
            }
            """;

        Manifest manifest = _loader.LoadFromJson(json);

        manifest.ShouldNotBeNull();
        manifest.Repos.ShouldNotBeNull();
        manifest.Repos.Length.ShouldBe(1);
        manifest.Repos[0].Name.ShouldBe("dotnet/runtime");
        manifest.Repos[0].Images.Length.ShouldBe(1);
        manifest.Repos[0].Images[0].Platforms.Length.ShouldBe(1);
        manifest.Repos[0].Images[0].Platforms[0].Dockerfile.ShouldBe("Dockerfile");
    }

    [Fact]
    public void LoadFromJson_FullManifest_Succeeds()
    {
        string json = """
            {
              "registry": "mcr.microsoft.com",
              "repos": [
                {
                  "id": "runtime",
                  "name": "dotnet/runtime",
                  "mcrTagsMetadataTemplate": "engconn/dotnet-runtime-tags-metadata-tmpl",
                  "images": [
                    {
                      "productVersion": "8.0",
                      "sharedTags": {
                        "8.0": {}
                      },
                      "platforms": [
                        {
                          "dockerfile": "8.0/runtime/linux/amd64/Dockerfile",
                          "os": "Linux",
                          "osVersion": "bookworm-slim",
                          "architecture": "Amd64",
                          "tags": {
                            "8.0-bookworm-slim": {}
                          }
                        }
                      ]
                    }
                  ]
                }
              ],
              "variables": {
                "baseUrl": "https://dotnetcli.azureedge.net"
              }
            }
            """;

        Manifest manifest = _loader.LoadFromJson(json);

        manifest.ShouldNotBeNull();
        manifest.Registry.ShouldBe("mcr.microsoft.com");
        manifest.Repos[0].Id.ShouldBe("runtime");
        manifest.Repos[0].McrTagsMetadataTemplate.ShouldBe("engconn/dotnet-runtime-tags-metadata-tmpl");
        manifest.Repos[0].Images[0].ProductVersion.ShouldBe("8.0");
        manifest.Repos[0].Images[0].SharedTags.ShouldNotBeNull();
        manifest.Repos[0].Images[0].SharedTags!.ContainsKey("8.0").ShouldBeTrue();
        manifest.Variables.ShouldContainKeyAndValue("baseUrl", "https://dotnetcli.azureedge.net");
    }

    [Fact]
    public void ToJson_MinimalManifest_ProducesValidJson()
    {
        Manifest manifest = new()
        {
            Repos =
            [
                new Repo
                {
                    Name = "dotnet/runtime",
                    Images =
                    [
                        new ManifestImage
                        {
                            Platforms =
                            [
                                new Platform
                                {
                                    Dockerfile = "Dockerfile",
                                    OS = OS.Linux,
                                    OsVersion = "bookworm-slim",
                                    Architecture = Architecture.AMD64,
                                    Tags = new Dictionary<string, Tag>()
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        string json = _loader.ToJson(manifest);

        // Verify round-trip
        Manifest roundTripped = _loader.LoadFromJson(json);
        roundTripped.Repos.Length.ShouldBe(1);
        roundTripped.Repos[0].Name.ShouldBe("dotnet/runtime");
    }

    [Fact]
    public void RoundTrip_PreservesData()
    {
        Manifest original = new()
        {
            Registry = "mcr.microsoft.com",
            Repos =
            [
                new Repo
                {
                    Id = "runtime",
                    Name = "dotnet/runtime",
                    Images =
                    [
                        new ManifestImage
                        {
                            ProductVersion = "8.0",
                            Platforms =
                            [
                                new Platform
                                {
                                    Dockerfile = "8.0/runtime/linux/amd64/Dockerfile",
                                    OS = OS.Linux,
                                    OsVersion = "bookworm-slim",
                                    Architecture = Architecture.AMD64,
                                    Tags = new Dictionary<string, Tag>()
                                }
                            ]
                        }
                    ]
                }
            ],
            Variables = new Dictionary<string, string>
            {
                ["key1"] = "value1",
                ["key2"] = "value2"
            }
        };

        string json = _loader.ToJson(original);
        Manifest roundTripped = _loader.LoadFromJson(json);

        roundTripped.Registry.ShouldBe(original.Registry);
        roundTripped.Repos.Length.ShouldBe(1);
        roundTripped.Repos[0].Id.ShouldBe("runtime");
        roundTripped.Repos[0].Name.ShouldBe("dotnet/runtime");
        roundTripped.Repos[0].Images[0].ProductVersion.ShouldBe("8.0");
        roundTripped.Variables.Count.ShouldBe(2);
    }
}
