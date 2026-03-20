// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using CsCheck;

namespace Microsoft.DotNet.ImageBuilder.Tests.Generators;

/// <summary>
/// CsCheck generators for <see cref="ImageName"/> components and complete image references,
/// following Docker's distribution reference grammar and OCI digest specifications.
/// </summary>
public static class ImageNameGenerator
{
    #region Registry

    // Registry domain components follow Docker's `domain-component` grammar:
    // /([a-zA-Z0-9]|[a-zA-Z0-9][a-zA-Z0-9-]*[a-zA-Z0-9])/
    // https://github.com/distribution/reference/blob/main/reference.go
    private const string DomainLabelBoundaryCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private const string DomainLabelMiddleCharacters = DomainLabelBoundaryCharacters + "-";

    // These generators deliberately cover explicit registry forms from Docker's host grammar:
    // domain names, optional ports, `localhost`, IPv4, and bracketed IPv6.
    // https://github.com/distribution/reference/blob/main/reference.go
    // https://github.com/distribution/reference/blob/main/regexp.go
    private static readonly Gen<string> AnyDomainLabel = Gen.Select(
        Gen.Char[DomainLabelBoundaryCharacters],
        Gen.Char[DomainLabelMiddleCharacters].Array[0, 10],
        Gen.Char[DomainLabelBoundaryCharacters],
        BuildDomainLabel);

    private static readonly Gen<string> AnyDomain = AnyDomainLabel.Array[2, 3]
        .Select(labels => string.Join(".", labels));

    private static readonly Gen<string> AnyPort = Gen.Int[1, 65535]
        .Select(port => port.ToString());

    public static readonly Gen<string> AnyRegistry = Gen.OneOf(
        AnyDomain,
        Gen.Select(AnyDomain, AnyPort, (domain, port) => $"{domain}:{port}"),
        Gen.OneOfConst("localhost"),
        AnyPort.Select(port => $"localhost:{port}"),
        Gen.OneOfConst("127.0.0.1"),
        AnyPort.Select(port => $"127.0.0.1:{port}"),
        Gen.OneOfConst("[2001:db8::1]"),
        AnyPort.Select(port => $"[2001:db8::1]:{port}"));

    private static string BuildDomainLabel(char firstCharacter, char[] middleCharacters, char lastCharacter) =>
        middleCharacters.Length == 0
            ? firstCharacter.ToString()
            : $"{firstCharacter}{new string(middleCharacters)}{lastCharacter}";

    #endregion

    #region Repository

    // Docker distribution caps repository names at 255 chars:
    // https://github.com/distribution/reference/blob/main/reference.go
    public const int MaxRepositoryLength = 255;

    // Repository path-components use Docker's lowercase `alpha-numeric` grammar:
    // /[a-z0-9]+/
    // https://github.com/distribution/reference/blob/main/reference.go
    private const string RepoAtomCharacters = "abcdefghijklmnopqrstuvwxyz0123456789";

    // Repository generators follow Docker's `path-component` and `separator` grammar.
    // The implicit variants keep the first segment safely out of explicit-registry territory.
    // https://github.com/distribution/reference/blob/main/reference.go
    // https://github.com/distribution/reference/blob/main/regexp.go
    private static readonly Gen<string> AnyRepoSegment = Gen.Char[RepoAtomCharacters]
        .Array[1, 8]
        .Select(characters => new string(characters));

    // Docker repository separators are `[_.]`, `__`, or repeated `-`.
    // https://github.com/distribution/reference/blob/main/reference.go
    private static readonly Gen<string> AnyRepoSeparator =
        Gen.OneOfConst(".", "_", "__", "-", "--", "---");

    // The implicit Docker Hub generators intentionally exclude `.` in the first segment because
    // the parser treats `.` there as an explicit registry boundary.
    private static readonly Gen<string> AnyImplicitRepoSeparator =
        Gen.OneOfConst("_", "__", "-", "--", "---");

    private static readonly Gen<string> AnyRepoComponent = Gen.Select(
        AnyRepoSegment.Array[1, 4],
        AnyRepoSeparator.Array[3],
        BuildRepositoryComponent);

    private static readonly Gen<string> AnyImplicitRepoComponent = Gen.Select(
        AnyRepoSegment.Array[1, 4],
        AnyImplicitRepoSeparator.Array[3],
        BuildRepositoryComponent);

    public static readonly Gen<string> AnySingleSegmentRepo = AnyRepoComponent;

    public static readonly Gen<string> AnyImplicitSingleSegmentRepo = AnyImplicitRepoComponent;

    public static readonly Gen<string> AnyMultiSegmentRepo = AnyRepoComponent.Array[2, 4]
        .Select(segments => string.Join("/", segments));

    public static readonly Gen<string> AnyImplicitMultiSegmentRepo = AnyImplicitRepoComponent.Array[2, 4]
        .Select(segments => string.Join("/", segments));

