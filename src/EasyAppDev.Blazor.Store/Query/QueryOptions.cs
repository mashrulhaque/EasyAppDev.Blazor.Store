// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

namespace EasyAppDev.Blazor.Store.Query;

/// <summary>
/// Configuration options for a query operation.
/// </summary>
/// <typeparam name="T">The type of data returned by the query.</typeparam>
public class QueryOptions<T>
{
    private TimeSpan? _staleTime;
    private int? _retry;
    private bool? _refetchOnWindowFocus;
    private bool? _refetchOnReconnect;

    /// <summary>
    /// Gets or sets the unique key for this query. Used for caching and invalidation.
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// Gets or sets the function that fetches the data.
    /// </summary>
    public required Func<CancellationToken, Task<T?>> QueryFn { get; set; }

    /// <summary>
    /// Gets or sets how long data is considered fresh (not stale).
    /// When not set explicitly, <see cref="QueryClientOptions.DefaultStaleTime"/> applies.
    /// Default is 0 (immediately stale).
    /// </summary>
    public TimeSpan StaleTime
    {
        get => _staleTime ?? TimeSpan.Zero;
        set => _staleTime = value;
    }

    /// <summary>
    /// Gets whether <see cref="StaleTime"/> was set explicitly (used to resolve client-level defaults).
    /// </summary>
    internal bool HasExplicitStaleTime => _staleTime.HasValue;

    /// <summary>
    /// Gets or sets how long unused data stays in the cache after unmounting.
    /// Default is 5 minutes.
    /// </summary>
    public TimeSpan CacheTime { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets whether to refetch when the browser window regains focus
    /// (window <c>focus</c> / document <c>visibilitychange</c> events).
    /// The refetch only happens when the query is enabled and its data is stale.
    /// When not set explicitly, <see cref="QueryClientOptions.DefaultRefetchOnWindowFocus"/> applies.
    /// Default is true.
    /// </summary>
    public bool RefetchOnWindowFocus
    {
        get => _refetchOnWindowFocus ?? true;
        set => _refetchOnWindowFocus = value;
    }

    /// <summary>
    /// Gets whether <see cref="RefetchOnWindowFocus"/> was set explicitly (used to resolve client-level defaults).
    /// </summary>
    internal bool HasExplicitRefetchOnWindowFocus => _refetchOnWindowFocus.HasValue;

    /// <summary>
    /// Gets or sets the interval for automatic refetching.
    /// Null means no automatic refetching.
    /// </summary>
    public TimeSpan? RefetchInterval { get; set; }

    /// <summary>
    /// Gets or sets whether to refetch when the network reconnects
    /// (window <c>online</c> event).
    /// The refetch only happens when the query is enabled and its data is stale.
    /// When not set explicitly, <see cref="QueryClientOptions.DefaultRefetchOnReconnect"/> applies.
    /// Default is true.
    /// </summary>
    public bool RefetchOnReconnect
    {
        get => _refetchOnReconnect ?? true;
        set => _refetchOnReconnect = value;
    }

    /// <summary>
    /// Gets whether <see cref="RefetchOnReconnect"/> was set explicitly (used to resolve client-level defaults).
    /// </summary>
    internal bool HasExplicitRefetchOnReconnect => _refetchOnReconnect.HasValue;

    /// <summary>
    /// Gets or sets the number of retry attempts for failed queries.
    /// When not set explicitly, <see cref="QueryClientOptions.DefaultRetry"/> applies.
    /// Default is 3.
    /// </summary>
    public int Retry
    {
        get => _retry ?? 3;
        set => _retry = value;
    }

    /// <summary>
    /// Gets whether <see cref="Retry"/> was set explicitly (used to resolve client-level defaults).
    /// </summary>
    internal bool HasExplicitRetry => _retry.HasValue;

    /// <summary>
    /// Gets or sets the delay function between retries.
    /// Takes the attempt number (0-based) and returns the delay.
    /// Default is exponential backoff: 2^attempt seconds.
    /// </summary>
    public Func<int, TimeSpan> RetryDelay { get; set; } = attempt =>
        TimeSpan.FromSeconds(Math.Pow(2, attempt));

    /// <summary>
    /// Gets or sets the callback invoked when the query succeeds.
    /// </summary>
    public Action<T>? OnSuccess { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked when the query fails.
    /// </summary>
    public Action<Exception>? OnError { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked when the query settles (success or error).
    /// </summary>
    public Action? OnSettled { get; set; }

    /// <summary>
    /// Gets or sets a function to transform the data after fetching.
    /// </summary>
    public Func<T, T>? Select { get; set; }

    /// <summary>
    /// Gets or sets a function that determines if the query is enabled.
    /// Default is always enabled.
    /// </summary>
    public Func<bool> Enabled { get; set; } = () => true;

    /// <summary>
    /// Gets or sets placeholder data to show while loading.
    /// </summary>
    public T? PlaceholderData { get; set; }

    /// <summary>
    /// Gets or sets initial data to use as if it came from the cache.
    /// </summary>
    public T? InitialData { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the initial data.
    /// </summary>
    public DateTime? InitialDataUpdatedAt { get; set; }

    /// <summary>
    /// Creates query options with a simple key and query function.
    /// </summary>
    public static QueryOptions<T> Create(string key, Func<Task<T?>> queryFn) => new()
    {
        Key = key,
        QueryFn = _ => queryFn()
    };

    /// <summary>
    /// Creates query options with a key and cancellable query function.
    /// </summary>
    public static QueryOptions<T> Create(string key, Func<CancellationToken, Task<T?>> queryFn) => new()
    {
        Key = key,
        QueryFn = queryFn
    };
}
