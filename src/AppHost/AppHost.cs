// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma warning disable ASPIRECSHARPAPPS001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

using Microsoft.DotNet.DockerTools.AppHost;
using Microsoft.DotNet.DockerTools.AppHost.Resources;

var builder = DistributedApplication.CreateBuilder(args);

var registry = builder.AddZotRegistry("zot-registry");

builder.AddCSharpApp("ImageBuilder", "../ImageBuilder")
    .WaitFor(registry)
    .WithExplicitStart()
    .WithPromptForCommandLineArgs()
    .WithEnvironment("publishConfig__buildAcr__server", registry.Resource.RegistryEndpoint)
    .WithEnvironment("publishConfig__publishAcr__server", registry.Resource.RegistryEndpoint);

builder.Build().Run();
