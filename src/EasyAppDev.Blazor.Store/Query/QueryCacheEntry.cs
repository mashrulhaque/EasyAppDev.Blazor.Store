// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

namespace EasyAppDev.Blazor.Store.Query;

/// <summary>
/// Non-generic interface for cache entry expiration checking.
/// Enables efficient expiration checks without reflection.
/// </summary>
internal interface IQueryCacheEntry
{
    /// <summary>
    /// Gets when the cache entry expires.
    /// </summary>
    DateTime ExpiresAt { get; }
}

/// <summary>
/// Represents a cached query result.
/// </summary>
/// <typeparam name="T">The type of cached data.</typeparam>
internal sealed class QueryCacheEntry<T> : IQueryCacheEntry
{
    /// <summary>
    /// Gets or sets the cached data.
    /// </summary>
    public T Data { get; set; } = default!;

    /// <summary>
    /// Gets or sets when the data was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets when the cache entry expires.
    /// </summary>
    public DateTime ExpiresAt { get; set; }
}
