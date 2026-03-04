// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.DockerTools.Automation.Models;

/// <summary>
/// Commit metadata returned from a remote git platform.
/// </summary>
/// <param name="Sha">The full commit SHA.</param>
/// <param name="Author">The commit author name or login.</param>
/// <param name="Message">The commit message.</param>
public record RemoteCommit(string Sha, string Author, string Message);
