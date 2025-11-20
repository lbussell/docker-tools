// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.DockerTools.AppHost.Resources;

internal static class ZotResourceBuilderExtensions
{
    public static IResourceBuilder<ZotResource> AddZotRegistry(this IDistributedApplicationBuilder builder, string name)
    {
        var resource = new ZotResource(name);
        return builder.AddResource(resource)
            .WithImage(ZotContainerImageTags.Image)
            .WithImageRegistry(ZotContainerImageTags.Registry)
            .WithImageTag(ZotContainerImageTags.Tag)
            .WithHttpEndpoint(
                port: ZotContainerImageTags.HostPort,
                targetPort: ZotContainerImageTags.ContainerPort,
                name: ZotResource.RegistryEndpointName,
                // This is needed or else pushing images won't work
                isProxied: false)
            .WithUrlForEndpoint(
                ZotResource.RegistryEndpointName,
                url => url.DisplayText = "Browse images")
            // TODO: zot automatically stores 2GB of Trivy + Java vulnerability metadata in this volume.
            // Figure out how to stop this by default.
            .WithVolume("zot-registry", "/var/lib/registry");
    }
}
