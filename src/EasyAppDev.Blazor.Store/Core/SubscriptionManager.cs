using Microsoft.Extensions.Logging;

namespace EasyAppDev.Blazor.Store.Core;

/// <summary>
/// Thread-safe subscription manager for state stores.
/// </summary>
/// <typeparam name="TState">The type of state being observed.</typeparam>
public sealed class SubscriptionManager<TState> : ISubscriptionManager<TState> where TState : notnull
{
    private readonly List<SubscriptionBase> _subscriptions;
    private readonly ILogger<SubscriptionManager<TState>>? _logger;
    private readonly Queue<SubscriptionBase> _pendingRemovals = new();
    private int _disposed; // 0 = not disposed, 1 = disposed
    private bool _isNotifying;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionManager{TState}"/> class.
    /// </summary>
    /// <param name="logger">Optional logger for error reporting.</param>
    public SubscriptionManager(ILogger<SubscriptionManager<TState>>? logger = null)
    {
        _subscriptions = new List<SubscriptionBase>();
        _logger = logger;
    }

    /// <inheritdoc />
    public int SubscriptionCount
    {
        get
        {
            lock (_subscriptions)
            {
                return _subscriptions.Count;
            }
        }
    }

    /// <inheritdoc />
    public IDisposable Subscribe(Action<TState> callback, Func<TState> stateGetter)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentNullException.ThrowIfNull(stateGetter);
        ThrowIfDisposed();

        var subscription = new FullStateSubscription(callback, stateGetter, RemoveSubscription);

        lock (_subscriptions)
        {
            _subscriptions.Add(subscription);
        }

        return subscription;
    }

    /// <inheritdoc />
    public IDisposable Subscribe<TSelected>(
        Func<TState, TSelected> selector,
        Action<TSelected> callback,
        Func<TState> stateGetter,
        IEqualityComparer<TSelected> comparer)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentNullException.ThrowIfNull(stateGetter);
        ArgumentNullException.ThrowIfNull(comparer);
        ThrowIfDisposed();

        var state = stateGetter();
        var initialValue = selector(state);

        var subscription = new SelectorSubscription<TSelected>(
            selector, callback, stateGetter, comparer, initialValue, RemoveSubscription);

        lock (_subscriptions)
        {
            _subscriptions.Add(subscription);
        }

        return subscription;
    }

    /// <inheritdoc />
    public void NotifyAll()
    {
        ThrowIfDisposed();

        List<SubscriptionBase> subscriptionsCopy;

        lock (_subscriptions)
        {
            subscriptionsCopy = new List<SubscriptionBase>(_subscriptions);
            _isNotifying = true;
        }

        try
        {
            foreach (var subscription in subscriptionsCopy)
            {
                try
                {
                    subscription.Invoke();
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error in subscription callback");
                }
            }
        }
        finally
        {
            ProcessPendingRemovals();
        }
    }

    /// <inheritdoc />
    public void NotifyAll(TState capturedState)
    {
        ThrowIfDisposed();

        List<SubscriptionBase> subscriptionsCopy;

        lock (_subscriptions)
        {
            subscriptionsCopy = new List<SubscriptionBase>(_subscriptions);
            _isNotifying = true;
        }

        try
        {
            foreach (var subscription in subscriptionsCopy)
            {
                try
                {
                    subscription.InvokeWithSnapshot(capturedState);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error in subscription callback");
                }
            }
        }
        finally
        {
            ProcessPendingRemovals();
        }
    }

    private void ProcessPendingRemovals()
    {
        lock (_subscriptions)
        {
            _isNotifying = false;
            while (_pendingRemovals.Count > 0)
            {
                var sub = _pendingRemovals.Dequeue();
                _subscriptions.Remove(sub);
            }
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        lock (_subscriptions)
        {
            _subscriptions.Clear();
        }
    }

    private void RemoveSubscription(SubscriptionBase subscription)
    {
        lock (_subscriptions)
        {
            // Queue removal if we're in the middle of notifying to prevent deadlocks
            if (_isNotifying)
            {
                _pendingRemovals.Enqueue(subscription);
            }
            else
            {
                _subscriptions.Remove(subscription);
            }
        }
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(SubscriptionManager<TState>));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Use Interlocked.Exchange for atomic check-and-set
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        lock (_subscriptions)
        {
            _subscriptions.Clear();
            _pendingRemovals.Clear();
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Base class for subscriptions supporting both legacy and snapshot-based invocation.
    /// </summary>
    private abstract class SubscriptionBase : IDisposable
    {
        private readonly Action<SubscriptionBase> _unsubscribe;
        private int _disposed; // 0 = not disposed, 1 = disposed (use int for Interlocked)

        protected SubscriptionBase(Action<SubscriptionBase> unsubscribe)
        {
            _unsubscribe = unsubscribe;
        }

        /// <summary>Invokes using the stored state getter (legacy behavior).</summary>
        public abstract void Invoke();

        /// <summary>Invokes with a captured state snapshot for consistency.</summary>
        public abstract void InvokeWithSnapshot(TState capturedState);

        public void Dispose()
        {
            // Atomic check-and-set to prevent double-dispose race condition
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            _unsubscribe(this);
        }
    }

    /// <summary>
    /// Subscription for full state callbacks.
    /// </summary>
    private sealed class FullStateSubscription : SubscriptionBase
    {
        private readonly Action<TState> _callback;
        private readonly Func<TState> _stateGetter;

        public FullStateSubscription(
            Action<TState> callback,
            Func<TState> stateGetter,
            Action<SubscriptionBase> unsubscribe) : base(unsubscribe)
        {
            _callback = callback;
            _stateGetter = stateGetter;
        }

        public override void Invoke() => _callback(_stateGetter());
        public override void InvokeWithSnapshot(TState capturedState) => _callback(capturedState);
    }

    /// <summary>
    /// Subscription for selector-based callbacks with change detection.
    /// </summary>
    private sealed class SelectorSubscription<TSelected> : SubscriptionBase
    {
        private readonly Func<TState, TSelected> _selector;
        private readonly Action<TSelected> _callback;
        private readonly Func<TState> _stateGetter;
        private readonly IEqualityComparer<TSelected> _comparer;
        private TSelected _previousValue;
        private readonly object _valueLock = new();

        public SelectorSubscription(
            Func<TState, TSelected> selector,
            Action<TSelected> callback,
            Func<TState> stateGetter,
            IEqualityComparer<TSelected> comparer,
            TSelected initialValue,
            Action<SubscriptionBase> unsubscribe) : base(unsubscribe)
        {
            _selector = selector;
            _callback = callback;
            _stateGetter = stateGetter;
            _comparer = comparer;
            _previousValue = initialValue;
        }

        public override void Invoke()
        {
            InvokeWithState(_stateGetter());
        }

        public override void InvokeWithSnapshot(TState capturedState)
        {
            InvokeWithState(capturedState);
        }

        private void InvokeWithState(TState state)
        {
            var currentValue = _selector(state);

            lock (_valueLock)
            {
                if (!_comparer.Equals(_previousValue, currentValue))
                {
                    _previousValue = currentValue;
                    _callback(currentValue);
                }
            }
        }
    }
}
