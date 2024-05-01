﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder
{
    [Export(typeof(IDockerServiceFactory))]
    public class DockerServiceFactory : IDockerServiceFactory
    {
        [ImportingConstructor]
        public DockerServiceFactory()
        {
        }

        public IDockerService Create(IManifestService manifestService) => new DockerService(manifestService);

        public DockerServiceCache CreateCached(IManifestService manifestService) => new(Create(manifestService));
    }
}
