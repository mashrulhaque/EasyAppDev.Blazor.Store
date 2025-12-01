using Microsoft.Extensions.Logging;

namespace EasyAppDev.Blazor.Store.Core;

/// <summary>
/// Thread-safe subscription manager for state stores.
/// </summary>
/// <typeparam name="TState">The type of state being observed.</typeparam>
public sealed class SubscriptionManager<TState> : ISubscriptionManager<TState> where TState : notnull
{
    private readonly List<Subscription> _subscriptions;
    private readonly ILogger<SubscriptionManager<TState>>? _logger;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionManager{TState}"/> class.
    /// </summary>
    /// <param name="logger">Optional logger for error reporting.</param>
    public SubscriptionManager(ILogger<SubscriptionManager<TState>>? logger = null)
    {
        _subscriptions = new List<Subscription>();
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

        var subscription = new Subscription(
            () => callback(stateGetter()),
            RemoveSubscription);

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
        // Use a wrapper object to safely track previous value across notifications
        var valueHolder = new SelectorValueHolder<TSelected>(selector(state));

        var subscription = new Subscription(
            () =>
            {
                var currentState = stateGetter();
                var currentValue = selector(currentState);

                // Thread-safe check and update of previous value
                lock (valueHolder)
                {
                    if (!comparer.Equals(valueHolder.Value, currentValue))
                    {
                        valueHolder.Value = currentValue;
                        callback(currentValue);
                    }
                }
            },
            RemoveSubscription);

        lock (_subscriptions)
        {
            _subscriptions.Add(subscription);
        }

        return subscription;
    }

    /// <summary>
    /// Thread-safe wrapper for selector value tracking.
    /// </summary>
    private sealed class SelectorValueHolder<T>
    {
        public T Value;
        public SelectorValueHolder(T value) => Value = value;
    }

    /// <inheritdoc />
    public void NotifyAll()
    {
        ThrowIfDisposed();

        List<Subscription> subscriptionsCopy;

        lock (_subscriptions)
        {
            subscriptionsCopy = new List<Subscription>(_subscriptions);
        }

        foreach (var subscription in subscriptionsCopy)
        {
            try
            {
                subscription.Invoke();
            }
            catch (Exception ex)
            {
                // Log error but continue notifying other subscribers
                _logger?.LogError(ex, "Error in subscription callback");
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

    private void RemoveSubscription(Subscription subscription)
    {
        lock (_subscriptions)
        {
            _subscriptions.Remove(subscription);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SubscriptionManager<TState>));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        lock (_subscriptions)
        {
            _subscriptions.Clear();
        }

        GC.SuppressFinalize(this);
    }

    private sealed class Subscription : IDisposable
    {
        private readonly Action _callback;
        private readonly Action<Subscription> _unsubscribe;
        private bool _disposed;

        public Subscription(Action callback, Action<Subscription> unsubscribe)
        {
            _callback = callback;
            _unsubscribe = unsubscribe;
        }

        public void Invoke() => _callback();

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _unsubscribe(this);
        }
    }
}
