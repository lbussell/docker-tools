// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Microsoft.DotNet.DockerTools.Infrastructure;

/// <summary>
/// Provides access to the <c>eng/docker-tools</c> infrastructure files (pipeline templates,
/// PowerShell scripts, and docs) that are embedded into this assembly at build time.
/// </summary>
/// <remarks>
/// A matching version of ImageBuilder ships these files so it can write them back out to disk,
/// keeping pipeline content coupled to the ImageBuilder version that consumes it.
/// </remarks>
public static class InfrastructureContent
{
    /// <summary>
    /// Prefix applied to the <c>LogicalName</c> of every embedded content resource.
    /// </summary>
    private const string ResourcePrefix = "Content/";

    private static readonly Assembly s_assembly = typeof(InfrastructureContent).Assembly;

    /// <summary>
    /// Maps each content file's path (relative to the embedded content root, using '/' separators)
    /// to its underlying manifest resource name.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> s_resourceNamesByPath = BuildIndex();

    /// <summary>
    /// Gets the paths of all embedded content files, relative to the content root and using
    /// '/' as the directory separator (for example, <c>templates/jobs/build-images.yml</c>).
    /// </summary>
    public static IReadOnlyList<string> GetRelativePaths() => [.. s_resourceNamesByPath.Keys];

    /// <summary>
    /// Reads the raw bytes of an embedded content file.
    /// </summary>
    /// <param name="relativePath">
    /// The file's path relative to the content root. Either '/' or '\' may be used as the separator.
    /// </param>
    public static byte[] ReadAllBytes(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        string normalizedPath = relativePath.Replace('\\', '/');
        if (!s_resourceNamesByPath.TryGetValue(normalizedPath, out string? resourceName))
        {
            throw new KeyNotFoundException($"No embedded infrastructure content found for path '{relativePath}'.");
        }

        using Stream stream = s_assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' could not be opened.");
        using MemoryStream memory = new();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static Dictionary<string, string> BuildIndex()
    {
        Dictionary<string, string> resourceNamesByPath = new(StringComparer.Ordinal);

        foreach (string resourceName in s_assembly.GetManifestResourceNames())
        {
            // Resource names use the build OS directory separator, so normalize before matching.
            string normalizedName = resourceName.Replace('\\', '/');
            if (!normalizedName.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            string relativePath = normalizedName[ResourcePrefix.Length..];
            resourceNamesByPath[relativePath] = resourceName;
        }

        return resourceNamesByPath;
    }
}
