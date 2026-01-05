using System.Globalization;

namespace EasyAppDev.Blazor.Store.Blazor.UrlSync;

/// <summary>
/// Default URL value converter that supports primitives, strings, Guid, DateTime, and enums.
/// Uses invariant culture for consistent URL encoding across locales.
/// </summary>
/// <typeparam name="T">The type to convert. Must be a supported type.</typeparam>
internal sealed class DefaultUrlValueConverter<T> : IUrlValueConverter<T>
{
    private readonly Action<string, Exception>? _onConversionError;

    public DefaultUrlValueConverter(Action<string, Exception>? onConversionError = null)
    {
        _onConversionError = onConversionError;
    }

    public T? FromUrl(string? urlValue)
    {
        if (string.IsNullOrEmpty(urlValue))
            return default;

        var targetType = typeof(T);
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        try
        {
            // Integers
            if (underlyingType == typeof(int))
                return (T)(object)int.Parse(urlValue, CultureInfo.InvariantCulture);

            if (underlyingType == typeof(long))
                return (T)(object)long.Parse(urlValue, CultureInfo.InvariantCulture);

            if (underlyingType == typeof(short))
                return (T)(object)short.Parse(urlValue, CultureInfo.InvariantCulture);

            if (underlyingType == typeof(byte))
                return (T)(object)byte.Parse(urlValue, CultureInfo.InvariantCulture);

            // Floating point
            if (underlyingType == typeof(float))
                return (T)(object)float.Parse(urlValue, CultureInfo.InvariantCulture);

            if (underlyingType == typeof(double))
                return (T)(object)double.Parse(urlValue, CultureInfo.InvariantCulture);

            if (underlyingType == typeof(decimal))
                return (T)(object)decimal.Parse(urlValue, CultureInfo.InvariantCulture);

            // Boolean
            if (underlyingType == typeof(bool))
                return (T)(object)bool.Parse(urlValue);

            // String
            if (underlyingType == typeof(string))
                return (T)(object)urlValue;

            // Guid
            if (underlyingType == typeof(Guid))
                return (T)(object)Guid.Parse(urlValue);

            // DateTime types
            if (underlyingType == typeof(DateTime))
                return (T)(object)DateTime.Parse(urlValue, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

            if (underlyingType == typeof(DateTimeOffset))
                return (T)(object)DateTimeOffset.Parse(urlValue, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

            if (underlyingType == typeof(TimeSpan))
                return (T)(object)TimeSpan.Parse(urlValue, CultureInfo.InvariantCulture);

            // Enum
            if (underlyingType.IsEnum)
                return (T)Enum.Parse(underlyingType, urlValue, ignoreCase: true);

            return default;
        }
        catch (Exception ex)
        {
            _onConversionError?.Invoke(urlValue, ex);
            return default;
        }
    }

    public string? ToUrl(T? stateValue)
    {
        if (stateValue == null)
            return null;

        var targetType = typeof(T);
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        try
        {
            // Integers
            if (underlyingType == typeof(int))
                return ((int)(object)stateValue).ToString(CultureInfo.InvariantCulture);

            if (underlyingType == typeof(long))
                return ((long)(object)stateValue).ToString(CultureInfo.InvariantCulture);

            if (underlyingType == typeof(short))
                return ((short)(object)stateValue).ToString(CultureInfo.InvariantCulture);

            if (underlyingType == typeof(byte))
                return ((byte)(object)stateValue).ToString(CultureInfo.InvariantCulture);

            // Floating point
            if (underlyingType == typeof(float))
                return ((float)(object)stateValue).ToString("G", CultureInfo.InvariantCulture);

            if (underlyingType == typeof(double))
                return ((double)(object)stateValue).ToString("G", CultureInfo.InvariantCulture);

            if (underlyingType == typeof(decimal))
                return ((decimal)(object)stateValue).ToString("G", CultureInfo.InvariantCulture);

            // Boolean
            if (underlyingType == typeof(bool))
                return ((bool)(object)stateValue).ToString().ToLowerInvariant();

            // String
            if (underlyingType == typeof(string))
                return (string)(object)stateValue;

            // Guid
            if (underlyingType == typeof(Guid))
                return ((Guid)(object)stateValue).ToString();

            // DateTime types - use roundtrip format for precision
            if (underlyingType == typeof(DateTime))
                return ((DateTime)(object)stateValue).ToString("O", CultureInfo.InvariantCulture);

            if (underlyingType == typeof(DateTimeOffset))
                return ((DateTimeOffset)(object)stateValue).ToString("O", CultureInfo.InvariantCulture);

            if (underlyingType == typeof(TimeSpan))
                return ((TimeSpan)(object)stateValue).ToString("c", CultureInfo.InvariantCulture);

            // Enum
            if (underlyingType.IsEnum)
                return stateValue.ToString();

            return null;
        }
        catch (Exception ex)
        {
            _onConversionError?.Invoke(stateValue.ToString() ?? "null", ex);
            return null;
        }
    }
}
