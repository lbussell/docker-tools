// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CsCheck;
using Shouldly;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests;

public sealed class ImageNamePropertiesTests
{
    // Docker distribution caps repository names at 255 chars and tags at 128 chars:
    // https://github.com/distribution/reference/blob/main/reference.go
    // https://github.com/distribution/reference/blob/main/regexp.go
    private const int PropertyIterationCount = 250;
    private const int MaxRepositoryLength = 255;
    private const int MaxTagLength = 128;

    // OCI registered digest lengths for the algorithms we intentionally bias toward:
    // https://github.com/opencontainers/image-spec/blob/main/descriptor.md#registered-algorithms
    private const int Sha256HexLength = 64;
    private const int Sha512HexLength = 128;

    // Registry domain components follow Docker's `domain-component` grammar:
    // /([a-zA-Z0-9]|[a-zA-Z0-9][a-zA-Z0-9-]*[a-zA-Z0-9])/
    // https://github.com/distribution/reference/blob/main/reference.go
    private const string DomainLabelBoundaryCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private const string DomainLabelMiddleCharacters = DomainLabelBoundaryCharacters + "-";

    // Repository path-components use Docker's lowercase `alpha-numeric` grammar:
    // /[a-z0-9]+/
    // https://github.com/distribution/reference/blob/main/reference.go
    private const string RepoAtomCharacters = "abcdefghijklmnopqrstuvwxyz0123456789";

    // Tags follow Docker's `[\w][\w.-]{0,127}` rule.
    // https://github.com/distribution/reference/blob/main/regexp.go
    private const string TagFirstCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_";
    private const string TagRestCharacters = TagFirstCharacters + ".-";

    // OCI registered sha256 and sha512 digests are lowercase hex.
    // https://github.com/opencontainers/image-spec/blob/main/descriptor.md#registered-algorithms
    private const string DigestHexCharacters = "0123456789abcdef";

    // Docker repository separators are `[_.]`, `__`, or repeated `-`.
    // https://github.com/distribution/reference/blob/main/reference.go
    private static readonly string[] RepoSeparatorValues =
    [
        ".",
        "_",
        "__",
        "-",
        "--",
        "---",
    ];

    // The implicit Docker Hub generators intentionally exclude `.` in the first segment because
    // the parser treats `.` there as an explicit registry boundary.
    private static readonly string[] ImplicitRepoSeparatorValues =
    [
        "_",
        "__",
        "-",
        "--",
        "---",
    ];

    // These generators deliberately cover explicit registry forms from Docker's host grammar:
    // domain names, optional ports, `localhost`, IPv4, and bracketed IPv6.
    // https://github.com/distribution/reference/blob/main/reference.go
    // https://github.com/distribution/reference/blob/main/regexp.go
    private static readonly Gen<string> DomainLabelGen = Gen.Select(
        Gen.Char[DomainLabelBoundaryCharacters],
        Gen.Char[DomainLabelMiddleCharacters].Array[0, 10],
        Gen.Char[DomainLabelBoundaryCharacters],
        BuildDomainLabel);

    private static readonly Gen<string> PortGen = Gen.Int[1, 65535]
        .Select(port => port.ToString());

    private static readonly Gen<string> ExplicitRegistryGen = Gen.Select(
        Gen.Int[0, 7],
        DomainLabelGen.Array[2, 3],
        PortGen,
        BuildExplicitRegistry);

    // Repository generators follow Docker's `path-component` and `separator` grammar.
    // The implicit variants keep the first segment safely out of explicit-registry territory.
    // https://github.com/distribution/reference/blob/main/reference.go
    // https://github.com/distribution/reference/blob/main/regexp.go
    private static readonly Gen<string> RepoAtomGen = Gen.Char[RepoAtomCharacters]
        .Array[1, 8]
        .Select(characters => new string(characters));

    private static readonly Gen<string> RepoSeparatorGen = Gen.Int[0, RepoSeparatorValues.Length - 1]
        .Select(index => RepoSeparatorValues[index]);

    private static readonly Gen<string> ImplicitRepoSeparatorGen = Gen.Int[0, ImplicitRepoSeparatorValues.Length - 1]
        .Select(index => ImplicitRepoSeparatorValues[index]);

    private static readonly Gen<string> RepoComponentGen = Gen.Select(
        RepoAtomGen.Array[1, 4],
        RepoSeparatorGen.Array[3],
        BuildRepositoryComponent);

    private static readonly Gen<string> ImplicitRepoComponentGen = Gen.Select(
        RepoAtomGen.Array[1, 4],
        ImplicitRepoSeparatorGen.Array[3],
        BuildRepositoryComponent);

