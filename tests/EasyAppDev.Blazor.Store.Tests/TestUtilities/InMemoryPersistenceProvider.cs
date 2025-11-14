using EasyAppDev.Blazor.Store.Persistence;

namespace EasyAppDev.Blazor.Store.Tests.TestUtilities;

/// <summary>
/// In-memory persistence provider for testing purposes.
/// Provides a simple dictionary-based storage that can be easily inspected and manipulated in tests.
/// </summary>
public class InMemoryPersistenceProvider : IPersistenceProvider
{
    private readonly Dictionary<string, string> _storage = new();
    private readonly object _lock = new();

    /// <summary>
    /// Gets the number of items currently stored.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _storage.Count;
            }
        }
    }

    /// <summary>
    /// Gets all stored keys.
    /// </summary>
    public IReadOnlyList<string> Keys
    {
        get
        {
            lock (_lock)
            {
                return _storage.Keys.ToList();
            }
        }
    }

    /// <inheritdoc />
    public Task<string?> LoadAsync(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        lock (_lock)
        {
            return Task.FromResult(_storage.TryGetValue(key, out var value) ? value : null);
        }
    }

    /// <inheritdoc />
    public Task SaveAsync(string key, string value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        lock (_lock)
        {
            _storage[key] = value;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        lock (_lock)
        {
            _storage.Remove(key);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> ContainsKeyAsync(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        lock (_lock)
        {
            return Task.FromResult(_storage.ContainsKey(key));
        }
    }

    /// <summary>
    /// Clears all stored data.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _storage.Clear();
        }
    }

    /// <summary>
    /// Gets the raw value for a key without deserialization.
    /// Useful for inspecting the stored JSON in tests.
    /// </summary>
    /// <param name="key">The storage key.</param>
    /// <returns>The raw stored value or null if not found.</returns>
    public string? GetRawValue(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        lock (_lock)
        {
            return _storage.TryGetValue(key, out var value) ? value : null;
        }
    }

    /// <summary>
    /// Gets all key-value pairs in storage.
    /// Useful for debugging and verification in tests.
    /// </summary>
    /// <returns>A dictionary containing all stored key-value pairs.</returns>
    public Dictionary<string, string> GetAllData()
    {
        lock (_lock)
        {
            return new Dictionary<string, string>(_storage);
        }
    }

    /// <summary>
    /// Sets a value directly without going through the async interface.
    /// Useful for setting up test data.
    /// </summary>
    /// <param name="key">The storage key.</param>
    /// <param name="value">The value to store.</param>
    public void SetValue(string key, string value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        lock (_lock)
        {
            _storage[key] = value;
        }
    }

    /// <summary>
    /// Simulates a storage failure for testing error handling.
    /// </summary>
    public bool SimulateFailure { get; set; }

    /// <summary>
    /// Creates a new instance with pre-populated data.
    /// </summary>
    /// <param name="initialData">The initial data to populate.</param>
    /// <returns>A new provider instance with the specified data.</returns>
    public static InMemoryPersistenceProvider WithData(Dictionary<string, string> initialData)
    {
        var provider = new InMemoryPersistenceProvider();
        foreach (var kvp in initialData)
        {
            provider.SetValue(kvp.Key, kvp.Value);
        }
        return provider;
    }
}
