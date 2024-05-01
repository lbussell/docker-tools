// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;
using System.Threading.Tasks;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder
{
    public class ManifestService : IManifestService
    {
        private readonly IRegistryContentClientFactory _registryClientFactory;
        private readonly RegistryAuthContext _registryAuthContext;

        public ManifestService(IRegistryContentClientFactory registryClientFactory, RegistryAuthContext registryAuthContext)
        {
            _registryClientFactory = registryClientFactory;
            _registryAuthContext = registryAuthContext;
        }

        public Task<ManifestQueryResult> GetManifestAsync(string image, bool isDryRun)
        {
            if (isDryRun)
            {
                return Task.FromResult(new ManifestQueryResult("", new JsonObject()));
            }

            ImageName imageName = ImageName.Parse(image, autoResolveImpliedNames: true);

            IRegistryContentClient registryClient = _registryClientFactory.Create(imageName.Registry!, imageName.Repo, _registryAuthContext);
            return registryClient.GetManifestAsync((imageName.Tag ?? imageName.Digest)!);
        }
    }
}
#nullable disable
