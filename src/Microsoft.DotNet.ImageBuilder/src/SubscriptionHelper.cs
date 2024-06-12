// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using LibGit2Sharp;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Models.Subscription;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Newtonsoft.Json;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder
{
    public static class SubscriptionHelper
    {
        public static IEnumerable<(T Subscription, ManifestInfo Manifest)> GetSubscriptionManifests<T>(
            string subscriptionsPath, ManifestFilterOptions filterOptions,
            IGitService gitService, Action<ManifestOptions>? configureOptions = null)
            where T : ManifestOnlySubscription
        {
            string subscriptionsJson = File.ReadAllText(subscriptionsPath);
            T[] subscriptions = JsonConvert.DeserializeObject<T[]>(subscriptionsJson)
                ?? throw new JsonException($"Unable to correctly deserialize path '{subscriptionsJson}'.");

            List<(T Subscription, ManifestInfo Manifest)> subscriptionManifests = [];
            foreach (T subscription in subscriptions)
            {
                ManifestInfo? manifest = GetSubscriptionManifest(
                    subscription, filterOptions, gitService, configureOptions);
                if (manifest is not null)
                {
                    subscriptionManifests.Add((subscription, manifest));
                }
            }

            return subscriptionManifests;
        }

        private static ManifestInfo? GetSubscriptionManifest<T>(T subscription,
            ManifestFilterOptions filterOptions, IGitService gitService, Action<ManifestOptions>? configureOptions)
            where T : ManifestOnlySubscription
        {
            // If the command is filtered with an OS type that does not match the OsType filter of the subscription,
            // then there are no images that need to be inspected.
            string osTypeRegexPattern = ManifestFilter.GetFilterRegexPattern(filterOptions.OsType);
            if (!string.IsNullOrEmpty(subscription.OsType) &&
                !Regex.IsMatch(subscription.OsType, osTypeRegexPattern, RegexOptions.IgnoreCase))
            {
                return null;
            }

            string uniqueName = $"{subscription.Manifest.Owner}-{subscription.Manifest.Repo}-{subscription.Manifest.Branch}";
            string repoPath = Path.Combine(Path.GetTempPath(), uniqueName);

            using IRepository repo = gitService.CloneRepository(
                $"https://github.com/{subscription.Manifest.Owner}/{subscription.Manifest.Repo}.git",
                repoPath,
                new CloneOptions
                {
                    BranchName = subscription.Manifest.Branch
                });

            try
            {
                TempManifestOptions manifestOptions = new(filterOptions)
                {
                    Manifest = Path.Combine(repoPath, subscription.Manifest.Path),
                    Variables = subscription.Manifest.Variables
                };

                configureOptions?.Invoke(manifestOptions);

                return ManifestInfo.Load(manifestOptions);
            }
            finally
            {
                repo.Dispose();
                FileHelper.ForceDeleteDirectory(repoPath);
            }
        }

        private class TempManifestOptions : ManifestOptions, IFilterableOptions
        {
            public TempManifestOptions(ManifestFilterOptions filterOptions)
            {
                FilterOptions = filterOptions;
            }

            public ManifestFilterOptions FilterOptions { get; }
        }
    }
}
#nullable disable
