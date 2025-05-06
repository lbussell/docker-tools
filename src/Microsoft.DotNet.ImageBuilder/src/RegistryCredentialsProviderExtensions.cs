// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Commands;

namespace Microsoft.DotNet.ImageBuilder;

#nullable enable

/// <summary>
/// Extension methods for <see cref="IRegistryCredentialsProvider"/> to simplify authentication workflows.
/// </summary>
internal static class RegistryCredentialsProviderExtensions
{
    /// <summary>
    /// Executes an asynchronous action with the necessary registry credentials.
    /// </summary>
    /// <param name="credsProvider">The registry credentials provider.</param>
    /// <param name="isDryRun">Indicates whether this is a dry run. When true, no actual login is performed.</param>
    /// <param name="action">The asynchronous action to execute with credentials.</param>
    /// <param name="credentialsOptions">Options for retrieving credentials.</param>
    /// <param name="registryName">The name of the registry to authenticate with.</param>
    /// <param name="ownedAcr">The name of the owned Azure Container Registry, if applicable.</param>
    /// <remarks>
    /// This method handles the login workflow before executing the action and ensures logout occurs
    /// after the action completes, even if exceptions are thrown.
    /// </remarks>
    public static async Task ExecuteWithCredentialsAsync(
        this IRegistryCredentialsProvider credsProvider,
        bool isDryRun,
        Func<Task> action,
        RegistryCredentialsOptions credentialsOptions,
        string registryName,
        string? ownedAcr)
    {
        bool loggedIn = await LogInToRegistry(
            credsProvider,
            isDryRun,
            credentialsOptions,
            registryName,
            ownedAcr);

        try
        {
            await action();
        }
        finally
        {
            if (loggedIn && !string.IsNullOrEmpty(registryName))
            {
                DockerHelper.Logout(registryName, isDryRun);
            }
        }
    }

    /// <summary>
    /// Executes a synchronous action with the necessary registry credentials.
    /// </summary>
    /// <param name="credsProvider">The registry credentials provider.</param>
    /// <param name="isDryRun">Indicates whether this is a dry run. When true, no actual login is performed.</param>
    /// <param name="action">The synchronous action to execute with credentials.</param>
    /// <param name="credentialsOptions">Options for retrieving credentials.</param>
    /// <param name="registryName">The name of the registry to authenticate with.</param>
    /// <param name="ownedAcr">The name of the owned Azure Container Registry, if applicable.</param>
    /// <remarks>
    /// This overload wraps the synchronous action in a task to reuse the asynchronous implementation.
    /// </remarks>
    public static async Task ExecuteWithCredentialsAsync(
        this IRegistryCredentialsProvider credsProvider,
        bool isDryRun,
        Action action,
        RegistryCredentialsOptions credentialsOptions,
        string registryName,
        string? ownedAcr)
    {
        await credsProvider.ExecuteWithCredentialsAsync(
            isDryRun,
            () => {
                action();
                return Task.CompletedTask;
            },
            credentialsOptions,
            registryName,
            ownedAcr
        );
    }

    /// <summary>
    /// Logs in to the specified registry using credentials from the provider.
    /// </summary>
    /// <param name="credsProvider">The registry credentials provider.</param>
    /// <param name="isDryRun">Indicates whether this is a dry run. When true, no actual login is performed.</param>
    /// <param name="credentialsOptions">Options for retrieving credentials.</param>
    /// <param name="registryName">The name of the registry to authenticate with.</param>
    /// <param name="ownedAcr">The name of the owned Azure Container Registry, if applicable.</param>
    /// <returns>A boolean indicating whether a login was performed.</returns>
    /// <remarks>
    /// Login is only performed when:
    /// 1. It's not a dry run
    /// 2. A valid registry name is provided
    /// 3. Valid credentials were retrieved from the provider
    /// </remarks>
    private static async Task<bool> LogInToRegistry(
        this IRegistryCredentialsProvider credsProvider,
        bool isDryRun,
        RegistryCredentialsOptions credentialsOptions,
        string registryName,
        string? ownedAcr)
    {
        bool loggedIn = false;

        RegistryCredentials? credentials = null;
        if (!isDryRun)
        {
            credentials = await credsProvider.GetCredentialsAsync(registryName, ownedAcr, credentialsOptions);
        }

        if (!string.IsNullOrEmpty(registryName) && credentials is not null)
        {
            DockerHelper.Login(credentials, registryName, isDryRun);
            loggedIn = true;
        }

        return loggedIn;
    }
}
