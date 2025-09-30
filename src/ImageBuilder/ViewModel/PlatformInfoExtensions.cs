// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Linq;

namespace Microsoft.DotNet.ImageBuilder.ViewModel;

public static class PlatformInfoExtensions
{
    extension(PlatformInfo platformInfo)
    {
        /// <summary>
        /// A unique identifier for a PlatformInfo's build args that can be
        /// used for comparing build args between different platforms.
        /// </summary>
        public string BuildArgsUniqueKey =>
            string.Join(
                separator: ',',
                values: platformInfo.BuildArgs
                    // Order by key to guarantee stable output.
                    .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
                    .Select(kvp => $"{kvp.Key}={kvp.Value ?? string.Empty}"));
    }
}
