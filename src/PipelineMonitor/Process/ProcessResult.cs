// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.PipelineMonitor.Process;

/// <summary>
/// Represents the result of executing a process.
/// </summary>
/// <param name="ExitCode">The exit code of the process.</param>
/// <param name="StandardOutput">The standard output from the process.</param>
/// <param name="StandardError">The standard error from the process.</param>
internal record ProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);
