// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.ImageBuilder.Models.Image.V2;

/// <summary>
/// Immutable representation of a single container image layer.
/// </summary>
/// <param name="Digest">Layer content digest (e.g., "sha256:abc...").</param>
/// <param name="Size">Layer size in bytes.</param>
public record Layer(string Digest, long Size);
