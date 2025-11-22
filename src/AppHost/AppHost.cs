// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma warning disable ASPIRECSHARPAPPS001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable ASPIREINTERACTION001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

using Microsoft.DotNet.DockerTools.AppHost.Resources;
using Microsoft.Extensions.DependencyInjection;

var builder = DistributedApplication.CreateBuilder(args);

// Add build/publish registry
var registry = builder.AddZotRegistry("zot-registry")
    .WithLifetime(ContainerLifetime.Persistent);

// Add kusto cluster for build telemetry
var kustoClusterName = builder
    .AddParameter("kustoClusterName", "Kusto cluster name")
    .Hidden();
var kustoClusterRg = builder
    .AddParameter("kustoClusterResourceGroup", "Kusto cluster resource group")
    .Hidden();
var kusto = builder.AddAzureKustoCluster("dotnettel")
    .RunAsEmulator()
    .PublishAsExisting(kustoClusterName, kustoClusterRg)
    .AddReadWriteDatabase("Telemetry", "Telemetry");

// Add ImageBuilder
builder.AddCSharpApp("ImageBuilder", "../ImageBuilder")
    .WaitFor(registry)
    .WithExplicitStart()
    .WithReference(kusto)
    .WithEnvironment("publishConfig__buildAcr__server", registry.Resource.RegistryEndpoint)
    .WithEnvironment("publishConfig__publishAcr__server", registry.Resource.RegistryEndpoint)
    .WithBuildCommand();

builder.Build().Run();