    private static readonly Gen<string> SingleSegmentRepoGen = RepoComponentGen;

    private static readonly Gen<string> ImplicitSingleSegmentRepoGen = ImplicitRepoComponentGen;

    private static readonly Gen<string> MultiSegmentRepoGen = RepoComponentGen.Array[2, 4]
        .Select(segments => string.Join("/", segments));

    private static readonly Gen<string> ImplicitMultiSegmentRepoGen = ImplicitRepoComponentGen.Array[2, 4]
        .Select(segments => string.Join("/", segments));

    private static readonly Gen<string> RepoGen = RepoComponentGen.Array[1, 4]
        .Select(segments => string.Join("/", segments));

    private static readonly Gen<string> ImplicitRepoGen = ImplicitRepoComponentGen.Array[1, 4]
        .Select(segments => string.Join("/", segments));

    // Tags follow Docker's `[\w][\w.-]{0,127}` grammar. We keep them short in the random
    // generator and cover the 128-char boundary explicitly in a dedicated test below.
    // https://github.com/distribution/reference/blob/main/regexp.go
    private static readonly Gen<string> TagGen = Gen.Select(
        Gen.Char[TagFirstCharacters],
        Gen.Char[TagRestCharacters].Array[0, 31],
        (firstCharacter, remainingCharacters) => firstCharacter + new string(remainingCharacters));

    private static readonly Gen<string> OptionalTagGen = Gen.Select(
        Gen.Int[0, 1],
        TagGen,
        (includeTag, tag) => includeTag == 0 ? string.Empty : tag);

    // OCI digests are `algorithm:encoded`. We intentionally bias toward the registered
    // sha256 and sha512 forms because those are the most relevant real-world cases here.
    // https://github.com/opencontainers/image-spec/blob/main/descriptor.md#digests
    // https://github.com/opencontainers/image-spec/blob/main/descriptor.md#registered-algorithms
    private static readonly Gen<string> Sha256DigestGen = Gen.Char[DigestHexCharacters]
        .Array[Sha256HexLength]
        .Select(characters => $"sha256:{new string(characters)}");

    private static readonly Gen<string> Sha512DigestGen = Gen.Char[DigestHexCharacters]
        .Array[Sha512HexLength]
        .Select(characters => $"sha512:{new string(characters)}");

    private static readonly Gen<string> DigestGen = Gen.Select(
        Gen.Int[0, 1],
        Sha256DigestGen,
        Sha512DigestGen,
        (algorithmIndex, sha256Digest, sha512Digest) => algorithmIndex == 0 ? sha256Digest : sha512Digest);

    private static readonly Gen<string> OptionalDigestGen = Gen.Select(
        Gen.Int[0, 1],
        DigestGen,
        (includeDigest, digest) => includeDigest == 0 ? string.Empty : digest);

    // Keep explicit-registry and implicit-Docker-Hub image generators separate so the tests
    // can intentionally exercise both sides of Docker's name parsing split.
    private static readonly Gen<ImageName> ExplicitRegistryImageNameGen = Gen.Select(
        ExplicitRegistryGen,
        RepoGen,
        OptionalTagGen,
        OptionalDigestGen,
        (registry, repo, tag, digest) => new ImageName(registry, repo, tag, digest));

    private static readonly Gen<ImageName> ImplicitRegistryImageNameGen = Gen.Select(
        ImplicitRepoGen,
        OptionalTagGen,
        OptionalDigestGen,
        (repo, tag, digest) => new ImageName(string.Empty, repo, tag, digest));

    private static readonly Gen<ImageName> ImageNameGen = Gen.Select(
        Gen.Int[0, 1],
        ImplicitRegistryImageNameGen,
        ExplicitRegistryImageNameGen,
        (imageType, implicitRegistryImageName, explicitRegistryImageName) =>
            imageType == 0 ? implicitRegistryImageName : explicitRegistryImageName);

    private static readonly Gen<ImageName> ExplicitRegistryImageNameWithTagAndDigestGen = Gen.Select(
        ExplicitRegistryGen,
        RepoGen,
        TagGen,
        DigestGen,
        (registry, repo, tag, digest) => new ImageName(registry, repo, tag, digest));

    private static readonly Gen<ImageName> ImplicitRegistryImageNameWithTagAndDigestGen = Gen.Select(
        ImplicitRepoGen,
        TagGen,
        DigestGen,
        (repo, tag, digest) => new ImageName(string.Empty, repo, tag, digest));

