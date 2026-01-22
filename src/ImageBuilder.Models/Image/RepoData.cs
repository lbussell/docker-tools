// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Microsoft.DotNet.ImageBuilder.Models.Image;

public class RepoData : IComparable<RepoData>
{
    [JsonRequired]
    public string Repo { get; set; } = string.Empty;

    public List<ImageData> Images { get; set; } = [];

    public int CompareTo([AllowNull] RepoData other)
    {
        if (other is null)
        {
            return 1;
        }

        return Repo.CompareTo(other.Repo);
    }
}
