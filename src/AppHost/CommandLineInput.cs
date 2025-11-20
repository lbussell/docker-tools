// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma warning disable ASPIREINTERACTION001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.DotNet.DockerTools.AppHost;

static class CommandLineInput
{
    private static string s_previousArgs = string.Empty;

    public static IResourceBuilder<ProjectResource> WithPromptForCommandLineArgs(
        this IResourceBuilder<ProjectResource> builder)
    {
        return builder.WithArgs(async context =>
            {
                var interactionService =
                    context.ExecutionContext.ServiceProvider.GetRequiredService<IInteractionService>();

                var inputs = new List<InteractionInput>
                {
                    new()
                    {
                        Name = "Args",
                        InputType = InputType.Text,
                        Label = "Arguments",
                        // Show previous args as the placeholder
                        Placeholder = string.IsNullOrWhiteSpace(s_previousArgs)
                            ? "Enter command line arguments"
                            : s_previousArgs,
                        Required = false,
                    },
                };

                var result = await interactionService.PromptInputsAsync(
                    title: "Run Task",
                    message: "Provide parameters",
                    inputs: inputs,
                    cancellationToken: context.CancellationToken);

                if (result.Canceled) return;

                // If the user provided no input, reuse the previous args since they were shown as the placeholder
                var args = result.Data?.First(i => i.Name == "Args").Value ?? s_previousArgs;
                var argsEnum = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                // Prepend "--" to separate app args from dotnet args
                context.Args.Add("--");
                foreach (var arg in argsEnum)
                {
                    context.Args.Add(arg);
                }

                s_previousArgs = args;
            });
    }
}
