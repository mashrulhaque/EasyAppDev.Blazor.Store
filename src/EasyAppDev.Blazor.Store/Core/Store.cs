using EasyAppDev.Blazor.Store.Middleware;
using EasyAppDev.Blazor.Store.Selectors;
using Microsoft.Extensions.Logging;

namespace EasyAppDev.Blazor.Store.Core;

/// <summary>
/// Thread-safe implementation of <see cref="IStore{TState}"/>.
/// </summary>
/// <remarks>
/// Delegates subscription management to <see cref="ISubscriptionManager{TState}"/>.
/// </remarks>
public class Store<TState> : IStore<TState>, IDisposable, IAsyncDisposable where TState : notnull
{
    private TState _state;
    private readonly SemaphoreSlim _lock;
    private readonly IEqualityComparer<TState> _comparer;
    private readonly ISubscriptionManager<TState> _subscriptionManager;
    private readonly IReadOnlyList<IMiddleware<TState>> _middlewares;
    private readonly MiddlewarePipeline<TState>? _middlewarePipeline;
    private readonly ILogger<Store<TState>>? _logger;
    private readonly StoreErrorHandler<TState>? _errorHandler;
    private int _disposed; // 0 = not disposed, 1 = disposed (using int for Interlocked)
    private readonly AsyncLocal<int> _updateDepth = new();
    // Monotonically increasing commit version, incremented under _lock. Passed to the
    // subscription manager so out-of-order notifications (possible on Blazor Server when
    // two updates race after releasing the lock) can be detected and stale ones skipped.
    private long _version;

    /// <summary>
    /// Initializes a new instance of the <see cref="Store{TState}"/> class.
    /// </summary>
    /// <param name="initialState">The initial state.</param>
    /// <param name="subscriptionManager">The subscription manager for handling observers.</param>
    /// <param name="comparer">Optional comparer for state equality.</param>
    /// <param name="middlewares">Optional collection of middleware.</param>
    /// <param name="middlewarePipelineLogger">Optional logger for middleware pipeline.</param>
    /// <param name="logger">Optional logger for store operations.</param>
    /// <param name="middlewareOptions">Optional configuration options for middleware pipeline.</param>
    /// <param name="errorHandler">Optional centralized error handler for store errors.</param>
    public Store(
        TState initialState,
        ISubscriptionManager<TState> subscriptionManager,
        IEqualityComparer<TState>? comparer = null,
        IEnumerable<IMiddleware<TState>>? middlewares = null,
        ILogger<MiddlewarePipeline<TState>>? middlewarePipelineLogger = null,
        ILogger<Store<TState>>? logger = null,
        MiddlewarePipelineOptions? middlewareOptions = null,
        StoreErrorHandler<TState>? errorHandler = null)
    {
        _state = initialState ?? throw new ArgumentNullException(nameof(initialState));
        _subscriptionManager = subscriptionManager ?? throw new ArgumentNullException(nameof(subscriptionManager));
        _lock = new SemaphoreSlim(1, 1);
        _comparer = comparer ?? EqualityComparer<TState>.Default;
        _logger = logger;
        _errorHandler = errorHandler;
        _middlewares = middlewares?.ToList() ?? (IReadOnlyList<IMiddleware<TState>>)Array.Empty<IMiddleware<TState>>();

        if (_middlewares.Count > 0)
        {
            _middlewarePipeline = new MiddlewarePipeline<TState>(_middlewares, middlewarePipelineLogger, middlewareOptions);
        }
    }

    /// <summary>
    /// Gets the middlewares attached to this store. Used internally for disposal.
    /// </summary>
    internal IReadOnlyList<IMiddleware<TState>> Middlewares => _middlewares;

