// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using V2 = Microsoft.DotNet.ImageBuilder.Models.Image.V2;

namespace Microsoft.DotNet.ImageBuilder.Services;

/// <summary>
/// Mutable builder for incrementally constructing a <see cref="V2.PlatformData"/> record.
/// Used by BuildCommand which sets platform properties across multiple passes
/// (digest after push, layers after inspection, etc.).
/// </summary>
public class PlatformDataBuilder
{
    private string _dockerfile;
    private string _architecture;
    private string _osType;
    private string _osVersion;
    private string _commitUrl;
    private string? _digest;
    private string? _baseImageDigest;
    private DateTime _created;
    private List<string> _simpleTags;
    private List<V2.Layer> _layers = [];
    private bool _isUnchanged;

    /// <summary>
    /// Creates a builder initialized with the required structural properties of a platform.
    /// </summary>
    /// <param name="dockerfile">Dockerfile path relative to manifest root.</param>
    /// <param name="architecture">CPU architecture.</param>
    /// <param name="osType">Operating system type.</param>
    /// <param name="osVersion">Operating system version.</param>
    /// <param name="commitUrl">Source commit URL.</param>
    /// <param name="simpleTags">Initial platform tags.</param>
    public PlatformDataBuilder(
        string dockerfile,
        string architecture,
        string osType,
        string osVersion,
        string commitUrl,
        List<string> simpleTags)
    {
        ArgumentNullException.ThrowIfNull(dockerfile);
        ArgumentNullException.ThrowIfNull(architecture);
        ArgumentNullException.ThrowIfNull(osType);
        ArgumentNullException.ThrowIfNull(osVersion);
        ArgumentNullException.ThrowIfNull(commitUrl);
        ArgumentNullException.ThrowIfNull(simpleTags);

        _dockerfile = dockerfile;
        _architecture = architecture;
        _osType = osType;
        _osVersion = osVersion;
        _commitUrl = commitUrl;
        _simpleTags = simpleTags;
    }

    /// <summary>
    /// Sets the image digest (typically after pushing to a registry).
    /// </summary>
    public PlatformDataBuilder SetDigest(string digest)
    {
        _digest = digest;
        return this;
    }

    /// <summary>
    /// Sets the base image digest.
    /// </summary>
    public PlatformDataBuilder SetBaseImageDigest(string? baseImageDigest)
    {
        _baseImageDigest = baseImageDigest;
        return this;
    }

    /// <summary>
    /// Sets the creation timestamp.
    /// </summary>
    public PlatformDataBuilder SetCreated(DateTime created)
    {
        _created = created;
        return this;
    }

    /// <summary>
    /// Sets the layer information (typically after image inspection).
    /// </summary>
    public PlatformDataBuilder SetLayers(List<V2.Layer> layers)
    {
        _layers = layers;
        return this;
    }

    /// <summary>
    /// Sets the unchanged flag (typically after cache hit detection).
    /// </summary>
    public PlatformDataBuilder SetIsUnchanged(bool isUnchanged)
    {
        _isUnchanged = isUnchanged;
        return this;
    }

    /// <summary>
    /// Materializes the builder state into an immutable <see cref="V2.PlatformData"/> record.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when required fields have not been set.</exception>
    public V2.PlatformData Build()
    {
        if (_digest is null)
        {
            throw new InvalidOperationException(
                $"Digest must be set before building PlatformData for '{_dockerfile}'.");
        }

        return new V2.PlatformData
        {
            Dockerfile = _dockerfile,
            Architecture = _architecture,
            OsType = _osType,
            OsVersion = _osVersion,
            CommitUrl = _commitUrl,
            SimpleTags = _simpleTags,
            Digest = _digest,
            BaseImageDigest = _baseImageDigest,
            Created = _created,
            Layers = _layers,
            IsUnchanged = _isUnchanged,
        };
    }
}
