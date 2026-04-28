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
using V2 = Microsoft.DotNet.ImageBuilder.Models.Image.V2;

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
            PlatformData oldPlatform = platform.ConvertToV1();
            string id1 = oldPlatform.GetIdentifier();
            string id2 = oldPlatform.GetIdentifier();
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
            PlatformData oldPlatform = platform.ConvertToV1();
            string identifier = oldPlatform.GetIdentifier();

            identifier.ShouldContain(oldPlatform.Dockerfile);
            identifier.ShouldContain(oldPlatform.Architecture);
            identifier.ShouldContain(oldPlatform.OsType);
            identifier.ShouldContain(oldPlatform.OsVersion);
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
            PlatformData oldPlatform = platform.ConvertToV1();
            string original = oldPlatform.GetIdentifier();

            oldPlatform.Dockerfile = oldPlatform.Dockerfile + "/modified";
            string modified = oldPlatform.GetIdentifier();

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
            PlatformData oldPlatform = platform.ConvertToV1();
            string original = oldPlatform.GetIdentifier();

            string newArch = oldPlatform.Architecture == "amd64" ? "arm64" : "amd64";
            oldPlatform.Architecture = newArch;
            string modified = oldPlatform.GetIdentifier();

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
            PlatformData oldPlatformA = platformA.ConvertToV1();
            PlatformData oldPlatformB = platformB.ConvertToV1();
            bool abResult = oldPlatformA.HasDifferentTagState(oldPlatformB);
            bool baResult = oldPlatformB.HasDifferentTagState(oldPlatformA);
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
            PlatformData oldPlatformA = platformA.ConvertToV1();
            PlatformData oldPlatformB = platformB.ConvertToV1();
            oldPlatformA.HasDifferentTagState(oldPlatformB).ShouldBeFalse();
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
            PlatformData oldPlatform = platform.ConvertToV1();
            PlatformData emptyTagPlatform = new()
            {
                Dockerfile = oldPlatform.Dockerfile,
                Architecture = oldPlatform.Architecture,
                OsType = oldPlatform.OsType,
                OsVersion = oldPlatform.OsVersion,
                Digest = oldPlatform.Digest,
                CommitUrl = oldPlatform.CommitUrl,
                SimpleTags = [],
            };

            oldPlatform.HasDifferentTagState(emptyTagPlatform).ShouldBeTrue();
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
            int actual = repoA.ConvertToV1().CompareTo(repoB.ConvertToV1());

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
            PlatformData oldPlatform = platform.ConvertToV1();
            PlatformData clone = new()
            {
                Dockerfile = oldPlatform.Dockerfile,
                Architecture = oldPlatform.Architecture,
                OsType = oldPlatform.OsType,
                OsVersion = oldPlatform.OsVersion,
                Digest = "different-digest",
                CommitUrl = "different-commit",
                SimpleTags = oldPlatform.SimpleTags.Count > 0 ? ["different-tag"] : [],
            };

            // Same structural identity → CompareTo == 0
            oldPlatform.CompareTo(clone).ShouldBe(0);
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
            List<PlatformData> oldPlatforms = platforms.Select(platform => platform.ConvertToV1()).ToList();
            List<PlatformData> sorted1 = [.. oldPlatforms.OrderBy(platform => platform)];
            List<PlatformData> sorted2 = [.. oldPlatforms.OrderBy(platform => platform)];

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
            PlatformData oldPlatform = platform.ConvertToV1();
            string withVersion = oldPlatform.GetIdentifier(excludeProductVersion: false);
            string withoutVersion = oldPlatform.GetIdentifier(excludeProductVersion: true);

            // Without version should be a prefix of with version (or equal if no version available)
            withVersion.ShouldStartWith(withoutVersion);
        });
    }

}
