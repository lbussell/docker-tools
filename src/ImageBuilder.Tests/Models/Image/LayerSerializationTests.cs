// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using Microsoft.DotNet.ImageBuilder.Models.Image;
using Xunit;
using static Microsoft.DotNet.ImageBuilder.Tests.Models.SerializationHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests.Models.Image;

/// <summary>
/// Serialization and deserialization tests for <see cref="Layer"/> model.
/// These tests ensure that serialization behavior does not change unexpectedly.
/// </summary>
public class LayerSerializationTests
{
    [Fact]
    public void Layer_Bidirectional()
    {
        Layer layer = new("sha256:abc123def456", 12345678);

        string json = """
            {
              "digest": "sha256:abc123def456",
              "size": 12345678
            }
            """;

        AssertBidirectional(layer, json, AssertLayersEqual);
    }

    [Fact]
    public void Layer_RoundTrip()
    {
        Layer layer = new("sha256:def789ghi012", 98765432);

        AssertRoundTrip(layer, AssertLayersEqual);
    }

    [Fact]
    public void Layer_ZeroSize_Bidirectional()
    {
        // Size of 0 is the default value and is omitted due to DefaultValueHandling.Ignore
        Layer layer = new("sha256:empty", 0);

        string json = """
            {
              "digest": "sha256:empty"
            }
            """;

        AssertBidirectional(layer, json, AssertLayersEqual);
    }

    [Fact]
    public void Layer_LargeSize_Bidirectional()
    {
        // Test with a large size value (> int.MaxValue)
        Layer layer = new("sha256:large", 5000000000L);

        string json = """
            {
              "digest": "sha256:large",
              "size": 5000000000
            }
            """;

        AssertBidirectional(layer, json, AssertLayersEqual);
    }

    private static void AssertLayersEqual(Layer expected, Layer actual)
    {
        Assert.Equal(expected.Digest, actual.Digest);
        Assert.Equal(expected.Size, actual.Size);
    }
}
