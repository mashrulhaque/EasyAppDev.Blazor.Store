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
    private bool _disposed;

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

            // Expired, remove it
            _cache.TryRemove(key, out _);
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

        // Fire and forget with proper error handling
        _ = Task.Run(async () =>
        {
            try
            {
                await TriggerRefetchAsync(key).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error triggering refetch for invalidated query {Key}", key);
            }
        });

        _logger?.LogDebug("Query invalidated for key {Key}", key);
    }

    /// <inheritdoc />
    public void InvalidateQueries(Func<string, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        foreach (var key in _cache.Keys.Where(predicate))
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
        var list = _queryRefetchers.GetOrAdd(key, _ => new List<Func<Task>>());
        lock (list)
        {
            list.Add(refetch);
        }
    }

    void IQueryClient.UnregisterQuery(string key, Func<Task> refetch)
    {
        if (_queryRefetchers.TryGetValue(key, out var list))
        {
            lock (list)
            {
                list.Remove(refetch);
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
        if (_disposed) return;

        var now = DateTime.UtcNow;
        var expiredKeys = new List<string>();

        foreach (var kvp in _cache)
        {
            var entryType = kvp.Value.GetType();
            if (entryType.IsGenericType && entryType.GetGenericTypeDefinition() == typeof(QueryCacheEntry<>))
            {
                var expiresAtProp = entryType.GetProperty("ExpiresAt");
                if (expiresAtProp?.GetValue(kvp.Value) is DateTime expiresAt && now >= expiresAt)
                {
                    expiredKeys.Add(kvp.Key);
                }
            }
        }

        foreach (var key in expiredKeys)
        {
            _cache.TryRemove(key, out _);
            _logger?.LogDebug("Expired cache entry removed for key {Key}", key);
        }
    }

    /// <summary>
    /// Disposes the query client.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cleanupTimer.Dispose();
        _cache.Clear();
        _queryRefetchers.Clear();
        _invalidatedKeys.Clear();
    }
}
