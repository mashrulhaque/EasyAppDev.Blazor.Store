namespace EasyAppDev.Blazor.Store.Core;

/// <summary>
/// Provides subscription capabilities for observing state changes using the Observer pattern.
/// </summary>
/// <typeparam name="TState">The type of state being observed. Must be a non-nullable reference type.</typeparam>
/// <remarks>
/// Thread-safe subscription operations. Callbacks are invoked synchronously on the update thread.
/// Selective subscriptions optimize performance by filtering changes to specific properties.
/// </remarks>
/// <example>
/// <code>
/// // Subscribe to all state changes
/// var subscription = stateObservable.Subscribe(state => {
///     Console.WriteLine($"Count changed to: {state.Count}");
/// });
///
/// // Subscribe to specific property (performance optimization)
/// var countSubscription = stateObservable.Subscribe(
///     state => state.Count,
///     count => Console.WriteLine($"Count: {count}")
/// );
///
/// // Unsubscribe when done
/// subscription.Dispose();
/// countSubscription.Dispose();
/// </code>
/// </example>
public interface IStateObservable<TState> where TState : notnull
{
    /// <summary>
    /// Subscribes to all state changes with a callback that receives the entire state.
    /// </summary>
    /// <param name="callback">Callback invoked on state changes. Must not be null.</param>
    /// <returns>Disposable subscription. Call Dispose to unsubscribe.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="callback"/> is null.</exception>
    /// <remarks>
    /// Subscribes to all state changes. For better performance with specific properties, use selective subscriptions.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Basic subscription
    /// var subscription = store.Subscribe(state => {
    ///     Console.WriteLine($"State updated: Count={state.Count}");
    /// });
    ///
    /// // In a Blazor component
    /// private IDisposable? _subscription;
    ///
    /// protected override void OnInitialized()
    /// {
    ///     _subscription = store.Subscribe(state => {
    ///         InvokeAsync(StateHasChanged);
    ///     });
    /// }
    ///
    /// public void Dispose() => _subscription?.Dispose();
    /// </code>
    /// </example>
    IDisposable Subscribe(Action<TState> callback);

    /// <summary>
    /// Subscribes to specific state changes using a selector function.
    /// The callback is only invoked when the selected value changes.
    /// </summary>
    /// <typeparam name="TSelected">The type of the selected value from the state.</typeparam>
    /// <param name="selector">Function to select a portion of the state. Must be a pure function.</param>
    /// <param name="callback">Callback invoked when the selected value changes. Must not be null.</param>
    /// <returns>Disposable subscription. Call Dispose to unsubscribe.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="selector"/> or <paramref name="callback"/> is null.</exception>
    /// <remarks>
    /// Callback is triggered only when selected value changes, using <see cref="EqualityComparer{T}.Default"/>.
    /// Selector should be pure and fast. For expensive operations, use memoized selectors.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Subscribe only to Count changes
    /// var subscription = store.Subscribe(
    ///     state => state.Count,
    ///     count => Console.WriteLine($"Count changed to: {count}")
    /// );
    ///
    /// // Subscribe to nested property
    /// var userNameSubscription = store.Subscribe(
    ///     state => state.User?.Name,
    ///     name => Console.WriteLine($"User name: {name ?? "None"}")
    /// );
    ///
    /// // Subscribe to computed value
    /// var isValidSubscription = store.Subscribe(
    ///     state => state.Count &gt; 0 &amp;&amp; state.Count &lt; 100,
    ///     isValid => Console.WriteLine($"Valid: {isValid}")
    /// );
    /// </code>
    /// </example>
    IDisposable Subscribe<TSelected>(
        Func<TState, TSelected> selector,
        Action<TSelected> callback);

    /// <summary>
    /// Subscribes to specific state changes using a selector function and custom equality comparer.
    /// The callback is only invoked when the selected value changes according to the comparer.
    /// </summary>
    /// <typeparam name="TSelected">The type of the selected value from the state.</typeparam>
    /// <param name="selector">Function to select a portion of the state. Must be a pure function.</param>
    /// <param name="callback">Callback invoked when the selected value changes.</param>
    /// <param name="comparer">Custom equality comparer to determine value changes. Must not be null.</param>
    /// <returns>Disposable subscription. Call Dispose to unsubscribe.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    /// <remarks>
    /// Useful for complex types requiring specialized comparison (deep equality, case-insensitive, custom logic).
    /// Comparer is called on every state update, so keep it efficient.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Case-insensitive string comparison
    /// var subscription = store.Subscribe(
    ///     state => state.SearchQuery,
    ///     query => PerformSearch(query),
    ///     StringComparer.OrdinalIgnoreCase
    /// );
    ///
    /// // Custom comparer for collections
    /// var listComparer = new ListEqualityComparer&lt;string&gt;();
    /// var itemsSubscription = store.Subscribe(
    ///     state => state.Items,
    ///     items => UpdateUI(items),
    ///     listComparer
    /// );
    ///
    /// // Structural equality for records
    /// var addressSubscription = store.Subscribe(
    ///     state => state.Address,
    ///     address => UpdateMap(address),
    ///     new StructuralEqualityComparer&lt;Address&gt;()
    /// );
    /// </code>
    /// </example>
    IDisposable Subscribe<TSelected>(
        Func<TState, TSelected> selector,
        Action<TSelected> callback,
        IEqualityComparer<TSelected> comparer);

    /// <summary>
    /// Subscribes to state changes using a memoized selector.
    /// The callback is only invoked when the selector result changes.
    /// </summary>
    /// <typeparam name="TSelected">The type of the selected value from the state.</typeparam>
    /// <param name="selector">A memoized selector that computes the derived value.</param>
    /// <param name="callback">Callback invoked when the selected value changes.</param>
    /// <returns>Disposable subscription. Call Dispose to unsubscribe.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="selector"/> or <paramref name="callback"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// This overload uses a pre-defined <see cref="Selectors.ISelector{TState, TResult}"/> for efficient
    /// memoized state derivation. Useful for complex computed values that should only recompute
    /// when their dependencies change.
    /// </para>
    /// <para>
    /// The selector is responsible for memoization; this method simply uses its Select method.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Define selectors once (e.g., in a static class)
    /// public static class CartSelectors
    /// {
    ///     public static readonly ISelector&lt;CartState, decimal&gt; Total =
    ///         Selectors.Create&lt;CartState, decimal&gt;(s => s.Items.Sum(i => i.Price * i.Quantity));
    /// }
    ///
    /// // Subscribe using the selector
    /// var subscription = store.Subscribe(CartSelectors.Total, total => {
    ///     Console.WriteLine($"Cart total: {total:C}");
    /// });
    /// </code>
    /// </example>
    IDisposable Subscribe<TSelected>(
        Selectors.ISelector<TState, TSelected> selector,
        Action<TSelected> callback);
}
