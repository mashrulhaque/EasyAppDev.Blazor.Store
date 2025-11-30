namespace EasyAppDev.Blazor.Store.Persistence;

/// <summary>
/// Interface for state persistence providers that enable automatic state save/restore.
/// </summary>
/// <remarks>
/// <para>
/// Implement this interface to create custom persistence backends (e.g., IndexedDB,
/// remote storage, encrypted storage). Built-in implementations include
/// <see cref="LocalStorageProvider"/> and <see cref="SessionStorageProvider"/>.
/// </para>
/// <para>
/// Implementations should handle errors gracefully and return null/false for missing keys
/// rather than throwing exceptions, as the persistence layer should not crash the application.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class IndexedDbProvider : IPersistenceProvider
/// {
///     public async Task&lt;string?&gt; LoadAsync(string key)
///     {
///         return await _jsRuntime.InvokeAsync&lt;string?&gt;("indexedDb.get", key);
///     }
///     // ... other methods
/// }
/// </code>
/// </example>
public interface IPersistenceProvider
{
    /// <summary>
    /// Loads persisted state from storage.
    /// </summary>
    /// <param name="key">The storage key identifying the state to load.</param>
    /// <returns>The JSON-serialized state string, or null if the key does not exist.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null or whitespace.</exception>
    Task<string?> LoadAsync(string key);

    /// <summary>
    /// Saves state to storage.
    /// </summary>
    /// <param name="key">The storage key identifying where to save the state.</param>
    /// <param name="value">The JSON-serialized state string to persist.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    Task SaveAsync(string key, string value);

    /// <summary>
    /// Removes persisted state from storage.
    /// </summary>
    /// <param name="key">The storage key to remove.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null or whitespace.</exception>
    Task RemoveAsync(string key);

    /// <summary>
    /// Checks if a key exists in storage.
    /// </summary>
    /// <param name="key">The storage key to check.</param>
    /// <returns>True if the key exists in storage; otherwise, false.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null or whitespace.</exception>
    Task<bool> ContainsKeyAsync(string key);
}
