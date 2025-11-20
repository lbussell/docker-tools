// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Configuration;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Microsoft.Extensions.Options;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public abstract class ManifestCommand<TOptions, TOptionsBuilder> : Command<TOptions, TOptionsBuilder>, IManifestCommand
        where TOptions : ManifestOptions, new()
        where TOptionsBuilder : ManifestOptionsBuilder, new()
    {
        #nullable enable
        public ManifestCommand(IOptions<PublishConfiguration>? publishConfiguration = null)
        {
            PublishConfig = publishConfiguration?.Value;
        }

        protected PublishConfiguration? PublishConfig { get; }
        #nullable disable

        public ManifestInfo Manifest { get; private set; }

        public virtual void LoadManifest()
        {
            if (Manifest is null)
            {
                Manifest = ManifestInfo.Load(Options, PublishConfig);
            }
        }

        protected override void Initialize(TOptions options)
        {
            base.Initialize(options);
            LoadManifest();
        }
    }
}
