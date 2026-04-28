// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CsCheck;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Services;
using Microsoft.DotNet.ImageBuilder.Tests.Generators;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Shouldly;
using Xunit;
using V2 = Microsoft.DotNet.ImageBuilder.Models.Image.V2;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.DockerfileHelper;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ManifestHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests.PropertyTests;

/// <summary>
/// Differential characterization tests verifying that the new <see cref="ImageInfoMerger"/>
/// produces results equivalent to the old <see cref="ImageInfoHelper.MergeImageArtifactDetails"/>.
/// </summary>
public class MergeMigrationPropertyTests
{
    /// <summary>
    /// Merging non-overlapping repos with the new merger produces the same JSON
    /// as the old merger when both paths start from the same JSON input.
    /// </summary>
    [Fact]
    public void Merge_NonOverlappingRepos_MatchesOldBehavior()
    {
        Gen.Select(
            ImageInfoGenerators.ImageArtifactDetails,
            ImageInfoGenerators.ImageArtifactDetails)
        .Sample((generatedA, generatedB) =>
        {
            V2.ImageArtifactDetails detailsA = PrefixRepos(generatedA, "a/");
            V2.ImageArtifactDetails detailsB = PrefixRepos(generatedB, "b/");

            MergeJsonScenario scenario = new(
                SourceJsons:
                [
                    ImageInfoSerializer.Serialize(detailsA),
                    ImageInfoSerializer.Serialize(detailsB),
                ],
                TargetJson: null,
                Options: new ImageInfoMergeOptions());

            AssertMergeOutputsMatch(scenario, MergeOldJson, MergeNewJson);
        });
    }

    /// <summary>
    /// Merging into an empty target with the new merger produces the same JSON
    /// as the old merger.
    /// </summary>
    [Fact]
    public void Merge_IntoEmptyTarget_MatchesOldBehavior()
    {
        ImageInfoGenerators.ImageArtifactDetails.Sample(source =>
        {
            MergeJsonScenario scenario = new(
                SourceJsons: [ImageInfoSerializer.Serialize(source)],
                TargetJson: null,
                Options: new ImageInfoMergeOptions());

            AssertMergeOutputsMatch(scenario, MergeOldJson, MergeNewJson);
        });
    }

    /// <summary>
    /// Build and publish merge modes handle replaceable string lists differently,
    /// and the new merger matches the old helper at the manifest + JSON boundary.
    /// </summary>
    [Fact]
    public void Merge_ReplaceableManifestSharedTags_MatchesOldBehavior()
    {
        using TempFolderContext tempFolderContext = new();

        string dockerfilePath = CreateDockerfile("1.0/repo/linux", tempFolderContext);
        Manifest manifest = CreateManifest(
            CreateRepo("repo",
                CreateImage(
                    [
                        CreatePlatform(dockerfilePath, ["platform-tag"])
                    ],
                    productVersion: "1.0")));

        string manifestPath = Path.Combine(tempFolderContext.Path, "manifest.json");
        File.WriteAllText(manifestPath, JsonHelper.SerializeObject(manifest));
        ManifestInfo manifestInfo = TestHelper.CreateManifestJsonService()
            .Load(GetManifestOptions(manifestPath));

        ImageInfoGenerators.MergeStringListScenario.Sample(scenario =>
        {
            MergeJsonScenario mergeScenario = new(
                SourceJsons:
                [
                    CreateSinglePlatformDetailsJson(
                        repo: "repo",
                        productVersion: "1.0",
                        dockerfile: dockerfilePath,
                        architecture: "amd64",
                        osType: "Linux",
                        osVersion: "noble",
                        sharedTags: scenario.Source,
                        simpleTags: ["platform-tag"],
                        digest: "repo@sha256:source"),
                ],
                TargetJson: CreateSinglePlatformDetailsJson(
                    repo: "repo",
                    productVersion: "1.0",
                    dockerfile: dockerfilePath,
                    architecture: "amd64",
                    osType: "Linux",
                    osVersion: "noble",
                    sharedTags: scenario.Target,
                    simpleTags: ["platform-tag"],
                    digest: "repo@sha256:target"),
                Options: new ImageInfoMergeOptions { IsPublish = scenario.IsPublish },
                ManifestInfo: manifestInfo,
                SkipManifestValidation: scenario.IsPublish);

            AssertMergeOutputsMatch(mergeScenario, MergeOldJson, MergeNewJson);
        });
    }

    /// <summary>
    /// The new merger is idempotent: merging the result into another empty target
    /// produces the same result.
    /// </summary>
    [Fact]
    public void Merge_IsIdempotent()
    {
        ImageInfoGenerators.ImageArtifactDetails.Sample(source =>
        {
            V2.ImageArtifactDetails v2Source = ImageInfoSerializer.Deserialize(ImageInfoSerializer.Serialize(source));
            V2.ImageArtifactDetails first = ImageInfoMerger.Merge(v2Source, new V2.ImageArtifactDetails());
            V2.ImageArtifactDetails second = ImageInfoMerger.Merge(first, new V2.ImageArtifactDetails());

            string firstJson = ImageInfoSerializer.Serialize(first);
            string secondJson = ImageInfoSerializer.Serialize(second);
            secondJson.ShouldBe(firstJson);
        });
    }

