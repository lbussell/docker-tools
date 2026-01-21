// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder
{
    public static class ImageInfoHelper
    {
        /// <summary>
        /// Overrides all digests in the ImageArtifactDetails with the given registry options.
        /// </summary>
        /// <param name="imageInfo"></param>
        /// <param name="overrideOptions"></param>
        /// <returns></returns>
        public static ImageArtifactDetails ApplyRegistryOverride(
            this ImageArtifactDetails imageInfo,
            RegistryOptions overrideOptions)
        {
            if (string.IsNullOrEmpty(overrideOptions.Registry)
                && string.IsNullOrEmpty(overrideOptions.RepoPrefix))
            {
                return imageInfo;
            }

            foreach (RepoData repo in imageInfo.Repos)
            {
                foreach (ImageData imageData in repo.Images)
                {
                    if (imageData.Manifest is not null)
                    {
                        imageData.Manifest.Digest =
                            overrideOptions.ApplyOverrideToDigest(imageData.Manifest.Digest, repoName: repo.Repo);
                    }

                    foreach (PlatformData platformData in imageData.Platforms)
                    {
                        platformData.Digest =
                            overrideOptions.ApplyOverrideToDigest(platformData.Digest, repoName: repo.Repo);
                    }
                }
            }

            return imageInfo;
        }

        /// <summary>
        /// Gets all of the digests listed in the given image info.
        /// </summary>
        public static List<string> GetAllDigests(this ImageArtifactDetails imageInfo)
        {
            return imageInfo.Repos
                .SelectMany(repo => repo.Images)
                .SelectMany(GetAllDigests)
                .ToList();
        }

        public static List<string> GetAllDigests(this ImageData imageData)
        {
            // Platform specific digests
            IEnumerable<string> digests = imageData.Platforms.Select(platform => platform.Digest);

            // Include manifest list digest if it exists
            if (imageData.Manifest is not null)
            {
                digests = [ ..digests, imageData.Manifest.Digest ];
            }

            return digests.ToList();
        }

        public static List<ImageDigestInfo> GetAllImageDigestInfos(this ImageArtifactDetails imageInfo)
        {
            return imageInfo.Repos
                .SelectMany(repo => repo.Images)
                .SelectMany(GetAllImageDigestInfos)
                .ToList();
        }

        public static List<ImageDigestInfo> GetAllImageDigestInfos(this ImageData imageInfo)
        {
            List<ImageDigestInfo> imageDigestInfos = imageInfo.Platforms
                .Select(platform => new ImageDigestInfo(
                    Digest: platform.Digest,
                    Tags: platform.SimpleTags,
                    IsManifestList: false))
                .ToList();

            if (imageInfo.Manifest is not null)
            {
                imageDigestInfos.Add(new ImageDigestInfo(
                    Digest: imageInfo.Manifest.Digest,
                    Tags: imageInfo.Manifest.SharedTags,
                    IsManifestList: true));
            }

            return imageDigestInfos;
        }

        /// <summary>
        /// Loads an image info file as a parsed model directly with no validation or filtering.
        /// </summary>
        /// <param name="path">Path to the image info file.</param>
        /// <exception cref="InvalidDataException"/>
        public static ImageArtifactDetails DeserializeImageArtifactDetails(string path)
        {
            string imageInfoText = File.ReadAllText(path);
            return ImageArtifactDetailsHelper.FromJson(imageInfoText) ??
                throw new InvalidDataException($"Unable to deserialize image info file {path}");
        }

        /// <summary>
        /// Loads image info string content as a parsed model with context.
        /// </summary>
        /// <param name="imageInfoContent">The image info content to load.</param>
        /// <param name="manifest">Representation of the manifest model.</param>
        /// <param name="skipManifestValidation">
        /// Whether to skip validation if no associated manifest model item was found for a given image info model item.
        /// </param>
        /// <param name="useFilteredManifest">Whether to use the filtered content of the manifest for lookups.</param>
        /// <returns>An ImageArtifactContext containing the loaded details and manifest associations.</returns>
        public static ImageArtifactContext LoadFromContentWithContext(string imageInfoContent, ManifestInfo manifest,
            bool skipManifestValidation = false, bool useFilteredManifest = false)
        {
            ImageArtifactDetails imageArtifactDetails = ImageArtifactDetailsHelper.FromJson(imageInfoContent);
            ImageArtifactContext context = new(imageArtifactDetails);

            foreach (RepoData repoData in imageArtifactDetails.Repos)
            {
                RepoInfo manifestRepo = (useFilteredManifest ? manifest.FilteredRepos : manifest.AllRepos)
                    .FirstOrDefault(repo => repo.Name == repoData.Repo);
                if (manifestRepo == null)
                {
                    Console.WriteLine($"Image info repo not loaded: {repoData.Repo}");
                    continue;
                }

                foreach (ImageData imageData in repoData.Images)
                {
                    ImageInfo matchedImageInfo = null;

                    foreach (PlatformData platformData in imageData.Platforms)
                    {
                        foreach (ImageInfo manifestImage in useFilteredManifest ? manifestRepo.FilteredImages : manifestRepo.AllImages)
                        {
                            PlatformInfo matchingManifestPlatform = (useFilteredManifest ? manifestImage.FilteredPlatforms : manifestImage.AllPlatforms)
                                .FirstOrDefault(platform => ArePlatformsEqual(platformData, imageData, platform, manifestImage));
                            if (matchingManifestPlatform != null)
                            {
                                if (matchedImageInfo is null)
                                {
                                    matchedImageInfo = manifestImage;
                                }

                                context.SetPlatformContext(platformData, matchingManifestPlatform, manifestImage);
                                break;
                            }
                        }
                    }

                    if (matchedImageInfo != null)
                    {
                        context.SetImageContext(imageData, matchedImageInfo, manifestRepo);
                    }

                    PlatformData representativePlatform = imageData.Platforms.FirstOrDefault();
                    if (!skipManifestValidation && matchedImageInfo == null && representativePlatform != null)
                    {
                        throw new InvalidOperationException(
                            $"Unable to find matching platform in manifest for platform '{representativePlatform.GetIdentifier()}'.");
                    }
                }
            }

            return context;
        }

        /// <summary>
        /// Loads an image info file as a parsed model with context.
        /// </summary>
        /// <param name="path">Path to the image info file.</param>
        /// <param name="manifest">Representation of the manifest model.</param>
        /// <param name="skipManifestValidation">
        /// Whether to skip validation if no associated manifest model item was found for a given image info model item.
        /// </param>
        /// <param name="useFilteredManifest">Whether to use the filtered content of the manifest for lookups.</param>
        public static ImageArtifactContext LoadFromFileWithContext(string path, ManifestInfo manifest, bool skipManifestValidation = false, bool useFilteredManifest = false)
        {
            return LoadFromContentWithContext(File.ReadAllText(path), manifest, skipManifestValidation, useFilteredManifest);
        }

        /// <summary>
        /// Loads image info string content as a parsed model.
        /// </summary>
        /// <param name="imageInfoContent">The image info content to load.</param>
        /// <param name="manifest">Representation of the manifest model.</param>
        /// <param name="skipManifestValidation">
        /// Whether to skip validation if no associated manifest model item was found for a given image info model item.
        /// </param>
        /// <param name="useFilteredManifest">Whether to use the filtered content of the manifest for lookups.</param>
        public static ImageArtifactDetails LoadFromContent(string imageInfoContent, ManifestInfo manifest,
            bool skipManifestValidation = false, bool useFilteredManifest = false)
        {
            // Use context-based loading internally
            ImageArtifactContext context = LoadFromContentWithContext(imageInfoContent, manifest, skipManifestValidation, useFilteredManifest);
            return context.Details;
        }

        /// <summary>
        /// Loads an image info file as a parsed model.
        /// </summary>
        /// <param name="path">Path to the image info file.</param>
        /// <param name="manifest">Representation of the manifest model.</param>
        /// <param name="skipManifestValidation">
        /// Whether to skip validation if no associated manifest model item was found for a given image info model item.
        /// </param>
        /// <param name="useFilteredManifest">Whether to use the filtered content of the manifest for lookups.</param>
        public static ImageArtifactDetails LoadFromFile(string path, ManifestInfo manifest, bool skipManifestValidation = false, bool useFilteredManifest = false)
        {
            return LoadFromContent(File.ReadAllText(path), manifest, skipManifestValidation, useFilteredManifest);
        }

        /// <summary>
        /// Finds the <see cref="PlatformData"/> that matches the given <see cref="PlatformInfo"/>.
        /// </summary>
        /// <param name="platform">Platform being searched.</param>
        /// <param name="repo">Repo that corresponds to the platform.</param>
        /// <param name="imageArtifactDetails">Image info content.</param>
        public static (PlatformData Platform, ImageData Image)? GetMatchingPlatformData(PlatformInfo platform, RepoInfo repo, ImageArtifactDetails imageArtifactDetails)
        {
            RepoData repoData = imageArtifactDetails.Repos.FirstOrDefault(s => s.Repo == repo.Name);
            if (repoData == null || repoData.Images == null)
            {
                return null;
            }

            foreach (ImageData imageData in repoData.Images)
            {
                PlatformData platformData = imageData.Platforms
                    .FirstOrDefault(pd => IsPlatformMatch(pd, platform));
                if (platformData != null)
                {
                    return (platformData, imageData);
                }
            }

            return null;
        }

        /// <summary>
        /// Checks if a PlatformData matches a PlatformInfo by comparing identifying properties.
        /// </summary>
        private static bool IsPlatformMatch(PlatformData platformData, PlatformInfo platformInfo)
        {
            return platformData.Dockerfile == platformInfo.DockerfilePathRelativeToManifest &&
                   platformData.Architecture == platformInfo.Model.Architecture.GetDisplayName() &&
                   platformData.OsType == platformInfo.Model.OS.ToString() &&
                   platformData.OsVersion == platformInfo.Model.OsVersion;
        }

        /// <summary>
        /// Finds the <see cref="PlatformData"/> that matches the given <see cref="PlatformInfo"/> using a context for lookups.
        /// </summary>
        /// <param name="platform">Platform being searched.</param>
        /// <param name="repo">Repo that corresponds to the platform.</param>
        /// <param name="context">Image artifact context containing lookup dictionaries.</param>
        public static (PlatformData Platform, ImageData Image)? GetMatchingPlatformData(PlatformInfo platform, RepoInfo repo, ImageArtifactContext context)
        {
            RepoData repoData = context.Details.Repos.FirstOrDefault(s => s.Repo == repo.Name);
            if (repoData == null || repoData.Images == null)
            {
                return null;
            }

            foreach (ImageData imageData in repoData.Images)
            {
                PlatformData platformData = imageData.Platforms
                    .FirstOrDefault(platformData => context.GetPlatformInfo(platformData) == platform);
                if (platformData != null)
                {
                    return (platformData, imageData);
                }
            }

            return null;
        }

        public static void MergeImageArtifactDetails(ImageArtifactDetails src, ImageArtifactDetails target, ImageInfoMergeOptions options = null)
        {
            if (options == null)
            {
                options = new ImageInfoMergeOptions();
            }

            MergeData(src, target, options);
        }

        /// <summary>
        /// Merges image artifact details using contexts to match images by their manifest associations.
        /// </summary>
        public static void MergeImageArtifactDetails(
            ImageArtifactContext srcContext,
            ImageArtifactContext targetContext,
            ImageInfoMergeOptions options = null)
        {
            if (options == null)
            {
                options = new ImageInfoMergeOptions();
            }

            ImageArtifactDetails src = srcContext.Details;
            ImageArtifactDetails target = targetContext.Details;

            // Merge repos
            foreach (RepoData srcRepo in src.Repos)
            {
                RepoData targetRepo = target.Repos.FirstOrDefault(r => r.Repo == srcRepo.Repo);
                if (targetRepo == null)
                {
                    target.Repos.Add(srcRepo);
                    
                    // Copy context mappings for all images in the added repo
                    foreach (ImageData srcImage in srcRepo.Images)
                    {
                        ImageInfo srcImageInfo = srcContext.GetImageInfo(srcImage);
                        if (srcImageInfo != null)
                        {
                            RepoInfo repoInfo = srcContext.GetRepoInfo(srcImage);
                            targetContext.SetImageContext(srcImage, srcImageInfo, repoInfo);
                            
                            foreach (PlatformData platform in srcImage.Platforms)
                            {
                                PlatformInfo platformInfo = srcContext.GetPlatformInfo(platform);
                                if (platformInfo != null)
                                {
                                    ImageInfo platformImageInfo = srcContext.GetImageInfoForPlatform(platform);
                                    targetContext.SetPlatformContext(platform, platformInfo, platformImageInfo);
                                }
                            }
                        }
                    }
                    continue;
                }

                // Merge images within the repo using context-based matching
                foreach (ImageData srcImage in srcRepo.Images)
                {
                    ImageInfo srcImageInfo = srcContext.GetImageInfo(srcImage);

                    // Find matching target image - they should map to the same manifest ImageInfo
                    // Since the same manifest is used for loading all files, ImageInfo references should match
                    ImageData targetImage = null;
                    
                    if (srcImageInfo != null)
                    {
                        targetImage = targetRepo.Images
                            .FirstOrDefault(img => targetContext.GetImageInfo(img) == srcImageInfo);
                    }

                    if (targetImage != null)
                    {
                        // Merge the images
                        MergeData(srcImage, targetImage, options);
                    }
                    else
                    {
                        // No matching image found, add the source image
                        targetRepo.Images.Add(srcImage);
                        
                        // Copy context mappings for the added image to target context
                        if (srcImageInfo != null)
                        {
                            RepoInfo repoInfo = srcContext.GetRepoInfo(srcImage);
                            targetContext.SetImageContext(srcImage, srcImageInfo, repoInfo);
                            
                            // Also copy platform mappings
                            foreach (PlatformData platform in srcImage.Platforms)
                            {
                                PlatformInfo platformInfo = srcContext.GetPlatformInfo(platform);
                                if (platformInfo != null)
                                {
                                    ImageInfo platformImageInfo = srcContext.GetImageInfoForPlatform(platform);
                                    targetContext.SetPlatformContext(platform, platformInfo, platformImageInfo);
                                }
                            }
                        }
                    }
                }

                // Sort images
                targetRepo.Images.Sort();
            }

            // Sort repos
            target.Repos.Sort();
        }

        private static bool ArePlatformsEqual(PlatformData platformData, ImageData imageData, PlatformInfo platform, ImageInfo manifestImage)
        {
            PlatformData otherPlatform = PlatformDataFactory.FromPlatformInfo(platform, manifestImage);
            // Compare without product version since we compare versions separately
            return !platformData.HasDifferentTagState(otherPlatform) &&
                platformData.GetIdentifier() == otherPlatform.GetIdentifier() &&
                AreProductVersionsEquivalent(imageData.ProductVersion, manifestImage.ProductVersion);
        }

        private static bool AreProductVersionsEquivalent(string productVersion1, string productVersion2)
        {
            if (productVersion1 == productVersion2)
            {
                return true;
            }

            // Product versions are considered equivalent if the major and minor segments are the same
            // See https://github.com/dotnet/docker-tools/issues/688
            return Version.TryParse(productVersion1, out Version version1) &&
                Version.TryParse(productVersion2, out Version version2) &&
                version1.ToString(2) == version2.ToString(2);
        }

        private static void MergePropertyData(object srcObj, object targetObj, PropertyInfo property, ImageInfoMergeOptions options)
        {
            object srcResult = property.GetValue(srcObj);
            object targetResult = property.GetValue(targetObj);
            if (srcResult is null && targetResult != null)
            {
                property.SetValue(targetObj, null);
            }
            else if (srcResult != null && targetResult is null)
            {
                property.SetValue(targetObj, srcResult);
            }
            else
            {
                MergeData(srcResult, targetResult, options);
            }
        }

        private static void MergeData(object srcObj, object targetObj, ImageInfoMergeOptions options)
        {
            if (!((srcObj is null && targetObj is null) || (!(srcObj is null) && !(targetObj is null))))
            {
                throw new InvalidOperationException("The src and target objects must either be both null or both non-null.");
            }

            if (srcObj is null)
            {
                return;
            }

            if (srcObj.GetType() != targetObj.GetType())
            {
                throw new ArgumentException("Object types don't match.", nameof(targetObj));
            }

            IEnumerable<PropertyInfo> properties = srcObj.GetType().GetProperties()
                .Where(property => property.GetCustomAttribute<JsonIgnoreAttribute>() == null);

            foreach (PropertyInfo property in properties)
            {
                if (property.PropertyType == typeof(string) ||
                    property.PropertyType == typeof(DateTime) ||
                    property.PropertyType == typeof(bool))
                {
                    if (property.CanWrite)
                    {
                        property.SetValue(targetObj, property.GetValue(srcObj));
                    }
                }
                else if (typeof(IDictionary).IsAssignableFrom(property.PropertyType))
                {
                    MergeDictionaries(property, srcObj, targetObj, options);
                }
                else if (typeof(IList<string>).IsAssignableFrom(property.PropertyType))
                {
                    if (options.IsPublish && IsReplaceableValueProperty(srcObj, property))
                    {
                        // Tags can be merged or replaced depending on the scenario.
                        // When merging multiple image info files together into a single file, the tags should be
                        // merged to account for cases where tags for a given image are spread across multiple
                        // image info files.  But when publishing an image info file the source tags should replace
                        // the destination tags.  Any of the image's tags contained in the target should be considered
                        // obsolete and should be replaced by the source.  This accounts for the scenario where shared
                        // tags are moved from one image to another. If we had merged instead of replaced, then the
                        // shared tag would not have been removed from the original image in the image info in such
                        // a scenario.
                        // See:
                        // - https://github.com/dotnet/docker-tools/pull/269
                        // - https://github.com/dotnet/docker-tools/issues/289

                        ReplaceValue(property, srcObj, targetObj);
                    }
                    else
                    {
                        MergeStringLists(property, srcObj, targetObj);
                    }
                }
                else if (typeof(IList<Layer>).IsAssignableFrom(property.PropertyType))
                {
                    // Layers are always unique and should never get merged.
                    // Layers are already sorted according to their position in the image. They should not be sorted alphabetically.
                    ReplaceValue(property, srcObj, targetObj, skipListSorting: true);
                }
                else if (typeof(IList<ImageData>).IsAssignableFrom(property.PropertyType))
                {
                    MergeLists<ImageData>(property, srcObj, targetObj, options);
                }
                else if (typeof(IList<PlatformData>).IsAssignableFrom(property.PropertyType))
                {
                    MergeLists<PlatformData>(property, srcObj, targetObj, options);
                }
                else if (typeof(IList<RepoData>).IsAssignableFrom(property.PropertyType))
                {
                    MergeLists<RepoData>(property, srcObj, targetObj, options);
                }
                else if (typeof(ManifestData).IsAssignableFrom(property.PropertyType))
                {
                    MergePropertyData(srcObj, targetObj, property, options);
                }
                else
                {
                    throw new NotSupportedException($"Unsupported model property type: '{property.PropertyType.FullName}'");
                }
            }
        }

        private static bool IsReplaceableValueProperty(object srcObj, PropertyInfo property) =>
            (
                srcObj is PlatformData &&
                property.Name == nameof(PlatformData.SimpleTags)
            ) ||
            (
                srcObj is ManifestData &&
                (
                    property.Name == nameof(ManifestData.SharedTags) ||
                    property.Name == nameof(ManifestData.SyndicatedDigests)
                )
            );

        private static void ReplaceValue(PropertyInfo property, object srcObj, object targetObj, bool skipListSorting = false)
        {
            object value = property.GetValue(srcObj);
            if (value is IList<string> stringList && !skipListSorting)
            {
                value = stringList
                    .OrderBy(item => item)
                    .ToList<string>();
            }

            property.SetValue(targetObj, value);
        }

        private static void MergeStringLists(PropertyInfo property, object srcObj, object targetObj)
        {
            IList<string> srcList = (IList<string>)property.GetValue(srcObj);
            if (srcList == null)
            {
                return;
            }

            IList<string> targetList = (IList<string>)property.GetValue(targetObj);

            if (srcList.Any())
            {
                if (targetList != null)
                {
                    targetList = targetList
                        .Union(srcList)
                        .OrderBy(element => element)
                        .ToList();
                }
                else
                {
                    targetList = srcList;
                }

                property.SetValue(targetObj, targetList);
            }
        }

        private static void MergeLists<T>(PropertyInfo property, object srcObj, object targetObj, ImageInfoMergeOptions options)
            where T : IComparable<T>
        {
            IList<T> srcList = (IList<T>)property.GetValue(srcObj);
            if (srcList == null)
            {
                return;
            }

            IList<T> targetList = (IList<T>)property.GetValue(targetObj);

            if (srcList.Any())
            {
                if (targetList?.Any() == true)
                {
                    foreach (T srcItem in srcList)
                    {
                        T matchingTargetItem = targetList
                            .FirstOrDefault(targetItem => srcItem.CompareTo(targetItem) == 0);
                        if (matchingTargetItem != null)
                        {
                            MergeData(srcItem, matchingTargetItem, options);
                        }
                        else
                        {
                            targetList.Add(srcItem);
                        }
                    }
                }
                else
                {
                    targetList = srcList;
                }

                List<T> sortedList = targetList.ToList();
                sortedList.Sort();

                property.SetValue(targetObj, sortedList);
            }
        }

        private static void MergeDictionaries(PropertyInfo property, object srcObj, object targetObj,
            ImageInfoMergeOptions options)
        {
            IDictionary srcDict = (IDictionary)property.GetValue(srcObj);
            if (srcDict == null)
            {
                return;
            }

            IDictionary targetDict = (IDictionary)property.GetValue(targetObj);

            if (srcDict.Cast<object>().Any())
            {
                if (targetDict != null)
                {
                    foreach (dynamic kvp in srcDict)
                    {
                        if (targetDict.Contains(kvp.Key))
                        {
                            object newValue = kvp.Value;
                            if (newValue is string)
                            {
                                targetDict[kvp.Key] = newValue;
                            }
                            else
                            {
                                MergeData(kvp.Value, targetDict[kvp.Key], options);
                            }
                        }
                        else
                        {
                            targetDict[kvp.Key] = kvp.Value;
                        }
                    }
                }
                else
                {
                    property.SetValue(targetObj, srcDict);
                }
            }
        }
    }
}
