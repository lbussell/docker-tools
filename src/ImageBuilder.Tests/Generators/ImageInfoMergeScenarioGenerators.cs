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
/// Scenario record for differential merge tests. Both old and new merge paths
/// consume the same JSON strings to ensure boundary-level compatibility.
/// </summary>
/// <param name="SourceJsons">Per-leg image-info JSON fragments to merge in order.</param>
/// <param name="InitialTargetJson">Optional pre-existing target content.</param>
/// <param name="Options">Build vs publish merge mode.</param>
/// <param name="Description">Human-readable description for test failure messages.</param>
public sealed record ImageInfoMergeScenario(
    IReadOnlyList<string> SourceJsons,
    string? InitialTargetJson,
    ImageInfoMergeOptions Options,
    string Description);

/// <summary>
/// CsCheck generators that produce valid merge scenarios rather than arbitrary image-info trees.
/// Each generator forces a specific merge shape to guarantee coverage of important edge cases.
/// </summary>
public static class ImageInfoMergeScenarioGenerators
{
    private static readonly string[] Architectures = ["amd64", "arm64", "arm32"];
    private static readonly string[] OsVersions = ["bookworm-slim", "noble", "alpine3.21"];
    private static readonly string[] RepoNames = ["dotnet/runtime-deps", "dotnet/runtime", "dotnet/aspnet", "dotnet/sdk"];

    private static readonly Gen<string> RepoName = Gen.OneOfConst(RepoNames);
    private static readonly Gen<string> Architecture = Gen.OneOfConst(Architectures);
    private static readonly Gen<string> OsVersion = Gen.OneOfConst(OsVersions);

    private static readonly Gen<string> ProductVersion =
        Gen.Select(Gen.Int[8, 12], Gen.Int[0, 20], (major, patch) => $"{major}.0.{patch}");

    private static readonly Gen<string> DigestHash =
        Gen.String[ImageInfoGenerators.HexChar, 64, 64]
            .Select(hex => $"sha256:{hex}");

    private static readonly Gen<string> SimpleTag =
        Gen.Select(
            Gen.OneOfConst("tag-a", "tag-b", "tag-c", "tag-d", "tag-e"),
            Gen.Int[1, 99],
            (prefix, suffix) => $"{prefix}-{suffix}");

    /// <summary>
    /// Generates scenarios where multiple source files contribute different platforms
    /// to the same repo/image, simulating multi-architecture build legs.
    /// </summary>
    public static Gen<ImageInfoMergeScenario> MultiLegPlatformAggregation { get; } =
        Gen.Select(
            RepoName,
            ProductVersion,
            OsVersion,
            Gen.Shuffle(Architectures),
            Gen.Int[2, 3],
            DigestHash.Array[3],
            SimpleTag.List[1, 3])
        .Select((repo, version, osVersion, archs, archCount, digests, tags) =>
        {
            string[] selectedArchs = archs[..Math.Min(archCount, archs.Length)];

            List<string> sourceJsons = selectedArchs.Select((arch, index) =>
                SerializeDetails(repo, version, osVersion, arch, digests[index % digests.Length], tags)
            ).ToList();

            return new ImageInfoMergeScenario(
                SourceJsons: sourceJsons,
                InitialTargetJson: null,
                Options: new ImageInfoMergeOptions(),
                Description: $"MultiLeg: {repo} v{version} archs=[{string.Join(",", selectedArchs)}]");
        });

    /// <summary>
    /// Generates scenarios where source and target contain the same platform identity
    /// but with different payload fields (digest, layers, commit URL).
    /// </summary>
    public static Gen<ImageInfoMergeScenario> SamePlatformReplacement { get; } =
        Gen.Select(
            RepoName,
            ProductVersion,
            OsVersion,
            Architecture,
            DigestHash,
            DigestHash,
            SimpleTag.List[1, 3])
        .SelectMany((repo, version, osVersion, arch, targetDigest, sourceDigest, tags) =>
            Gen.Select(
                ImageInfoGenerators.Layer.List[1, 3],
                ImageInfoGenerators.Layer.List[1, 3])
            .Select((targetLayers, sourceLayers) =>
            {
                string targetJson = SerializeDetails(repo, version, osVersion, arch, targetDigest, tags, targetLayers);
                string sourceJson = SerializeDetails(repo, version, osVersion, arch, sourceDigest, tags, sourceLayers);

                return new ImageInfoMergeScenario(
                    SourceJsons: [sourceJson],
                    InitialTargetJson: targetJson,
                    Options: new ImageInfoMergeOptions(),
                    Description: $"SamePlatformReplace: {repo} v{version} {arch}");
            }));

