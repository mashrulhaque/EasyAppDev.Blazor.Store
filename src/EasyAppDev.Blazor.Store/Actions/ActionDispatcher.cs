using EasyAppDev.Blazor.Store.Core;

namespace EasyAppDev.Blazor.Store.Actions;

/// <summary>
/// Dispatches actions to the store through registered reducers.
/// </summary>
/// <typeparam name="TState">The type of state managed by the store.</typeparam>
public interface IActionDispatcher<TState> where TState : notnull
{
    /// <summary>
    /// Dispatches an action to the store.
    /// </summary>
    /// <typeparam name="TAction">The type of action to dispatch.</typeparam>
    /// <param name="action">The action to dispatch.</param>
    /// <returns>A task that completes when the action is processed.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no reducer is registered for the action type.
    /// </exception>
    Task DispatchAsync<TAction>(TAction action) where TAction : IAction;

    /// <summary>
    /// Dispatches an action and returns immediately without waiting.
    /// Use sparingly - prefer DispatchAsync for proper error handling.
    /// </summary>
    /// <typeparam name="TAction">The type of action to dispatch.</typeparam>
    /// <param name="action">The action to dispatch.</param>
    void Dispatch<TAction>(TAction action) where TAction : IAction;

    /// <summary>
    /// Checks if a reducer is registered for the given action type.
    /// </summary>
    /// <typeparam name="TAction">The type of action to check.</typeparam>
    /// <returns>True if a reducer is registered, false otherwise.</returns>
    bool CanHandle<TAction>() where TAction : IAction;
}

/// <summary>
/// Default implementation of <see cref="IActionDispatcher{TState}"/>.
/// </summary>
/// <typeparam name="TState">The type of state managed by the store.</typeparam>
public sealed class ActionDispatcher<TState> : IActionDispatcher<TState> where TState : notnull
{
    private readonly IStore<TState> _store;
    private readonly Dictionary<Type, Func<TState, IAction, TState>> _reducers = new();
    private readonly List<IReducer<TState>> _patternReducers = new();
    private readonly object _lock = new();

    /// <summary>
    /// Creates a new action dispatcher for the specified store.
    /// </summary>
    /// <param name="store">The store to dispatch actions to.</param>
    public ActionDispatcher(IStore<TState> store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <summary>
    /// Registers a reducer function for a specific action type.
    /// </summary>
    /// <typeparam name="TAction">The type of action to handle.</typeparam>
    /// <param name="reducer">The reducer function.</param>
    /// <returns>This dispatcher for chaining.</returns>
    public ActionDispatcher<TState> Register<TAction>(Func<TState, TAction, TState> reducer)
        where TAction : IAction
    {
        ArgumentNullException.ThrowIfNull(reducer);

        lock (_lock)
        {
            _reducers[typeof(TAction)] = (state, action) => reducer(state, (TAction)action);
        }

        return this;
    }

    /// <summary>
    /// Registers a reducer instance for a specific action type.
    /// </summary>
    /// <typeparam name="TAction">The type of action to handle.</typeparam>
    /// <param name="reducer">The reducer instance.</param>
    /// <returns>This dispatcher for chaining.</returns>
    public ActionDispatcher<TState> Register<TAction>(IReducer<TState, TAction> reducer)
        where TAction : IAction
    {
        ArgumentNullException.ThrowIfNull(reducer);

        lock (_lock)
        {
            _reducers[typeof(TAction)] = (state, action) => reducer.Reduce(state, (TAction)action);
        }

        return this;
    }

    /// <summary>
    /// Registers a pattern matching reducer that handles multiple action types.
    /// </summary>
    /// <param name="reducer">The pattern reducer instance.</param>
    /// <returns>This dispatcher for chaining.</returns>
    public ActionDispatcher<TState> RegisterPattern(IReducer<TState> reducer)
    {
        ArgumentNullException.ThrowIfNull(reducer);

        lock (_lock)
        {
            _patternReducers.Add(reducer);
        }

        return this;
    }

    /// <summary>
    /// Registers a pattern matching reducer function.
    /// </summary>
    /// <param name="reducer">The reducer function that handles any action type.</param>
    /// <returns>This dispatcher for chaining.</returns>
    public ActionDispatcher<TState> RegisterPattern(Func<TState, IAction, TState> reducer)
    {
        ArgumentNullException.ThrowIfNull(reducer);

        lock (_lock)
        {
            _patternReducers.Add(new FunctionalReducer<TState>(reducer));
        }

        return this;
    }

    /// <inheritdoc />
    public async Task DispatchAsync<TAction>(TAction action) where TAction : IAction
    {
        ArgumentNullException.ThrowIfNull(action);

        var actionType = action.GetType();
        var actionName = actionType.Name;

        await _store.UpdateAsync(state =>
        {
            // Try specific reducer first
            if (_reducers.TryGetValue(actionType, out var reducer))
            {
                return reducer(state, action);
            }

            // Fall back to pattern reducers
            var currentState = state;
            foreach (var patternReducer in _patternReducers)
            {
                currentState = patternReducer.Reduce(currentState, action);
            }

            // If no reducers handled this and the state didn't change,
            // that's fine - actions without effects are valid
            return currentState;
        }, actionName).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispatch<TAction>(TAction action) where TAction : IAction
    {
        _ = DispatchAsync(action);
    }

    /// <inheritdoc />
    public bool CanHandle<TAction>() where TAction : IAction
    {
        lock (_lock)
        {
            return _reducers.ContainsKey(typeof(TAction)) || _patternReducers.Count > 0;
        }
    }

    private sealed class FunctionalReducer<TStateInner> : IReducer<TStateInner> where TStateInner : notnull
    {
        private readonly Func<TStateInner, IAction, TStateInner> _reducer;

        public FunctionalReducer(Func<TStateInner, IAction, TStateInner> reducer)
        {
            _reducer = reducer;
        }

        public TStateInner Reduce(TStateInner state, IAction action) => _reducer(state, action);
    }
}
