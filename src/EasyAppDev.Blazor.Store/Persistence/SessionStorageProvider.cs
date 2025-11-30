using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace EasyAppDev.Blazor.Store.Persistence;

/// <summary>
/// Persistence provider using browser SessionStorage.
/// Data persists only for the current browser session/tab.
/// </summary>
/// <remarks>
/// This provider wraps browser sessionStorage API through JavaScript interop.
/// Data is cleared when the browser tab is closed. For persistent storage across
/// sessions, use <see cref="LocalStorageProvider"/> instead.
/// Errors are logged at Warning level and don't throw exceptions to ensure
/// application stability when storage is unavailable.
/// </remarks>
public class SessionStorageProvider : IPersistenceProvider
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<SessionStorageProvider>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionStorageProvider"/> class.
    /// </summary>
    /// <param name="jsRuntime">The JS runtime for interop.</param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="jsRuntime"/> is null.
    /// </exception>
    public SessionStorageProvider(IJSRuntime jsRuntime, ILogger<SessionStorageProvider>? logger = null)
    {
        _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string?> LoadAsync(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        try
        {
            return await _jsRuntime.InvokeAsync<string?>("sessionStorage.getItem", key)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load from sessionStorage key: {Key}", key);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        try
        {
            await _jsRuntime.InvokeVoidAsync("sessionStorage.setItem", key, value)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to save to sessionStorage key: {Key}", key);
        }
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        try
        {
            await _jsRuntime.InvokeVoidAsync("sessionStorage.removeItem", key)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to remove sessionStorage key: {Key}", key);
        }
    }

    /// <inheritdoc />
    public async Task<bool> ContainsKeyAsync(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var value = await LoadAsync(key).ConfigureAwait(false);
        return value != null;
    }
}
