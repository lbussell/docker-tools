// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using CsCheck;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Services;
using Microsoft.DotNet.ImageBuilder.Tests.Generators;
using Shouldly;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests.PropertyTests;

/// <summary>
/// Metamorphic tests verifying that the new <see cref="ImageInfoIdentity"/> functions
/// produce results equivalent to the old <see cref="PlatformData"/> instance methods.
/// </summary>
public class IdentityMigrationPropertyTests
{
    /// <summary>
    /// ImageInfoIdentity.GetPlatformKey without version matches the old
    /// PlatformData.GetIdentifier(excludeProductVersion: true) which is used
    /// by the platform matching logic in ArePlatformsEqual.
    /// </summary>
    [Fact]
    public void GetPlatformKey_MatchesOldGetIdentifier_ExcludingVersion()
    {
        ImageInfoGenerators.PlatformData.Sample(oldPlatform =>
        {
            string oldIdentifier = oldPlatform.GetIdentifier(excludeProductVersion: true);

            string newKey = ImageInfoIdentity.GetPlatformKey(
                oldPlatform.Dockerfile,
                oldPlatform.Architecture,
                oldPlatform.OsType,
                oldPlatform.OsVersion);

            newKey.ShouldBe(oldIdentifier);
        });
    }

    /// <summary>
    /// ImageInfoIdentity.GetPlatformKey with a product version matches
    /// the old GetIdentifier when ImageInfo.ProductVersion is set.
    /// </summary>
    [Fact]
    public void GetPlatformKey_MatchesOldGetIdentifier_WithVersion()
    {
        Gen.Select(
            ImageInfoGenerators.PlatformData,
            Gen.OneOfConst("8.0", "8.0.15", "9.0", "9.0.5", "10.0", "10.0.0-preview.1"))
        .Sample((oldPlatform, version) =>
        {
            string? expectedMajorMinor = ImageInfoIdentity.GetMajorMinorVersion(version);
            string expected = $"{oldPlatform.Dockerfile}-{oldPlatform.Architecture}-{oldPlatform.OsType}-{oldPlatform.OsVersion}-{expectedMajorMinor}";

            string actual = ImageInfoIdentity.GetPlatformKey(
                oldPlatform.Dockerfile,
                oldPlatform.Architecture,
                oldPlatform.OsType,
                oldPlatform.OsVersion,
                version);

            actual.ShouldBe(expected);
        });
    }

    /// <summary>
    /// ImageInfoIdentity.HasDifferentTagState matches old PlatformData.HasDifferentTagState.
    /// </summary>
    [Fact]
    public void HasDifferentTagState_MatchesOldBehavior()
    {
        Gen.Select(
            ImageInfoGenerators.PlatformData,
            ImageInfoGenerators.PlatformData)
        .Sample((platformA, platformB) =>
        {
            bool oldResult = platformA.HasDifferentTagState(platformB);
            bool newResult = ImageInfoIdentity.HasDifferentTagState(platformA.SimpleTags, platformB.SimpleTags);
            newResult.ShouldBe(oldResult);
        });
    }

    /// <summary>
    /// ImageInfoIdentity.AreProductVersionsEquivalent matches the old private
    /// AreProductVersionsEquivalent behavior. Both consider versions with matching
    /// major.minor segments as equivalent.
    /// </summary>
    [Fact]
    public void AreProductVersionsEquivalent_MatchesKnownCases()
    {
        // Identical versions
        ImageInfoIdentity.AreProductVersionsEquivalent("8.0", "8.0").ShouldBeTrue();
        ImageInfoIdentity.AreProductVersionsEquivalent("9.0.5", "9.0.5").ShouldBeTrue();

        // Same major.minor, different patch
        ImageInfoIdentity.AreProductVersionsEquivalent("8.0", "8.0.15").ShouldBeTrue();
        ImageInfoIdentity.AreProductVersionsEquivalent("9.0.5", "9.0.10").ShouldBeTrue();

        // Different major.minor
        ImageInfoIdentity.AreProductVersionsEquivalent("8.0", "9.0").ShouldBeFalse();
        ImageInfoIdentity.AreProductVersionsEquivalent("8.0.15", "9.0.5").ShouldBeFalse();

        // Preview suffix stripped
        ImageInfoIdentity.AreProductVersionsEquivalent("10.0.0-preview.1", "10.0").ShouldBeTrue();

        // Null handling
        ImageInfoIdentity.AreProductVersionsEquivalent(null, null).ShouldBeTrue();
        ImageInfoIdentity.AreProductVersionsEquivalent("8.0", null).ShouldBeFalse();
        ImageInfoIdentity.AreProductVersionsEquivalent(null, "8.0").ShouldBeFalse();
    }

    /// <summary>
    /// GetMajorMinorVersion extracts the major.minor portion correctly.
    /// </summary>
    [Fact]
    public void GetMajorMinorVersion_ExtractsCorrectly()
    {
        ImageInfoIdentity.GetMajorMinorVersion("8.0").ShouldBe("8.0");
        ImageInfoIdentity.GetMajorMinorVersion("8.0.15").ShouldBe("8.0");
        ImageInfoIdentity.GetMajorMinorVersion("10.0.0-preview.1").ShouldBe("10.0");
        ImageInfoIdentity.GetMajorMinorVersion(null).ShouldBeNull();
        ImageInfoIdentity.GetMajorMinorVersion("").ShouldBeNull();
    }
}
