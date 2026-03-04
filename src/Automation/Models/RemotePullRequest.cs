// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.DockerTools.Automation.Models;

/// <summary>
/// Pull request metadata returned from a remote git platform.
/// </summary>
/// <param name="Url">The URL of the pull request.</param>
/// <param name="Id">The platform-specific numeric ID of the pull request.</param>
/// <param name="Title">The pull request title.</param>
/// <param name="HeadBranch">The source branch containing the changes.</param>
/// <param name="BaseBranch">The target branch to merge into.</param>
public record RemotePullRequest(string Url, int Id, string Title, string HeadBranch, string BaseBranch);
