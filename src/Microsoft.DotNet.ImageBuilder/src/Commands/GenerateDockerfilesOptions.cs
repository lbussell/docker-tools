// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using System.Linq;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class GenerateDockerfilesOptions : GenerateArtifactsOptions
    {
        public DockerfileFilterOptions FilterOptions { get; set; } = new DockerfileFilterOptions();

        public GenerateDockerfilesOptions() : base()
        {
        }
    }

    public class GenerateDockerfilesOptionsBuilder : GenerateArtifactsOptionsBuilder
    {
        private readonly DockerfileFilterOptionsBuilder _dockerfileFilterOptionsBuilder = new();

        public override IEnumerable<Option> GetCliOptions() =>
            base.GetCliOptions()
                .Concat(_dockerfileFilterOptionsBuilder.GetCliOptions());

        public override IEnumerable<Argument> GetCliArguments() =>
            base.GetCliArguments()
                .Concat(_dockerfileFilterOptionsBuilder.GetCliArguments());
    }
}
