// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EasyAppDev.Blazor.Store.Utilities;

/// <summary>
/// Provides in-memory caching with expiration and request deduplication.
/// </summary>
/// <remarks>
/// <para>
/// LazyCache automatically:
/// - Caches loaded data with optional expiration
/// - Deduplicates concurrent requests for the same key
/// - Cleans up expired entries on access
/// - Handles errors by not caching failed requests
/// </para>
/// <example>
/// <code>
/// using var cache = new LazyCache();
///
/// // Multiple concurrent calls - only one API request
/// var task1 = cache.GetOrLoadAsync("user-1", () => LoadUserAsync(1));
/// var task2 = cache.GetOrLoadAsync("user-1", () => LoadUserAsync(1));
/// var task3 = cache.GetOrLoadAsync("user-1", () => LoadUserAsync(1));
///
/// await Task.WhenAll(task1, task2, task3);
/// // Only ONE LoadUserAsync call executed!
/// </code>
/// </example>
/// </remarks>
public sealed class LazyCache : ILazyCache
{
    private sealed class CacheEntry<T>
    {
        public required T Value { get; init; }
        public required DateTime ExpiresAt { get; init; }

        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    }

    private readonly Dictionary<string, object> _cache = new();
    private readonly Dictionary<string, Task<object?>> _inFlightRequests = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Gets or loads data from cache.
    /// </summary>
    /// <typeparam name="T">The type of data to cache.</typeparam>
    /// <param name="key">Unique cache key.</param>
    /// <param name="loader">Function to load data if not cached.</param>
    /// <param name="cacheFor">Optional time to cache data. If null, caches indefinitely.</param>
    /// <returns>The cached or freshly loaded data.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="key"/> or <paramref name="loader"/> is null.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// Thrown when this cache has been disposed.
    /// </exception>
    /// <remarks>
    /// If multiple concurrent requests use the same key, only one loader executes
    /// and all callers receive the same result (request deduplication).
    /// </remarks>
    public async Task<T> GetOrLoadAsync<T>(
        string key,
        Func<Task<T>> loader,
        TimeSpan? cacheFor = null)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(loader);

        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();

            // Check cache for existing valid entry
            if (_cache.TryGetValue(key, out var cachedObj) &&
                cachedObj is CacheEntry<T> entry)
            {
                if (!entry.IsExpired)
                {
                    return entry.Value;
                }

                // Remove expired entry
                _cache.Remove(key);
            }

            // Check for in-flight request (deduplication)
            if (_inFlightRequests.TryGetValue(key, out var inFlightTask))
            {
                // Release lock while waiting for in-flight request
                _lock.Release();
                try
                {
                    var result = await inFlightTask.ConfigureAwait(false);
                    return (T)result!;
                }
                finally
                {
                    await _lock.WaitAsync().ConfigureAwait(false);
                }
            }

            // No cache hit and no in-flight request - start loading
            var loadTask = LoadAndCacheAsync(key, loader, cacheFor);
            _inFlightRequests[key] = loadTask;

            // Release lock before waiting for load task
            _lock.Release();
            try
            {
                var value = await loadTask.ConfigureAwait(false);
                return (T)value!;
            }
            finally
            {
                await _lock.WaitAsync().ConfigureAwait(false);
                // Remove from in-flight requests
                _inFlightRequests.Remove(key);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<object?> LoadAndCacheAsync<T>(
        string key,
        Func<Task<T>> loader,
        TimeSpan? cacheFor)
    {
        try
        {
            // Load the value (outside lock - already released by caller)
            var value = await loader().ConfigureAwait(false);

            // Acquire lock to update cache
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                ThrowIfDisposed();

                // Cache the loaded value
                var expiresAt = cacheFor.HasValue
                    ? DateTime.UtcNow.Add(cacheFor.Value)
                    : DateTime.MaxValue;

                _cache[key] = new CacheEntry<T>
                {
                    Value = value,
                    ExpiresAt = expiresAt
                };

                return value;
            }
            finally
            {
                _lock.Release();
            }
        }
        catch
        {
            // Don't cache errors - let caller handle
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                _cache.Remove(key);
            }
            finally
            {
                _lock.Release();
            }
            throw;
        }
    }

    /// <summary>
    /// Removes a specific entry from the cache.
    /// </summary>
    /// <param name="key">The cache key to remove.</param>
    public async Task RemoveAsync(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            _cache.Remove(key);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Clears all entries from the cache.
    /// </summary>
    public async Task ClearAsync()
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            _cache.Clear();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Removes all expired entries from the cache.
    /// </summary>
    /// <returns>The number of expired entries removed.</returns>
    public async Task<int> CleanupExpiredAsync()
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();

            var keysToRemove = new List<string>();

            foreach (var kvp in _cache)
            {
                // Use reflection to check if entry is expired
                var entryType = kvp.Value.GetType();
                if (entryType.IsGenericType &&
                    entryType.GetGenericTypeDefinition() == typeof(CacheEntry<>))
                {
                    var isExpiredProperty = entryType.GetProperty("IsExpired");
                    if (isExpiredProperty != null)
                    {
                        var isExpired = (bool)isExpiredProperty.GetValue(kvp.Value)!;
                        if (isExpired)
                        {
                            keysToRemove.Add(kvp.Key);
                        }
                    }
                }
            }

            foreach (var key in keysToRemove)
            {
                _cache.Remove(key);
            }

            return keysToRemove.Count;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Gets the number of entries currently in the cache.
    /// </summary>
    public int Count
    {
        get
        {
            _lock.Wait();
            try
            {
                return _cache.Count;
            }
            finally
            {
                _lock.Release();
            }
        }
    }

    /// <summary>
    /// Disposes the cache, clearing all entries.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _lock.Wait();
        try
        {
            _cache.Clear();
            _inFlightRequests.Clear();
            _disposed = true;
        }
        finally
        {
            _lock.Release();
            _lock.Dispose();
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(LazyCache));
        }
    }
}
