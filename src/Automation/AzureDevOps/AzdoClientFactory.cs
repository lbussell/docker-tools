// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.Common;

namespace Microsoft.DotNet.DockerTools.Automation.AzureDevOps;

internal class AzdoClientFactory(ILoggerFactory loggerFactory) : IAzdoClientFactory
{
    public IAzdoBuildClient CreateBuildClient(Uri baseUrl, VssCredentials credentials)
    {
        var buildClient = new BuildHttpClient(baseUrl, credentials);
        return new AzdoBuildClient(buildClient, loggerFactory.CreateLogger<AzdoBuildClient>());
    }

    public IAzdoProjectClient CreateProjectClient(Uri baseUrl, VssCredentials credentials)
    {
        var projectClient = new ProjectHttpClient(baseUrl, credentials);
        return new AzdoProjectClient(projectClient, loggerFactory.CreateLogger<AzdoProjectClient>());
    }
}
