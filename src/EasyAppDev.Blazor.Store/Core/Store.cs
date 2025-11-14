using EasyAppDev.Blazor.Store.Middleware;
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
    private bool _disposed;

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
    public Store(
        TState initialState,
        ISubscriptionManager<TState> subscriptionManager,
        IEqualityComparer<TState>? comparer = null,
        IEnumerable<IMiddleware<TState>>? middlewares = null,
        ILogger<MiddlewarePipeline<TState>>? middlewarePipelineLogger = null,
        ILogger<Store<TState>>? logger = null,
        MiddlewarePipelineOptions? middlewareOptions = null)
    {
        _state = initialState ?? throw new ArgumentNullException(nameof(initialState));
        _subscriptionManager = subscriptionManager ?? throw new ArgumentNullException(nameof(subscriptionManager));
        _lock = new SemaphoreSlim(1, 1);
        _comparer = comparer ?? EqualityComparer<TState>.Default;
        _logger = logger;

        if (middlewares?.Any() == true)
        {
            _middlewarePipeline = new MiddlewarePipeline<TState>(middlewares, middlewarePipelineLogger, middlewareOptions);
        }
    }

    /// <inheritdoc />
    public TState GetState()
    {
        ThrowIfDisposed();
        return _state;
    }

    /// <inheritdoc />
    public void Update(Func<TState, TState> updater, string? action = null)
    {
        UpdateAsync(updater, action).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Func<TState, TState> updater, string? action = null)
    {
        ArgumentNullException.ThrowIfNull(updater);
        ThrowIfDisposed();

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
                return;

            _state = newState;

            if (_middlewarePipeline != null)
            {
                await _middlewarePipeline.ExecuteAfterUpdateAsync(previousState, _state, action)
                    .ConfigureAwait(false);
            }

            NotifySubscribers();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Func<TState, Task<TState>> asyncUpdater, string? action = null)
    {
        ArgumentNullException.ThrowIfNull(asyncUpdater);
        ThrowIfDisposed();

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
                return;

            _state = newState;

            if (_middlewarePipeline != null)
            {
                await _middlewarePipeline.ExecuteAfterUpdateAsync(previousState, _state, action)
                    .ConfigureAwait(false);
            }

            NotifySubscribers();
        }
        finally
        {
            _lock.Release();
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

    private void NotifySubscribers()
    {
        _subscriptionManager.NotifyAll();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(Store<TState>));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _subscriptionManager.Clear();
        _subscriptionManager.Dispose();
        _lock.Dispose();
        GC.SuppressFinalize(this);
    }
}
