// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.ViewModel;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public partial class MergeImageInfoCommand : ManifestCommand<MergeImageInfoOptions, MergeImageInfoOptionsBuilder>
    {
        protected override string Description => "Merges the content of multiple image info files into one file";

        public override Task ExecuteAsync()
        {
            IEnumerable<string> imageInfoFiles = Directory.EnumerateFiles(
                Options.SourceImageInfoFolderPath,
                "*.json",
                SearchOption.AllDirectories);

            // Load image info files with context for ViewModel lookups
            List<(string Path, ImageArtifactContext Context)> srcContextList = imageInfoFiles
                .OrderBy(file => file) // Ensure the files are ordered for testing consistency between OS's.
                .Select(imageDataPath =>
                    (imageDataPath, ImageInfoHelper.LoadFromFileWithContext(
                                        imageDataPath,
                                        Manifest,
                                        skipManifestValidation: Options.IsPublishScenario)))
                .ToList();

            if (!srcContextList.Any())
            {
                throw new InvalidOperationException(
                    $"No JSON files found in source folder '{Options.SourceImageInfoFolderPath}'");
            }

            ImageInfoMergeOptions options = new()
            {
                IsPublish = Options.IsPublishScenario
            };

            // Keep track of initial state to identify updated images
            ImageArtifactContext? initialContext = null;

            ImageArtifactContext targetContext;
            if (Options.InitialImageInfoPath != null)
            {
                targetContext = srcContextList.First(item => item.Path == Options.InitialImageInfoPath).Context;

                // Store a deep copy of the initial state for comparison if CommitUrlOverride is specified
                if (!string.IsNullOrEmpty(Options.CommitOverride))
                {
                    initialContext = ImageInfoHelper.LoadFromContentWithContext(
                        JsonHelper.SerializeObject(targetContext.Details),
                        Manifest,
                        skipManifestValidation: Options.IsPublishScenario
                    );
                }

                if (Options.IsPublishScenario)
                {
                    RemoveOutOfDateContent(targetContext);
                }
            }
            else
            {
                targetContext = new ImageArtifactContext(new ImageArtifactDetails());
            }

            foreach (ImageArtifactContext srcContext in
                srcContextList
                    .Select(item => item.Context)
                    .Where(ctx => ctx.Details != targetContext.Details))
            {
                ImageInfoHelper.MergeImageArtifactDetails(srcContext, targetContext, options);
            }

            // Apply CommitUrl override to updated images
            if (!string.IsNullOrEmpty(Options.CommitOverride) && initialContext != null)
            {
                ApplyCommitOverrideToUpdatedImages(targetContext, initialContext, Options.CommitOverride);
            }

            string destinationContents = JsonHelper.SerializeObject(targetContext.Details) + Environment.NewLine;
            File.WriteAllText(Options.DestinationImageInfoPath, destinationContents);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Applies a commit URL override to platforms that have been updated
        /// since the initial state.
        /// </summary>
        private static void ApplyCommitOverrideToUpdatedImages(
            ImageArtifactContext currentContext,
            ImageArtifactContext initialContext,
            string commitOverride)
        {
            // If commitOverride does not contain a valid SHA, throw an error
            if (!CommitShaRegex.IsMatch(commitOverride))
            {
                throw new ArgumentException(
                    $"The commit override '{commitOverride}' is not a valid SHA.",
                    nameof(commitOverride));
            }

            foreach (RepoData currentRepo in currentContext.Details.Repos)
            {
                RepoData? initialRepo = initialContext.Details.Repos
                    .FirstOrDefault(r => r.CompareTo(currentRepo) == 0);

                foreach (ImageData currentImage in currentRepo.Images)
                {
                    // Match images by ProductVersion since ManifestImage is no longer available on the model.
                    // Use context to get the ManifestImage for checking if it's a valid image.
                    ImageInfo? currentManifestImage = currentContext.GetImageInfo(currentImage);

                    ImageData? initialImage = null;
                    if (initialRepo is not null && currentManifestImage is not null)
                    {
                        // Find initial image that has the same ManifestImage (via context lookup)
                        initialImage = initialRepo.Images
                            .FirstOrDefault(i => initialContext.GetImageInfo(i) == currentManifestImage);
                    }

                    // If no match found by ManifestImage reference, the currentImage is treated as new

                    foreach (PlatformData currentPlatform in currentImage.Platforms)
                    {
                        PlatformData? initialPlatform = initialImage?.Platforms
                            .FirstOrDefault(p => p.CompareTo(currentPlatform) == 0);

                        // If platform doesn't exist in initial or has been updated (different digest or commit),
                        // override CommitUrl
                        if (initialPlatform is null
                            || initialPlatform.Digest != currentPlatform.Digest
                            || initialPlatform.CommitUrl != currentPlatform.CommitUrl)
                        {
                            if (!string.IsNullOrEmpty(currentPlatform.CommitUrl))
                            {
                                // Replace the commit SHA in the current URL with the one from the override
                                currentPlatform.CommitUrl =
                                    CommitShaRegex.Replace(currentPlatform.CommitUrl, commitOverride);
                            }
                        }
                    }
                }
            }
        }

        private void RemoveOutOfDateContent(ImageArtifactContext context)
        {
            ImageArtifactDetails imageArtifactDetails = context.Details;

            for (int repoIndex = imageArtifactDetails.Repos.Count - 1; repoIndex >= 0; repoIndex--)
            {
                RepoData repoData = imageArtifactDetails.Repos[repoIndex];

                // Since the registry name is not represented in the image info, make sure to compare the repo name with the
                // manifest's repo model name which isn't registry-qualified.
                RepoInfo? manifestRepo = Manifest.AllRepos.FirstOrDefault(manifestRepo => manifestRepo.Name == repoData.Repo);

                // If there doesn't exist a matching repo in the manifest, remove it from the image info
                if (manifestRepo is null)
                {
                    imageArtifactDetails.Repos.Remove(repoData);
                    continue;
                }

                for (int imageIndex = repoData.Images.Count - 1; imageIndex >= 0; imageIndex--)
                {
                    ImageData imageData = repoData.Images[imageIndex];
                    ImageInfo? manifestImage = context.GetImageInfo(imageData);

                    // If there doesn't exist a matching image in the manifest, remove it from the image info
                    if (manifestImage is null)
                    {
                        repoData.Images.Remove(imageData);
                        continue;
                    }

                    for (int platformIndex = imageData.Platforms.Count - 1; platformIndex >= 0; platformIndex--)
                    {
                        PlatformData platformData = imageData.Platforms[platformIndex];
                        PlatformInfo? platformInfo = context.GetPlatformInfo(platformData);
                        PlatformInfo? manifestPlatform = platformInfo is not null
                            ? manifestImage.AllPlatforms.FirstOrDefault(mp => mp == platformInfo)
                            : null;

                        // If there doesn't exist a matching platform in the manifest, remove it from the image info
                        if (manifestPlatform is null)
                        {
                            imageData.Platforms.Remove(platformData);
                        }
                    }
                }
            }

            if (imageArtifactDetails.Repos.Count == 0)
            {
                // Failsafe to prevent wiping out the image info due to a bug in the logic
                throw new InvalidOperationException(
                    "Removal of out-of-date content resulted in there being no content remaining in the target image info file. Something is probably wrong with the logic.");
            }
        }

        [GeneratedRegex(@"[0-9a-f]{40}")]
        public static partial Regex CommitShaRegex { get; }
    }
}
