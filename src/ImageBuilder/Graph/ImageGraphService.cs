// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder;

public class GenerateImageGraphOptions : ManifestOptions, IFilterableOptions
{
    public ManifestFilterOptions FilterOptions { get; set; } = new();
}

public class GenerateImageGraphOptionsBuilder : ManifestOptionsBuilder
{
    private readonly ManifestFilterOptionsBuilder _manifestFilterOptionsBuilder = new();

    public override IEnumerable<Option> GetCliOptions() =>
    [
        ..base.GetCliOptions(),
        .._manifestFilterOptionsBuilder.GetCliOptions(),
    ];

    public override IEnumerable<Argument> GetCliArguments() =>
    [
        ..base.GetCliArguments(),
        .._manifestFilterOptionsBuilder.GetCliArguments(),
    ];
}

public class GenerateImageGraphCommand : ManifestCommand<GenerateImageGraphOptions, GenerateImageGraphOptionsBuilder>
{
    protected override string Description => "Generates a graph of image dependencies";

    public override Task ExecuteAsync()
    {
        var allImages = Manifest.GetAllImages().ToList();
        var allPlatforms = Manifest.GetAllPlatforms().ToList();
        Console.WriteLine($"There are {allImages.Count} images in the manifest.");
        Console.WriteLine($"There are {allPlatforms.Count} platforms in the manifest.");

        var filteredImages = Manifest.GetFilteredImages().ToList();
        var filteredPlatforms = Manifest.GetFilteredPlatforms().ToList();
        Console.WriteLine($"There are {filteredImages.Count} images after applying filters.");
        Console.WriteLine($"There are {filteredPlatforms.Count} platforms after applying filters.");

        var graph = GetDependencyGraph(filteredPlatforms);
        var mermaidDiagram = GenerateMermaidDiagram(graph);

        const string outputFilePath = "image-dependency-graph.mmd";
        File.WriteAllText(outputFilePath, mermaidDiagram);
        Console.WriteLine($"Wrote {outputFilePath}");

        return Task.CompletedTask;
    }

    private IEnumerable<ImageDependency> GetDependencyGraph(IEnumerable<PlatformInfo> platforms)
    {
        return platforms.SelectMany(GetPlatformDependencies);
    }

    private string GenerateMermaidDiagram(IEnumerable<ImageDependency> dependencies)
    {
        var sb = new StringBuilder();
        sb.AppendLine("flowchart LR");

        foreach (var dependency in dependencies)
        {
            sb.AppendLine($"    {SanitizeForMermaid(dependency.From.Tag)} -->|{dependency.Type.Label}| {SanitizeForMermaid(dependency.To.Tag)}");
        }

        return sb.ToString();
    }

    private static string SanitizeForMermaid(string tag)
    {
        // Mermaid node IDs can't contain special characters, so we create a sanitized ID
        // and use brackets to display the original tag as the label
        var nodeId = tag
            .Replace("/", "_")
            .Replace(":", "_")
            .Replace(".", "_")
            .Replace("-", "_");
        var label = tag.Replace("\"", "#quot;");
        return $"{nodeId}[\"{label}\"]";
    }

    private IEnumerable<ImageDependency> GetPlatformDependencies(PlatformInfo platform)
    {
        List<ImageDependency> dependencies = [];

        var thisPlatformInfo = SimplePlatformInfo.Create(platform);

        var internalDependencies =
            platform.InternalFromImages
                .Select(GetPlatformInfoByTag)
                .Select(SimplePlatformInfo.Create)
                .Select(dependencyPlatformInfo =>
                    new ImageDependency(From: thisPlatformInfo, To: dependencyPlatformInfo, DependencyType.BaseImage));

        // var externalDependencies =
        //     platform.ExternalFromImages
        //         .Select(tag => new ExternalPlatformInfo(tag))
        //         .Select(dependencyPlatformInfo =>
        //             new ImageDependency(From: thisPlatformInfo, To: dependencyPlatformInfo));

        // var customDependencies =
        //     platform.CustomLegGroups
        //         .SelectMany(customLegGroupKvp => customLegGroupKvp.Value.Dependencies)
        //         .Select(GetPlatformInfoByTag)
        //         .Select(SimplePlatformInfo.Create)
        //         .Select(dependencyPlatformInfo =>
        //             new ImageDependency(From: thisPlatformInfo, To: dependencyPlatformInfo));

        dependencies.AddRange([
            ..internalDependencies,
            // ..externalDependencies,
            // ..customDependencies,
        ]);

        return dependencies;
    }

    private PlatformInfo GetPlatformInfoByTag(string tag)
    {
        return Manifest.GetPlatformByTag(tag);
    }
}

internal readonly record struct ImageDependency(ISimplePlatformInfo From, ISimplePlatformInfo To, DependencyType Type);

internal record DependencyType(string Label)
{
    public static DependencyType BaseImage = new("has base image");
}

internal interface ISimplePlatformInfo
{
    string Tag { get; }
}

internal sealed record ExternalPlatformInfo(string Tag) : ISimplePlatformInfo;

internal sealed record SimplePlatformInfo(PlatformInfo PlatformInfo, string Tag) : ISimplePlatformInfo
{
    public static SimplePlatformInfo Create(PlatformInfo platformInfo)
    {
        // Use the longest tag
        var tag = platformInfo.Tags.OrderByDescending(t => t.FullyQualifiedName.Length).First().FullyQualifiedName;
        return new SimplePlatformInfo(platformInfo, tag);
    }
}
