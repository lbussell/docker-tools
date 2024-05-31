using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder;

#nullable enable
public class ImageDigestService(IManifestService manifestService) : IImageDigestService
{
    private readonly IManifestService _manifestService = manifestService;

    /// <summary>
    /// Retrieves the image digest for the specified image and checks if the digest is up-to-date
    /// with the latest published digest.
    /// </summary>
    /// <param name="image">The name of the image.</param>
    /// <param name="isDryRun">A flag indicating whether the operation is a dry run.</param>
    /// <returns>The image digest, or null if the image digest was not found.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a published digest is found for the specified image tag, but no matching digest value is found
    /// from the set of locally pulled digests. This indicates that the tag has been updated since it was last pulled.
    /// </exception>
    public async Task<string?> GetImageDigestAsync(string image, bool isDryRun)
    {
        IEnumerable<string> localDigests = DockerHelper.GetLocalImageDigests(image, isDryRun);

        // A digest will not exist for images that have been built locally or have been manually installed
        if (!localDigests.Any())
        {
            return null;
        }

        string latestDigestSha = await _manifestService.GetManifestDigestShaAsync(image, isDryRun);
        if (latestDigestSha is null)
        {
            return null;
        }

        string latestDigest = DockerHelper.GetDigestString(DockerHelper.GetRepo(image), latestDigestSha);
        if (!localDigests.Contains(latestDigest))
        {
            throw new InvalidOperationException($"""
                Found published digest '{latestDigestSha}' for tag '{image}' but could not find a matching digest value
                from the set of locally pulled digests for this tag: { string.Join(", ", localDigests) }. This most
                likely means that this tag has been updated since it was last pulled.
                """);
        }

        return latestDigest;
    }

    public Task<ManifestQueryResult> GetManifestAsync(string image, bool isDryRun) => throw new NotImplementedException();
}
