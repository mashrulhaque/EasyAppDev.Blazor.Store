// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

namespace EasyAppDev.Blazor.Store.Query;

/// <summary>
/// Represents a query that fetches and caches data.
/// Inspired by TanStack Query for a familiar and powerful data-fetching pattern.
/// </summary>
/// <typeparam name="T">The type of data returned by the query.</typeparam>
public sealed class Query<T> : IDisposable, IQueryInitializable
{
    private readonly QueryOptions<T> _options;
    private readonly IQueryClient _queryClient;
    private readonly Action _onStateChange;
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly Func<Task> _refetchDelegate;
    private readonly SynchronizationContext? _syncContext;
    private readonly TimeSpan _staleTime;
    private readonly int _retry;
    private readonly bool _refetchOnWindowFocus;
    private readonly bool _refetchOnReconnect;
    private CancellationTokenSource? _fetchCts;
    private Timer? _refetchTimer;
    private volatile bool _disposed;

    private T? _data;
    private Exception? _error;
    private int _status = (int)QueryStatus.Idle;
    private int _isFetching; // 0 = not fetching, 1 = fetching (using int for Interlocked)
    private long _dataUpdatedAtTicks; // 0 = never updated; UTC ticks otherwise
    private int _failureCount;
    private bool _isPlaceholderData;

    internal Query(QueryOptions<T> options, IQueryClient queryClient, Action onStateChange)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _queryClient = queryClient ?? throw new ArgumentNullException(nameof(queryClient));
        _onStateChange = onStateChange ?? throw new ArgumentNullException(nameof(onStateChange));

        // Capture the current synchronization context (the Blazor dispatcher when
        // constructed from a component) so user callbacks can be marshalled back to it.
        _syncContext = SynchronizationContext.Current;

        // Resolve client-level defaults: explicit per-query values win,
        // otherwise fall back to the query client's configured defaults.
        var clientOptions = _queryClient.Options;
        _staleTime = _options.HasExplicitStaleTime || clientOptions is null
            ? _options.StaleTime
            : clientOptions.DefaultStaleTime;
        _retry = _options.HasExplicitRetry || clientOptions is null
            ? _options.Retry
            : clientOptions.DefaultRetry;
        _refetchOnWindowFocus = _options.HasExplicitRefetchOnWindowFocus || clientOptions is null
            ? _options.RefetchOnWindowFocus
            : clientOptions.DefaultRefetchOnWindowFocus;
        _refetchOnReconnect = _options.HasExplicitRefetchOnReconnect || clientOptions is null
            ? _options.RefetchOnReconnect
            : clientOptions.DefaultRefetchOnReconnect;

        // Create refetch delegate for registration.
        // Invalidation-triggered refetches must respect Enabled() (TanStack semantics):
        // a disabled query is not fetched by InvalidateQueries. Manual RefetchAsync()
        // still forces a fetch.
        _refetchDelegate = () => _options.Enabled() ? FetchAsync(forceRefetch: true) : Task.CompletedTask;

        // Register with QueryClient for invalidation support and for
        // window-focus / reconnect refetching (TanStack semantics: only stale,
        // enabled queries are refetched by those events).
        _queryClient.RegisterQuery(
            _options.Key,
            _refetchDelegate,
            ShouldRefetchOnWindowFocus,
            ShouldRefetchOnReconnect);

        // Initialize with initial data or placeholder
        if (_options.InitialData is not null)
        {
            _data = _options.InitialData;
            _dataUpdatedAtTicks = (_options.InitialDataUpdatedAt ?? DateTime.UtcNow).Ticks;
            _status = (int)QueryStatus.Success;
        }
        else if (_options.PlaceholderData is not null)
        {
            _data = _options.PlaceholderData;
            _isPlaceholderData = true;
        }

