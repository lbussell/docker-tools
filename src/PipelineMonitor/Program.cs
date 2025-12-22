// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ConsoleAppFramework;
using Microsoft.DotNet.PipelineMonitor.Commands;
using Microsoft.DotNet.PipelineMonitor.Git;
using Microsoft.DotNet.PipelineMonitor.Pipelines;
using Microsoft.DotNet.PipelineMonitor.Process;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<IAzurePipelinesService, AzurePipelinesService>();
builder.Services.AddSingleton<IProcessService, ProcessService>();
builder.Services.AddSingleton<IGitCliService, GitCliService>();

var app = builder.ToConsoleAppBuilder();

app.Add<RootCommand>();
app.Add<RunPipelineCommand>();

await app.RunAsync(args);