    /// <summary>
    /// Generates scenarios with overlapping tags merged in build mode (union semantics).
    /// </summary>
    public static Gen<ImageInfoMergeScenario> BuildModeTagUnion { get; } =
        Gen.Select(
            RepoName,
            ProductVersion,
            OsVersion,
            Architecture,
            DigestHash)
        .SelectMany((repo, version, osVersion, arch, digest) =>
            Gen.Select(
                SimpleTag.List[1, 4],
                SimpleTag.List[1, 4],
                SimpleTag.List[0, 3],
                SimpleTag.List[0, 3])
            .Select((targetTags, sourceTags, targetSharedTags, sourceSharedTags) =>
            {
                string targetJson = SerializeDetails(repo, version, osVersion, arch, digest, targetTags,
                    sharedTags: targetSharedTags);
                string sourceJson = SerializeDetails(repo, version, osVersion, arch, digest, sourceTags,
                    sharedTags: sourceSharedTags);

                return new ImageInfoMergeScenario(
                    SourceJsons: [sourceJson],
                    InitialTargetJson: targetJson,
                    Options: new ImageInfoMergeOptions { IsPublish = false },
                    Description: $"BuildModeTagUnion: {repo} v{version} {arch}");
            }));

    /// <summary>
    /// Generates scenarios with overlapping tags merged in publish mode (replacement semantics).
    /// </summary>
    public static Gen<ImageInfoMergeScenario> PublishModeTagReplacement { get; } =
        Gen.Select(
            RepoName,
            ProductVersion,
            OsVersion,
            Architecture,
            DigestHash)
        .SelectMany((repo, version, osVersion, arch, digest) =>
            Gen.Select(
                SimpleTag.List[1, 4],
                SimpleTag.List[1, 4],
                SimpleTag.List[0, 3],
                SimpleTag.List[0, 3])
            .Select((targetTags, sourceTags, targetSharedTags, sourceSharedTags) =>
            {
                string targetJson = SerializeDetails(repo, version, osVersion, arch, digest, targetTags,
                    sharedTags: targetSharedTags);
                string sourceJson = SerializeDetails(repo, version, osVersion, arch, digest, sourceTags,
                    sharedTags: sourceSharedTags);

                return new ImageInfoMergeScenario(
                    SourceJsons: [sourceJson],
                    InitialTargetJson: targetJson,
                    Options: new ImageInfoMergeOptions { IsPublish = true },
                    Description: $"PublishModeTagReplacement: {repo} v{version} {arch}");
            }));

    /// <summary>
    /// Generates scenarios where platforms share the same identity fields but have
    /// different tag-presence state (one has tags, the other doesn't), so they should NOT match.
    /// </summary>
    public static Gen<ImageInfoMergeScenario> TagStateNearMiss { get; } =
        Gen.Select(
            RepoName,
            ProductVersion,
            OsVersion,
            Architecture,
            DigestHash,
            DigestHash,
            SimpleTag.List[1, 3])
        .Select((repo, version, osVersion, arch, digest1, digest2, tags) =>
        {
            // Target has tags, source has no tags — should not match
            string targetJson = SerializeDetails(repo, version, osVersion, arch, digest1, tags);
            string sourceJson = SerializeDetails(repo, version, osVersion, arch, digest2, simpleTags: []);

            return new ImageInfoMergeScenario(
                SourceJsons: [sourceJson],
                InitialTargetJson: targetJson,
                Options: new ImageInfoMergeOptions(),
                Description: $"TagStateNearMiss: {repo} v{version} {arch}");
        });

