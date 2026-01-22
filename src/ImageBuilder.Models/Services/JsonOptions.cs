// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Reflection;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Microsoft.DotNet.ImageBuilder.Models.Image;

namespace Microsoft.DotNet.ImageBuilder.Models.Services;

/// <summary>
/// Shared JSON serialization options for ImageBuilder.Models.
/// </summary>
public static class JsonOptions
{
    /// <summary>
    /// Gets the default serializer settings for serializing models to JSON.
    /// Uses camelCase naming, ignores null/default values, and handles empty lists.
    /// </summary>
    public static JsonSerializerSettings SerializerSettings => new()
    {
        ContractResolver = new CustomContractResolver(),
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore,
        DefaultValueHandling = DefaultValueHandling.Ignore
    };

    /// <summary>
    /// Gets the deserializer settings for loading ImageArtifactDetails, including
    /// backward-compatible Layer conversion (schema version 1 → version 2).
    /// </summary>
    public static JsonSerializerSettings ImageArtifactDetailsDeserializerSettings => new()
    {
        Converters =
        [
            new SchemaVersion2LayerConverter()
        ]
    };

    /// <summary>
    /// Deserializes JSON to the specified type, throwing a <see cref="SerializationException"/> if null.
    /// </summary>
    public static T DeserializeOrThrow<T>(string json, JsonSerializerSettings? settings = null)
    {
        return JsonConvert.DeserializeObject<T>(json, settings)
            ?? throw new SerializationException(
                $"Failed to deserialize {typeof(T).Name}. JSON content was empty or null.");
    }

    private class CustomContractResolver : DefaultContractResolver
    {
        public CustomContractResolver()
        {
            NamingStrategy = new CamelCaseNamingStrategy();
        }

        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            JsonProperty property = base.CreateProperty(member, memberSerialization);

            // Required properties should always be serialized (even if default/empty)
            // Let JSON.NET's Required validation handle null checking
            if (property.Required == Required.Always)
            {
                property.NullValueHandling = NullValueHandling.Include;
                property.DefaultValueHandling = DefaultValueHandling.Include;
            }
            else
            {
                // Skip empty lists for non-required properties
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
            var propertyValue = property.ValueProvider?.GetValue(targetObj);
            return propertyValue is IList list && list.Count == 0;
        }
    }

    /// <summary>
    /// Converter for backward-compatible Layer deserialization.
    /// Schema version 1 stored layers as strings (digest only).
    /// Schema version 2 stores layers as objects with Digest and Size.
    /// </summary>
    private class SchemaVersion2LayerConverter : JsonConverter
    {
        private static readonly JsonSerializer s_jsonSerializer =
            JsonSerializer.Create(SerializerSettings);

        // We do not want to handle writing at all. We only want to convert
        // the old Layer format to the new format, and all writing should be
        // done using the new format (via the default conversion settings).
        public override bool CanWrite => false;

        public override bool CanConvert(Type objectType) => objectType == typeof(Layer);

        public override object? ReadJson(
            JsonReader reader,
            Type objectType,
            object? existingValue,
            JsonSerializer serializer)
        {
            JToken token = JToken.Load(reader);
            return token.Type switch
            {
                // If the token is an object, proceed as normal.
                // We can't use the JsonSerializer passed into the method since
                // it contains this converter. Doing so would cause a stack
                // overflow since this method would be called again recursively.
                JTokenType.Object => token.ToObject<Layer>(s_jsonSerializer),

                // If we encounter a string, we want to convert it to the Layer
                // object defined in schema version 2. Assume a size of 0. The
                // next time an image is built, the size will be updated.
                JTokenType.String =>
                    new Layer(
                        Digest: token.Value<string>()
                            ?? throw new JsonSerializationException(
                                $"Unable to serialize digest from '{token}'"),
                        Size: 0),

                // Handle null and other token types
                JTokenType.Null => null,
                _ => throw new JsonSerializationException(
                        $"Unexpected token type: {token.Type} when parsing Layer.")
            };
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
