// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

namespace EasyAppDev.Blazor.Store.Utilities;

/// <summary>
/// Provides in-memory caching with expiration, request deduplication, and automatic cleanup.
/// </summary>
/// <remarks>
/// Caches expensive data operations with optional time-based expiration.
/// Multiple concurrent requests for the same key execute only one loader.
/// </remarks>
/// <example>
/// <code>
/// public async Task&lt;User&gt; GetUserAsync(int userId)
/// {
///     return await _cache.GetOrLoadAsync(
///         $"user-{userId}",
///         async () => await apiClient.FetchUserAsync(userId),
///         cacheFor: TimeSpan.FromMinutes(5)
///     );
/// }
/// </code>
/// </example>
public interface ILazyCache : IDisposable
{
    /// <summary>
    /// Gets data from cache or loads it using the provided loader function if not cached or expired.
    /// </summary>
    /// <typeparam name="T">The type of data to cache.</typeparam>
    /// <param name="key">Unique cache key identifying this data.</param>
    /// <param name="loader">Async function to load the data if not cached or expired.</param>
    /// <param name="cacheFor">Optional duration to cache the data. If null, data is cached indefinitely.</param>
    /// <returns>The cached data if available and not expired, otherwise the result of executing the loader function.</returns>
    /// <remarks>
    /// Multiple concurrent requests for the same key execute only one loader.
    /// Failed requests are not cached and can be retried on the next call.
    /// </remarks>
    /// <example>
    /// <code>
    /// var user = await cache.GetOrLoadAsync(
    ///     $"user-{userId}",
    ///     async () => await userService.GetUserAsync(userId),
    ///     TimeSpan.FromMinutes(5)
    /// );
    /// </code>
    /// </example>
    Task<T> GetOrLoadAsync<T>(
        string key,
        Func<Task<T>> loader,
        TimeSpan? cacheFor = null);

    /// <summary>
    /// Removes a specific entry from the cache by its key.
    /// </summary>
    /// <param name="key">The cache key to remove. No effect if key doesn't exist.</param>
    /// <returns>A task that completes when the entry has been removed.</returns>
    Task RemoveAsync(string key);

    /// <summary>
    /// Clears all entries from the cache, both expired and active.
    /// </summary>
    /// <returns>A task that completes when all entries have been cleared.</returns>
    Task ClearAsync();

    /// <summary>
    /// Removes all expired entries from the cache and returns the count of entries removed.
    /// </summary>
    /// <returns>The number of expired entries that were removed from the cache.</returns>
    /// <remarks>
    /// Expired entries are removed lazily on access. Call this method periodically in memory-sensitive applications.
    /// </remarks>
    Task<int> CleanupExpiredAsync();

    /// <summary>
    /// Gets the current number of entries in the cache, including expired entries.
    /// </summary>
    /// <value>The total count of entries in the cache (includes expired entries).</value>
    int Count { get; }
}
