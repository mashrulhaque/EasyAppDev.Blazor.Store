// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

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
    private readonly List<Regex> _compiledPatterns;
    private static readonly Dictionary<Type, PropertyInfo[]> PropertyCache = new();
    private static readonly object CacheLock = new();

    /// <summary>
    /// Creates a new converter instance.
    /// </summary>
    /// <param name="options">Filter options.</param>
    public SensitiveDataFilterConverter(SensitiveDataFilterOptions options)
    {
        _options = options;

        // Pre-compile regex patterns for better performance
        _compiledPatterns = new List<Regex>();
        foreach (var pattern in _options.FilteredPropertyPatterns)
        {
            try
            {
                _compiledPatterns.Add(new Regex(pattern,
                    RegexOptions.IgnoreCase | RegexOptions.Compiled,
                    TimeSpan.FromMilliseconds(100))); // Timeout to prevent ReDoS attacks
            }
            catch (ArgumentException)
            {
                // Skip invalid patterns
            }
        }
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

        // Handle dictionaries BEFORE the generic IEnumerable branch so they
        // round-trip as JSON objects (serializing them as arrays of
        // KeyValuePair objects breaks deserialization on hydration).
        if (value is System.Collections.IDictionary dictionary)
        {
            writer.WriteStartObject();
            foreach (System.Collections.DictionaryEntry entry in dictionary)
            {
                var keyName = entry.Key?.ToString() ?? string.Empty;
                if (options.DictionaryKeyPolicy != null)
                {
                    keyName = options.DictionaryKeyPolicy.ConvertName(keyName);
                }

                writer.WritePropertyName(keyName);

                if (IsSensitiveName(keyName) && entry.Value is string)
                {
                    // Only string values can safely carry the replacement marker
                    writer.WriteStringValue(_options.ReplacementValue);
                }
                else if (entry.Value == null)
                {
                    writer.WriteNullValue();
                }
                else
                {
                    // Serialize with the same options so nested objects stay filtered
                    JsonSerializer.Serialize(writer, entry.Value, entry.Value.GetType(), options);
                }
            }
            writer.WriteEndObject();
            return;
        }

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
                WriteFilteredValue(writer, propName, prop, propValue, options);
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

    /// <summary>
    /// Writes a type-aware replacement for a filtered property. String properties get the
    /// replacement marker; non-string properties get the type's default value so the
    /// produced JSON always round-trips back into the original state type.
    /// </summary>
    private void WriteFilteredValue(
        Utf8JsonWriter writer,
        string propName,
        PropertyInfo prop,
        object? propValue,
        JsonSerializerOptions options)
    {
        writer.WritePropertyName(propName);

        // Only string-typed properties (or object-typed properties currently holding a
        // string) can safely carry the replacement marker.
        if (prop.PropertyType == typeof(string) || propValue is string)
        {
            writer.WriteStringValue(_options.ReplacementValue);
            return;
        }

        // Non-string sensitive property: write the type's default value
        // (0 for numbers, false for bool, null for reference/nullable types)
        // so deserialization never fails on a "[FILTERED]" string.
        var defaultValue = prop.PropertyType.IsValueType && Nullable.GetUnderlyingType(prop.PropertyType) == null
            ? Activator.CreateInstance(prop.PropertyType)
            : null;

        JsonSerializer.Serialize(writer, defaultValue, prop.PropertyType, options);
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
        // Check for [AlwaysInclude] attribute - this overrides all other filtering
        if (prop.GetCustomAttribute<AlwaysIncludeAttribute>() != null)
        {
            return false;
        }

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

        return IsSensitiveName(prop.Name);
    }

    /// <summary>
    /// Determines whether a property or dictionary key name matches the configured
    /// sensitive keywords or regex patterns.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When <see cref="SensitiveDataFilterOptions.UseExactMatch"/> is false, names are split
    /// into tokens on camelCase/PascalCase and underscore/separator boundaries and keywords
    /// are matched against consecutive token sequences instead of raw substrings.
    /// This prevents innocent names from being corrupted by accidental substring hits
    /// (e.g. "ShippingAddress" does NOT match the keyword "Pin").
    /// </para>
    /// <para>
    /// Token matching is deliberately conservative: any name containing a sensitive keyword
    /// as a whole token is filtered. For example "TokenCount" contains the token "Token" and
    /// IS filtered. Use <see cref="AlwaysIncludeAttribute"/> on such properties to opt out.
    /// </para>
    /// </remarks>
    private bool IsSensitiveName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        // Exact match always applies (HashSet uses OrdinalIgnoreCase)
        if (_options.FilteredPropertyNames.Contains(name))
        {
            return true;
        }

        if (!_options.UseExactMatch)
        {
            // Token-boundary match: split the name on camelCase/underscore boundaries
            // and match keywords against consecutive whole-token sequences.
            var tokens = Tokenize(name);
            foreach (var keyword in _options.FilteredPropertyNames)
            {
                if (MatchesTokenSequence(tokens, keyword))
                {
                    return true;
                }
            }
        }

        // Check regex patterns
        foreach (var regex in _compiledPatterns)
        {
            try
            {
                if (regex.IsMatch(name))
                {
                    return true;
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // Pattern timed out (possible ReDoS), skip it
            }
        }

        return false;
    }

    /// <summary>
    /// Splits a property name into tokens on underscore/hyphen/space/dot separators,
    /// camelCase/PascalCase transitions, acronym boundaries (e.g. "HTTPSProxy" =>
    /// "HTTPS", "Proxy") and digit runs.
    /// </summary>
    private static List<string> Tokenize(string name)
    {
        var tokens = new List<string>();
        var start = -1;

        void Flush(int end)
        {
            if (start >= 0 && end > start)
            {
                tokens.Add(name[start..end]);
            }
            start = -1;
        }

        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];

            if (c is '_' or '-' or ' ' or '.')
            {
                Flush(i);
                continue;
            }

            if (start < 0)
            {
                start = i;
                continue;
            }

            var prev = name[i - 1];

            // Boundary: lower/digit -> Upper (e.g. "userPin" => "user", "Pin")
            if (char.IsUpper(c) && (char.IsLower(prev) || char.IsDigit(prev)))
            {
                Flush(i);
                start = i;
                continue;
            }

            // Boundary: letter <-> digit transition
            if (char.IsDigit(c) != char.IsDigit(prev))
            {
                Flush(i);
                start = i;
                continue;
            }

            // Acronym boundary: "HTTPSProxy" => split before "Proxy"
            if (char.IsLower(c) && char.IsUpper(prev) && i - start > 1)
            {
                Flush(i - 1);
                start = i - 1;
            }
        }

        Flush(name.Length);
        return tokens;
    }

    /// <summary>
    /// Checks whether the keyword equals the concatenation of any consecutive
    /// run of tokens (case-insensitive). Multi-token keywords like "CardNumber"
    /// match consecutive token sequences ("Card", "Number").
    /// </summary>
    private static bool MatchesTokenSequence(List<string> tokens, string keyword)
    {
        for (var startIndex = 0; startIndex < tokens.Count; startIndex++)
        {
            var matchedLength = 0;

            for (var endIndex = startIndex; endIndex < tokens.Count; endIndex++)
            {
                var token = tokens[endIndex];

                if (matchedLength + token.Length > keyword.Length)
                {
                    break;
                }

                if (string.Compare(keyword, matchedLength, token, 0, token.Length,
                        StringComparison.OrdinalIgnoreCase) != 0)
                {
                    break;
                }

                matchedLength += token.Length;

                if (matchedLength == keyword.Length)
                {
                    return true;
                }
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