    /// <inheritdoc />
    public TState GetState()
    {
        ThrowIfDisposed();
        // Memory barrier ensures visibility of the latest state value across threads
        Thread.MemoryBarrier();
        return _state;
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Func<TState, TState> updater, string? action = null)
    {
        ArgumentNullException.ThrowIfNull(updater);
        ThrowIfDisposed();

        TState? stateSnapshot = default;
        var shouldNotify = false;
        long version = 0;

        _updateDepth.Value++;
        try
        {
            ThrowIfNestedUpdate();

            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                var previousState = _state;

                if (_middlewarePipeline != null)
                {
                    await _middlewarePipeline.ExecuteBeforeUpdateAsync(previousState, action)
                        .ConfigureAwait(false);
                }

                var newState = updater(_state);

                if (newState is null)
                    throw new InvalidOperationException("Updater function returned null.");

                if (!_comparer.Equals(previousState, newState))
                {
                    _state = newState;

                    if (_middlewarePipeline != null)
                    {
                        try
                        {
                            await _middlewarePipeline.ExecuteAfterUpdateAsync(previousState, _state, action)
                                .ConfigureAwait(false);
                        }
                        catch
                        {
                            // An after-update middleware rejected the update (e.g. validation).
                            // Roll back the committed state before propagating, otherwise the
                            // state would be silently committed without notifying subscribers.
                            // The lock is still held, so this rollback is safe.
                            _state = previousState;
                            throw;
                        }
                    }

                    // Capture state snapshot and commit version for consistent notification
                    stateSnapshot = _state;
                    version = ++_version;
                    shouldNotify = true;
                }
            }
            finally
            {
                _lock.Release();
            }
        }
        finally
        {
            // Decrement BEFORE notifying subscribers: a subscriber-initiated update is safe
            // (the lock is already released) and must not be reported as a nested update.
            _updateDepth.Value--;
        }

