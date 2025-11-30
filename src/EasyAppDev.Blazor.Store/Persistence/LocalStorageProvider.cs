using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace EasyAppDev.Blazor.Store.Persistence;

/// <summary>
/// Persistence provider using browser LocalStorage.
/// Data persists across browser sessions.
/// </summary>
/// <remarks>
/// This provider wraps browser localStorage API through JavaScript interop.
/// Operations are async due to Blazor's JS interop requirements.
/// Errors are logged at Warning level and don't throw exceptions to ensure
/// application stability when storage is unavailable or quota exceeded.
/// </remarks>
public class LocalStorageProvider : IPersistenceProvider
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<LocalStorageProvider>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalStorageProvider"/> class.
    /// </summary>
    /// <param name="jsRuntime">The JS runtime for interop.</param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="jsRuntime"/> is null.
    /// </exception>
    public LocalStorageProvider(IJSRuntime jsRuntime, ILogger<LocalStorageProvider>? logger = null)
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
            return await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", key)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load from localStorage key: {Key}", key);
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
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, value)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to save to localStorage key: {Key}", key);
        }
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", key)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to remove localStorage key: {Key}", key);
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
