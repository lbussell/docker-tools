// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma warning disable ASPIRECSHARPAPPS001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable ASPIREINTERACTION001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.DotNet.DockerTools.AppHost.Resources;

internal static class InputExtensions
{
}

internal static class ImageBuilderExtensions
{
    public static IResourceBuilder<ProjectResource> AddImageBuilder(
        this IDistributedApplicationBuilder builder, string name)
    {
        return builder.AddCSharpApp(name, "../ImageBuilder")
            // Add "--" arg to separate app args from dotnet args
            .WithArgs("--");
    }

    public static IResourceBuilder<ProjectResource> WithBuildCommand(this IResourceBuilder<ProjectResource> builder) =>
        builder.WithCommand("build", "Build", async context =>
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
                var variables = new Dictionary<string, string> { { "UniqueId", uniqueId } };

                // Don't use context.ResourceName, since that has extra stuff after the resource name, e.g. ImageBuilder-abcdefgh
                var thisResource = model.Resources.First(r => r.Name == "ImageBuilder");
                // Can't modify the args here.
                // We can add an annotation, and process it into CLI args as part of WithArgs(...)
                thisResource.Annotations.Add(new BuildCommandAnnotation(manifestValue, push, variables));

                return await commandService.ExecuteCommandAsync(thisResource, KnownResourceCommands.StartCommand, context.CancellationToken);
            })
            .WithArgs(async context =>
            {
                // Look for annotation added from command; if present, add args.
                if (context.Resource.TryGetLastAnnotation<BuildCommandAnnotation>(out var buildCommand))
                {
                    foreach (var arg in buildCommand.Args) context.Args.Add(arg);
                }
            });
}

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