    /// <summary>
    /// The new merger does not mutate the source or target inputs.
    /// </summary>
    [Fact]
    public void Merge_DoesNotMutateInputs()
    {
        ImageInfoGenerators.ImageArtifactDetails.Sample(source =>
        {
            V2.ImageArtifactDetails v2Source = ImageInfoSerializer.Deserialize(ImageInfoSerializer.Serialize(source));
            V2.ImageArtifactDetails v2Target = new V2.ImageArtifactDetails
            {
                Repos =
                [
                    new V2.RepoData
                    {
                        Repo = "existing-repo",
                        Images = [],
                    }
                ]
            };

            string sourceJsonBefore = ImageInfoSerializer.Serialize(v2Source);
            string targetJsonBefore = ImageInfoSerializer.Serialize(v2Target);

            ImageInfoMerger.Merge(v2Source, v2Target);

            string sourceJsonAfter = ImageInfoSerializer.Serialize(v2Source);
            string targetJsonAfter = ImageInfoSerializer.Serialize(v2Target);

            sourceJsonAfter.ShouldBe(sourceJsonBefore);
            targetJsonAfter.ShouldBe(targetJsonBefore);
        });
    }

    private static void AssertMergeOutputsMatch(
        MergeJsonScenario scenario,
        Func<MergeJsonScenario, string> oldMerge,
        Func<MergeJsonScenario, string> newMerge)
    {
        string oldJson = oldMerge(scenario);
        string newJson = newMerge(scenario);

        newJson.ShouldBe(oldJson);
    }

    private static string MergeOldJson(MergeJsonScenario scenario)
    {
        ImageArtifactDetails target = scenario.TargetJson is null
            ? new ImageArtifactDetails()
            : DeserializeOld(scenario.TargetJson, scenario);

        foreach (string sourceJson in scenario.SourceJsons)
        {
            ImageArtifactDetails source = DeserializeOld(sourceJson, scenario);
            ImageInfoHelper.MergeImageArtifactDetails(source, target, scenario.Options);
        }

        return JsonHelper.SerializeObject(target);
    }

    private static string MergeNewJson(MergeJsonScenario scenario)
    {
        V2.ImageArtifactDetails target = scenario.TargetJson is null
            ? new V2.ImageArtifactDetails()
            : ImageInfoSerializer.Deserialize(scenario.TargetJson);

        foreach (string sourceJson in scenario.SourceJsons)
        {
            V2.ImageArtifactDetails source = ImageInfoSerializer.Deserialize(sourceJson);
            target = ImageInfoMerger.Merge(source, target, scenario.Options);
        }

        return ImageInfoSerializer.Serialize(target);
    }

    private static ImageArtifactDetails DeserializeOld(string json, MergeJsonScenario scenario) =>
        scenario.ManifestInfo is null
            ? ImageArtifactDetails.FromJson(json)
            : ImageInfoHelper.LoadFromContent(
                json,
                scenario.ManifestInfo,
                skipManifestValidation: scenario.SkipManifestValidation);

    private static string CreateSinglePlatformDetailsJson(
        string repo,
        string productVersion,
        string dockerfile,
        string architecture,
        string osType,
        string osVersion,
        IReadOnlyList<string> sharedTags,
        List<string>? simpleTags = null,
        string digest = "dotnet/runtime@sha256:abc123")
    {
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
                            ProductVersion = productVersion,
                            Manifest = new V2.ManifestData
                            {
                                SharedTags = sharedTags.ToList(),
                            },
                            Platforms =
                            [
                                new V2.PlatformData
                                {
                                    Dockerfile = dockerfile,
                                    Architecture = architecture,
                                    OsType = osType,
                                    OsVersion = osVersion,
                                    Digest = digest,
                                    SimpleTags = simpleTags ?? [],
                                    CommitUrl = "https://github.com/dotnet/dotnet-docker/commit/abc123",
                                }
                            ],
                        }
                    ],
                }
            ],
        };

        return ImageInfoSerializer.Serialize(details);
    }

    private static V2.ImageArtifactDetails PrefixRepos(V2.ImageArtifactDetails details, string prefix) =>
        details with
        {
            Repos = details.Repos.Select(repo => repo with
            {
                Repo = $"{prefix}{repo.Repo}",
            }).ToList(),
        };

    private sealed record MergeJsonScenario(
        IReadOnlyList<string> SourceJsons,
        string? TargetJson,
        ImageInfoMergeOptions Options,
        ManifestInfo? ManifestInfo = null,
        bool SkipManifestValidation = false);
}
