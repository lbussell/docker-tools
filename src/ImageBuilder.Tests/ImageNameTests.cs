// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CsCheck;
using Shouldly;
using Xunit;
using static Microsoft.DotNet.ImageBuilder.Tests.Generators.ImageNameGenerator;

namespace Microsoft.DotNet.ImageBuilder.Tests;

public sealed class ImageNameTests
{
    private const int PropertyIterationCount = 250;

    [Fact]
    public void Parse_PreservesTagAndDigestWhenBothArePresent()
    {
        Check.Sample(
            AnyImageNameWithTagAndDigest,
            imageName => ImageName.Parse(imageName.ToString()).ShouldBe(imageName),
            iter: PropertyIterationCount);
    }

    [Fact]
    public void Parse_AndToString_RoundTripWithoutAutoResolve()
    {
        Check.Sample(
            AnyImageName,
            imageName => ImageName.Parse(imageName.ToString()).ShouldBe(imageName),
            iter: PropertyIterationCount);
    }

    [Fact]
    public void Parse_WithAutoResolve_AddsDockerHubDefaultsForSingleSegmentRepos()
    {
        Check.Sample(
            Gen.Select(AnyImplicitSingleSegmentRepo, OptionalImageTag, OptionalImageDigest),
            input =>
            {
                string repo = input.Item1;
                string tag = input.Item2;
                string digest = input.Item3;
                string image = new ImageName(string.Empty, repo, tag, digest).ToString();
                ImageName parsed = ImageName.Parse(image, autoResolveImpliedNames: true);

                parsed.Registry.ShouldBe(DockerHelper.DockerHubRegistry);
                parsed.Repo.ShouldBe($"library/{repo}");
                parsed.Tag.ShouldBe(tag);
                parsed.Digest.ShouldBe(digest);
            },
            iter: PropertyIterationCount);
    }

    [Fact]
    public void Parse_WithAutoResolve_PreservesMultiSegmentDockerHubRepos()
    {
        Check.Sample(
            Gen.Select(AnyImplicitMultiSegmentRepo, OptionalImageTag, OptionalImageDigest),
            input =>
            {
                string repo = input.Item1;
                string tag = input.Item2;
                string digest = input.Item3;
                string image = new ImageName(string.Empty, repo, tag, digest).ToString();
                ImageName parsed = ImageName.Parse(image, autoResolveImpliedNames: true);

                parsed.Registry.ShouldBe(DockerHelper.DockerHubRegistry);
                parsed.Repo.ShouldBe(repo);
                parsed.Tag.ShouldBe(tag);
                parsed.Digest.ShouldBe(digest);
            },
            iter: PropertyIterationCount);
    }

    [Fact]
    public void Parse_WithAutoResolve_PreservesExplicitRegistries()
    {
        Check.Sample(
            Gen.Select(AnyRegistry, AnyRepo, OptionalImageTag, OptionalImageDigest),
            input =>
            {
                ImageName imageName = new ImageName(input.Item1, input.Item2, input.Item3, input.Item4);

                ImageName.Parse(imageName.ToString(), autoResolveImpliedNames: true).ShouldBe(imageName);
            },
            iter: PropertyIterationCount);
    }

    [Theory]
    [InlineData("localhost/myimage:latest", "localhost", "myimage", "latest", "")]
    [InlineData("localhost:5000/myimage:latest", "localhost:5000", "myimage", "latest", "")]
    [InlineData("127.0.0.1/myimage:latest", "127.0.0.1", "myimage", "latest", "")]
    [InlineData("[2001:db8::1]/myimage:latest", "[2001:db8::1]", "myimage", "latest", "")]
    [InlineData("[2001:db8::1]:5000/myimage:latest", "[2001:db8::1]:5000", "myimage", "latest", "")]
    [InlineData("EXAMPLE-1.com/myimage:latest", "EXAMPLE-1.com", "myimage", "latest", "")]
    public void Parse_SupportsSpecCompliantRegistryForms(string image, string registry, string repo, string tag, string digest)
    {
        ImageName parsed = ImageName.Parse(image);

        parsed.Registry.ShouldBe(registry);
        parsed.Repo.ShouldBe(repo);
        parsed.Tag.ShouldBe(tag);
        parsed.Digest.ShouldBe(digest);
    }

    [Fact]
    public void Parse_AndToString_RoundTripSupportsMaximumRepositoryLength()
    {
        string repo = new string('a', MaxRepositoryLength);
        ImageName imageName = new ImageName("example.com", repo, "latest", null);

        ImageName.Parse(imageName.ToString()).ShouldBe(imageName);
    }

    [Fact]
    public void Parse_AndToString_RoundTripSupportsMaximumTagLengthAndBoundaryCharacters()
    {
        string tag = "_" + new string('A', MaxTagLength - 3) + ".-";
        ImageName imageName = new ImageName("example.com", "repo", tag, null);

        ImageName.Parse(imageName.ToString()).ShouldBe(imageName);
    }

    [Fact]
    public void Parse_AndToString_RoundTripSupportsRepositorySeparatorEdgeCases()
    {
        ImageName imageName = new ImageName("example.com", "a.b/a__b/a---c", "_tag.1-2", null);

        ImageName.Parse(imageName.ToString()).ShouldBe(imageName);
    }

    [Fact]
    public void Parse_AndToString_RoundTripSupportsOciShaDigests()
    {
        ImageName sha256ImageName = new ImageName("example.com", "repo", null, CreateDigest("sha256", Sha256HexLength, 'a'));
        ImageName sha512ImageName = new ImageName("example.com", "repo", null, CreateDigest("sha512", Sha512HexLength, 'b'));

        ImageName.Parse(sha256ImageName.ToString()).ShouldBe(sha256ImageName);
        ImageName.Parse(sha512ImageName.ToString()).ShouldBe(sha512ImageName);
    }

    [Fact]
    public void ImplicitConversion_StringToImageName()
    {
        ImageName imageName = "mcr.microsoft.com/dotnet/sdk:8.0";

        imageName.Registry.ShouldBe("mcr.microsoft.com");
        imageName.Repo.ShouldBe("dotnet/sdk");
        imageName.Tag.ShouldBe("8.0");
    }

    [Fact]
    public void ImplicitConversion_StringToImageName_ResolvesDockerHub()
    {
        ImageName imageName = "ubuntu";

        imageName.Registry.ShouldBe(DockerHelper.DockerHubRegistry);
        imageName.Repo.ShouldBe("library/ubuntu");
    }

    [Fact]
    public void ImplicitConversion_ImageNameToString()
    {
        ImageName imageName = new ImageName("mcr.microsoft.com", "dotnet/runtime", "8.0", null);

        string result = imageName;

        result.ShouldBe("mcr.microsoft.com/dotnet/runtime:8.0");
    }

    [Fact]
    public void ImplicitConversion_RoundTrip()
    {
        string original = "mcr.microsoft.com/dotnet/sdk:8.0";

        ImageName imageName = original;
        string roundTripped = imageName;

        roundTripped.ShouldBe(original);
    }

}
