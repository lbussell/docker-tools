// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CsCheck;
using Shouldly;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests;

public sealed class ImageNamePropertiesTests
{
    private const int PropertyIterationCount = 250;
    private const string RegistryCharacters = "abcdefghijklmnopqrstuvwxyz0123456789-";
    private const string RepoCharacters = "abcdefghijklmnopqrstuvwxyz0123456789-_";
    private const string TagCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._";
    private const string DigestCharacters = "0123456789abcdef";

    private static readonly Gen<string> RegistryLabelGen = Gen.Char[RegistryCharacters]
        .Array[1, 12]
        .Select(characters => new string(characters));

    private static readonly Gen<string> ExplicitRegistryGen = Gen.Select(
        RegistryLabelGen,
        RegistryLabelGen,
        (left, right) => $"{left}.{right}.registry");

    private static readonly Gen<string> OptionalRegistryGen = Gen.Select(
        Gen.Int[0, 1],
        ExplicitRegistryGen,
        (includeRegistry, registry) => includeRegistry == 0 ? string.Empty : registry);

    private static readonly Gen<string> RepoSegmentGen = Gen.Char[RepoCharacters]
        .Array[1, 12]
        .Select(characters => new string(characters));

    private static readonly Gen<string> SingleSegmentRepoGen = RepoSegmentGen;

    private static readonly Gen<string> MultiSegmentRepoGen = RepoSegmentGen.Array[2, 3]
        .Select(segments => string.Join("/", segments));

    private static readonly Gen<string> RepoGen = RepoSegmentGen.Array[1, 3]
        .Select(segments => string.Join("/", segments));

    private static readonly Gen<string> TagGen = Gen.Char[TagCharacters]
        .Array[1, 12]
        .Select(characters => new string(characters));

    private static readonly Gen<string> OptionalTagGen = Gen.Select(
        Gen.Int[0, 1],
        TagGen,
        (includeTag, tag) => includeTag == 0 ? string.Empty : tag);

    private static readonly Gen<string> DigestGen = Gen.Char[DigestCharacters]
        .Array[12, 24]
        .Select(characters => $"sha256:{new string(characters)}");

    private static readonly Gen<string> OptionalDigestGen = Gen.Select(
        Gen.Int[0, 1],
        DigestGen,
        (includeDigest, digest) => includeDigest == 0 ? string.Empty : digest);

    private static readonly Gen<ImageName> ImageNameGen = Gen.Select(
        OptionalRegistryGen,
        RepoGen,
        OptionalTagGen,
        OptionalDigestGen,
        (registry, repo, tag, digest) => new ImageName(registry, repo, tag, digest));

    private static readonly Gen<ImageName> ImageNameWithTagAndDigestGen = Gen.Select(
        OptionalRegistryGen,
        RepoGen,
        TagGen,
        DigestGen,
        (registry, repo, tag, digest) => new ImageName(registry, repo, tag, digest));

    [Fact]
    public void Parse_PreservesTagAndDigestWhenBothArePresent()
    {
        Check.Sample(
            ImageNameWithTagAndDigestGen,
            imageName =>
            {
                ImageName parsed = ImageName.Parse(imageName.ToString());

                parsed.ShouldBe(imageName);
            },
            iter: PropertyIterationCount);
    }

    [Fact]
    public void Parse_AndToString_RoundTripWithoutAutoResolve()
    {
        Check.Sample(
            ImageNameGen,
            imageName =>
            {
                ImageName parsed = ImageName.Parse(imageName.ToString());

                parsed.ShouldBe(imageName);
            },
            iter: PropertyIterationCount);
    }

    [Fact]
    public void Parse_WithAutoResolve_AddsDockerHubDefaultsForSingleSegmentRepos()
    {
        Check.Sample(
            Gen.Select(SingleSegmentRepoGen, OptionalTagGen, OptionalDigestGen),
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
            Gen.Select(MultiSegmentRepoGen, OptionalTagGen, OptionalDigestGen),
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
            Gen.Select(ExplicitRegistryGen, RepoGen, OptionalTagGen, OptionalDigestGen),
            input =>
            {
                ImageName imageName = new(input.Item1, input.Item2, input.Item3, input.Item4);
                ImageName parsed = ImageName.Parse(imageName.ToString(), autoResolveImpliedNames: true);

                parsed.ShouldBe(imageName);
            },
            iter: PropertyIterationCount);
    }
}
