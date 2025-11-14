using Microsoft.JSInterop;

namespace EasyAppDev.Blazor.Store.Persistence;

/// <summary>
/// Persistence provider using browser SessionStorage.
/// Data persists only for the current browser session/tab.
/// </summary>
public class SessionStorageProvider : IPersistenceProvider
{
    private readonly IJSRuntime _jsRuntime;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionStorageProvider"/> class.
    /// </summary>
    /// <param name="jsRuntime">The JS runtime for interop.</param>
    public SessionStorageProvider(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
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
            Console.WriteLine($"Error loading from sessionStorage: {ex.Message}");
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
            Console.WriteLine($"Error saving to sessionStorage: {ex.Message}");
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
            Console.WriteLine($"Error removing from sessionStorage: {ex.Message}");
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
