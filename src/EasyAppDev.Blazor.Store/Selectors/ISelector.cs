namespace EasyAppDev.Blazor.Store.Selectors;

/// <summary>
/// Represents a memoized selector for derived state.
/// </summary>
/// <typeparam name="TState">The type of state to select from.</typeparam>
/// <typeparam name="TResult">The type of result computed by the selector.</typeparam>
/// <remarks>
/// Selectors are used to compute derived state from the store state.
/// Memoization ensures the selector only recomputes when the input state changes,
/// providing performance optimization for expensive computations.
/// </remarks>
public interface ISelector<TState, TResult>
{
    /// <summary>
    /// Computes the result from the state.
    /// </summary>
    /// <param name="state">The state to select from.</param>
    /// <returns>The selected/computed result.</returns>
    /// <remarks>
    /// The implementation should use memoization to cache the result
    /// and only recompute when the state actually changes.
    /// </remarks>
    TResult Select(TState state);

    /// <summary>
    /// Resets the memoization cache.
    /// </summary>
    /// <remarks>
    /// Forces the selector to recompute on the next call to <see cref="Select"/>,
    /// regardless of whether the state has changed.
    /// </remarks>
    void Reset();
}