        // Notify subscribers AFTER releasing lock to prevent reentrancy deadlocks
        // Pass captured snapshot for consistent state across all subscribers
        if (shouldNotify)
        {
            NotifySubscribers(stateSnapshot!, version, action);
        }
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Func<TState, Task<TState>> asyncUpdater, string? action = null)
    {
        ArgumentNullException.ThrowIfNull(asyncUpdater);
        ThrowIfDisposed();

        TState? stateSnapshot = default;
        var shouldNotify = false;
        long version = 0;

        _updateDepth.Value++;
        try
        {
            ThrowIfNestedUpdate();

            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                var previousState = _state;

                if (_middlewarePipeline != null)
                {
                    await _middlewarePipeline.ExecuteBeforeUpdateAsync(previousState, action)
                        .ConfigureAwait(false);
                }

                var newState = await asyncUpdater(_state).ConfigureAwait(false);

                if (newState is null)
                    throw new InvalidOperationException("Updater function returned null.");

                if (!_comparer.Equals(previousState, newState))
                {
                    _state = newState;

                    if (_middlewarePipeline != null)
                    {
                        try
                        {
                            await _middlewarePipeline.ExecuteAfterUpdateAsync(previousState, _state, action)
                                .ConfigureAwait(false);
                        }
                        catch
                        {
                            // An after-update middleware rejected the update (e.g. validation).
                            // Roll back the committed state before propagating, otherwise the
                            // state would be silently committed without notifying subscribers.
                            // The lock is still held, so this rollback is safe.
                            _state = previousState;
                            throw;
                        }
                    }

                    // Capture state snapshot and commit version for consistent notification
                    stateSnapshot = _state;
                    version = ++_version;
                    shouldNotify = true;
                }
            }
            finally
            {
                _lock.Release();
            }
        }
        finally
        {
            // Decrement BEFORE notifying subscribers: a subscriber-initiated update is safe
            // (the lock is already released) and must not be reported as a nested update.
            _updateDepth.Value--;
        }

        // Notify subscribers AFTER releasing lock to prevent reentrancy deadlocks
        // Pass captured snapshot for consistent state across all subscribers
        if (shouldNotify)
        {
            NotifySubscribers(stateSnapshot!, version, action);
        }
    }

    private void ThrowIfNestedUpdate()
    {
        if (_updateDepth.Value > 1)
        {
            throw new InvalidOperationException(
                $"Nested UpdateAsync detected from middleware or updater on Store<{typeof(TState).Name}> — " +
                "this would deadlock because the store's update lock is not reentrant. " +
                "Do not call UpdateAsync from middleware or updater functions; " +
                "subscribe to the store and update from the subscription callback instead.");
        }
    }

    /// <inheritdoc />
    public IDisposable Subscribe(Action<TState> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ThrowIfDisposed();

        return _subscriptionManager.Subscribe(callback, () => _state);
    }

    /// <inheritdoc />
    public IDisposable Subscribe(Action<TState, string?> listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        ThrowIfDisposed();

        return _subscriptionManager.Subscribe(listener, () => _state);
    }

    /// <inheritdoc />
    public IDisposable Subscribe<TSelected>(
        Func<TState, TSelected> selector,
        Action<TSelected> callback)
    {
        return Subscribe(selector, callback, EqualityComparer<TSelected>.Default);
    }

    /// <inheritdoc />
    public IDisposable Subscribe<TSelected>(
        Func<TState, TSelected> selector,
        Action<TSelected> callback,
        IEqualityComparer<TSelected> comparer)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentNullException.ThrowIfNull(comparer);
        ThrowIfDisposed();

        return _subscriptionManager.Subscribe(selector, callback, () => _state, comparer);
    }

    /// <inheritdoc />
    public IDisposable Subscribe<TSelected>(
        ISelector<TState, TSelected> selector,
        Action<TSelected> callback)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(callback);
        ThrowIfDisposed();

        return _subscriptionManager.Subscribe(
            state => selector.Select(state),
            callback,
            () => _state,
            EqualityComparer<TSelected>.Default);
    }

    private void NotifySubscribers(TState capturedState, long version, string? action)
    {
        // The store may be disposed while an update is in flight; in that case there is
        // nobody left to notify and the disposed subscription manager must not be touched.
        if (Volatile.Read(ref _disposed) != 0)
            return;

        try
        {
            _subscriptionManager.NotifyAll(capturedState, version, action);
        }
        catch (ObjectDisposedException)
        {
            // Disposed concurrently between the check above and NotifyAll - ignore.
        }
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(Store<TState>));
    }

    /// <summary>
    /// Reports an error to the registered error handler.
    /// </summary>
    internal void HandleError(Exception exception, ErrorLocation location, string? action = null)
    {
        var error = new StoreError<TState>(exception, _state, action, location);

        _logger?.LogError(exception, "Store error in {Location}: {Message}", location, exception.Message);

        try
        {
            _errorHandler?.Invoke(error);
        }
        catch (Exception handlerEx)
        {
            _logger?.LogError(handlerEx, "Error in store error handler");
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Use Interlocked.Exchange for atomic check-and-set to prevent race conditions
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _subscriptionManager.Clear();
        _subscriptionManager.Dispose();

        foreach (var middleware in _middlewares)
        {
            try
            {
                if (middleware is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                else if (middleware is IAsyncDisposable asyncDisposable)
                {
                    // Best effort from the synchronous path: fire-and-forget the async
                    // disposal. Use DisposeAsync() for deterministic async cleanup.
                    _ = asyncDisposable.DisposeAsync().AsTask();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error disposing middleware {MiddlewareType}", middleware.GetType().Name);
            }
        }

        // NOTE: _lock (SemaphoreSlim) is intentionally NOT disposed. It holds no unmanaged
        // resources as long as AvailableWaitHandle is never accessed (it is not), and
        // disposing it would make in-flight UpdateAsync calls throw ObjectDisposedException
        // from their finally { _lock.Release(); } blocks.
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        // Use Interlocked.Exchange for atomic check-and-set to prevent race conditions
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _subscriptionManager.Clear();
        _subscriptionManager.Dispose();

        foreach (var middleware in _middlewares)
        {
            try
            {
                if (middleware is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                }
                else if (middleware is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error disposing middleware {MiddlewareType}", middleware.GetType().Name);
            }
        }

        // NOTE: _lock (SemaphoreSlim) is intentionally NOT disposed - see Dispose().
        GC.SuppressFinalize(this);
    }
}
