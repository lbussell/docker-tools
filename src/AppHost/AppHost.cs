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
    .WithCommand("build", "Build", async context =>
    {
        var model = context.ServiceProvider.GetRequiredService<DistributedApplicationModel>();
        var commandService = context.ServiceProvider.GetRequiredService<ResourceCommandService>();
        var interactionService = context.ServiceProvider.GetRequiredService<IInteractionService>();

        const string defaultManifest = "../manifest.json";

        var inputs = new List<InteractionInput>
        {
            new()
            {
                Name = "manifest",
                InputType = InputType.Text,
                Label = "Manifest",
                Description = "Path to the manifest.json file to build.",
                Placeholder = defaultManifest,
                Required = false,
            },
            new()
            {
                Name = "push",
                InputType = InputType.Boolean,
                Label = "Push images to registry?",
                Description = "If checked, images will be pushed to the registry after being built.",
                Required = false,
            },
        };

        var result = await interactionService.PromptInputsAsync(
            title: "Build Images",
            message: "Provide build options",
            inputs: inputs,
            cancellationToken: context.CancellationToken);

        if (result.Canceled) return CommandResults.Canceled();

        var manifestValue = result.Data?.FirstOrDefault(i => i?.Name == "manifest", null)?.Value ?? defaultManifest;
        var push = bool.TryParse(result.Data?.FirstOrDefault(i => i?.Name == "push", null)?.Value, out var pushFlag) && pushFlag;

        var uniqueId = DateTime.UtcNow.ToString("yyyyMMddHHmm");
        var variables = new Dictionary<string, string> {{ "UniqueId", uniqueId }};

        // Don't use context.ResourceName, since that has extra stuff after the resource name, e.g. ImageBuilder-abcdefgh
        var thisResource = model.Resources.First(r => r.Name == "ImageBuilder");
        // Can't modify the args here.
        // We can add an annotation, and process it into CLI args as part of WithArgs(...)
        thisResource.Annotations.Add(new BuildCommandAnnotation(manifestValue, push, variables));

        return await commandService.ExecuteCommandAsync(thisResource, KnownResourceCommands.StartCommand, context.CancellationToken);
    })
    .WithArgs(async context =>
    {
        // Prepend "--" to separate app args from dotnet args
        context.Args.Add("--");

        // Look annotation added from command; if present, add args.
        if (context.Resource.TryGetLastAnnotation<BuildCommandAnnotation>(out var buildCommand))
        {
            foreach (var arg in buildCommand.Args) context.Args.Add(arg);
        }
    });

builder.Build().Run();
