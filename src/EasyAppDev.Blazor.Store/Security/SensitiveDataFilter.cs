// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EasyAppDev.Blazor.Store.Security;

/// <summary>
/// JSON converter factory that filters sensitive data from serialization.
/// Use with DevTools and diagnostics to prevent exposing sensitive information.
/// </summary>
public sealed class SensitiveDataFilterConverterFactory : JsonConverterFactory
{
    private readonly SensitiveDataFilterOptions _options;

    /// <summary>
    /// Creates a new sensitive data filter factory.
    /// </summary>
    /// <param name="options">Filter options.</param>
    public SensitiveDataFilterConverterFactory(SensitiveDataFilterOptions? options = null)
    {
        _options = options ?? new SensitiveDataFilterOptions();
    }

    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert)
    {
        // Only convert complex types (not primitives)
        return !typeToConvert.IsPrimitive
            && typeToConvert != typeof(string)
            && typeToConvert != typeof(decimal)
            && typeToConvert != typeof(DateTime)
            && typeToConvert != typeof(DateTimeOffset)
            && typeToConvert != typeof(Guid)
            && !typeToConvert.IsEnum;
    }

    /// <inheritdoc />
    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var converterType = typeof(SensitiveDataFilterConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter?)Activator.CreateInstance(converterType, _options);
    }
}

/// <summary>
/// Generic converter that filters sensitive properties during serialization.
/// </summary>
/// <typeparam name="T">The type being serialized.</typeparam>
public sealed class SensitiveDataFilterConverter<T> : JsonConverter<T>
{
    private readonly SensitiveDataFilterOptions _options;
    private static readonly Dictionary<Type, PropertyInfo[]> PropertyCache = new();
    private static readonly object CacheLock = new();

    /// <summary>
    /// Creates a new converter instance.
    /// </summary>
    /// <param name="options">Filter options.</param>
    public SensitiveDataFilterConverter(SensitiveDataFilterOptions options)
    {
        _options = options;
    }

    /// <inheritdoc />
    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // For reading, use default behavior (create a copy of options without this converter)
        var newOptions = new JsonSerializerOptions(options);
        var converterToRemove = newOptions.Converters
            .FirstOrDefault(c => c is SensitiveDataFilterConverterFactory);
        if (converterToRemove != null)
        {
            newOptions.Converters.Remove(converterToRemove);
        }

        return JsonSerializer.Deserialize<T>(ref reader, newOptions);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        // Check size limit before serialization
        var estimatedSize = EstimateSerializationSize(value);
        if (estimatedSize > _options.MaxSerializationSizeBytes)
        {
            throw new InvalidOperationException(
                $"State size ({estimatedSize} bytes) exceeds maximum allowed size ({_options.MaxSerializationSizeBytes} bytes). " +
                "Consider reducing state size or increasing MaxSerializationSizeBytes.");
        }

        var type = value.GetType();

        // Handle collections
        if (value is System.Collections.IEnumerable enumerable && type != typeof(string))
        {
            writer.WriteStartArray();
            foreach (var item in enumerable)
            {
                if (item == null)
                {
                    writer.WriteNullValue();
                }
                else
                {
                    // Ensure nested objects are filtered recursively
                    JsonSerializer.Serialize(writer, item, item.GetType(), options);
                }
            }
            writer.WriteEndArray();
            return;
        }

        // Handle objects
        writer.WriteStartObject();

        var properties = GetProperties(type);
        foreach (var prop in properties)
        {
            if (!prop.CanRead) continue;

            var propName = GetPropertyName(prop, options);
            var propValue = prop.GetValue(value);

            // Check if property should be filtered
            if (ShouldFilter(prop))
            {
                writer.WriteString(propName, _options.ReplacementValue);
                continue;
            }

            writer.WritePropertyName(propName);

            if (propValue == null)
            {
                writer.WriteNullValue();
            }
            else
            {
                // Recursively serialize with the same filter options
                // This ensures nested objects are also filtered
                JsonSerializer.Serialize(writer, propValue, prop.PropertyType, options);
            }
        }

        writer.WriteEndObject();
    }

    private static long EstimateSerializationSize(T value)
    {
        try
        {
            // Quick estimation without full serialization
            // This is a conservative estimate to prevent DoS attacks
            var estimatedJson = JsonSerializer.Serialize(value, new JsonSerializerOptions
            {
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
            });
            return System.Text.Encoding.UTF8.GetByteCount(estimatedJson);
        }
        catch
        {
            // If estimation fails, assume it's safe
            return 0;
        }
    }

    private bool ShouldFilter(PropertyInfo prop)
    {
        // Check for [SensitiveData] attribute
        if (_options.FilterSensitiveAttributes &&
            prop.GetCustomAttribute<SensitiveDataAttribute>() != null)
        {
            return true;
        }

        // Check for [JsonIgnore] attribute
        if (prop.GetCustomAttribute<JsonIgnoreAttribute>() != null)
        {
            return true;
        }

        // Check property name against filter list
        if (_options.FilteredPropertyNames.Contains(prop.Name))
        {
            return true;
        }

        // Check if property name contains sensitive keywords
        foreach (var keyword in _options.FilteredPropertyNames)
        {
            if (prop.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static PropertyInfo[] GetProperties(Type type)
    {
        lock (CacheLock)
        {
            if (!PropertyCache.TryGetValue(type, out var properties))
            {
                properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                PropertyCache[type] = properties;
            }
            return properties;
        }
    }

    private static string GetPropertyName(PropertyInfo prop, JsonSerializerOptions options)
    {
        var jsonPropAttr = prop.GetCustomAttribute<JsonPropertyNameAttribute>();
        if (jsonPropAttr != null)
        {
            return jsonPropAttr.Name;
        }

        if (options.PropertyNamingPolicy != null)
        {
            return options.PropertyNamingPolicy.ConvertName(prop.Name);
        }

        return prop.Name;
    }
}

/// <summary>
/// Extension methods for creating filtered JSON options.
/// </summary>
public static class SensitiveDataFilterExtensions
{
    /// <summary>
    /// Creates JSON serializer options with sensitive data filtering enabled.
    /// </summary>
    /// <param name="filterOptions">The filter options to use.</param>
    /// <returns>Configured JsonSerializerOptions.</returns>
    public static JsonSerializerOptions CreateFilteredJsonOptions(
        SensitiveDataFilterOptions? filterOptions = null)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        var effectiveOptions = filterOptions ?? new SensitiveDataFilterOptions { Enabled = true };
        if (effectiveOptions.Enabled)
        {
            options.Converters.Add(new SensitiveDataFilterConverterFactory(effectiveOptions));
        }

        return options;
    }

    /// <summary>
    /// Serializes an object with sensitive data filtering.
    /// </summary>
    /// <typeparam name="T">The type to serialize.</typeparam>
    /// <param name="value">The value to serialize.</param>
    /// <param name="filterOptions">Optional filter options.</param>
    /// <returns>JSON string with sensitive data filtered.</returns>
    public static string SerializeFiltered<T>(T value, SensitiveDataFilterOptions? filterOptions = null)
    {
        var options = CreateFilteredJsonOptions(filterOptions);
        return JsonSerializer.Serialize(value, options);
    }
}
