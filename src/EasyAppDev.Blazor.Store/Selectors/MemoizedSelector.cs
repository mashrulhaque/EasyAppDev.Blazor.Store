namespace EasyAppDev.Blazor.Store.Selectors;

/// <summary>
/// Memoized selector that caches the result until dependencies change.
/// </summary>
/// <typeparam name="TState">The type of state to select from.</typeparam>
/// <typeparam name="TResult">The type of result computed by the selector.</typeparam>
/// <remarks>
/// This implementation uses reference equality for state comparison and configurable
/// equality comparison for result caching. The selector function is only invoked when
/// the state reference changes or when the cache is reset.
/// </remarks>
internal class MemoizedSelector<TState, TResult> : ISelector<TState, TResult>
{
    private readonly Func<TState, TResult> _selector;
    private readonly IEqualityComparer<TResult> _comparer;
    private TState? _lastState;
    private TResult? _cachedResult;
    private bool _hasCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoizedSelector{TState, TResult}"/> class.
    /// </summary>
    /// <param name="selector">The selector function to compute the result.</param>
    /// <param name="comparer">Optional comparer to determine if the result changed. Defaults to default equality comparer.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="selector"/> is null.
    /// </exception>
    public MemoizedSelector(
        Func<TState, TResult> selector,
        IEqualityComparer<TResult>? comparer = null)
    {
        _selector = selector ?? throw new ArgumentNullException(nameof(selector));
        _comparer = comparer ?? EqualityComparer<TResult>.Default;
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
    /// The selector function is only invoked when:
    /// <list type="bullet">
    /// <item><description>The state reference has changed (using reference equality)</description></item>
    /// <item><description>The cache has been reset via <see cref="Reset"/></description></item>
    /// <item><description>This is the first invocation</description></item>
    /// </list>
    /// </remarks>
    public TResult Select(TState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        // Check if we have a cached result for this state
        if (_hasCache && EqualityComparer<TState>.Default.Equals(_lastState, state))
        {
            return _cachedResult!;
        }

        // Compute new result
        var result = _selector(state);

        // Update cache only if result changed or this is first computation
        if (!_hasCache || !_comparer.Equals(_cachedResult, result))
        {
            _lastState = state;
            _cachedResult = result;
            _hasCache = true;
        }

        return result;
    }

    /// <summary>
    /// Resets the memoization cache, forcing recomputation on next <see cref="Select"/> call.
    /// </summary>
    public void Reset()
    {
        _hasCache = false;
        _lastState = default;
        _cachedResult = default;
    }
}
