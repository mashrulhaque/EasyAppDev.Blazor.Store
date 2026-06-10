// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EasyAppDev.Blazor.Store.Query;

/// <summary>
/// Default implementation of the query client for managing query caching and invalidation.
/// </summary>
public sealed class QueryClient : IQueryClient, IDisposable
{
    private readonly ConcurrentDictionary<string, object> _cache = new();
    private readonly ConcurrentDictionary<string, List<Func<Task>>> _queryRefetchers = new();
    private readonly ConcurrentDictionary<string, bool> _invalidatedKeys = new();
    private readonly QueryClientOptions _options;
    private readonly ILogger<QueryClient>? _logger;
    private readonly Timer _cleanupTimer;
    private int _disposed; // 0 = not disposed, 1 = disposed (use int for Interlocked)

    /// <summary>
    /// Creates a new query client with the specified options.
    /// </summary>
    public QueryClient(
        IOptions<QueryClientOptions>? options = null,
        ILogger<QueryClient>? logger = null)
    {
        _options = options?.Value ?? new QueryClientOptions();
        _logger = logger;

        _cleanupTimer = new Timer(
            CleanupExpiredEntries,
            null,
            _options.CacheCleanupInterval,
            _options.CacheCleanupInterval);
    }

    /// <summary>
    /// Gets the default options for this client.
    /// </summary>
    public QueryClientOptions Options => _options;

    QueryClientOptions? IQueryClient.Options => _options;

    /// <inheritdoc />
    public T? GetQueryData<T>(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (_cache.TryGetValue(key, out var entry) && entry is QueryCacheEntry<T> typedEntry)
        {
            if (DateTime.UtcNow < typedEntry.ExpiresAt)
            {
                return typedEntry.Data;
            }

            // Expired - conditionally remove only the exact expired entry instance,
            // so a concurrent SetQueryData write is never deleted.
            _cache.TryRemove(new KeyValuePair<string, object>(key, entry));
        }

        return default;
    }

    /// <inheritdoc />
    public void SetQueryData<T>(string key, T data, TimeSpan? cacheTime = null)
    {
        ArgumentNullException.ThrowIfNull(key);

        var duration = cacheTime ?? _options.DefaultCacheTime;
        var entry = new QueryCacheEntry<T>
        {
            Data = data,
            UpdatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow + duration
        };

        _cache[key] = entry;
        _invalidatedKeys.TryRemove(key, out _);

        _logger?.LogDebug("Query cache updated for key {Key}", key);
    }

    /// <inheritdoc />
    public void SetQueryData<T>(string key, Func<T?, T> updater)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(updater);

