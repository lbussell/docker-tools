// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.DockerTools.AppHost.Resources;

internal sealed record BuildCommandAnnotation(string ManifestPath, bool Push, Dictionary<string, string> Variables) : IResourceAnnotation
{
    public IEnumerable<string> Args => GetArgs();

    private List<string> GetArgs()
    {
        List<string> args =
        [
            "build",
            "--manifest",
            ManifestPath,
        ];

        if (Push) args.Add("--push");

        foreach (var kvp in Variables)
        {
            args.Add("--var");
                args.Add($"{kvp.Key}={kvp.Value}");
        }

        return args;
    }
};
