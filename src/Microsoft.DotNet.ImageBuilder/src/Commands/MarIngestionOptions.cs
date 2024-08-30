// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.CommandLine;
using static Microsoft.DotNet.ImageBuilder.Commands.CliHelper;

namespace Microsoft.DotNet.ImageBuilder.Commands;

#nullable enable
public class MarIngestionOptions
{
    public static string StatusApiResourceIdName { get; } = "status-api-resource";

    public TimeSpan WaitTimeout { get; set; }

    public TimeSpan RequeryDelay { get; set; }

    public string StatusApiResourceId { get; set; } = string.Empty;
}

internal class MarIngestionOptionsBuilder
{
    public IEnumerable<Option> GetCliOptions(TimeSpan defaultTimeout, TimeSpan defaultRequeryDelay) =>
        [
            CreateOption(
                alias: "timeout",
                propertyName: nameof(MarIngestionOptions.WaitTimeout),
                description: $"Maximum time to wait for ingestion",
                convert: TimeSpan.Parse,
                defaultValue: defaultTimeout),

            CreateOption(
                alias: "requery-delay",
                propertyName: nameof(MarIngestionOptions.RequeryDelay),
                description: $"Amount of time to wait before requerying the status",
                convert: TimeSpan.Parse,
                defaultValue: defaultRequeryDelay),

            CreateOption<string>(
                alias: "status-api-resource",
                propertyName: nameof(MarIngestionOptions.StatusApiResourceId),
                description: "The MAR status API resource ID",
                convert: id => id.StartsWith("api://") ? id : $"api://{id}")
        ];

    public IEnumerable<Argument> GetCliArguments() => [];
}
