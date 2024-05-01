// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel.Composition;

namespace Microsoft.DotNet.ImageBuilder;

[Export(typeof(IManifestServiceFactory))]
[method: ImportingConstructor]
public class ManifestServiceFactory(IRegistryContentClientFactory registryClientFactory) : IManifestServiceFactory
{
    private readonly IRegistryContentClientFactory _registryClientFactory = registryClientFactory;

    public IManifestService Create(RegistryAuthContext registryAuthContext)
    {
        return new ManifestService(_registryClientFactory, registryAuthContext);
    }
}
