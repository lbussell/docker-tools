// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.PipelineMonitor.Git;

/// <summary>
/// Represents a Git remote.
/// </summary>
/// <param name="Name">The name of the remote (e.g., "origin").</param>
/// <param name="Url">The URL of the remote.</param>
/// <param name="Type">The type of the remote (e.g., "fetch" or "push").</param>
internal record GitRemote(string Name, string Url, string Type);
