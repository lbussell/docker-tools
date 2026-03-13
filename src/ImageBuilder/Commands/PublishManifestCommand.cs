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
        private readonly IManifestListService _manifestListService;

        public PublishManifestCommand(
            IManifestJsonService manifestJsonService,
            IDockerService dockerService,
            ILogger<PublishManifestCommand> logger,
            IRegistryCredentialsProvider registryCredentialsProvider,
            IManifestListService manifestListService) : base(manifestJsonService)
        {
            _dockerService = dockerService ?? throw new System.ArgumentNullException(nameof(dockerService));
            _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
            _registryCredentialsProvider = registryCredentialsProvider ?? throw new System.ArgumentNullException(nameof(registryCredentialsProvider));
            _manifestListService = manifestListService ?? throw new System.ArgumentNullException(nameof(manifestListService));
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
                    IReadOnlyList<string> manifestTags = _manifestListService.CreateManifestLists(
                        Manifest, imageArtifactDetails, Options.RepoPrefix, Options.IsDryRun);

                    System.Threading.Tasks.Parallel.ForEach(manifestTags, tag =>
                    {
                        _dockerService.PushManifestList(tag, Options.IsDryRun);
                    });

                    WriteManifestSummary(manifestTags);

                    return Task.CompletedTask;
                },
                Options.CredentialsOptions,
                registryName: Manifest.Registry);
        }

        private void WriteManifestSummary(IReadOnlyList<string> manifestTags)
        {
            _logger.LogInformation("MANIFEST TAGS PUBLISHED");

            if (manifestTags.Count > 0)
            {
                foreach (string tag in manifestTags)
                {
                    _logger.LogInformation(tag);
                }
            }
            else
            {
                _logger.LogInformation("No manifests published");
            }

            _logger.LogInformation(string.Empty);
        }
    }
}
