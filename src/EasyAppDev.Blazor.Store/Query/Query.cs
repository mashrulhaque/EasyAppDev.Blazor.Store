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
    private CancellationTokenSource? _fetchCts;
    private Timer? _refetchTimer;
    private bool _disposed;

    private T? _data;
    private Exception? _error;
    private QueryStatus _status = QueryStatus.Idle;
    private int _isFetching; // 0 = not fetching, 1 = fetching (using int for Interlocked)
    private DateTime? _dataUpdatedAt;
    private int _failureCount;
    private bool _isPlaceholderData;

    internal Query(QueryOptions<T> options, IQueryClient queryClient, Action onStateChange)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _queryClient = queryClient ?? throw new ArgumentNullException(nameof(queryClient));
        _onStateChange = onStateChange ?? throw new ArgumentNullException(nameof(onStateChange));

        // Create refetch delegate for registration
        _refetchDelegate = () => RefetchAsync();

        // Register with QueryClient for invalidation support
        _queryClient.RegisterQuery(_options.Key, _refetchDelegate);

        // Initialize with initial data or placeholder
        if (_options.InitialData is not null)
        {
            _data = _options.InitialData;
            _dataUpdatedAt = _options.InitialDataUpdatedAt ?? DateTime.UtcNow;
            _status = QueryStatus.Success;
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
            _dataUpdatedAt = cachedEntry.UpdatedAt;
            _status = QueryStatus.Success;
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
    public QueryStatus Status => _status;

    /// <summary>
    /// Gets whether the query is in loading state (initial load).
    /// </summary>
    public bool IsLoading => _status == QueryStatus.Loading;

    /// <summary>
    /// Gets whether the query has an error.
    /// </summary>
    public bool IsError => _status == QueryStatus.Error;

    /// <summary>
    /// Gets whether the query was successful.
    /// </summary>
    public bool IsSuccess => _status == QueryStatus.Success;

    /// <summary>
    /// Gets whether any fetch is in progress (including background refetch).
    /// </summary>
    public bool IsFetching => Volatile.Read(ref _isFetching) != 0;

    /// <summary>
    /// Gets whether the data is stale (older than staleTime).
    /// </summary>
    public bool IsStale => _dataUpdatedAt is null ||
        DateTime.UtcNow - _dataUpdatedAt > _options.StaleTime;

    /// <summary>
    /// Gets whether the current data is placeholder data.
    /// </summary>
    public bool IsPlaceholderData => _isPlaceholderData;

    /// <summary>
    /// Gets when the data was last updated.
    /// </summary>
    public DateTime? DataUpdatedAt => _dataUpdatedAt;

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
        _dataUpdatedAt = DateTime.UtcNow;
        _status = QueryStatus.Success;
        _error = null;
        _isPlaceholderData = false;
        _failureCount = 0;

        _queryClient.SetQueryData(_options.Key, data);
        NotifyStateChange();
    }

    async Task IQueryInitializable.InitializeAsync()
    {
        if (!_options.Enabled())
        {
            return;
        }

        // If we have cached data and it's fresh, don't fetch
        if (!IsStale && _status == QueryStatus.Success)
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

        // Cancel any existing fetch
        _fetchCts?.Cancel();
        _fetchCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token);

        // Only set to Loading if we don't have data
        if (_status != QueryStatus.Success && !_isPlaceholderData)
        {
            _status = QueryStatus.Loading;
        }

        NotifyStateChange();

        var attempt = 0;
        Exception? lastException = null;

        while (attempt <= _options.Retry)
        {
            try
            {
                var result = await _options.QueryFn(_fetchCts.Token);

                if (_disposed || _fetchCts.Token.IsCancellationRequested)
                {
                    Interlocked.Exchange(ref _isFetching, 0);
                    return;
                }

                // Apply transformation if specified
                if (result is not null && _options.Select is not null)
                {
                    result = _options.Select(result);
                }

                _data = result;
                _dataUpdatedAt = DateTime.UtcNow;
                _status = QueryStatus.Success;
                _error = null;
                _failureCount = 0;
                _isPlaceholderData = false;
                Interlocked.Exchange(ref _isFetching, 0);

                // Update cache
                if (result is not null)
                {
                    _queryClient.SetQueryData(_options.Key, result, _options.CacheTime);
                }

                _options.OnSuccess?.Invoke(result!);
                _options.OnSettled?.Invoke();
                NotifyStateChange();
                return;
            }
            catch (OperationCanceledException) when (_fetchCts.Token.IsCancellationRequested)
            {
                Interlocked.Exchange(ref _isFetching, 0);
                return;
            }
            catch (Exception ex)
            {
                lastException = ex;
                attempt++;
                _failureCount++;

                if (attempt <= _options.Retry)
                {
                    var delay = _options.RetryDelay(attempt - 1);
                    try
                    {
                        await Task.Delay(delay, _fetchCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        Interlocked.Exchange(ref _isFetching, 0);
                        return;
                    }
                }
            }
        }

        // All retries exhausted
        if (_disposed)
        {
            Interlocked.Exchange(ref _isFetching, 0);
            return;
        }

        _error = lastException;
        _status = QueryStatus.Error;
        Interlocked.Exchange(ref _isFetching, 0);

        _options.OnError?.Invoke(lastException!);
        _options.OnSettled?.Invoke();
        NotifyStateChange();
    }

    private void SetupRefetchInterval()
    {
        if (_options.RefetchInterval is null || _disposed)
            return;

        _refetchTimer?.Dispose();
        _refetchTimer = new Timer(
            async _ => await RefetchAsync(),
            null,
            _options.RefetchInterval.Value,
            _options.RefetchInterval.Value);
    }

    private void NotifyStateChange()
    {
        if (!_disposed)
        {
            _onStateChange();
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
        _fetchCts?.Cancel();
        _fetchCts?.Dispose();
        _disposeCts.Cancel();
        _disposeCts.Dispose();
    }
}
