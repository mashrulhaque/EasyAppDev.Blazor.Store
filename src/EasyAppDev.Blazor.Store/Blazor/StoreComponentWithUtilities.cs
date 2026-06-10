using Microsoft.AspNetCore.Components;
using EasyAppDev.Blazor.Store.AsyncActions;
using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Utilities;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;

namespace EasyAppDev.Blazor.Store.Blazor;

/// <summary>
/// Extended base component class that includes utility methods for debouncing,
/// throttling, lazy loading, and async action execution.
/// Use this when you need the full feature set. For a leaner component,
/// use <see cref="StoreComponent{TState}"/> instead.
/// </summary>
/// <typeparam name="TState">The type of state managed by the store.</typeparam>
public abstract class StoreComponentWithUtilities<TState> : StoreComponent<TState>
    where TState : notnull
{
    private readonly HashSet<string> _registeredDebounceKeys = new();
    private readonly HashSet<string> _registeredThrottleKeys = new();
    private readonly object _keysLock = new();

    // Per-instance discriminator so two instances of the same component type do not
    // cancel/replace each other's pending debounce/throttle operations on the
    // circuit-scoped managers (and disposing one doesn't cancel the other's).
    private readonly string _instanceId = Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// Builds a debounce/throttle key that is unique per component instance and action.
    /// </summary>
    private string BuildOperationKey(string? action)
        => $"{GetType().Name}_{_instanceId}_{action ?? "update"}";

    /// <summary>
    /// Gets the injected debounce manager.
    /// </summary>
    [Inject]
    protected IDebounceManager DebounceManager { get; set; } = default!;

    /// <summary>
    /// Gets the injected throttle manager.
    /// </summary>
    [Inject]
    protected IThrottleManager ThrottleManager { get; set; } = default!;

    /// <summary>
    /// Gets the injected lazy cache.
    /// </summary>
    [Inject]
    protected ILazyCache LazyCache { get; set; } = default!;

    /// <summary>
    /// Gets the injected service provider.
    /// </summary>
    [Inject]
    private IServiceProvider ServiceProvider { get; set; } = default!;

    /// <summary>
    /// Gets the injected async action executor (optional).
    /// </summary>
    protected IAsyncActionExecutor<TState>? AsyncExecutor { get; private set; }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();

        // Try to resolve AsyncExecutor - it's optional
        AsyncExecutor = ServiceProvider.GetService(typeof(IAsyncActionExecutor<TState>)) as IAsyncActionExecutor<TState>;
    }

    /// <summary>
    /// Updates state with debouncing, cancelling any pending updates with the same action name.
    /// </summary>
    /// <param name="updater">Function to transform the current state.</param>
    /// <param name="delayMilliseconds">Delay in milliseconds before applying the update.</param>
    /// <param name="action">Optional action name for debugging (auto-filled with caller method name).</param>
    protected Task UpdateDebounced(
        Func<TState, TState> updater,
        int delayMilliseconds,
        [CallerMemberName] string? action = null)
    {
        ArgumentNullException.ThrowIfNull(updater);

        var key = BuildOperationKey(action);
        TrackDebounceKey(key);
        return DebounceManager.Debounce(key, async () =>
        {
            // Check if disposed before invoking to prevent operations on disposed component
            if (IsDisposed) return;

            try
            {
                await InvokeAsync(async () =>
                {
                    if (IsDisposed) return;
                    await Store.UpdateAsync(updater, action);
                });
            }
            catch (ObjectDisposedException)
            {
                // Component was disposed during async invoke - expected behavior
            }
        }, delayMilliseconds);
    }

    /// <summary>
    /// Updates state asynchronously with debouncing.
    /// </summary>
    /// <param name="asyncUpdater">Async function to transform the current state.</param>
    /// <param name="delayMilliseconds">Delay in milliseconds before applying the update.</param>
    /// <param name="action">Optional action name for debugging (auto-filled with caller method name).</param>
    protected Task UpdateDebouncedAsync(
        Func<TState, Task<TState>> asyncUpdater,
        int delayMilliseconds,
        [CallerMemberName] string? action = null)
    {
        ArgumentNullException.ThrowIfNull(asyncUpdater);

        var key = BuildOperationKey(action);
        TrackDebounceKey(key);
        return DebounceManager.Debounce(key, async () =>
        {
            // Check if disposed before invoking to prevent operations on disposed component
            if (IsDisposed) return;

            try
            {
                await InvokeAsync(async () =>
                {
                    if (IsDisposed) return;
                    await Store.UpdateAsync(asyncUpdater, action);
                });
            }
            catch (ObjectDisposedException)
            {
                // Component was disposed during async invoke - expected behavior
            }
        }, delayMilliseconds);
    }

    /// <summary>
    /// Updates state with throttling, limiting update frequency.
    /// </summary>
    /// <param name="updater">Function to transform the current state.</param>
    /// <param name="intervalMilliseconds">Minimum interval in milliseconds between updates.</param>
    /// <param name="action">Optional action name for debugging (auto-filled with caller method name).</param>
    protected Task UpdateThrottled(
        Func<TState, TState> updater,
        int intervalMilliseconds,
        [CallerMemberName] string? action = null)
    {
        ArgumentNullException.ThrowIfNull(updater);

        var key = BuildOperationKey(action);
        TrackThrottleKey(key);
        return ThrottleManager.Throttle(key, async () =>
        {
            // Check if disposed before invoking to prevent operations on disposed component
            if (IsDisposed) return;

            try
            {
                await InvokeAsync(async () =>
                {
                    if (IsDisposed) return;
                    await Store.UpdateAsync(updater, action);
                });
            }
            catch (ObjectDisposedException)
            {
                // Component was disposed during async invoke - expected behavior
            }
        }, intervalMilliseconds);
    }

    /// <summary>
    /// Updates state asynchronously with throttling.
    /// </summary>
    /// <param name="asyncUpdater">Async function to transform the current state.</param>
    /// <param name="intervalMilliseconds">Minimum interval in milliseconds between updates.</param>
    /// <param name="action">Optional action name for debugging (auto-filled with caller method name).</param>
    protected Task UpdateThrottledAsync(
        Func<TState, Task<TState>> asyncUpdater,
        int intervalMilliseconds,
        [CallerMemberName] string? action = null)
    {
        ArgumentNullException.ThrowIfNull(asyncUpdater);

        var key = BuildOperationKey(action);
        TrackThrottleKey(key);
        return ThrottleManager.Throttle(key, async () =>
        {
            // Check if disposed before invoking to prevent operations on disposed component
            if (IsDisposed) return;

            try
            {
                await InvokeAsync(async () =>
                {
                    if (IsDisposed) return;
                    await Store.UpdateAsync(asyncUpdater, action);
                });
            }
            catch (ObjectDisposedException)
            {
                // Component was disposed during async invoke - expected behavior
            }
        }, intervalMilliseconds);
    }

    /// <summary>
    /// Executes an async action with automatic loading, success, and error state handling.
    /// </summary>
    /// <typeparam name="TResult">The type of result returned by the async action.</typeparam>
    /// <param name="asyncAction">The async action to execute.</param>
    /// <param name="loading">Function to transform state to loading state.</param>
    /// <param name="success">Function to transform state with the result on success.</param>
    /// <param name="error">Optional function to transform state on error.</param>
    /// <param name="action">Optional action name for debugging (auto-filled with caller method name).</param>
    protected Task ExecuteAsync<TResult>(
        Func<Task<TResult>> asyncAction,
        Func<TState, TState> loading,
        Func<TState, TResult, TState> success,
        Func<TState, Exception, TState>? error = null,
        [CallerMemberName] string? action = null)
    {
        if (AsyncExecutor == null)
        {
            throw new InvalidOperationException(
                $"IAsyncActionExecutor<{typeof(TState).Name}> is not registered. " +
                $"Call builder.Services.AddAsyncActionExecutor<{typeof(TState).Name}>() before using ExecuteAsync.");
        }
        return AsyncExecutor.ExecuteAsync(asyncAction, loading, success, error, action);
    }

    /// <summary>
    /// Executes an async action without a result value.
    /// </summary>
    /// <param name="asyncAction">The async action to execute.</param>
    /// <param name="loading">Function to transform state to loading state.</param>
    /// <param name="success">Function to transform state on success.</param>
    /// <param name="error">Optional function to transform state on error.</param>
    /// <param name="action">Optional action name for debugging (auto-filled with caller method name).</param>
    protected Task ExecuteAsync(
        Func<Task> asyncAction,
        Func<TState, TState> loading,
        Func<TState, TState> success,
        Func<TState, Exception, TState>? error = null,
        [CallerMemberName] string? action = null)
    {
        if (AsyncExecutor == null)
        {
            throw new InvalidOperationException(
                $"IAsyncActionExecutor<{typeof(TState).Name}> is not registered. " +
                $"Call builder.Services.AddAsyncActionExecutor<{typeof(TState).Name}>() before using ExecuteAsync.");
        }
        return AsyncExecutor.ExecuteAsync(asyncAction, loading, success, error, action);
    }

    /// <summary>
    /// Executes an async action with simplified success handler that ignores the result.
    /// </summary>
    /// <typeparam name="TResult">The type of result returned by the async action.</typeparam>
    /// <param name="asyncAction">The async action to execute.</param>
    /// <param name="loading">Function to transform state to loading state.</param>
    /// <param name="success">Function to transform state on success (ignores result).</param>
    /// <param name="error">Optional function to transform state on error.</param>
    /// <param name="action">Optional action name for debugging (auto-filled with caller method name).</param>
    protected Task ExecuteAsync<TResult>(
        Func<Task<TResult>> asyncAction,
        Func<TState, TState> loading,
        Func<TState, TState> success,
        Func<TState, Exception, TState>? error = null,
        [CallerMemberName] string? action = null)
    {
        if (AsyncExecutor == null)
        {
            throw new InvalidOperationException(
                $"IAsyncActionExecutor<{typeof(TState).Name}> is not registered. " +
                $"Call builder.Services.AddAsyncActionExecutor<{typeof(TState).Name}>() before using ExecuteAsync.");
        }
        return AsyncExecutor.ExecuteAsync(asyncAction, loading, success, error, action);
    }

    /// <summary>
    /// Executes an async action with caching and full deduplication of both fetch and state updates.
    /// </summary>
    /// <typeparam name="TResult">The type of result returned by the async action.</typeparam>
    /// <param name="cacheKey">Unique key for deduplication. Concurrent calls with the same key share one execution.</param>
    /// <param name="asyncAction">The async action to execute.</param>
    /// <param name="loading">Function to transform state to loading state.</param>
    /// <param name="success">Function to transform state with the result on success.</param>
    /// <param name="error">Optional function to transform state on error.</param>
    /// <param name="cacheFor">Optional duration to cache the result.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <param name="action">Optional action name for debugging (auto-filled with caller method name).</param>
    /// <returns>The result from the async action.</returns>
    /// <remarks>
    /// <para>
    /// Unlike <see cref="ExecuteAsync{TResult}(Func{Task{TResult}}, Func{TState, TState}, Func{TState, TResult, TState}, Func{TState, Exception, TState}?, string?)"/>,
    /// this method deduplicates both the async fetch AND the state updates. If 5 components call
    /// this with the same cache key concurrently, only 2 state updates occur (one loading, one success/error).
    /// </para>
    /// <para>
    /// <strong>Important Callback Behavior:</strong> Only the first caller's callbacks (loading, success, error)
    /// are executed. Concurrent callers waiting for the same cache key receive the result but their callbacks
    /// are NOT invoked. This ensures exactly 2 state updates regardless of the number of concurrent callers.
    /// Ensure all concurrent callers provide consistent callbacks or design callbacks to be idempotent.
    /// </para>
    /// </remarks>
    protected Task<TResult> ExecuteCachedAsync<TResult>(
        string cacheKey,
        Func<Task<TResult>> asyncAction,
        Func<TState, TState> loading,
        Func<TState, TResult, TState> success,
        Func<TState, Exception, TState>? error = null,
        TimeSpan? cacheFor = null,
        CancellationToken cancellationToken = default,
        [CallerMemberName] string? action = null)
    {
        if (AsyncExecutor == null)
        {
            throw new InvalidOperationException(
                $"IAsyncActionExecutor<{typeof(TState).Name}> is not registered. " +
                $"Call builder.Services.AddAsyncActionExecutor<{typeof(TState).Name}>() before using ExecuteCachedAsync.");
        }
        return AsyncExecutor.ExecuteCachedAsync(cacheKey, asyncAction, loading, success, error, cacheFor, cancellationToken, action);
    }

    /// <summary>
    /// Executes an async action with caching and simplified success handler that ignores the result.
    /// </summary>
    /// <typeparam name="TResult">The type of result returned by the async action.</typeparam>
    /// <param name="cacheKey">Unique key for deduplication.</param>
    /// <param name="asyncAction">The async action to execute.</param>
    /// <param name="loading">Function to transform state to loading state.</param>
    /// <param name="success">Function to transform state on success (ignores result).</param>
    /// <param name="error">Optional function to transform state on error.</param>
    /// <param name="cacheFor">Optional duration to cache the result.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <param name="action">Optional action name for debugging (auto-filled with caller method name).</param>
    /// <remarks>
    /// <para>
    /// <strong>Important:</strong> Only the first caller's callbacks (loading, success, error) are executed.
    /// Concurrent callers waiting for the same cache key do NOT have their callbacks invoked.
    /// See the overload with TResult success parameter for detailed explanation.
    /// </para>
    /// </remarks>
    protected Task ExecuteCachedAsync<TResult>(
        string cacheKey,
        Func<Task<TResult>> asyncAction,
        Func<TState, TState> loading,
        Func<TState, TState> success,
        Func<TState, Exception, TState>? error = null,
        TimeSpan? cacheFor = null,
        CancellationToken cancellationToken = default,
        [CallerMemberName] string? action = null)
    {
        if (AsyncExecutor == null)
        {
            throw new InvalidOperationException(
                $"IAsyncActionExecutor<{typeof(TState).Name}> is not registered. " +
                $"Call builder.Services.AddAsyncActionExecutor<{typeof(TState).Name}>() before using ExecuteCachedAsync.");
        }
        return AsyncExecutor.ExecuteCachedAsync(cacheKey, asyncAction, loading, success, error, cacheFor, cancellationToken, action);
    }

    /// <summary>
    /// Invalidates a cached result from ExecuteCachedAsync by its cache key.
    /// </summary>
    /// <param name="cacheKey">The cache key to invalidate.</param>
    /// <remarks>
    /// This only affects cached results, not in-flight operations.
    /// The next call to ExecuteCachedAsync with this key will execute the async action again.
    /// </remarks>
    protected void InvalidateCachedResult(string cacheKey)
    {
        AsyncExecutor?.InvalidateCache(cacheKey);
    }

    /// <summary>
    /// Invalidates all cached results from ExecuteCachedAsync with keys starting with the specified prefix.
    /// </summary>
    /// <param name="prefix">The prefix to match cache keys against.</param>
    /// <remarks>
    /// Useful for invalidating related cache entries (e.g., "product-" invalidates "product-1", "product-2", etc.).
    /// </remarks>
    protected void InvalidateCachedResultsByPrefix(string prefix)
    {
        AsyncExecutor?.InvalidateCacheByPrefix(prefix);
    }

    /// <summary>
    /// Clears all cached results from ExecuteCachedAsync.
    /// </summary>
    /// <remarks>
    /// This only affects cached results, not in-flight operations.
    /// </remarks>
    protected void ClearCachedResults()
    {
        AsyncExecutor?.ClearCache();
    }

    /// <summary>
    /// Loads data with automatic caching and request deduplication.
    /// </summary>
    /// <typeparam name="T">The type of data to load.</typeparam>
    /// <param name="cacheKey">Unique key for caching the result.</param>
    /// <param name="loader">Function that loads the data.</param>
    /// <param name="cacheFor">Optional cache duration. If null, data is cached indefinitely.</param>
    /// <returns>The loaded or cached data.</returns>
    protected Task<T> LazyLoad<T>(
        string cacheKey,
        Func<Task<T>> loader,
        TimeSpan? cacheFor = null)
    {
        ArgumentNullException.ThrowIfNull(cacheKey);
        ArgumentNullException.ThrowIfNull(loader);

        return LazyCache.GetOrLoadAsync(cacheKey, loader, cacheFor);
    }

    /// <summary>
    /// Tracks a debounce key for cleanup on disposal.
    /// </summary>
    private void TrackDebounceKey(string key)
    {
        lock (_keysLock)
        {
            _registeredDebounceKeys.Add(key);
        }
    }

    /// <summary>
    /// Tracks a throttle key for cleanup on disposal.
    /// </summary>
    private void TrackThrottleKey(string key)
    {
        lock (_keysLock)
        {
            _registeredThrottleKeys.Add(key);
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Cancel all registered debounce and throttle operations for this component
            CancelPendingOperations();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Cancels all pending debounce and throttle operations registered by this component.
    /// </summary>
    private void CancelPendingOperations()
    {
        string[] debounceKeys;
        string[] throttleKeys;

        lock (_keysLock)
        {
            debounceKeys = _registeredDebounceKeys.ToArray();
            throttleKeys = _registeredThrottleKeys.ToArray();
            _registeredDebounceKeys.Clear();
            _registeredThrottleKeys.Clear();
        }

        // Cancel debounce operations (fire-and-forget since we're disposing)
        foreach (var key in debounceKeys)
        {
            _ = DebounceManager.CancelAsync(key);
        }

        // Cancel throttle operations
        foreach (var key in throttleKeys)
        {
            _ = ThrottleManager.CancelAsync(key);
        }
    }
}