    private static readonly Gen<ImageName> ImageNameWithTagAndDigestGen = Gen.Select(
        Gen.Int[0, 1],
        ImplicitRegistryImageNameWithTagAndDigestGen,
        ExplicitRegistryImageNameWithTagAndDigestGen,
        (imageType, implicitRegistryImageName, explicitRegistryImageName) =>
            imageType == 0 ? implicitRegistryImageName : explicitRegistryImageName);

    [Fact]
    public void Parse_PreservesTagAndDigestWhenBothArePresent()
    {
        Check.Sample(
            ImageNameWithTagAndDigestGen,
            imageName => AssertEquivalent(ImageName.Parse(imageName.ToString()), imageName),
            iter: PropertyIterationCount);
    }

    [Fact]
    public void Parse_AndToString_RoundTripWithoutAutoResolve()
    {
        Check.Sample(
            ImageNameGen,
            imageName => AssertEquivalent(ImageName.Parse(imageName.ToString()), imageName),
            iter: PropertyIterationCount);
    }

    [Fact]
    public void Parse_WithAutoResolve_AddsDockerHubDefaultsForSingleSegmentRepos()
    {
        Check.Sample(
            Gen.Select(ImplicitSingleSegmentRepoGen, OptionalTagGen, OptionalDigestGen),
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
            Gen.Select(ImplicitMultiSegmentRepoGen, OptionalTagGen, OptionalDigestGen),
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
                ImageName imageName = new ImageName(input.Item1, input.Item2, input.Item3, input.Item4);

                AssertEquivalent(ImageName.Parse(imageName.ToString(), autoResolveImpliedNames: true), imageName);
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

        AssertEquivalent(ImageName.Parse(imageName.ToString()), imageName);
    }

    [Fact]
    public void Parse_AndToString_RoundTripSupportsMaximumTagLengthAndBoundaryCharacters()
    {
        string tag = "_" + new string('A', MaxTagLength - 3) + ".-";
        ImageName imageName = new ImageName("example.com", "repo", tag, null);

        AssertEquivalent(ImageName.Parse(imageName.ToString()), imageName);
    }

    [Fact]
    public void Parse_AndToString_RoundTripSupportsRepositorySeparatorEdgeCases()
    {
        ImageName imageName = new ImageName("example.com", "a.b/a__b/a---c", "_tag.1-2", null);

        AssertEquivalent(ImageName.Parse(imageName.ToString()), imageName);
    }

    [Fact]
    public void Parse_AndToString_RoundTripSupportsOciShaDigests()
    {
        ImageName sha256ImageName = new ImageName("example.com", "repo", null, CreateDigest("sha256", Sha256HexLength, 'a'));
        ImageName sha512ImageName = new ImageName("example.com", "repo", null, CreateDigest("sha512", Sha512HexLength, 'b'));

        AssertEquivalent(ImageName.Parse(sha256ImageName.ToString()), sha256ImageName);
        AssertEquivalent(ImageName.Parse(sha512ImageName.ToString()), sha512ImageName);
    }

    private static void AssertEquivalent(ImageName actual, ImageName expected)
    {
        actual.Registry.ShouldBe(expected.Registry);
        actual.Repo.ShouldBe(expected.Repo);
        actual.Tag.ShouldBe(expected.Tag);
        actual.Digest.ShouldBe(expected.Digest);
    }

    private static string BuildDomainLabel(char firstCharacter, char[] middleCharacters, char lastCharacter) =>
        middleCharacters.Length == 0
            ? firstCharacter.ToString()
            : $"{firstCharacter}{new string(middleCharacters)}{lastCharacter}";

    private static string BuildExplicitRegistry(int variant, string[] domainLabels, string port)
    {
        string domain = string.Join(".", domainLabels);

        return variant switch
        {
            0 => domain,
            1 => $"{domain}:{port}",
            2 => "localhost",
            3 => $"localhost:{port}",
            4 => "127.0.0.1",
            5 => $"127.0.0.1:{port}",
            6 => "[2001:db8::1]",
            _ => $"[2001:db8::1]:{port}",
        };
    }

    private static string BuildRepositoryComponent(string[] atoms, string[] separators)
    {
        string component = atoms[0];

        for (int i = 1; i < atoms.Length; i++)
        {
            component += separators[i - 1] + atoms[i];
        }

        return component;
    }

    private static string CreateDigest(string algorithm, int encodedLength, char encodedCharacter) =>
        $"{algorithm}:{new string(encodedCharacter, encodedLength)}";
}
