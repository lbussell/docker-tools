// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CsCheck;

namespace Microsoft.DotNet.ImageBuilder.Tests.Generators;

/// <summary>
/// CsCheck generators for <see cref="ImageName"/> components and complete image references,
/// following Docker's distribution reference grammar and OCI digest specifications.
/// </summary>
internal static class ImageNameGenerator
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
    private static readonly Gen<string> DomainLabelGen = Gen.Select(
        Gen.Char[DomainLabelBoundaryCharacters],
        Gen.Char[DomainLabelMiddleCharacters].Array[0, 10],
        Gen.Char[DomainLabelBoundaryCharacters],
        BuildDomainLabel);

    private static readonly Gen<string> DomainGen = DomainLabelGen.Array[2, 3]
        .Select(labels => string.Join(".", labels));

    private static readonly Gen<string> PortGen = Gen.Int[1, 65535]
        .Select(port => port.ToString());

    internal static readonly Gen<string> ExplicitRegistryGen = Gen.OneOf(
        DomainGen,
        Gen.Select(DomainGen, PortGen, (domain, port) => $"{domain}:{port}"),
        Gen.OneOfConst("localhost"),
        PortGen.Select(port => $"localhost:{port}"),
        Gen.OneOfConst("127.0.0.1"),
        PortGen.Select(port => $"127.0.0.1:{port}"),
        Gen.OneOfConst("[2001:db8::1]"),
        PortGen.Select(port => $"[2001:db8::1]:{port}"));

    private static string BuildDomainLabel(char firstCharacter, char[] middleCharacters, char lastCharacter) =>
        middleCharacters.Length == 0
            ? firstCharacter.ToString()
            : $"{firstCharacter}{new string(middleCharacters)}{lastCharacter}";

    #endregion

    #region Repository

    // Docker distribution caps repository names at 255 chars:
    // https://github.com/distribution/reference/blob/main/reference.go
    internal const int MaxRepositoryLength = 255;

    // Repository path-components use Docker's lowercase `alpha-numeric` grammar:
    // /[a-z0-9]+/
    // https://github.com/distribution/reference/blob/main/reference.go
    private const string RepoAtomCharacters = "abcdefghijklmnopqrstuvwxyz0123456789";

    // Repository generators follow Docker's `path-component` and `separator` grammar.
    // The implicit variants keep the first segment safely out of explicit-registry territory.
    // https://github.com/distribution/reference/blob/main/reference.go
    // https://github.com/distribution/reference/blob/main/regexp.go
    private static readonly Gen<string> RepoAtomGen = Gen.Char[RepoAtomCharacters]
        .Array[1, 8]
        .Select(characters => new string(characters));

    // Docker repository separators are `[_.]`, `__`, or repeated `-`.
    // https://github.com/distribution/reference/blob/main/reference.go
    private static readonly Gen<string> RepoSeparatorGen =
        Gen.OneOfConst(".", "_", "__", "-", "--", "---");

    // The implicit Docker Hub generators intentionally exclude `.` in the first segment because
    // the parser treats `.` there as an explicit registry boundary.
    private static readonly Gen<string> ImplicitRepoSeparatorGen =
        Gen.OneOfConst("_", "__", "-", "--", "---");

    private static readonly Gen<string> RepoComponentGen = Gen.Select(
        RepoAtomGen.Array[1, 4],
        RepoSeparatorGen.Array[3],
        BuildRepositoryComponent);

    private static readonly Gen<string> ImplicitRepoComponentGen = Gen.Select(
        RepoAtomGen.Array[1, 4],
        ImplicitRepoSeparatorGen.Array[3],
        BuildRepositoryComponent);

    internal static readonly Gen<string> SingleSegmentRepoGen = RepoComponentGen;

    internal static readonly Gen<string> ImplicitSingleSegmentRepoGen = ImplicitRepoComponentGen;

    internal static readonly Gen<string> MultiSegmentRepoGen = RepoComponentGen.Array[2, 4]
        .Select(segments => string.Join("/", segments));

    internal static readonly Gen<string> ImplicitMultiSegmentRepoGen = ImplicitRepoComponentGen.Array[2, 4]
        .Select(segments => string.Join("/", segments));

    internal static readonly Gen<string> RepoGen = RepoComponentGen.Array[1, 4]
        .Select(segments => string.Join("/", segments));

    internal static readonly Gen<string> ImplicitRepoGen = ImplicitRepoComponentGen.Array[1, 4]
        .Select(segments => string.Join("/", segments));

    private static string BuildRepositoryComponent(string[] atoms, string[] separators)
    {
        string component = atoms[0];

        for (int i = 1; i < atoms.Length; i++)
        {
            component += separators[i - 1] + atoms[i];
        }

        return component;
    }

    #endregion

    #region Tag

    // Docker distribution caps tags at 128 chars:
    // https://github.com/distribution/reference/blob/main/regexp.go
    internal const int MaxTagLength = 128;

    // Tags follow Docker's `[\w][\w.-]{0,127}` rule.
    // https://github.com/distribution/reference/blob/main/regexp.go
    private const string TagFirstCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_";
    private const string TagRestCharacters = TagFirstCharacters + ".-";

    // We keep tags short in the random generator and cover the 128-char boundary
    // explicitly in a dedicated test.
    internal static readonly Gen<string> TagGen = Gen.Select(
        Gen.Char[TagFirstCharacters],
        Gen.Char[TagRestCharacters].Array[0, 31],
        (firstCharacter, remainingCharacters) => firstCharacter + new string(remainingCharacters));

    internal static readonly Gen<string> OptionalTagGen =
        Gen.OneOf(Gen.OneOfConst(string.Empty), TagGen);

    #endregion

    #region Digest

    // OCI registered digest lengths for the algorithms we intentionally bias toward:
    // https://github.com/opencontainers/image-spec/blob/main/descriptor.md#registered-algorithms
    internal const int Sha256HexLength = 64;
    internal const int Sha512HexLength = 128;

    // OCI registered sha256 and sha512 digests are lowercase hex.
    // https://github.com/opencontainers/image-spec/blob/main/descriptor.md#registered-algorithms
    private const string DigestHexCharacters = "0123456789abcdef";

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

    internal static readonly Gen<string> DigestGen =
        Gen.OneOf(Sha256DigestGen, Sha512DigestGen);

    internal static readonly Gen<string> OptionalDigestGen =
        Gen.OneOf(Gen.OneOfConst(string.Empty), DigestGen);

    internal static string CreateDigest(string algorithm, int encodedLength, char encodedCharacter) =>
        $"{algorithm}:{new string(encodedCharacter, encodedLength)}";

    #endregion

    #region Composite ImageName

    // Keep explicit-registry and implicit-Docker-Hub image generators separate so the tests
    // can intentionally exercise both sides of Docker's name parsing split.
    internal static readonly Gen<ImageName> ExplicitRegistryImageNameGen = Gen.Select(
        ExplicitRegistryGen,
        RepoGen,
        OptionalTagGen,
        OptionalDigestGen,
        (registry, repo, tag, digest) => new ImageName(registry, repo, tag, digest));

    internal static readonly Gen<ImageName> ImplicitRegistryImageNameGen = Gen.Select(
        ImplicitRepoGen,
        OptionalTagGen,
        OptionalDigestGen,
        (repo, tag, digest) => new ImageName(string.Empty, repo, tag, digest));

    internal static readonly Gen<ImageName> ImageNameGen =
        Gen.OneOf(ImplicitRegistryImageNameGen, ExplicitRegistryImageNameGen);

    internal static readonly Gen<ImageName> ExplicitRegistryImageNameWithTagAndDigestGen = Gen.Select(
        ExplicitRegistryGen,
        RepoGen,
        TagGen,
        DigestGen,
        (registry, repo, tag, digest) => new ImageName(registry, repo, tag, digest));

    internal static readonly Gen<ImageName> ImplicitRegistryImageNameWithTagAndDigestGen = Gen.Select(
        ImplicitRepoGen,
        TagGen,
        DigestGen,
        (repo, tag, digest) => new ImageName(string.Empty, repo, tag, digest));

    internal static readonly Gen<ImageName> ImageNameWithTagAndDigestGen =
        Gen.OneOf(ImplicitRegistryImageNameWithTagAndDigestGen, ExplicitRegistryImageNameWithTagAndDigestGen);

    #endregion
}
