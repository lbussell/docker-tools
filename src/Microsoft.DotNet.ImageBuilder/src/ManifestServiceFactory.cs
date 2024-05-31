// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.ComponentModel.Composition;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder
{
    [Export(typeof(IManifestServiceFactory))]
    [method: ImportingConstructor]
    public class ManifestServiceFactory(IRegistryContentClientFactory registryClientFactory) : IManifestServiceFactory
    {
        private readonly object _cacheLock = new();

        private readonly IRegistryContentClientFactory _registryClientFactory = registryClientFactory;

        private readonly Dictionary<string, IManifestService> _createdServices = [];

        public IManifestService Create(string? ownedAcr = null, IRegistryCredentialsHost? credsHost = null)
        {
            ownedAcr ??= string.Empty;
            return LockHelper.DoubleCheckedLockLookup(
                _cacheLock,
                _createdServices,
                ownedAcr,
                () => new ManifestService(_registryClientFactory, ownedAcr, credsHost));
        }
    }
}
