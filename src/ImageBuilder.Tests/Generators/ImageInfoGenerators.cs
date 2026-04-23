// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using CsCheck;
using Microsoft.DotNet.ImageBuilder.Models.Image;

namespace Microsoft.DotNet.ImageBuilder.Tests.Generators;

/// <summary>
/// CsCheck generators for image-info model types.
/// Produces realistic data suitable for property-based and metamorphic testing.
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
            Gen.OneOfConst(ProductVersions),
            Gen.OneOfConst(LinuxOsVersions.Concat(WindowsOsVersions).ToArray()),
            Gen.OneOfConst(Architectures),
            (version, os, arch) => $"{version}-{os}-{arch}");

    /// <summary>
    /// Generates a <see cref="Layer"/> with a realistic digest and non-negative size.
    /// </summary>
    public static Gen<Layer> Layer { get; } =
        Gen.Select(DigestHash, Gen.Long[0, 500_000_000], (digest, size) => new Layer(digest, size));

    /// <summary>
    /// Generates a <see cref="ManifestData"/> with optional shared tags and digest.
    /// </summary>
    public static Gen<ManifestData> ManifestData { get; } =
        Gen.Select(
            FullDigest,
            Gen.DateTime,
            SimpleTag.List[0, 4],
            FullDigest.List[0, 2])
        .Select((digest, created, sharedTags, syndicatedDigests) => new ManifestData
        {
            Digest = digest,
            Created = created,
            SharedTags = sharedTags,
            SyndicatedDigests = syndicatedDigests,
        });

    /// <summary>
    /// Generates a <see cref="PlatformData"/> with consistent os/arch/dockerfile values.
    /// </summary>
    public static Gen<PlatformData> PlatformData { get; } =
        Gen.Select(
            Gen.OneOfConst(Architectures),
            Gen.Bool,
            Gen.OneOfConst(ProductVersions))
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
                new PlatformData
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
    /// Generates an <see cref="ImageData"/> with a product version and 1-3 platforms.
    /// </summary>
    public static Gen<ImageData> ImageData { get; } =
        Gen.Select(
            Gen.OneOfConst(ProductVersions).Null(),
            ManifestData.Null(),
            PlatformData.List[1, 3])
        .Select((version, manifest, platforms) => new ImageData
        {
            ProductVersion = version,
            Manifest = manifest,
            Platforms = platforms,
        });

    /// <summary>
    /// Generates a <see cref="RepoData"/> with a realistic repo name and 1-3 images.
    /// </summary>
    public static Gen<RepoData> RepoData { get; } =
        Gen.Select(
            Gen.OneOfConst(RepoNames),
            ImageData.List[1, 3])
        .Select((repo, images) => new RepoData
        {
            Repo = repo,
            Images = images,
        });

    /// <summary>
    /// Generates an <see cref="ImageArtifactDetails"/> with 1-3 repos, each with a unique name.
    /// </summary>
    public static Gen<ImageArtifactDetails> ImageArtifactDetails { get; } =
        Gen.Shuffle(RepoNames)
            .SelectMany(shuffled =>
            {
                int count = Math.Min(shuffled.Length, 3);
                return Gen.Int[1, count].SelectMany(repoCount =>
                    ImageData.List[1, 3].Array[repoCount]
                        .Select(imageLists =>
                            imageLists.Select((images, index) => new RepoData
                            {
                                Repo = shuffled[index],
                                Images = images,
                            }).ToList()));
            })
            .Select(repos => new ImageArtifactDetails
            {
                Repos = repos,
            });
}
