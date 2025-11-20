// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.DockerTools.AppHost.Resources;

internal sealed class ZotResource(string name) : ContainerResource(name), IContainerRegistry
{
    internal const string RegistryEndpointName = "registry";

    private readonly string _name = name;

    // Fields for lazy initialization
    private ReferenceExpression? _registryReferenceExpression;
    private EndpointReference? _registryEndpoint = null;

    public ReferenceExpression RegistryEndpoint => ((IContainerRegistry)this).Endpoint;

    // This actually gets the endpoint from the resource
    private EndpointReference RegistryEndpointReference =>
        _registryEndpoint ??= new EndpointReference(this, RegistryEndpointName);

    // Implement IContainerRegistry
    ReferenceExpression IContainerRegistry.Endpoint => _registryReferenceExpression ??= BuildRegistryEndpoint();
    ReferenceExpression IContainerRegistry.Name => ReferenceExpression.Create($"{_name}");

    private ReferenceExpression BuildRegistryEndpoint()
    {
        var builder = new ReferenceExpressionBuilder();
        builder.Append($"{RegistryEndpointReference.Property(EndpointProperty.HostAndPort)}");
        return builder.Build();
    }
}
