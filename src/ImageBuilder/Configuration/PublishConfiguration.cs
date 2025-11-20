// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
#nullable enable

using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.DotNet.ImageBuilder.Configuration;

public sealed record PublishConfiguration
{
    public const string AppSettingsSection = "publishConfig";

    public AcrConfiguration? BuildAcr { get; init; }
    public AcrConfiguration? PublishAcr { get; init; }
    public AcrConfiguration? InternalMirrorAcr { get; init; }
    public AcrConfiguration? PublicMirrorAcr { get; init; }
}

/// <summary>
/// Configuration for an Azure Container Registry endpoint.
/// </summary>
public sealed record AcrConfiguration
{
    public required string Server { get; init; }
    public string? ResourceGroup { get; init; } = null;
    public string? Subscription { get; init; } = null;
    public string? RepoPrefix { get; init; } = null;
    public ServiceConnectionOptions? ServiceConnection { get; init; } = null;
}

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPublishConfiguration(this IServiceCollection services)
    {
        services
            .AddOptions<PublishConfiguration>()
            .BindConfiguration(PublishConfiguration.AppSettingsSection);

        return services;
    }
}
