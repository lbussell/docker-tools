// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using V2 = Microsoft.DotNet.ImageBuilder.Models.Image.V2;

namespace Microsoft.DotNet.ImageBuilder.Services;

/// <summary>
/// Serializes and deserializes V2 image-info records to/from JSON.
/// Produces output compatible with the existing image-info.json format.
/// </summary>
public static class ImageInfoSerializer
{
    private static readonly JsonSerializerSettings s_serializerSettings = new()
    {
        ContractResolver = new ImageInfoContractResolver(),
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore,
        DefaultValueHandling = DefaultValueHandling.Ignore,
    };

    /// <summary>
    /// Serializes an <see cref="V2.ImageArtifactDetails"/> to a JSON string.
    /// </summary>
    public static string Serialize(V2.ImageArtifactDetails imageArtifactDetails) =>
        JsonConvert.SerializeObject(imageArtifactDetails, s_serializerSettings);

    /// <summary>
    /// Deserializes a JSON string to an <see cref="V2.ImageArtifactDetails"/>.
    /// </summary>
    /// <exception cref="SerializationException">Thrown when deserialization fails.</exception>
    public static V2.ImageArtifactDetails Deserialize(string json) =>
        JsonConvert.DeserializeObject<V2.ImageArtifactDetails>(json, s_serializerSettings)
            ?? throw new SerializationException(
                $"""
                Failed to deserialize {nameof(V2.ImageArtifactDetails)} from content:
                {json}
                """);

    /// <summary>
    /// Custom contract resolver that reproduces the serialization behavior of the
    /// existing image-info format:
    /// - camelCase property names
    /// - Required fields always serialized (even when default/empty)
    /// - Empty lists omitted for non-required properties
    /// </summary>
    private sealed class ImageInfoContractResolver : DefaultContractResolver
    {
        /// <summary>
        /// Properties that must always be present in the serialized output,
        /// matching the [JsonProperty(Required = Required.Always)] behavior of the old model.
        /// </summary>
        private static readonly HashSet<(Type, string)> RequiredProperties =
        [
            (typeof(V2.RepoData), nameof(V2.RepoData.Repo)),
            (typeof(V2.PlatformData), nameof(V2.PlatformData.Dockerfile)),
            (typeof(V2.PlatformData), nameof(V2.PlatformData.Digest)),
            (typeof(V2.PlatformData), nameof(V2.PlatformData.OsType)),
            (typeof(V2.PlatformData), nameof(V2.PlatformData.OsVersion)),
            (typeof(V2.PlatformData), nameof(V2.PlatformData.Architecture)),
            (typeof(V2.PlatformData), nameof(V2.PlatformData.CommitUrl)),
        ];

        public ImageInfoContractResolver()
        {
            NamingStrategy = new CamelCaseNamingStrategy();
        }

        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            JsonProperty property = base.CreateProperty(member, memberSerialization);

            bool isRequired = property.DeclaringType is not null
                && property.UnderlyingName is not null
                && RequiredProperties.Contains((property.DeclaringType, property.UnderlyingName));

            if (isRequired)
            {
                property.Required = Required.Always;
                property.NullValueHandling = NullValueHandling.Include;
                property.DefaultValueHandling = DefaultValueHandling.Include;
            }
            else
            {
                Predicate<object>? originalShouldSerialize = property.ShouldSerialize;
                property.ShouldSerialize = targetObj =>
                {
                    if (originalShouldSerialize is not null && !originalShouldSerialize(targetObj))
                    {
                        return false;
                    }

                    return !IsEmptyList(property, targetObj);
                };
            }

            return property;
        }

        private static bool IsEmptyList(JsonProperty property, object targetObj)
        {
            object? propertyValue = property.ValueProvider?.GetValue(targetObj);
            return propertyValue is IList list && list.Count == 0;
        }
    }
}
