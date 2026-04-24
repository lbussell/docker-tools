// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.ImageBuilder.Models.Image.V2;

/// <summary>
/// Immutable representation of a single platform-specific container image build artifact.
/// </summary>
public record PlatformData
{
    /// <summary>
    /// Path to the Dockerfile relative to the manifest root.
    /// </summary>
    public required string Dockerfile { get; init; }

    /// <summary>
    /// Platform-specific tags applied to this image (e.g., "8.0-noble-amd64").
    /// </summary>
    public IReadOnlyList<string> SimpleTags { get; init; } = [];

    /// <summary>
    /// Fully-qualified image digest (e.g., "dotnet/aspnet@sha256:abc...").
    /// </summary>
    public required string Digest { get; init; }

    /// <summary>
    /// Digest of the base image used in the final FROM stage. Null if not tracked.
    /// </summary>
    public string? BaseImageDigest { get; init; }

    /// <summary>
    /// Operating system type (e.g., "Linux", "Windows").
    /// </summary>
    public required string OsType { get; init; }

    /// <summary>
    /// Operating system version (e.g., "noble", "nanoserver-ltsc2022").
    /// </summary>
    public required string OsVersion { get; init; }

    /// <summary>
    /// CPU architecture (e.g., "amd64", "arm64").
    /// </summary>
    public required string Architecture { get; init; }

    /// <summary>
    /// Timestamp when the image was created/built.
    /// </summary>
    public DateTime Created { get; init; }

    /// <summary>
    /// URL to the source commit that produced this image.
    /// </summary>
    public required string CommitUrl { get; init; }

    /// <summary>
    /// Layer digests and sizes composing this image.
    /// </summary>
    public IReadOnlyList<Layer> Layers { get; init; } = [];

    /// <summary>
    /// Whether the image is unchanged since last publish. Used internally during builds;
    /// stripped from published image-info content.
    /// </summary>
    public bool IsUnchanged { get; init; }
}
