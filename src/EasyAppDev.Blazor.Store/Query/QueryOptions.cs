// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

namespace EasyAppDev.Blazor.Store.Query;

/// <summary>
/// Configuration options for a query operation.
/// </summary>
/// <typeparam name="T">The type of data returned by the query.</typeparam>
public class QueryOptions<T>
{
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
    /// Default is 0 (immediately stale).
    /// </summary>
    public TimeSpan StaleTime { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Gets or sets how long unused data stays in the cache after unmounting.
    /// Default is 5 minutes.
    /// </summary>
    public TimeSpan CacheTime { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets whether to refetch when the window regains focus.
    /// Default is true.
    /// </summary>
    public bool RefetchOnWindowFocus { get; set; } = true;

    /// <summary>
    /// Gets or sets the interval for automatic refetching.
    /// Null means no automatic refetching.
    /// </summary>
    public TimeSpan? RefetchInterval { get; set; }

    /// <summary>
    /// Gets or sets whether to refetch when network reconnects.
    /// Default is true.
    /// </summary>
    public bool RefetchOnReconnect { get; set; } = true;

    /// <summary>
    /// Gets or sets the number of retry attempts for failed queries.
    /// Default is 3.
    /// </summary>
    public int Retry { get; set; } = 3;

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
