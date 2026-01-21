// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ImageBuilder.Models.Image;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public class PlatformDataTests
    {
        [Theory]
        [InlineData("5.0.0-preview.3", "5.0")]
        [InlineData("5.0", "5.0")]
        [InlineData("5.0.1", "5.0")]
        public void GetIdentifier_WithProductVersion(string productVersion, string expectedVersion)
        {
            PlatformData platform = new PlatformData
            {
                Architecture = "amd64",
                OsType = "linux",
                OsVersion = "noble",
                Dockerfile = "path"
            };

            string identifier = platform.GetIdentifier(productVersion);
            Assert.Equal($"path-amd64-linux-noble-{expectedVersion}", identifier);
        }

        [Fact]
        public void GetIdentifier_WithoutProductVersion()
        {
            PlatformData platform = new PlatformData
            {
                Architecture = "amd64",
                OsType = "linux",
                OsVersion = "noble",
                Dockerfile = "path"
            };

            string identifier = platform.GetIdentifier();
            Assert.Equal("path-amd64-linux-noble", identifier);
        }
    }
}
