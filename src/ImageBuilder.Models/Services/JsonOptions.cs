// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.DotNet.ImageBuilder.Models.Image;

namespace Microsoft.DotNet.ImageBuilder.Models.Services;

/// <summary>
/// Shared JSON serialization options for ImageBuilder.Models.
/// </summary>
public static class JsonOptions
{
    /// <summary>
    /// Gets the default serializer options for serializing models to JSON.
    /// Uses camelCase naming for properties and preserves original case for enums.
    /// </summary>
    public static JsonSerializerOptions SerializerOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }  // No naming policy = preserve original case
    };

    /// <summary>
    /// Gets the deserializer options for loading models from JSON.
    /// Includes backward-compatible Layer conversion (schema version 1 → version 2).
    /// </summary>
    public static JsonSerializerOptions DeserializerOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new JsonStringEnumConverter(),  // No naming policy = case-insensitive read
            new SchemaVersion2LayerConverter()
        }
    };

    /// <summary>
    /// Deserializes JSON to the specified type, throwing a <see cref="SerializationException"/> if null.
    /// </summary>
    public static T DeserializeOrThrow<T>(string json, JsonSerializerOptions? options = null)
    {
        return JsonSerializer.Deserialize<T>(json, options ?? DeserializerOptions)
            ?? throw new SerializationException(
                $"Failed to deserialize {typeof(T).Name}. JSON content was empty or null.");
    }

    /// <summary>
    /// Serializes an object to JSON.
    /// </summary>
    public static string Serialize<T>(T value, JsonSerializerOptions? options = null)
    {
        return JsonSerializer.Serialize(value, options ?? SerializerOptions);
    }

    /// <summary>
    /// Converter for backward-compatible Layer deserialization.
    /// Schema version 1 stored layers as strings (digest only).
    /// Schema version 2 stores layers as objects with Digest and Size.
    /// </summary>
    private class SchemaVersion2LayerConverter : JsonConverter<Layer>
    {
        public override Layer? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.TokenType switch
            {
                // If the token is an object, deserialize as Layer (pass options for camelCase mapping)
                JsonTokenType.StartObject => DeserializeLayerObject(ref reader, options),

                // If we encounter a string, convert to Layer with size 0
                JsonTokenType.String =>
                    new Layer(
                        Digest: reader.GetString()
                            ?? throw new JsonException("Unable to read digest string"),
                        Size: 0),

                // Handle null
                JsonTokenType.Null => null,

                _ => throw new JsonException($"Unexpected token type: {reader.TokenType} when parsing Layer.")
            };
        }

        private static Layer? DeserializeLayerObject(ref Utf8JsonReader reader, JsonSerializerOptions options)
        {
            // Create new options without this converter to avoid infinite recursion
            var innerOptions = new JsonSerializerOptions(options);
            innerOptions.Converters.Clear();
            foreach (var converter in options.Converters)
            {
                if (converter is not SchemaVersion2LayerConverter)
                {
                    innerOptions.Converters.Add(converter);
                }
            }
            return JsonSerializer.Deserialize<Layer>(ref reader, innerOptions);
        }

        public override void Write(Utf8JsonWriter writer, Layer value, JsonSerializerOptions options)
        {
            // Write as object format (schema version 2)
            writer.WriteStartObject();
            writer.WriteString("digest", value.Digest);
            writer.WriteNumber("size", value.Size);
            writer.WriteEndObject();
        }
    }
}