    /// <summary>
    /// Generates scenarios testing product version equivalence rules:
    /// same major.minor should match, different major.minor should not.
    /// Uses a shared Dockerfile path so only the product version differs.
    /// </summary>
    public static Gen<ImageInfoMergeScenario> ProductVersionEquivalence { get; } =
        Gen.Select(
            RepoName,
            ImageInfoGenerators.SameMajorMinorProductVersionPair,
            OsVersion,
            Architecture,
            DigestHash,
            DigestHash,
            SimpleTag.List[1, 3])
        .Select((repo, versionPair, osVersion, arch, digest1, digest2, tags) =>
        {
            // Use a shared Dockerfile path so only the product version differs
            string sharedDockerfile = $"src/runtime/{osVersion}/{arch}/Dockerfile";
            string targetJson = SerializeDetails(repo, versionPair.Version1, osVersion, arch, digest1, tags,
                dockerfile: sharedDockerfile);
            string sourceJson = SerializeDetails(repo, versionPair.Version2, osVersion, arch, digest2, tags,
                dockerfile: sharedDockerfile);

            return new ImageInfoMergeScenario(
                SourceJsons: [sourceJson],
                InitialTargetJson: targetJson,
                Options: new ImageInfoMergeOptions(),
                Description: $"ProductVersionEquivalence: {versionPair.Version1} vs {versionPair.Version2}");
        });

    /// <summary>
    /// Generates scenarios testing manifest data null/non-null replacement behavior.
    /// </summary>
    public static Gen<ImageInfoMergeScenario> ManifestNullReplacement { get; } =
        Gen.Select(
            RepoName,
            ProductVersion,
            OsVersion,
            Architecture,
            DigestHash,
            SimpleTag.List[1, 3])
        .SelectMany((repo, version, osVersion, arch, digest, tags) =>
            Gen.Select(
                SimpleTag.List[0, 3],
                SimpleTag.List[0, 3],
                Gen.OneOfConst("source-null", "target-null", "both-non-null"))
            .Select((sharedTagsA, sharedTagsB, nullVariant) =>
            {
                IReadOnlyList<string>? targetSharedTags = nullVariant == "target-null" ? null : sharedTagsA;
                IReadOnlyList<string>? sourceSharedTags = nullVariant == "source-null" ? null : sharedTagsB;

                string targetJson = SerializeDetails(repo, version, osVersion, arch, digest, tags,
                    sharedTags: targetSharedTags);
                string sourceJson = SerializeDetails(repo, version, osVersion, arch, digest, tags,
                    sharedTags: sourceSharedTags);

                return new ImageInfoMergeScenario(
                    SourceJsons: [sourceJson],
                    InitialTargetJson: targetJson,
                    Options: new ImageInfoMergeOptions(),
                    Description: $"ManifestNullReplacement: {nullVariant}");
            }));

    /// <summary>
    /// Serializes a single-platform image-info details JSON string.
    /// </summary>
    private static string SerializeDetails(
        string repo,
        string version,
        string osVersion,
        string architecture,
        string digest,
        IReadOnlyList<string> simpleTags,
        IReadOnlyList<V2.Layer>? layers = null,
        IReadOnlyList<string>? sharedTags = null,
        string? dockerfile = null)
    {
        dockerfile ??= $"src/{version}/{osVersion}/{architecture}/Dockerfile";
        string fullDigest = $"{repo}@{digest}";

        V2.ManifestData? manifest = sharedTags is not null
            ? new V2.ManifestData { SharedTags = sharedTags.ToList() }
            : null;

        V2.ImageArtifactDetails details = new()
        {
            Repos =
            [
                new V2.RepoData
                {
                    Repo = repo,
                    Images =
                    [
                        new V2.ImageData
                        {
                            ProductVersion = version,
                            Manifest = manifest,
                            Platforms =
                            [
                                new V2.PlatformData
                                {
                                    Dockerfile = dockerfile,
                                    Architecture = architecture,
                                    OsType = "Linux",
                                    OsVersion = osVersion,
                                    Digest = fullDigest,
                                    SimpleTags = simpleTags.ToList(),
                                    Layers = layers?.ToList() ?? [],
                                    CommitUrl = "https://github.com/dotnet/dotnet-docker/commit/abc123",
                                }
                            ],
                        }
                    ],
                }
            ],
        };

        return Services.ImageInfoSerializer.Serialize(details);
    }
}
