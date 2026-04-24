// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.ImageBuilder.Services;
using Shouldly;
using Xunit;
using V2 = Microsoft.DotNet.ImageBuilder.Models.Image.V2;

namespace Microsoft.DotNet.ImageBuilder.Tests.PropertyTests;

/// <summary>
/// Tests for <see cref="PlatformDataBuilder"/>.
/// </summary>
public class PlatformDataBuilderTests
{
    [Fact]
    public void Build_WithAllFields_ProducesCorrectRecord()
    {
        List<V2.Layer> layers = [new V2.Layer("sha256:abc", 100)];
        DateTime created = new(2026, 1, 15, 10, 30, 0);

        V2.PlatformData result = new PlatformDataBuilder(
                dockerfile: "src/8.0/noble/amd64/Dockerfile",
                architecture: "amd64",
                osType: "Linux",
                osVersion: "noble",
                commitUrl: "https://github.com/dotnet/dotnet-docker/commit/abc",
                simpleTags: ["8.0-noble-amd64"])
            .SetDigest("dotnet/runtime@sha256:abc123")
            .SetBaseImageDigest("sha256:base")
            .SetCreated(created)
            .SetLayers(layers)
            .SetIsUnchanged(true)
            .Build();

        result.Dockerfile.ShouldBe("src/8.0/noble/amd64/Dockerfile");
        result.Architecture.ShouldBe("amd64");
        result.OsType.ShouldBe("Linux");
        result.OsVersion.ShouldBe("noble");
        result.Digest.ShouldBe("dotnet/runtime@sha256:abc123");
        result.BaseImageDigest.ShouldBe("sha256:base");
        result.Created.ShouldBe(created);
        result.CommitUrl.ShouldBe("https://github.com/dotnet/dotnet-docker/commit/abc");
        result.SimpleTags.ShouldBe(["8.0-noble-amd64"]);
        result.Layers.ShouldBe(layers);
        result.IsUnchanged.ShouldBeTrue();
    }

    [Fact]
    public void Build_WithoutDigest_Throws()
    {
        PlatformDataBuilder builder = new(
            dockerfile: "src/Dockerfile",
            architecture: "amd64",
            osType: "Linux",
            osVersion: "noble",
            commitUrl: "https://example.com",
            simpleTags: []);

        Should.Throw<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void Build_ProducesImmutableRecord()
    {
        V2.PlatformData result = new PlatformDataBuilder(
                dockerfile: "src/Dockerfile",
                architecture: "amd64",
                osType: "Linux",
                osVersion: "noble",
                commitUrl: "https://example.com",
                simpleTags: ["tag"])
            .SetDigest("dotnet/runtime@sha256:abc")
            .Build();

        // Record should be usable with `with` expressions
        V2.PlatformData modified = result with { Digest = "dotnet/runtime@sha256:def" };
        modified.Digest.ShouldBe("dotnet/runtime@sha256:def");
        result.Digest.ShouldBe("dotnet/runtime@sha256:abc");
    }
}
