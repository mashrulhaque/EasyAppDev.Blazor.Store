namespace EasyAppDev.Blazor.Store.Persistence;

/// <summary>
/// Interface for state persistence providers.
/// </summary>
public interface IPersistenceProvider
{
    /// <summary>
    /// Loads persisted state from storage.
    /// </summary>
    /// <param name="key">The storage key.</param>
    /// <returns>The serialized state or null if not found.</returns>
    Task<string?> LoadAsync(string key);

    /// <summary>
    /// Saves state to storage.
    /// </summary>
    /// <param name="key">The storage key.</param>
    /// <param name="value">The serialized state.</param>
    Task SaveAsync(string key, string value);

    /// <summary>
    /// Removes persisted state from storage.
    /// </summary>
    /// <param name="key">The storage key.</param>
    Task RemoveAsync(string key);

    /// <summary>
    /// Checks if a key exists in storage.
    /// </summary>
    /// <param name="key">The storage key.</param>
    /// <returns>True if the key exists.</returns>
    Task<bool> ContainsKeyAsync(string key);
}