        var existing = GetQueryData<T>(key);
        var updated = updater(existing);
        SetQueryData(key, updated);
    }

    /// <inheritdoc />
    public void InvalidateQueries(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        _invalidatedKeys[key] = true;

        // Use a tracked task for better reliability - fire and forget but logged
        _ = InvalidateQueriesInternalAsync(key);

        _logger?.LogDebug("Query invalidated for key {Key}", key);
    }

    /// <summary>
    /// Invalidates queries for the specified key and awaits the refetch operation.
    /// Use this when you need to ensure the refetch completes before continuing.
    /// </summary>
    /// <param name="key">The query key to invalidate.</param>
    public async Task InvalidateQueriesAsync(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        _invalidatedKeys[key] = true;
        await TriggerRefetchAsync(key).ConfigureAwait(false);

        _logger?.LogDebug("Query invalidated and refetched for key {Key}", key);
    }

    private async Task InvalidateQueriesInternalAsync(string key)
    {
        try
        {
            await TriggerRefetchAsync(key).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error triggering refetch for invalidated query {Key}", key);
        }
    }

    /// <inheritdoc />
    public void InvalidateQueries(Func<string, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        // Include active-but-uncached queries (registered refetchers) as well as
        // cached keys, deduplicated.
        var keys = _cache.Keys
            .Concat(_queryRefetchers.Keys)
            .Distinct()
            .Where(predicate)
            .ToList();

        foreach (var key in keys)
        {
            InvalidateQueries(key);
        }
    }

    /// <inheritdoc />
    public async Task RefetchQueriesAsync(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        await TriggerRefetchAsync(key);
    }

    /// <inheritdoc />
    public void RemoveQueries(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        _cache.TryRemove(key, out _);
        _invalidatedKeys.TryRemove(key, out _);

        _logger?.LogDebug("Query removed for key {Key}", key);
    }

    /// <inheritdoc />
    public void RemoveQueries(Func<string, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        foreach (var key in _cache.Keys.Where(predicate).ToList())
        {
            RemoveQueries(key);
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        _cache.Clear();
        _invalidatedKeys.Clear();

        _logger?.LogDebug("Query cache cleared");
    }

    QueryCacheEntry<T>? IQueryClient.GetCacheEntry<T>(string key)
    {
        if (_cache.TryGetValue(key, out var entry) && entry is QueryCacheEntry<T> typedEntry)
        {
            if (DateTime.UtcNow < typedEntry.ExpiresAt)
            {
                return typedEntry;
            }
        }

        return null;
    }

    void IQueryClient.RegisterQuery(string key, Func<Task> refetch)
    {
        while (true)
        {
            var list = _queryRefetchers.GetOrAdd(key, _ => new List<Func<Task>>());
            lock (list)
            {
                // The list may have been removed from the dictionary (when it became
                // empty) between GetOrAdd and taking the lock - retry if so.
                if (_queryRefetchers.TryGetValue(key, out var current) && ReferenceEquals(current, list))
                {
                    list.Add(refetch);
                    return;
                }
            }
        }
    }

    void IQueryClient.UnregisterQuery(string key, Func<Task> refetch)
    {
        if (_queryRefetchers.TryGetValue(key, out var list))
        {
            lock (list)
            {
                list.Remove(refetch);

                // Remove the dictionary entry when the per-key list becomes empty
                // to avoid unbounded growth. Conditional removal (key + exact list
                // instance) under the list lock keeps RegisterQuery race-free.
                if (list.Count == 0)
                {
                    _queryRefetchers.TryRemove(new KeyValuePair<string, List<Func<Task>>>(key, list));
                }
            }
        }
    }

    void IQueryClient.MarkInvalidated(string key)
    {
        _invalidatedKeys[key] = true;
    }

    bool IQueryClient.IsInvalidated(string key)
    {
        return _invalidatedKeys.TryGetValue(key, out var invalidated) && invalidated;
    }

    private async Task TriggerRefetchAsync(string key)
    {
        if (!_queryRefetchers.TryGetValue(key, out var refetchers))
            return;

        List<Func<Task>> copy;
        lock (refetchers)
        {
            copy = refetchers.ToList();
        }

        foreach (var refetch in copy)
        {
            try
            {
                await refetch();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error during query refetch for key {Key}", key);
            }
        }
    }

    private void CleanupExpiredEntries(object? state)
    {
        if (Volatile.Read(ref _disposed) != 0) return;

        var now = DateTime.UtcNow;
        var expiredEntries = new List<KeyValuePair<string, object>>();

        // Create a snapshot of the cache to avoid concurrent modification issues
        var cacheSnapshot = _cache.ToArray();

        foreach (var kvp in cacheSnapshot)
        {
            try
            {
                // Use interface for type-safe expiration check without reflection
                if (kvp.Value is IQueryCacheEntry entry && now >= entry.ExpiresAt)
                {
                    expiredEntries.Add(kvp);
                }
            }
            catch (Exception ex)
            {
                // Entry may have been modified/removed during iteration
                _logger?.LogDebug(ex, "Error checking cache entry {Key} during cleanup", kvp.Key);
            }
        }

        foreach (var kvp in expiredEntries)
        {
            // Conditionally remove only the exact expired entry instance so freshly
            // written data for the same key is never deleted.
            if (_cache.TryRemove(kvp))
            {
                _logger?.LogDebug("Expired cache entry removed for key {Key}", kvp.Key);
            }
        }

        // Clean up invalidation flags for keys that have neither a cache entry nor
        // any registered (active) query, to prevent unbounded growth.
        foreach (var key in _invalidatedKeys.Keys)
        {
            if (!_cache.ContainsKey(key) && !_queryRefetchers.ContainsKey(key))
            {
                _invalidatedKeys.TryRemove(key, out _);
            }
        }
    }

    /// <summary>
    /// Disposes the query client.
    /// </summary>
    public void Dispose()
    {
        // Use Interlocked.Exchange for atomic check-and-set to prevent race conditions
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _cleanupTimer.Dispose();
        _cache.Clear();
        _queryRefetchers.Clear();
        _invalidatedKeys.Clear();
    }
}
