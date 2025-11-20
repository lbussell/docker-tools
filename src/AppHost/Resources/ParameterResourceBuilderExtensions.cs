// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.DockerTools.AppHost.Resources;

public static class ParameterResourceBuilderExtensions
{
    /// <summary>
    /// Marks a <see cref="ParameterResource"/> as hidden by applying an initial state snapshot
    /// equivalent to the previous inline <c>WithInitialState</c> usage.
    /// </summary>
    /// <param name="builder">The parameter resource builder.</param>
    /// <returns>The same builder instance for chaining.</returns>
    public static IResourceBuilder<ParameterResource> Hidden(this IResourceBuilder<ParameterResource> builder)
    {
        return builder.WithInitialState(new CustomResourceSnapshot
        {
            ResourceType = nameof(ParameterResource),
            Properties = [],
            IsHidden = true
        });
    }
}
