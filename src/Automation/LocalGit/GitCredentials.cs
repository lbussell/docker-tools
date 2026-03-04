// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.DockerTools.Automation.LocalGit;

/// <summary>
/// Credentials for authenticating local git operations (clone, push).
/// Applied by injecting into the repository URL (https://user:token@host/...).
/// </summary>
/// <param name="Username">The username for authentication.</param>
/// <param name="Token">The personal access token or password.</param>
public record GitCredentials(string Username, string Token);
