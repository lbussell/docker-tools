// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using CsCheck;
using V2 = Microsoft.DotNet.ImageBuilder.Models.Image.V2;

namespace Microsoft.DotNet.ImageBuilder.Tests.Generators;

/// <summary>
/// CsCheck generators for image-info model types.
/// Production-shaped generators model common image-info output, while edge-biased
/// generators intentionally include empty collections and optional values.
/// </summary>
public static class ImageInfoGenerators
{
    private static readonly string[] Architectures = ["amd64", "arm64", "arm", "s390x", "ppc64le"];
    private static readonly string[] OsTypes = ["Linux", "Windows"];
    private static readonly string[] LinuxOsVersions = ["noble", "jammy", "bookworm-slim", "alpine3.21", "azurelinux3.0"];
    private static readonly string[] WindowsOsVersions = ["nanoserver-ltsc2022", "nanoserver-ltsc2025", "windowsservercore-ltsc2022"];
    private static readonly string[] RepoNames = ["dotnet/sdk", "dotnet/aspnet", "dotnet/runtime", "dotnet/runtime-deps", "dotnet/monitor"];
    private static readonly string[] ProductVersions = ["8.0", "8.0.15", "9.0", "9.0.5", "10.0", "10.0.0-preview.1"];

    private static readonly Gen<char> HexChar =
        Gen.OneOfConst('0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'a', 'b', 'c', 'd', 'e', 'f');

    /// <summary>
    /// Generates a known product version used by image-info test data.
    /// </summary>
    public static Gen<string> ProductVersion { get; } =
        Gen.OneOfConst(ProductVersions);

    /// <summary>
    /// Generates product-version pairs that share the same major.minor identity.
    /// </summary>
    public static Gen<(string Version1, string Version2)> SameMajorMinorProductVersionPair { get; } =
        Gen.Select(
            Gen.Int[1, 12],
            Gen.Int[0, 20],
            Gen.Int[0, 50],
            Gen.Int[0, 50])
        .Select((major, minor, patch1, patch2) =>
            ($"{major}.{minor}.{patch1}", $"{major}.{minor}.{patch2}"));

    /// <summary>
    /// Generates product-version pairs with different major.minor identities.
    /// </summary>
    public static Gen<(string Version1, string Version2)> DifferentMajorMinorProductVersionPair { get; } =
        Gen.Select(
            Gen.Int[1, 12],
            Gen.Int[0, 20],
            Gen.Int[0, 50],
            Gen.Int[0, 50])
        .Select((major, minor, patch1, patch2) =>
            ($"{major}.{minor}.{patch1}", $"{major + 1}.{minor}.{patch2}"));

    /// <summary>
    /// Generates a valid SHA-256 digest string in the format "sha256:{64 hex chars}".
    /// </summary>
    public static Gen<string> DigestHash { get; } =
        Gen.String[HexChar, 64, 64]
            .Select(hex => $"sha256:{hex}");

    /// <summary>
    /// Generates a fully-qualified digest string in the format "repo@sha256:{hash}".
    /// </summary>
    public static Gen<string> FullDigest { get; } =
        Gen.Select(
            Gen.OneOfConst(RepoNames),
            DigestHash,
            (repo, hash) => $"{repo}@{hash}");

    /// <summary>
    /// Generates a simple tag name like "8.0-noble-amd64".
    /// </summary>
    public static Gen<string> SimpleTag { get; } =
        Gen.Select(
            ProductVersion,
            Gen.OneOfConst(LinuxOsVersions.Concat(WindowsOsVersions).ToArray()),
            Gen.OneOfConst(Architectures),
            (version, os, arch) => $"{version}-{os}-{arch}");

    /// <summary>
    /// Generates replaceable string-list merge scenarios for build and publish merge modes.
    /// </summary>
    public static Gen<(IReadOnlyList<string> Source, IReadOnlyList<string> Target, bool IsPublish)> MergeStringListScenario { get; } =
        Gen.Select(
            SimpleTag.List[0, 4],
            SimpleTag.List[0, 4],
            Gen.Bool)
        .Select((source, target, isPublish) =>
            ((IReadOnlyList<string>)source, (IReadOnlyList<string>)target, isPublish));

    /// <summary>
    /// Generates a <see cref="V2.Layer"/> with a realistic digest and non-negative size.
    /// </summary>
    public static Gen<V2.Layer> Layer { get; } =
        Gen.Select(DigestHash, Gen.Long[0, 500_000_000], (digest, size) => new V2.Layer(digest, size));

    /// <summary>
    /// Generates a <see cref="V2.ManifestData"/> with optional shared tags and digest.
    /// </summary>
    public static Gen<V2.ManifestData> ManifestData { get; } =
        Gen.Select(
            FullDigest,
            Gen.DateTime,
            SimpleTag.List[0, 4],
            FullDigest.List[0, 2])
        .Select((digest, created, sharedTags, syndicatedDigests) => new V2.ManifestData
        {
            Digest = digest,
            Created = created,
            SharedTags = sharedTags,
            SyndicatedDigests = syndicatedDigests,
        });

    /// <summary>
    /// Generates a <see cref="V2.PlatformData"/> with consistent os/arch/dockerfile values.
    /// </summary>
    public static Gen<V2.PlatformData> PlatformData { get; } =
        Gen.Select(
            Gen.OneOfConst(Architectures),
            Gen.Bool,
            ProductVersion)
        .SelectMany((architecture, isWindows, version) =>
        {
            string osType = isWindows ? "Windows" : "Linux";
            string[] osVersions = isWindows ? WindowsOsVersions : LinuxOsVersions;
            return Gen.Select(
                Gen.OneOfConst(osVersions),
                FullDigest,
                DigestHash.Null(),
                Gen.DateTime,
                SimpleTag.List[0, 4],
                Layer.List[0, 5],
                Gen.Bool,
                Gen.OneOfConst(
                    "https://github.com/dotnet/dotnet-docker/commit/abc123",
                    "https://github.com/dotnet/dotnet-docker/commit/def456",
                    "https://github.com/dotnet/dotnet-docker/commit/789abc"))
            .Select((osVersion, digest, baseImageDigest, created, tags, layers, isUnchanged, commitUrl) =>
                new V2.PlatformData
                {
                    Dockerfile = $"src/{version}/{osVersion}/{architecture}/Dockerfile",
                    SimpleTags = tags,
                    Digest = digest,
                    BaseImageDigest = baseImageDigest,
                    OsType = osType,
                    OsVersion = osVersion,
                    Architecture = architecture,
                    Created = created,
                    CommitUrl = commitUrl,
                    Layers = layers,
                    IsUnchanged = isUnchanged,
                });
        });

    /// <summary>
    /// Generates a <see cref="V2.ImageData"/> with a product version and 1-3 platforms.
    /// </summary>
    public static Gen<V2.ImageData> ImageData { get; } =
        Gen.Select(
            ProductVersion.Null(),
            ManifestData.Null(),
            PlatformData.List[1, 3])
        .Select((version, manifest, platforms) => new V2.ImageData
        {
            ProductVersion = version,
            Manifest = manifest,
            Platforms = platforms,
        });

    /// <summary>
    /// Generates a <see cref="V2.RepoData"/> with a realistic repo name and 1-3 images.
    /// </summary>
    public static Gen<V2.RepoData> RepoData { get; } =
        Gen.Select(
            Gen.OneOfConst(RepoNames),
            ImageData.List[1, 3])
        .Select((repo, images) => new V2.RepoData
        {
            Repo = repo,
            Images = images,
        });

    /// <summary>
    /// Generates a <see cref="V2.ImageArtifactDetails"/> with 1-3 repos, each with a unique name.
    /// </summary>
    public static Gen<V2.ImageArtifactDetails> ImageArtifactDetails { get; } =
        Gen.Shuffle(RepoNames)
            .SelectMany(shuffled =>
            {
                int count = Math.Min(shuffled.Length, 3);
                return Gen.Int[1, count].SelectMany(repoCount =>
                    ImageData.List[1, 3].Array[repoCount]
                        .Select(imageLists =>
                            imageLists.Select((images, index) => new V2.RepoData
                            {
                                Repo = shuffled[index],
                                Images = images,
                            }).ToList()));
            })
            .Select(repos => new V2.ImageArtifactDetails
            {
                Repos = repos,
            });

}
