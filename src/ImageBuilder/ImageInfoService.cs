// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder;

/// <inheritdoc />
public class ImageInfoService(IFileSystem fileSystem) : IImageInfoService
{
    private readonly IFileSystem _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

    /// <inheritdoc />
    public ImageArtifactDetails DeserializeImageArtifactDetails(string path)
    {
        var imageInfoText = _fileSystem.ReadAllText(path);
        return ImageArtifactDetails.FromJson(imageInfoText) ??
            throw new InvalidDataException($"Unable to deserialize image info file {path}");
    }

    /// <inheritdoc />
    public ImageArtifactDetails LoadFromFile(
        string path,
        ManifestInfo manifest,
        bool skipManifestValidation = false,
        bool useFilteredManifest = false) =>
        ImageInfoHelper.LoadFromContent(
            _fileSystem.ReadAllText(path),
            manifest,
            skipManifestValidation,
            useFilteredManifest);

    /// <inheritdoc />
    public ImageArtifactContext CreateContext(
        ImageArtifactDetails details,
        ManifestInfo manifest,
        bool skipManifestValidation = false,
        bool useFilteredManifest = false)
    {
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(manifest);

        var platformInfoMap = new Dictionary<PlatformData, PlatformInfo>();
        var platformImageInfoMap = new Dictionary<PlatformData, ImageInfo>();
        var manifestImageMap = new Dictionary<ImageData, ImageInfo>();
        var manifestRepoMap = new Dictionary<ImageData, RepoInfo>();

        foreach (var repoData in details.Repos)
        {
            var manifestRepo = (useFilteredManifest ? manifest.FilteredRepos : manifest.AllRepos)
                .FirstOrDefault(repo => repo.Name == repoData.Repo);
            if (manifestRepo is null)
            {
                Console.WriteLine($"Image info repo not loaded: {repoData.Repo}");
                continue;
            }

            foreach (var imageData in repoData.Images)
            {
                manifestRepoMap[imageData] = manifestRepo;

                foreach (var platformData in imageData.Platforms)
                {
                    foreach (var manifestImage in useFilteredManifest ? manifestRepo.FilteredImages : manifestRepo.AllImages)
                    {
                        var matchingManifestPlatform = (useFilteredManifest ? manifestImage.FilteredPlatforms : manifestImage.AllPlatforms)
                            .FirstOrDefault(platform => ImageInfoHelper.ArePlatformsEqual(platformData, imageData, platform, manifestImage));
                        if (matchingManifestPlatform is not null)
                        {
                            if (!manifestImageMap.ContainsKey(imageData))
                            {
                                manifestImageMap[imageData] = manifestImage;
                            }

                            platformInfoMap[platformData] = matchingManifestPlatform;
                            platformImageInfoMap[platformData] = manifestImage;
                            break;
                        }
                    }
                }

                var representativePlatform = imageData.Platforms.FirstOrDefault();
                if (!skipManifestValidation && !manifestImageMap.ContainsKey(imageData) && representativePlatform is not null)
                {
                    throw new InvalidOperationException(
                        $"Unable to find matching platform in manifest for platform '{representativePlatform.GetIdentifier()}'.");
                }
            }
        }

        // Dual-write: also set the [JsonIgnore] navigation properties on the models
        // so existing consumers that read them directly continue to work.
        // This will be removed once all consumers are migrated to use ImageArtifactContext.
        foreach (var (platformData, platformInfo) in platformInfoMap)
        {
            platformData.PlatformInfo = platformInfo;
        }

        foreach (var (platformData, imageInfo) in platformImageInfoMap)
        {
            platformData.ImageInfo = imageInfo;
        }

        foreach (var (imageData, imageInfo) in manifestImageMap)
        {
            imageData.ManifestImage = imageInfo;
        }

        foreach (var (imageData, repoInfo) in manifestRepoMap)
        {
            imageData.ManifestRepo = repoInfo;
        }

        return new ImageArtifactContext(details, platformInfoMap, platformImageInfoMap, manifestImageMap, manifestRepoMap);
    }

    /// <inheritdoc />
    public ImageArtifactContext LoadContext(
        string path,
        ManifestInfo manifest,
        bool skipManifestValidation = false,
        bool useFilteredManifest = false)
    {
        var details = DeserializeImageArtifactDetails(path);
        return CreateContext(details, manifest, skipManifestValidation, useFilteredManifest);
    }
}
