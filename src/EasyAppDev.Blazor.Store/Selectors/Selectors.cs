namespace EasyAppDev.Blazor.Store.Selectors;

/// <summary>
/// Factory for creating memoized selectors.
/// </summary>
/// <remarks>
/// Selectors are used to derive and compute state from the store.
/// They provide memoization to avoid expensive recomputation when the state hasn't changed.
/// Selectors can also be composed to build complex derived state from simpler selectors.
/// </remarks>
public static class Selectors
{
    /// <summary>
    /// Creates a memoized selector.
    /// </summary>
    /// <typeparam name="TState">The type of state to select from.</typeparam>
    /// <typeparam name="TResult">The type of result computed by the selector.</typeparam>
    /// <param name="selector">The selector function to compute the result.</param>
    /// <param name="comparer">Optional comparer to determine if the result changed.</param>
    /// <returns>A memoized selector instance.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="selector"/> is null.
    /// </exception>
    /// <example>
    /// <code>
    /// var completedTodosSelector = Selectors.Create&lt;TodoState, List&lt;Todo&gt;&gt;(
    ///     state => state.Todos.Where(t => t.IsCompleted).ToList()
    /// );
    ///
    /// var completedTodos = completedTodosSelector.Select(store.GetState());
    /// </code>
    /// </example>
    public static ISelector<TState, TResult> Create<TState, TResult>(
        Func<TState, TResult> selector,
        IEqualityComparer<TResult>? comparer = null)
    {
        return new MemoizedSelector<TState, TResult>(selector, comparer);
    }

    /// <summary>
    /// Creates a composed selector that depends on two input selectors.
    /// </summary>
    /// <typeparam name="TState">The type of state to select from.</typeparam>
    /// <typeparam name="TInput1">The type of the first input selector result.</typeparam>
    /// <typeparam name="TInput2">The type of the second input selector result.</typeparam>
    /// <typeparam name="TResult">The type of the final result.</typeparam>
    /// <param name="selector1">The first input selector.</param>
    /// <param name="selector2">The second input selector.</param>
    /// <param name="combiner">Function to combine the two input results.</param>
    /// <returns>A memoized selector that composes the input selectors.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when any parameter is null.
    /// </exception>
    /// <example>
    /// <code>
    /// var completedSelector = Selectors.Create&lt;TodoState, List&lt;Todo&gt;&gt;(
    ///     state => state.Todos.Where(t => t.IsCompleted).ToList()
    /// );
    ///
    /// var activeSelector = Selectors.Create&lt;TodoState, List&lt;Todo&gt;&gt;(
    ///     state => state.Todos.Where(t => !t.IsCompleted).ToList()
    /// );
    ///
    /// var statsSelector = Selectors.Create(
    ///     completedSelector,
    ///     activeSelector,
    ///     (completed, active) => new { CompletedCount = completed.Count, ActiveCount = active.Count }
    /// );
    /// </code>
    /// </example>
    public static ISelector<TState, TResult> Create<TState, TInput1, TInput2, TResult>(
        ISelector<TState, TInput1> selector1,
        ISelector<TState, TInput2> selector2,
        Func<TInput1, TInput2, TResult> combiner)
    {
        ArgumentNullException.ThrowIfNull(selector1);
        ArgumentNullException.ThrowIfNull(selector2);
        ArgumentNullException.ThrowIfNull(combiner);

        return new MemoizedSelector<TState, TResult>(state =>
        {
            var input1 = selector1.Select(state);
            var input2 = selector2.Select(state);
            return combiner(input1, input2);
        });
    }

    /// <summary>
    /// Creates a composed selector that depends on three input selectors.
    /// </summary>
    /// <typeparam name="TState">The type of state to select from.</typeparam>
    /// <typeparam name="TInput1">The type of the first input selector result.</typeparam>
    /// <typeparam name="TInput2">The type of the second input selector result.</typeparam>
    /// <typeparam name="TInput3">The type of the third input selector result.</typeparam>
    /// <typeparam name="TResult">The type of the final result.</typeparam>
    /// <param name="selector1">The first input selector.</param>
    /// <param name="selector2">The second input selector.</param>
    /// <param name="selector3">The third input selector.</param>
    /// <param name="combiner">Function to combine the three input results.</param>
    /// <returns>A memoized selector that composes the input selectors.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when any parameter is null.
    /// </exception>
    /// <example>
    /// <code>
    /// var completedSelector = Selectors.Create&lt;TodoState, int&gt;(
    ///     state => state.Todos.Count(t => t.IsCompleted)
    /// );
    ///
    /// var activeSelector = Selectors.Create&lt;TodoState, int&gt;(
    ///     state => state.Todos.Count(t => !t.IsCompleted)
    /// );
    ///
    /// var totalSelector = Selectors.Create&lt;TodoState, int&gt;(
    ///     state => state.Todos.Count
    /// );
    ///
    /// var progressSelector = Selectors.Create(
    ///     completedSelector,
    ///     activeSelector,
    ///     totalSelector,
    ///     (completed, active, total) => new
    ///     {
    ///         Completed = completed,
    ///         Active = active,
    ///         Total = total,
    ///         Progress = total > 0 ? (double)completed / total : 0
    ///     }
    /// );
    /// </code>
    /// </example>
    public static ISelector<TState, TResult> Create<TState, TInput1, TInput2, TInput3, TResult>(
        ISelector<TState, TInput1> selector1,
        ISelector<TState, TInput2> selector2,
        ISelector<TState, TInput3> selector3,
        Func<TInput1, TInput2, TInput3, TResult> combiner)
    {
        ArgumentNullException.ThrowIfNull(selector1);
        ArgumentNullException.ThrowIfNull(selector2);
        ArgumentNullException.ThrowIfNull(selector3);
        ArgumentNullException.ThrowIfNull(combiner);

        return new MemoizedSelector<TState, TResult>(state =>
        {
            var input1 = selector1.Select(state);
            var input2 = selector2.Select(state);
            var input3 = selector3.Select(state);
            return combiner(input1, input2, input3);
        });
    }
}
