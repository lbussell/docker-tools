// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ConsoleAppFramework;
using Microsoft.DotNet.PipelineMonitor;
using Microsoft.DotNet.PipelineMonitor.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<IPipelineService, PipelineService>();

var app = builder.ToConsoleAppBuilder();

app.Add<RootCommand>();
app.Add<MonitorCommand>();

await app.RunAsync(args);
