namespace Microsoft.DotNet.ImageBuilder;

#nullable enable
public interface IImageDigestServiceFactory
{
    public IImageDigestService Create(string? ownedAcr = null, IRegistryCredentialsHost? credsHost = null);
    public IImageDigestService Create(IManifestService manifestService);
}
