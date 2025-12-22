using Microsoft.Extensions.Logging;

namespace EasyAppDev.Blazor.Store.Core;

/// <summary>
/// Manages subscriptions for a state store with thread-safe operations.
/// </summary>
/// <typeparam name="TState">The type of state being observed.</typeparam>
/// <remarks>
/// Internal interface used by Store implementation. Application code should use <see cref="IStateObservable{TState}"/> instead.
/// </remarks>
public interface ISubscriptionManager<TState> : IDisposable where TState : notnull
{
    /// <summary>
    /// Subscribes to all state changes with a callback and state getter function.
    /// </summary>
    /// <param name="callback">Callback invoked on state changes. Exceptions are caught and logged.</param>
    /// <param name="stateGetter">Function to get the current state when notifying.</param>
    /// <returns>Disposable subscription.</returns>
    IDisposable Subscribe(Action<TState> callback, Func<TState> stateGetter);

    /// <summary>
    /// Subscribes to selective state changes. Callback invoked only when selected value changes per comparer.
    /// </summary>
    /// <typeparam name="TSelected">The type of the selected value.</typeparam>
    /// <param name="selector">Pure function to select a portion of state.</param>
    /// <param name="callback">Callback invoked when selected value changes.</param>
    /// <param name="stateGetter">Function to get the current state when notifying.</param>
    /// <param name="comparer">Equality comparer to detect changes.</param>
    /// <returns>Disposable subscription.</returns>
    IDisposable Subscribe<TSelected>(
        Func<TState, TSelected> selector,
        Action<TSelected> callback,
        Func<TState> stateGetter,
        IEqualityComparer<TSelected> comparer);

    /// <summary>
    /// Notifies all subscribers of a state change. Exceptions in callbacks are caught and logged.
    /// </summary>
    void NotifyAll();

    /// <summary>
    /// Notifies all subscribers of a state change with a captured state snapshot.
    /// This ensures subscribers see a consistent state even if concurrent updates occur.
    /// </summary>
    /// <param name="capturedState">The state snapshot captured before releasing the store lock.</param>
    void NotifyAll(TState capturedState);

    /// <summary>
    /// Clears all active subscriptions. Typically called during store disposal.
    /// </summary>
    void Clear();

    /// <summary>
    /// Gets the current number of active subscriptions.
    /// </summary>
    int SubscriptionCount { get; }
}
