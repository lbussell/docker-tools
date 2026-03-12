// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace TriagePipelines;

/// <summary>
/// Convenience extension methods for common Azure DevOps API calls.
/// </summary>
public static class AzureDevOpsExtensions
{
    /// <summary>
    /// Returns the build definitions in the specified folder, with their latest completed builds.
    /// </summary>
    /// <param name="client">The Azure DevOps client.</param>
    /// <param name="folder">The pipeline folder path (e.g., <c>\dotnet\docker-tools</c>).</param>
    public static async Task<DefinitionsResponse> GetBuildDefinitionsAsync(
        this AzureDevOpsClient client, string folder) =>
        await client.GetAsJsonAsync(
            "_apis/build/definitions",
            AzureDevOpsJsonContext.Default.DefinitionsResponse,
            new() { ["path"] = folder, ["includeLatestBuilds"] = "true" });
}
