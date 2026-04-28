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
using V2 = Microsoft.DotNet.ImageBuilder.Models.Image.V2;

namespace Microsoft.DotNet.ImageBuilder.Tests.PropertyTests;

/// <summary>
/// Differential characterization tests verifying that the new <see cref="ImageInfoIdentity"/> functions
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
        ImageInfoGenerators.PlatformData.Sample(platform =>
        {
            PlatformData oldModelPlatform = platform.ConvertToV1();
            string oldIdentifier = oldModelPlatform.GetIdentifier(excludeProductVersion: true);

            string newKey = ImageInfoIdentity.GetPlatformKey(
                platform.Dockerfile,
                platform.Architecture,
                platform.OsType,
                platform.OsVersion);

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
        .Sample((platform, version) =>
        {
            string? expectedMajorMinor = ImageInfoIdentity.GetMajorMinorVersion(version);
            string expected = $"{platform.Dockerfile}-{platform.Architecture}-{platform.OsType}-{platform.OsVersion}-{expectedMajorMinor}";

            string actual = ImageInfoIdentity.GetPlatformKey(
                platform.Dockerfile,
                platform.Architecture,
                platform.OsType,
                platform.OsVersion,
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
            bool oldResult = platformA.ConvertToV1().HasDifferentTagState(platformB.ConvertToV1());
            bool newResult = ImageInfoIdentity.HasDifferentTagState(platformA.SimpleTags, platformB.SimpleTags);
            newResult.ShouldBe(oldResult);
        });
    }

    /// <summary>
    /// Identical product versions are equivalent.
    /// </summary>
    [Fact]
    public void AreProductVersionsEquivalent_IdenticalVersions_ReturnsTrue()
    {
        ImageInfoGenerators.ProductVersion.Sample(version =>
        {
            ImageInfoIdentity.AreProductVersionsEquivalent(version, version).ShouldBeTrue();
        });
    }

    /// <summary>
    /// Product versions with the same major.minor segments are equivalent.
    /// </summary>
    [Fact]
    public void AreProductVersionsEquivalent_SameMajorMinor_ReturnsTrue()
    {
        ImageInfoGenerators.SameMajorMinorProductVersionPair.Sample(pair =>
        {
            ImageInfoIdentity.AreProductVersionsEquivalent(pair.Version1, pair.Version2).ShouldBeTrue();
        });
    }

    /// <summary>
    /// Product versions with different major.minor segments are not equivalent.
    /// </summary>
    [Fact]
    public void AreProductVersionsEquivalent_DifferentMajorMinor_ReturnsFalse()
    {
        ImageInfoGenerators.DifferentMajorMinorProductVersionPair.Sample(pair =>
        {
            ImageInfoIdentity.AreProductVersionsEquivalent(pair.Version1, pair.Version2).ShouldBeFalse();
        });
    }

    /// <summary>
    /// Preview suffixes are stripped before product-version equivalence is evaluated.
    /// </summary>
    [Fact]
    public void AreProductVersionsEquivalent_StripsPreviewSuffix()
    {
        ImageInfoIdentity.AreProductVersionsEquivalent("10.0.0-preview.1", "10.0").ShouldBeTrue();
    }

    /// <summary>
    /// Null product versions match only another null product version.
    /// </summary>
    [Fact]
    public void AreProductVersionsEquivalent_NullHandling()
    {
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
