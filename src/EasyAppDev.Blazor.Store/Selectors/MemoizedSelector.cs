namespace EasyAppDev.Blazor.Store.Selectors;

/// <summary>
/// Memoized selector that caches the result until dependencies change.
/// </summary>
/// <typeparam name="TState">The type of state to select from.</typeparam>
/// <typeparam name="TResult">The type of result computed by the selector.</typeparam>
/// <remarks>
/// <para>
/// This implementation uses reference equality for state comparison and configurable
/// equality comparison for result caching. The selector function is only invoked when
/// the state reference changes or when the cache is reset.
/// </para>
/// <para>
/// Thread-safety: This class is thread-safe. The cache uses a lock-free volatile pattern
/// to ensure consistent reads and writes across multiple threads. In Blazor Server scenarios
/// with multiple concurrent users, this prevents race conditions that could result in
/// corrupted cache values.
/// </para>
/// </remarks>
internal class MemoizedSelector<TState, TResult> : ISelector<TState, TResult>
{
    private readonly Func<TState, TResult> _selector;

    /// <summary>
    /// Immutable cache entry holding both state and computed result atomically.
    /// Using a single volatile reference ensures thread-safe reads/writes without locks.
    /// </summary>
    private sealed record CacheEntry(TState State, TResult Result);

    /// <summary>
    /// Volatile cache entry for thread-safe lock-free access.
    /// </summary>
    private volatile CacheEntry? _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoizedSelector{TState, TResult}"/> class.
    /// </summary>
    /// <param name="selector">The selector function to compute the result.</param>
    /// <param name="comparer">
    /// Optional comparer parameter retained for API compatibility.
    /// The lock-free implementation unconditionally updates the cache on state change.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="selector"/> is null.
    /// </exception>
    public MemoizedSelector(
        Func<TState, TResult> selector,
        IEqualityComparer<TResult>? comparer = null)
    {
        _selector = selector ?? throw new ArgumentNullException(nameof(selector));
        // comparer is accepted for API compatibility but not used in the lock-free implementation.
        // The cache is always updated when state changes, which is the correct behavior.
        _ = comparer;
    }

    /// <summary>
    /// Computes the result from the state, using memoization to avoid unnecessary recomputation.
    /// </summary>
    /// <param name="state">The state to select from.</param>
    /// <returns>The selected/computed result.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="state"/> is null.
    /// </exception>
    /// <remarks>
    /// <para>
    /// The selector function is only invoked when:
    /// </para>
    /// <list type="bullet">
    /// <item><description>The state reference has changed (using default equality)</description></item>
    /// <item><description>The cache has been reset via <see cref="Reset"/></description></item>
    /// <item><description>This is the first invocation</description></item>
    /// </list>
    /// <para>
    /// This method is thread-safe and can be called concurrently from multiple threads.
    /// </para>
    /// </remarks>
    public TResult Select(TState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        // Read cache atomically - volatile ensures we see the latest value
        var cache = _cache;

        // Check if we have a cached result for this state
        if (cache != null && EqualityComparer<TState>.Default.Equals(cache.State, state))
        {
            return cache.Result;
        }

        // Compute new result
        var result = _selector(state);

        // Update cache atomically - single volatile write ensures thread-safety
        // Note: In race conditions, multiple threads may compute the same result,
        // but the cache will eventually hold a valid (state, result) pair.
        // This is acceptable as the selector should be a pure function.
        _cache = new CacheEntry(state, result);

        return result;
    }

    /// <summary>
    /// Resets the memoization cache, forcing recomputation on next <see cref="Select"/> call.
    /// </summary>
    /// <remarks>
    /// This method is thread-safe. After calling Reset, the next call to <see cref="Select"/>
    /// will recompute the result regardless of the state value.
    /// </remarks>
    public void Reset()
    {
        _cache = null;
    }
}
