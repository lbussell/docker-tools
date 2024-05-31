using System.ComponentModel.Composition;

namespace Microsoft.DotNet.ImageBuilder;

#nullable enable

[Export(typeof(IImageDigestServiceFactory))]
[method: ImportingConstructor]
public class ImageDigestServiceFactory(IManifestServiceFactory manifestServiceFactory) : IImageDigestServiceFactory
{
    private readonly IManifestServiceFactory _manifestServiceFactory = manifestServiceFactory;

    public IImageDigestService Create(string? ownedAcr = null, IRegistryCredentialsHost? credsHost = null)
        => new ImageDigestService(_manifestServiceFactory.Create(ownedAcr, credsHost));

    public IImageDigestService Create(IManifestService manifestService)
        => new ImageDigestService(manifestService);
}
