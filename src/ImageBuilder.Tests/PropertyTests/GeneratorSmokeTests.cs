// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CsCheck;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Tests.Generators;
using Shouldly;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests.PropertyTests;

/// <summary>
/// Smoke tests to verify CsCheck generators produce valid image-info data.
/// </summary>
public class GeneratorSmokeTests
{
    [Fact]
    public void ImageArtifactDetails_GeneratesValidData()
    {
        ImageInfoGenerators.ImageArtifactDetails.Sample(details =>
        {
            details.ShouldNotBeNull();
            details.Repos.ShouldNotBeEmpty();
            details.SchemaVersion.ShouldBe("2.0");

            foreach (RepoData repo in details.Repos)
            {
                repo.Repo.ShouldNotBeNullOrWhiteSpace();
                repo.Images.ShouldNotBeEmpty();

                foreach (ImageData image in repo.Images)
                {
                    image.Platforms.ShouldNotBeEmpty();

                    foreach (PlatformData platform in image.Platforms)
                    {
                        platform.Dockerfile.ShouldNotBeNullOrWhiteSpace();
                        platform.Digest.ShouldNotBeNullOrWhiteSpace();
                        platform.OsType.ShouldNotBeNullOrWhiteSpace();
                        platform.OsVersion.ShouldNotBeNullOrWhiteSpace();
                        platform.Architecture.ShouldNotBeNullOrWhiteSpace();
                    }
                }
            }
        });
    }

    [Fact]
    public void Layer_GeneratesValidDigestFormat()
    {
        ImageInfoGenerators.Layer.Sample(layer =>
        {
            layer.Digest.ShouldStartWith("sha256:");
            layer.Digest.Length.ShouldBe(71); // "sha256:" (7) + 64 hex chars
            layer.Size.ShouldBeGreaterThanOrEqualTo(0);
        });
    }

    [Fact]
    public void PlatformData_HasConsistentOsTypeAndVersion()
    {
        string[] windowsVersions = ["nanoserver-ltsc2022", "nanoserver-ltsc2025", "windowsservercore-ltsc2022"];
        string[] linuxVersions = ["noble", "jammy", "bookworm-slim", "alpine3.21", "azurelinux3.0"];

        ImageInfoGenerators.PlatformData.Sample(platform =>
        {
            if (platform.OsType == "Windows")
            {
                windowsVersions.ShouldContain(platform.OsVersion);
            }
            else
            {
                linuxVersions.ShouldContain(platform.OsVersion);
            }
        }, iter: 500);
    }
}