    public static readonly Gen<string> AnyRepo = AnyRepoComponent.Array[1, 4]
        .Select(segments => string.Join("/", segments));

    public static readonly Gen<string> AnyImplicitRepo = AnyImplicitRepoComponent.Array[1, 4]
        .Select(segments => string.Join("/", segments));

    private static string BuildRepositoryComponent(string[] segments, string[] separators) =>
        segments[0] + string.Concat(segments.Skip(1).Zip(separators, (seg, sep) => sep + seg));

    #endregion

    #region Tag

    // Docker distribution caps tags at 128 chars:
    // https://github.com/distribution/reference/blob/main/regexp.go
    public const int MaxTagLength = 128;

    // Tags follow Docker's `[\w][\w.-]{0,127}` rule.
    // https://github.com/distribution/reference/blob/main/regexp.go
    private const string TagFirstCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_";
    private const string TagRestCharacters = TagFirstCharacters + ".-";

    // We keep tags short in the random generator and cover the 128-char boundary
    // explicitly in a dedicated test.
    public static readonly Gen<string> ImageTag = Gen.Select(
        Gen.Char[TagFirstCharacters],
        Gen.Char[TagRestCharacters].Array[0, 31],
        (firstCharacter, remainingCharacters) => firstCharacter + new string(remainingCharacters));

    public static readonly Gen<string> OptionalImageTag =
        Gen.OneOf(Gen.OneOfConst(string.Empty), ImageTag);

    #endregion

    #region Digest

    // OCI registered digest lengths for the algorithms we intentionally bias toward:
    // https://github.com/opencontainers/image-spec/blob/main/descriptor.md#registered-algorithms
    public const int Sha256HexLength = 64;
    public const int Sha512HexLength = 128;

    // OCI registered sha256 and sha512 digests are lowercase hex.
    // https://github.com/opencontainers/image-spec/blob/main/descriptor.md#registered-algorithms
    private const string DigestHexCharacters = "0123456789abcdef";

    // OCI digests are `algorithm:encoded`. We intentionally bias toward the registered
    // sha256 and sha512 forms because those are the most relevant real-world cases here.
    // https://github.com/opencontainers/image-spec/blob/main/descriptor.md#digests
    // https://github.com/opencontainers/image-spec/blob/main/descriptor.md#registered-algorithms
    private static readonly Gen<string> AnySha256Digest = Gen.Char[DigestHexCharacters]
        .Array[Sha256HexLength]
        .Select(characters => $"sha256:{new string(characters)}");

    private static readonly Gen<string> AnySha512Digest = Gen.Char[DigestHexCharacters]
        .Array[Sha512HexLength]
        .Select(characters => $"sha512:{new string(characters)}");

    public static readonly Gen<string> ImageDigest =
        Gen.OneOf(AnySha256Digest, AnySha512Digest);

    public static readonly Gen<string> OptionalImageDigest =
        Gen.OneOf(Gen.OneOfConst(string.Empty), ImageDigest);

    public static string CreateDigest(string algorithm, int encodedLength, char encodedCharacter) =>
        $"{algorithm}:{new string(encodedCharacter, encodedLength)}";

    #endregion

    #region Composite ImageName

    // Keep explicit-registry and implicit-Docker-Hub image generators separate so the tests
    // can intentionally exercise both sides of Docker's name parsing split.
    public static readonly Gen<ImageName> ImageNameWithExplicitRegistry = Gen.Select(
        AnyRegistry,
        AnyRepo,
        OptionalImageTag,
        OptionalImageDigest,
        (registry, repo, tag, digest) => new ImageName(registry, repo, tag, digest));

    public static readonly Gen<ImageName> ImageNameWithImplicitRegistry = Gen.Select(
        AnyImplicitRepo,
        OptionalImageTag,
        OptionalImageDigest,
        (repo, tag, digest) => new ImageName(string.Empty, repo, tag, digest));

    public static readonly Gen<ImageName> AnyImageName =
        Gen.OneOf(ImageNameWithImplicitRegistry, ImageNameWithExplicitRegistry);

    public static readonly Gen<ImageName> ImageNameWithExplicitRegistryTagAndDigest = Gen.Select(
        AnyRegistry,
        AnyRepo,
        ImageTag,
        ImageDigest,
        (registry, repo, tag, digest) => new ImageName(registry, repo, tag, digest));

    public static readonly Gen<ImageName> ImageNameWithImplicitRegistryTagAndDigest = Gen.Select(
        AnyImplicitRepo,
        ImageTag,
        ImageDigest,
        (repo, tag, digest) => new ImageName(string.Empty, repo, tag, digest));

    public static readonly Gen<ImageName> AnyImageNameWithTagAndDigest =
        Gen.OneOf(ImageNameWithImplicitRegistryTagAndDigest, ImageNameWithExplicitRegistryTagAndDigest);

    #endregion
}
