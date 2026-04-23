// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.ImageBuilder.Models.Image.V2;

/// <summary>
/// Immutable representation of image build artifact metadata.
/// This is the root container for all image-info data (schema version 2.0).
/// </summary>
public record ImageArtifactDetails
{
    /// <summary>
    /// Schema version of the image-info format. Always "2.0".
    /// </summary>
    public string SchemaVersion => "2.0";

    /// <summary>
    /// Collection of repository data containing built image information.
    /// </summary>
    public IReadOnlyList<RepoData> Repos { get; init; } = [];
}
