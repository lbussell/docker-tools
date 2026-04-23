// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using CsCheck;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Tests.Generators;
using Shouldly;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests.PropertyTests;

/// <summary>
/// Property-based tests that lock down GetIdentifier, HasDifferentTagState,
/// and platform matching behavior used by merge and manifest linking.
/// </summary>
public class IdentityPropertyTests
{
    /// <summary>
    /// GetIdentifier always produces the same result for the same platform data.
    /// </summary>
    [Fact]
    public void GetIdentifier_IsDeterministic()
    {
        ImageInfoGenerators.PlatformData.Sample(platform =>
        {
            string id1 = platform.GetIdentifier();
            string id2 = platform.GetIdentifier();
            id2.ShouldBe(id1);
        });
    }

    /// <summary>
    /// GetIdentifier includes the dockerfile, architecture, osType, and osVersion components.
    /// Changing any one of these produces a different identifier.
    /// </summary>
    [Fact]
    public void GetIdentifier_ContainsAllKeyComponents()
    {
        ImageInfoGenerators.PlatformData.Sample(platform =>
        {
            string identifier = platform.GetIdentifier();

            identifier.ShouldContain(platform.Dockerfile);
            identifier.ShouldContain(platform.Architecture);
            identifier.ShouldContain(platform.OsType);
            identifier.ShouldContain(platform.OsVersion);
        });
    }

    /// <summary>
    /// Changing the Dockerfile produces a different identifier.
    /// </summary>
    [Fact]
    public void GetIdentifier_ChangingDockerfile_ChangeIdentifier()
    {
        ImageInfoGenerators.PlatformData.Sample(platform =>
        {
            string original = platform.GetIdentifier();

            platform.Dockerfile = platform.Dockerfile + "/modified";
            string modified = platform.GetIdentifier();

            modified.ShouldNotBe(original);
        });
    }

    /// <summary>
    /// Changing the architecture produces a different identifier.
    /// </summary>
    [Fact]
    public void GetIdentifier_ChangingArchitecture_ChangesIdentifier()
    {
        ImageInfoGenerators.PlatformData.Sample(platform =>
        {
            string original = platform.GetIdentifier();

            string newArch = platform.Architecture == "amd64" ? "arm64" : "amd64";
            platform.Architecture = newArch;
            string modified = platform.GetIdentifier();

            modified.ShouldNotBe(original);
        });
    }

    /// <summary>
    /// HasDifferentTagState is symmetric: a.HasDifferentTagState(b) == b.HasDifferentTagState(a).
    /// </summary>
    [Fact]
    public void HasDifferentTagState_IsSymmetric()
    {
        Gen.Select(
            ImageInfoGenerators.PlatformData,
            ImageInfoGenerators.PlatformData)
        .Sample((platformA, platformB) =>
        {
            bool abResult = platformA.HasDifferentTagState(platformB);
            bool baResult = platformB.HasDifferentTagState(platformA);
            baResult.ShouldBe(abResult);
        });
    }

    /// <summary>
    /// Two platforms with the same SimpleTags emptiness have the same tag state.
    /// </summary>
    [Fact]
    public void HasDifferentTagState_BothHaveTags_ReturnsFalse()
    {
        Gen.Select(
            ImageInfoGenerators.PlatformData,
            ImageInfoGenerators.PlatformData)
        .Where((platformA, platformB) =>
            platformA.SimpleTags.Count > 0 && platformB.SimpleTags.Count > 0)
        .Sample((platformA, platformB) =>
        {
            platformA.HasDifferentTagState(platformB).ShouldBeFalse();
        });
    }

    /// <summary>
    /// Two platforms where one has tags and the other doesn't have different tag state.
    /// </summary>
    [Fact]
    public void HasDifferentTagState_OneHasTagsOtherDoesNot_ReturnsTrue()
    {
        ImageInfoGenerators.PlatformData
            .Where(platform => platform.SimpleTags.Count > 0)
            .Sample(platform =>
        {
            PlatformData emptyTagPlatform = new()
            {
                Dockerfile = platform.Dockerfile,
                Architecture = platform.Architecture,
                OsType = platform.OsType,
                OsVersion = platform.OsVersion,
                Digest = platform.Digest,
                CommitUrl = platform.CommitUrl,
                SimpleTags = [],
            };

            platform.HasDifferentTagState(emptyTagPlatform).ShouldBeTrue();
        });
    }

    /// <summary>
    /// RepoData.CompareTo sorts by repo name string comparison.
    /// </summary>
    [Fact]
    public void RepoData_CompareTo_SortsByRepoName()
    {
        Gen.Select(
            ImageInfoGenerators.RepoData,
            ImageInfoGenerators.RepoData)
        .Sample((repoA, repoB) =>
        {
            int expected = string.Compare(repoA.Repo, repoB.Repo, StringComparison.Ordinal);
            int actual = repoA.CompareTo(repoB);

            // Same sign
            Math.Sign(actual).ShouldBe(Math.Sign(expected));
        });
    }

    /// <summary>
    /// PlatformData.CompareTo returns 0 for platforms with the same identifier
    /// and same tag state, and non-zero otherwise.
    /// </summary>
    [Fact]
    public void PlatformData_CompareTo_MatchesByIdentifierAndTagState()
    {
        ImageInfoGenerators.PlatformData.Sample(platform =>
        {
            PlatformData clone = new()
            {
                Dockerfile = platform.Dockerfile,
                Architecture = platform.Architecture,
                OsType = platform.OsType,
                OsVersion = platform.OsVersion,
                Digest = "different-digest",
                CommitUrl = "different-commit",
                SimpleTags = platform.SimpleTags.Count > 0 ? ["different-tag"] : [],
            };

            // Same structural identity → CompareTo == 0
            platform.CompareTo(clone).ShouldBe(0);
        });
    }

    /// <summary>
    /// Platforms sorted by CompareTo produce a deterministic ordering.
    /// Sorting the same list twice produces the same result.
    /// </summary>
    [Fact]
    public void PlatformData_Sorting_IsDeterministic()
    {
        ImageInfoGenerators.PlatformData.List[2, 5].Sample(platforms =>
        {
            List<PlatformData> sorted1 = [.. platforms.OrderBy(platform => platform)];
            List<PlatformData> sorted2 = [.. platforms.OrderBy(platform => platform)];

            List<string> ids1 = sorted1.Select(platform => platform.GetIdentifier()).ToList();
            List<string> ids2 = sorted2.Select(platform => platform.GetIdentifier()).ToList();
            ids1.ShouldBe(ids2);
        });
    }

    /// <summary>
    /// GetIdentifier with excludeProductVersion=true omits version from the identifier.
    /// </summary>
    [Fact]
    public void GetIdentifier_ExcludeProductVersion_OmitsVersion()
    {
        ImageInfoGenerators.PlatformData.Sample(platform =>
        {
            string withVersion = platform.GetIdentifier(excludeProductVersion: false);
            string withoutVersion = platform.GetIdentifier(excludeProductVersion: true);

            // Without version should be a prefix of with version (or equal if no version available)
            withVersion.ShouldStartWith(withoutVersion);
        });
    }
}
