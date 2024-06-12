﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder.Models.Subscription;

public class ManifestOnlySubscription
{
    [JsonProperty(Required = Required.Always)]
    public SubscriptionManifest Manifest { get; set; }

    public string Id => $"{Manifest.Owner}/{Manifest.Repo}/{Manifest.Branch}/{Manifest.Path}";

    public string OsType { get; set; }

    public override string ToString() => Id;
}
