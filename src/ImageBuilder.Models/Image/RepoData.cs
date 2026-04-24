// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.DotNet.ImageBuilder.Models.Image.V2;

/// <summary>
/// Immutable representation of a container image repository and its built images.
/// </summary>
public record RepoData
{
    /// <summary>
    /// Fully-qualified repository name (e.g., "dotnet/aspnet").
    /// </summary>
    public required string Repo { get; init; }

    /// <summary>
    /// Collection of image entries in this repository.
    /// </summary>
    public IReadOnlyList<ImageData> Images { get; init; } = [];
}
