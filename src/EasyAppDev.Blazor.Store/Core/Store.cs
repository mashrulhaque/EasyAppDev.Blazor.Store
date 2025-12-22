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
public class Store<TState> : IStore<TState>, IDisposable where TState : notnull
{
    private TState _state;
    private readonly SemaphoreSlim _lock;
    private readonly IEqualityComparer<TState> _comparer;
    private readonly ISubscriptionManager<TState> _subscriptionManager;
    private readonly MiddlewarePipeline<TState>? _middlewarePipeline;
    private readonly ILogger<Store<TState>>? _logger;
    private readonly StoreErrorHandler<TState>? _errorHandler;
    private int _disposed; // 0 = not disposed, 1 = disposed (using int for Interlocked)
    private readonly AsyncLocal<int> _updateDepth = new();

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

        if (middlewares?.Any() == true)
        {
            _middlewarePipeline = new MiddlewarePipeline<TState>(middlewares, middlewarePipelineLogger, middlewareOptions);
        }
    }

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

        _updateDepth.Value++;
        try
        {
            if (_updateDepth.Value > 1)
            {
                _logger?.LogWarning(
                    "Reentrancy detected in Store<{StateType}>. Update depth: {Depth}. " +
                    "This may indicate cross-store updates or nested update calls. " +
                    "Consider using UpdateAsync and ensuring proper async flow.",
                    typeof(TState).Name,
                    _updateDepth.Value);
            }

            TState? stateSnapshot = default;
            bool shouldNotify;
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

                if (_comparer.Equals(previousState, newState))
                {
                    shouldNotify = false;
                }
                else
                {
                    _state = newState;

                    if (_middlewarePipeline != null)
                    {
                        await _middlewarePipeline.ExecuteAfterUpdateAsync(previousState, _state, action)
                            .ConfigureAwait(false);
                    }

                    // Capture state snapshot for consistent notification
                    stateSnapshot = _state;
                    shouldNotify = true;
                }
            }
            finally
            {
                _lock.Release();
            }

            // Notify subscribers AFTER releasing lock to prevent reentrancy deadlocks
            // Pass captured snapshot for consistent state across all subscribers
            if (shouldNotify)
            {
                NotifySubscribers(stateSnapshot!);
            }
        }
        finally
        {
            _updateDepth.Value--;
        }
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Func<TState, Task<TState>> asyncUpdater, string? action = null)
    {
        ArgumentNullException.ThrowIfNull(asyncUpdater);
        ThrowIfDisposed();

        _updateDepth.Value++;
        try
        {
            if (_updateDepth.Value > 1)
            {
                _logger?.LogWarning(
                    "Reentrancy detected in Store<{StateType}>. Update depth: {Depth}. " +
                    "This may indicate cross-store updates or nested update calls. " +
                    "Consider using UpdateAsync and ensuring proper async flow.",
                    typeof(TState).Name,
                    _updateDepth.Value);
            }

            TState? stateSnapshot = default;
            bool shouldNotify;
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

                if (_comparer.Equals(previousState, newState))
                {
                    shouldNotify = false;
                }
                else
                {
                    _state = newState;

                    if (_middlewarePipeline != null)
                    {
                        await _middlewarePipeline.ExecuteAfterUpdateAsync(previousState, _state, action)
                            .ConfigureAwait(false);
                    }

                    // Capture state snapshot for consistent notification
                    stateSnapshot = _state;
                    shouldNotify = true;
                }
            }
            finally
            {
                _lock.Release();
            }

            // Notify subscribers AFTER releasing lock to prevent reentrancy deadlocks
            // Pass captured snapshot for consistent state across all subscribers
            if (shouldNotify)
            {
                NotifySubscribers(stateSnapshot!);
            }
        }
        finally
        {
            _updateDepth.Value--;
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

    private void NotifySubscribers(TState capturedState)
    {
        _subscriptionManager.NotifyAll(capturedState);
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
        _lock.Dispose();
        GC.SuppressFinalize(this);
    }
}
