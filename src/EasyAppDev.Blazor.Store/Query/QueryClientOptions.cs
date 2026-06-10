// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

namespace EasyAppDev.Blazor.Store.Query;

/// <summary>
/// Global configuration options for the query client.
/// </summary>
public class QueryClientOptions
{
    /// <summary>
    /// Gets or sets the default time data is considered fresh.
    /// Default is 0 (immediately stale).
    /// </summary>
    public TimeSpan DefaultStaleTime { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Gets or sets the default time unused data stays in cache.
    /// Default is 5 minutes.
    /// </summary>
    public TimeSpan DefaultCacheTime { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the default retry count for failed queries.
    /// Default is 3.
    /// </summary>
    public int DefaultRetry { get; set; } = 3;

    /// <summary>
    /// Gets or sets whether queries refetch when the browser window regains
    /// focus by default. Applies to queries that do not set
    /// <see cref="QueryOptions{T}.RefetchOnWindowFocus"/> explicitly; only
    /// enabled queries with stale data are refetched.
    /// Default is true.
    /// </summary>
    public bool DefaultRefetchOnWindowFocus { get; set; } = true;

    /// <summary>
    /// Gets or sets whether queries refetch when the network reconnects
    /// (window <c>online</c> event) by default. Applies to queries that do not
    /// set <see cref="QueryOptions{T}.RefetchOnReconnect"/> explicitly; only
    /// enabled queries with stale data are refetched.
    /// Default is true.
    /// </summary>
    public bool DefaultRefetchOnReconnect { get; set; } = true;

    /// <summary>
    /// Gets or sets the interval for cache cleanup.
    /// Default is 1 minute.
    /// </summary>
    public TimeSpan CacheCleanupInterval { get; set; } = TimeSpan.FromMinutes(1);
}