        // Try to get cached data (only if not invalidated)
        var cachedEntry = _queryClient.GetCacheEntry<T>(_options.Key);
        if (cachedEntry is not null && !_queryClient.IsInvalidated(_options.Key))
        {
            _data = cachedEntry.Data;
            _dataUpdatedAtTicks = cachedEntry.UpdatedAt.Ticks;
            _status = (int)QueryStatus.Success;
            _isPlaceholderData = false;
        }
    }

    /// <summary>
    /// Gets the data from the query if successful.
    /// </summary>
    public T? Data => _data;

    /// <summary>
    /// Gets the error if the query failed.
    /// </summary>
    public Exception? Error => _error;

    /// <summary>
    /// Gets the current status of the query.
    /// </summary>
    public QueryStatus Status => (QueryStatus)Volatile.Read(ref _status);

    /// <summary>
    /// Gets whether the query is in loading state (initial load).
    /// </summary>
    public bool IsLoading => Status == QueryStatus.Loading;

    /// <summary>
    /// Gets whether the query has an error.
    /// </summary>
    public bool IsError => Status == QueryStatus.Error;

    /// <summary>
    /// Gets whether the query was successful.
    /// </summary>
    public bool IsSuccess => Status == QueryStatus.Success;

    /// <summary>
    /// Gets whether any fetch is in progress (including background refetch).
    /// </summary>
    public bool IsFetching => Volatile.Read(ref _isFetching) != 0;

    /// <summary>
    /// Gets whether the data is stale (older than staleTime).
    /// </summary>
    public bool IsStale
    {
        get
        {
            var updatedAt = DataUpdatedAt;
            return updatedAt is null || DateTime.UtcNow - updatedAt > _staleTime;
        }
    }

    /// <summary>
    /// Gets whether the current data is placeholder data.
    /// </summary>
    public bool IsPlaceholderData => _isPlaceholderData;

    /// <summary>
    /// Gets when the data was last updated.
    /// </summary>
    public DateTime? DataUpdatedAt
    {
        get
        {
            var ticks = Volatile.Read(ref _dataUpdatedAtTicks);
            return ticks == 0 ? null : new DateTime(ticks, DateTimeKind.Utc);
        }
    }

    /// <summary>
    /// Gets the number of consecutive failures.
    /// </summary>
    public int FailureCount => _failureCount;

    /// <summary>
    /// Refetches the data.
    /// </summary>
    public async Task RefetchAsync()
    {
        if (_disposed) return;
        await FetchAsync(forceRefetch: true);
    }

    /// <summary>
    /// Invalidates the query, causing it to refetch on next access.
    /// </summary>
    public void Invalidate()
    {
        if (_disposed) return;
        _queryClient.InvalidateQueries(_options.Key);
    }

    /// <summary>
    /// Sets the data directly, updating the cache.
    /// </summary>
    public void SetData(T data)
    {
        if (_disposed) return;

        _data = data;
        _error = null;
        _isPlaceholderData = false;
        _failureCount = 0;
        Volatile.Write(ref _dataUpdatedAtTicks, DateTime.UtcNow.Ticks);
        Volatile.Write(ref _status, (int)QueryStatus.Success);

        _queryClient.SetQueryData(_options.Key, data, _options.CacheTime);
        NotifyStateChange();
    }

    async Task IQueryInitializable.InitializeAsync()
    {
        if (!_options.Enabled())
        {
            return;
        }

        // If we have cached data and it's fresh, don't fetch
        if (!IsStale && Status == QueryStatus.Success)
        {
            SetupRefetchInterval();
            return;
        }

        await FetchAsync(forceRefetch: false);
        SetupRefetchInterval();
    }

    private async Task FetchAsync(bool forceRefetch)
    {
        if (_disposed) return;
        // Only check Enabled for automatic fetches, not manual refetch
        if (!forceRefetch && !_options.Enabled()) return;

        // Thread-safe check to prevent duplicate fetches
        // For force refetch, always proceed; otherwise check if already fetching
        if (!forceRefetch && Interlocked.CompareExchange(ref _isFetching, 1, 0) != 0)
            return;

        // For force refetch, set the flag (may already be 1)
        if (forceRefetch)
            Interlocked.Exchange(ref _isFetching, 1);

        // Create THIS fetch's CTS and capture both it and its token as locals.
        // The _fetchCts field may be swapped by a concurrent force refetch, so the
        // field must never be re-read for cancellation decisions in this method.
        CancellationTokenSource cts;
        try
        {
            cts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token);
        }
        catch (ObjectDisposedException)
        {
            // Query was disposed concurrently
            return;
        }

        var token = cts.Token;

        // Install our CTS and cancel any in-flight fetch. The superseded fetch
        // owns (and will dispose) its own CTS in its finally block.
        var previous = Interlocked.Exchange(ref _fetchCts, cts);
        if (previous is not null)
        {
            try
            {
                previous.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The owning fetch already completed and disposed it
            }
        }

        try
        {
            // Only set to Loading if we don't have data
            if (Status != QueryStatus.Success && !_isPlaceholderData)
            {
                Volatile.Write(ref _status, (int)QueryStatus.Loading);
            }

            NotifyStateChange();

            var attempt = 0;
            Exception? lastException = null;
            var fetchSucceeded = false;
            T? result = default;

            while (attempt <= _retry)
            {
                try
                {
                    result = await _options.QueryFn(token);

                    if (_disposed || token.IsCancellationRequested || !IsCurrentFetch(cts))
                    {
                        // Superseded by a newer fetch (or disposed) - never
                        // overwrite state/cache with this (potentially stale) result.
                        return;
                    }

                    fetchSucceeded = true;
                    break;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    // THIS fetch was cancelled (dispose or force refetch) - not a failure.
                    return;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    attempt++;
                    _failureCount++;

                    if (attempt <= _retry)
                    {
                        var delay = _options.RetryDelay(attempt - 1);
                        try
                        {
                            await Task.Delay(delay, token);
                        }
                        catch (OperationCanceledException)
                        {
                            return;
                        }
                    }
                }
            }

            if (fetchSucceeded)
            {
                // Success path. User callbacks (Select/OnSuccess/OnSettled) run outside
                // the retry loop so a callback exception can never be misclassified as a
                // fetch failure (re-running a successful fetch and ending in Error state).
                if (result is not null && _options.Select is not null)
                {
                    try
                    {
                        result = _options.Select(result);
                    }
                    catch
                    {
                        // Select threw - keep the untransformed result rather than
                        // discarding a successful fetch.
                    }
                }

                _data = result;
                _error = null;
                _failureCount = 0;
                _isPlaceholderData = false;
                Volatile.Write(ref _dataUpdatedAtTicks, DateTime.UtcNow.Ticks);
                // Status is the last state write (release) so a reader observing
                // Success also observes the _data write (acquire via Status getter).
                Volatile.Write(ref _status, (int)QueryStatus.Success);

                // Update cache
                if (result is not null)
                {
                    _queryClient.SetQueryData(_options.Key, result, _options.CacheTime);
                }

                if (_options.OnSuccess is { } onSuccess)
                {
                    CallbackDispatcher.Invoke(_syncContext, () => onSuccess(result!));
                }

                if (_options.OnSettled is { } onSettled)
                {
                    CallbackDispatcher.Invoke(_syncContext, onSettled);
                }

                NotifyStateChange();
                return;
            }

            // All retries exhausted
            if (_disposed || !IsCurrentFetch(cts))
            {
                return;
            }

            _error = lastException;
            Volatile.Write(ref _status, (int)QueryStatus.Error);

            if (_options.OnError is { } onError)
            {
                CallbackDispatcher.Invoke(_syncContext, () => onError(lastException!));
            }

            if (_options.OnSettled is { } onSettledAfterError)
            {
                CallbackDispatcher.Invoke(_syncContext, onSettledAfterError);
            }

            NotifyStateChange();
        }
        finally
        {
            // Each FetchAsync invocation disposes ITS OWN CTS. Clear the field
            // first (only if we're still the current fetch) so nobody can grab a
            // reference to a CTS that is about to be disposed. Resetting _isFetching
            // only when still current avoids clobbering a superseding fetch's flag,
            // and runs after the cache write + callbacks above.
            if (Interlocked.CompareExchange(ref _fetchCts, null, cts) == cts)
            {
                Interlocked.Exchange(ref _isFetching, 0);
            }

            cts.Dispose();
        }
    }

    private bool IsCurrentFetch(CancellationTokenSource cts) =>
        ReferenceEquals(Volatile.Read(ref _fetchCts), cts);

    // Window-event participation predicates, evaluated by QueryClient when the
    // browser window regains focus or the network reconnects. Only stale,
    // enabled, non-disposed queries with the corresponding flag refetch.
    private bool ShouldRefetchOnWindowFocus() =>
        !_disposed && _refetchOnWindowFocus && _options.Enabled() && IsStale;

    private bool ShouldRefetchOnReconnect() =>
        !_disposed && _refetchOnReconnect && _options.Enabled() && IsStale;

    private void SetupRefetchInterval()
    {
        if (_options.RefetchInterval is null || _disposed)
            return;

        _refetchTimer?.Dispose();
        _refetchTimer = new Timer(
            async _ =>
            {
                try
                {
                    await RefetchAsync();
                }
                catch
                {
                    // Query disposed or fetch failed - never crash the timer
                    // (an unhandled exception in an async-void timer callback
                    // would crash the process on Blazor Server).
                }
            },
            null,
            _options.RefetchInterval.Value,
            _options.RefetchInterval.Value);
    }

    private void NotifyStateChange()
    {
        if (!_disposed)
        {
            try
            {
                _onStateChange();
            }
            catch
            {
                // The owning component may be tearing down - never let a render
                // notification crash a background fetch or timer.
            }
        }
    }

    /// <summary>
    /// Disposes the query and cancels any pending operations.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Unregister from QueryClient
        _queryClient.UnregisterQuery(_options.Key, _refetchDelegate);

        _refetchTimer?.Dispose();

        // Cancel any in-flight fetch; its FetchAsync owns and disposes the CTS.
        var fetchCts = Interlocked.Exchange(ref _fetchCts, null);
        if (fetchCts is not null)
        {
            try
            {
                fetchCts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The owning fetch already disposed it
            }
        }

        _disposeCts.Cancel();
        _disposeCts.Dispose();
    }
}
