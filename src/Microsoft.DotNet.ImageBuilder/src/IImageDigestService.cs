using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder;

#nullable enable
public interface IImageDigestService
{
    Task<string?> GetImageDigestAsync(string image, bool isDryRun);
}
