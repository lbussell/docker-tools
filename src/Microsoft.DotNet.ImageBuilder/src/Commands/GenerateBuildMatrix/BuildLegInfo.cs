// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.DotNet.ImageBuilder.Commands.GenerateBuildMatrix;

#nullable enable
public record BuildLegInfo
{
    public required string Name { get; init; }

    public List<(string Name, string Value)> Variables { get; init; } = [];
}
