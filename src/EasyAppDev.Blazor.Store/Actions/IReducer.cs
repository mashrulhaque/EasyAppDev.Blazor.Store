namespace EasyAppDev.Blazor.Store.Actions;

/// <summary>
/// A reducer handles an action and returns a new state.
/// </summary>
/// <typeparam name="TState">The type of state managed by the store.</typeparam>
/// <typeparam name="TAction">The type of action this reducer handles.</typeparam>
/// <remarks>
/// Reducers are pure functions - they should not have side effects:
/// <code>
/// public class IncrementReducer : IReducer&lt;CounterState, Increment&gt;
/// {
///     public CounterState Reduce(CounterState state, Increment action)
///         => state with { Count = state.Count + 1 };
/// }
/// </code>
/// </remarks>
public interface IReducer<TState, in TAction>
    where TState : notnull
    where TAction : IAction
{
    /// <summary>
    /// Applies the action to the state and returns a new state.
    /// </summary>
    /// <param name="state">The current state.</param>
    /// <param name="action">The action to apply.</param>
    /// <returns>The new state after applying the action.</returns>
    TState Reduce(TState state, TAction action);
}

/// <summary>
/// A non-generic reducer that handles any action type using pattern matching.
/// </summary>
/// <typeparam name="TState">The type of state managed by the store.</typeparam>
/// <remarks>
/// Use for handling multiple action types with pattern matching:
/// <code>
/// public class CounterReducer : IReducer&lt;CounterState&gt;
/// {
///     public CounterState Reduce(CounterState state, IAction action) => action switch
///     {
///         Increment => state with { Count = state.Count + 1 },
///         Decrement => state with { Count = state.Count - 1 },
///         IncrementBy a => state with { Count = state.Count + a.Amount },
///         _ => state
///     };
/// }
/// </code>
/// </remarks>
public interface IReducer<TState> where TState : notnull
{
    /// <summary>
    /// Applies the action to the state and returns a new state.
    /// Return the unchanged state for unhandled actions.
    /// </summary>
    /// <param name="state">The current state.</param>
    /// <param name="action">The action to apply.</param>
    /// <returns>The new state after applying the action.</returns>
    TState Reduce(TState state, IAction action);
}
