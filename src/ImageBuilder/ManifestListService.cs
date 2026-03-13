// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder;

/// <inheritdoc/>
public class ManifestListService : IManifestListService
{
    private readonly IDockerService _dockerService;

    public ManifestListService(IDockerService dockerService)
    {
        _dockerService = dockerService ?? throw new ArgumentNullException(nameof(dockerService));
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> CreateManifestLists(
        ManifestInfo manifest,
        ImageArtifactDetails imageArtifactDetails,
        string? repoPrefix,
        bool isDryRun)
    {
        ConcurrentBag<string> createdManifestTags = [];

        IEnumerable<(RepoInfo Repo, ImageInfo Image)> manifests = manifest.FilteredRepos
            .SelectMany(repo =>
                repo.FilteredImages
                    .Where(image => image.SharedTags.Any())
                    .Where(image => image.AllPlatforms
                        .Select(platform =>
                            ImageInfoHelper.GetMatchingPlatformData(platform, repo, imageArtifactDetails))
                        .Where(platformMapping => platformMapping != null)
                        .Any(platformMapping => !platformMapping?.Platform.IsUnchanged ?? false))
                    .Select(image => (repo, image)))
            .ToList();

        Parallel.ForEach(manifests, ((RepoInfo Repo, ImageInfo Image) repoImage) =>
        {
            CreateManifestsForImage(
                repoImage.Repo, repoImage.Image, manifest, imageArtifactDetails,
                repoPrefix, isDryRun, createdManifestTags);
        });

        return createdManifestTags.ToList().AsReadOnly();
    }

    private void CreateManifestsForImage(
        RepoInfo repo,
        ImageInfo image,
        ManifestInfo manifest,
        ImageArtifactDetails imageArtifactDetails,
        string? repoPrefix,
        bool isDryRun,
        ConcurrentBag<string> createdManifestTags)
    {
        // Create manifest lists for normal (non-syndicated) shared tags
        CreateManifestListsForTags(
            repo, image, manifest, imageArtifactDetails,
            image.SharedTags.Select(tag => tag.Name),
            tag => DockerHelper.GetImageName(manifest.Registry, repoPrefix + repo.Name, tag),
            platform => platform.Tags.First(),
            isDryRun, createdManifestTags);

        // Create manifest lists for syndicated repos
        IEnumerable<IGrouping<string, TagInfo>> syndicatedTagGroups = image.SharedTags
            .Where(tag => tag.SyndicatedRepo != null)
            .GroupBy(tag => tag.SyndicatedRepo);

        foreach (IGrouping<string, TagInfo> syndicatedTags in syndicatedTagGroups)
        {
            string syndicatedRepo = syndicatedTags.Key;
            IEnumerable<string> destinationTags = syndicatedTags.SelectMany(tag => tag.SyndicatedDestinationTags);

            CreateManifestListsForTags(
                repo, image, manifest, imageArtifactDetails,
                destinationTags,
                tag => DockerHelper.GetImageName(manifest.Registry, repoPrefix + syndicatedRepo, tag),
                platform => platform.Tags.FirstOrDefault(tag => tag.SyndicatedRepo == syndicatedRepo),
                isDryRun, createdManifestTags);
        }
    }

    private void CreateManifestListsForTags(
        RepoInfo repo,
        ImageInfo image,
        ManifestInfo manifest,
        ImageArtifactDetails imageArtifactDetails,
        IEnumerable<string> tags,
        Func<string, string> getImageName,
        Func<PlatformInfo, TagInfo?> getTagRepresentative,
        bool isDryRun,
        ConcurrentBag<string> createdManifestTags)
    {
        foreach (string tag in tags)
        {
            CreateManifestList(
                repo, image, manifest, imageArtifactDetails,
                tag, getImageName, getTagRepresentative,
                isDryRun, createdManifestTags);
        }
    }

    private void CreateManifestList(
        RepoInfo repo,
        ImageInfo image,
        ManifestInfo manifest,
        ImageArtifactDetails imageArtifactDetails,
        string tag,
        Func<string, string> getImageName,
        Func<PlatformInfo, TagInfo?> getTagRepresentative,
        bool isDryRun,
        ConcurrentBag<string> createdManifestTags)
    {
        string manifestListTag = getImageName(tag);
        List<string> images = [];

        foreach (PlatformInfo platform in image.AllPlatforms)
        {
            // Only include platforms that have entries in image-info (i.e., were actually built)
            (PlatformData Platform, ImageData Image)? platformMapping =
                ImageInfoHelper.GetMatchingPlatformData(platform, repo, imageArtifactDetails);

            if (platformMapping is null)
            {
                continue;
            }

            TagInfo? imageTag;
            if (platform.Tags.Any())
            {
                imageTag = getTagRepresentative(platform);
            }
            else
            {
                // Platform has no tags of its own - find a matching platform from another image
                PlatformInfo platformInfo = repo.AllImages
                    .SelectMany(img =>
                        img.AllPlatforms
                            .Select(p => (Image: img, Platform: p))
                            .Where(imagePlatform => platform != imagePlatform.Platform &&
                                PlatformInfo.AreMatchingPlatforms(image, platform, imagePlatform.Image, imagePlatform.Platform) &&
                                imagePlatform.Platform.Tags.Any()))
                    .FirstOrDefault()
                    .Platform;

                if (platformInfo is null)
                {
                    throw new InvalidOperationException(
                        $"Could not find a platform with concrete tags for '{platform.DockerfilePathRelativeToManifest}'.");
                }

                imageTag = getTagRepresentative(platformInfo);
            }

            if (imageTag is not null)
            {
                images.Add(getImageName(imageTag.Name));
            }
        }

        if (images.Count > 0)
        {
            createdManifestTags.Add(manifestListTag);
            _dockerService.CreateManifestList(manifestListTag, images, isDryRun);
        }
    }
}
