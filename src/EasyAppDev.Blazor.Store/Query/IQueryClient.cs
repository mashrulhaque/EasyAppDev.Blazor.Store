// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

namespace EasyAppDev.Blazor.Store.Query;

/// <summary>
/// Manages query caching and invalidation.
/// </summary>
public interface IQueryClient
{
    /// <summary>
    /// Gets data from the cache for the specified key.
    /// </summary>
    /// <typeparam name="T">The type of data.</typeparam>
    /// <param name="key">The query key.</param>
    /// <returns>The cached data or null if not found.</returns>
    T? GetQueryData<T>(string key);

    /// <summary>
    /// Sets data in the cache for the specified key.
    /// </summary>
    /// <typeparam name="T">The type of data.</typeparam>
    /// <param name="key">The query key.</param>
    /// <param name="data">The data to cache.</param>
    /// <param name="cacheTime">Optional cache duration. Uses default if not specified.</param>
    void SetQueryData<T>(string key, T data, TimeSpan? cacheTime = null);

    /// <summary>
    /// Updates cached data using a transformation function.
    /// </summary>
    /// <typeparam name="T">The type of data.</typeparam>
    /// <param name="key">The query key.</param>
    /// <param name="updater">Function to update the data.</param>
    void SetQueryData<T>(string key, Func<T?, T> updater);

    /// <summary>
    /// Invalidates queries matching the specified key.
    /// </summary>
    /// <param name="key">The query key to invalidate.</param>
    void InvalidateQueries(string key);

    /// <summary>
    /// Invalidates queries matching the predicate.
    /// </summary>
    /// <param name="predicate">Function to determine which keys to invalidate.</param>
    void InvalidateQueries(Func<string, bool> predicate);

    /// <summary>
    /// Refetches queries matching the specified key.
    /// </summary>
    /// <param name="key">The query key to refetch.</param>
    Task RefetchQueriesAsync(string key);

    /// <summary>
    /// Removes queries from the cache.
    /// </summary>
    /// <param name="key">The query key to remove.</param>
    void RemoveQueries(string key);

    /// <summary>
    /// Removes queries matching the predicate.
    /// </summary>
    /// <param name="predicate">Function to determine which keys to remove.</param>
    void RemoveQueries(Func<string, bool> predicate);

    /// <summary>
    /// Clears all cached queries.
    /// </summary>
    void Clear();

    /// <summary>
    /// Gets the cache entry for a key (internal use).
    /// </summary>
    internal QueryCacheEntry<T>? GetCacheEntry<T>(string key);

    /// <summary>
    /// Registers a query for refetch notifications.
    /// </summary>
    internal void RegisterQuery(string key, Func<Task> refetch);

    /// <summary>
    /// Unregisters a query from refetch notifications.
    /// </summary>
    internal void UnregisterQuery(string key, Func<Task> refetch);

    /// <summary>
    /// Marks a query key as invalidated (stale).
    /// </summary>
    internal void MarkInvalidated(string key);

    /// <summary>
    /// Checks if a query key is marked as invalidated.
    /// </summary>
    internal bool IsInvalidated(string key);
}
