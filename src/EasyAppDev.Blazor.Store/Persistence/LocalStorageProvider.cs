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
/// In WebAssembly mode, synchronous operations are available via <see cref="IJSInProcessRuntime"/>.
/// </remarks>
public class LocalStorageProvider : IPersistenceProvider
{
    private readonly IJSRuntime _jsRuntime;
    private readonly IJSInProcessRuntime? _jsInProcessRuntime;
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
        _jsInProcessRuntime = jsRuntime as IJSInProcessRuntime;
        _logger = logger;
    }

    /// <summary>
    /// Gets whether synchronous operations are available (WebAssembly mode).
    /// </summary>
    public bool SupportsSyncOperations => _jsInProcessRuntime != null;

    /// <summary>
    /// Synchronously loads a value from localStorage.
    /// Only available in WebAssembly mode where <see cref="IJSInProcessRuntime"/> is available.
    /// </summary>
    /// <param name="key">The storage key.</param>
    /// <returns>The stored value, or null if not found.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when called in Blazor Server mode where synchronous JS interop is not available.
    /// </exception>
    public string? LoadSync(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_jsInProcessRuntime == null)
        {
            throw new InvalidOperationException(
                "Synchronous JS interop is not available. " +
                "Use LoadAsync instead, or ensure you're running in WebAssembly mode.");
        }

        try
        {
            var value = _jsInProcessRuntime.Invoke<string?>("localStorage.getItem", key);

            if (value != null)
            {
                _logger?.LogDebug("Loaded from localStorage key: {Key}, Size: {Size:N0} bytes (sync)", key, value.Length);
            }
            else
            {
                _logger?.LogDebug("No value found in localStorage for key: {Key} (sync)", key);
            }

            return value;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load from localStorage key: {Key} (sync)", key);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<string?> LoadAsync(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        try
        {
            var value = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", key)
                .ConfigureAwait(false);

            if (value != null)
            {
                _logger?.LogDebug("Loaded from localStorage key: {Key}, Size: {Size:N0} bytes", key, value.Length);
            }
            else
            {
                _logger?.LogDebug("No value found in localStorage for key: {Key}", key);
            }

            return value;
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
            _logger?.LogDebug("Saving to localStorage key: {Key}, Size: {Size:N0} bytes", key, value.Length);

            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, value)
                .ConfigureAwait(false);

            _logger?.LogDebug("Successfully saved to localStorage key: {Key}", key);
        }
        catch (JSException jsEx) when (IsQuotaExceededException(jsEx))
        {
            _logger?.LogError(jsEx, "localStorage quota exceeded for key: {Key}. Size: {Size:N0} bytes. " +
                "Consider reducing state size or clearing old data.", key, value.Length);
            throw new InvalidOperationException(
                $"Browser localStorage quota exceeded. Cannot save state (size: {value.Length:N0} bytes). " +
                "Try clearing old data or reducing state size.", jsEx);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to save to localStorage key: {Key}", key);
            throw;
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

    /// <summary>
    /// Determines if a JSException represents a storage quota exceeded error.
    /// Handles various browser-specific error messages.
    /// </summary>
    private static bool IsQuotaExceededException(JSException ex)
    {
        var message = ex.Message;
        if (string.IsNullOrEmpty(message))
            return false;

        // Standard DOMException name (Chrome, Firefox, Safari, Edge)
        if (message.Contains("QuotaExceededError", StringComparison.OrdinalIgnoreCase))
            return true;

        // Legacy error name
        if (message.Contains("QUOTA_EXCEEDED_ERR", StringComparison.OrdinalIgnoreCase))
            return true;

        // Firefox legacy format
        if (message.Contains("NS_ERROR_DOM_QUOTA_REACHED", StringComparison.OrdinalIgnoreCase))
            return true;

        // Generic quota keyword as fallback
        if (message.Contains("quota", StringComparison.OrdinalIgnoreCase) &&
            message.Contains("exceed", StringComparison.OrdinalIgnoreCase))
            return true;

        // Check for storage full indicators
        if (message.Contains("storage", StringComparison.OrdinalIgnoreCase) &&
            message.Contains("full", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}
