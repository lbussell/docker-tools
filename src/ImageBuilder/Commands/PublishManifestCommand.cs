// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Image;
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    /// <summary>
    /// Creates and pushes Docker manifest lists to the registry.
    /// Digest recording has been moved to <see cref="CreateManifestListCommand"/>.
    /// </summary>
    public class PublishManifestCommand : ManifestCommand<PublishManifestOptions, PublishManifestOptionsBuilder>
    {
        private readonly IDockerService _dockerService;
        private readonly ILogger<PublishManifestCommand> _logger;
        private readonly IRegistryCredentialsProvider _registryCredentialsProvider;

        public PublishManifestCommand(
            IManifestJsonService manifestJsonService,
            IDockerService dockerService,
            ILogger<PublishManifestCommand> logger,
            IRegistryCredentialsProvider registryCredentialsProvider) : base(manifestJsonService)
        {
            _dockerService = dockerService ?? throw new System.ArgumentNullException(nameof(dockerService));
            _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
            _registryCredentialsProvider = registryCredentialsProvider ?? throw new System.ArgumentNullException(nameof(registryCredentialsProvider));
        }

        protected override string Description => "Creates and publishes the manifest to the Docker Registry";

        public override async Task ExecuteAsync()
        {
            _logger.LogInformation("GENERATING MANIFESTS");

            if (!File.Exists(Options.ImageInfoPath))
            {
                _logger.LogInformation(PipelineHelper.FormatWarningCommand(
                    "Image info file not found. Skipping manifest publishing."));
                return;
            }

            ImageArtifactDetails imageArtifactDetails = ImageInfoHelper.LoadFromFile(Options.ImageInfoPath, Manifest);

            await _registryCredentialsProvider.ExecuteWithCredentialsAsync(
                Options.IsDryRun,
                () =>
                {
                    IReadOnlyList<ManifestListInfo> manifestLists =
                        ManifestListHelper.GetManifestListsForChangedImages(
                            Manifest, imageArtifactDetails, Options.RepoPrefix);

                    foreach (ManifestListInfo manifestListInfo in manifestLists)
                    {
                        _dockerService.CreateManifestList(manifestListInfo.Tag, manifestListInfo.PlatformTags, Options.IsDryRun);
                    }

                    Parallel.ForEach(manifestLists, manifestListInfo =>
                    {
                        _dockerService.PushManifestList(manifestListInfo.Tag, Options.IsDryRun);
                    });

                    WriteManifestSummary(manifestLists);

                    return Task.CompletedTask;
                },
                Options.CredentialsOptions,
                registryName: Manifest.Registry);
        }

        private void WriteManifestSummary(IReadOnlyList<ManifestListInfo> manifestLists)
        {
            _logger.LogInformation("MANIFEST TAGS PUBLISHED");

            foreach (ManifestListInfo manifestListInfo in manifestLists)
            {
                _logger.LogInformation(manifestListInfo.Tag);
            }

            if (manifestLists.Count == 0)
            {
                _logger.LogInformation("No manifests published");
            }

            _logger.LogInformation(string.Empty);
        }
    }
}
