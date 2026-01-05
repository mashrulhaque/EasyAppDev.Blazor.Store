namespace EasyAppDev.Blazor.Store.Blazor.UrlSync;

/// <summary>
/// Converts values between URL string representation and typed state properties.
/// </summary>
/// <typeparam name="T">The property type</typeparam>
/// <remarks>
/// Implementations must be thread-safe and handle null values gracefully.
/// </remarks>
public interface IUrlValueConverter<T>
{
    /// <summary>
    /// Convert URL string to typed value.
    /// </summary>
    /// <param name="urlValue">URL parameter value (can be null or empty)</param>
    /// <returns>Converted value, or default if conversion fails</returns>
    /// <remarks>
    /// Should return default(T) if conversion fails rather than throwing.
    /// Invalid URL parameters should be handled gracefully.
    /// </remarks>
    T? FromUrl(string? urlValue);

    /// <summary>
    /// Convert typed value to URL string.
    /// </summary>
    /// <param name="stateValue">State property value</param>
    /// <returns>URL-safe string representation, or null to omit parameter</returns>
    /// <remarks>
    /// Return null to omit the parameter from the URL (cleaner URLs).
    /// Must produce URL-safe strings (no special character encoding needed).
    /// </remarks>
    string? ToUrl(T? stateValue);
}
