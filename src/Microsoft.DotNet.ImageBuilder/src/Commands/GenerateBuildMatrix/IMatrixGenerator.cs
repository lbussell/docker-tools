// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder.Commands.GenerateBuildMatrix;

#nullable enable
public interface IMatrixGenerator
{
    string Key { get; }

    IEnumerable<BuildMatrixInfo> GenerateMatrixInfoAsync(IEnumerable<PlatformInfo> platforms);
}

[Export(typeof(IMatrixGenerator))]
public class SimpleMatrixGenerator : IMatrixGenerator
{
    public string Key => "simple";

    public IEnumerable<BuildMatrixInfo> GenerateMatrixInfoAsync(IEnumerable<PlatformInfo> platforms)
    {
        return [];
    }
}

[Export(typeof(IMatrixGenerator))]
public class ComplexMatrixGenerator : IMatrixGenerator
{
    public string Key => "complex";

    public IEnumerable<BuildMatrixInfo> GenerateMatrixInfoAsync(IEnumerable<PlatformInfo> platforms)
    {
        return [];
    }
}
