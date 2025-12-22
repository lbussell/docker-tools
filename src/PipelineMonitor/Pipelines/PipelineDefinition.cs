// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.PipelineMonitor.Pipelines;

/// <summary>
/// Represents a pipeline definition with its associated metadata.
/// </summary>
/// <param name="File">The file containing the pipeline definition.</param>
/// <param name="OrganizationUrl">The Azure DevOps organization URL.</param>
/// <param name="ProjectName">The Azure DevOps project name.</param>
/// <param name="Id">The pipeline ID.</param>
internal record PipelineDefinition(
    FileInfo File,
    string OrganizationUrl,
    string ProjectName,
    int Id);
