using System.Collections.Concurrent;
using EasyAppDev.Blazor.Store.Core;
using Microsoft.Extensions.Logging;

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
    private readonly ILogger<ActionDispatcher<TState>>? _logger;

    // Thread-safe reads: typed reducers live in a ConcurrentDictionary; pattern
    // reducers use copy-on-write (the list reference is replaced under _lock and
    // readers take a snapshot of the reference).
    private readonly ConcurrentDictionary<Type, Func<TState, IAction, TState>> _reducers = new();
    private volatile IReadOnlyList<IReducer<TState>> _patternReducers = Array.Empty<IReducer<TState>>();
    private readonly object _lock = new();

    /// <summary>
    /// Creates a new action dispatcher for the specified store.
    /// </summary>
    /// <param name="store">The store to dispatch actions to.</param>
    /// <param name="logger">Optional logger used to surface failures from fire-and-forget dispatches.</param>
    public ActionDispatcher(IStore<TState> store, ILogger<ActionDispatcher<TState>>? logger = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _logger = logger;
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

        _reducers[typeof(TAction)] = (state, action) => reducer(state, (TAction)action);

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

        _reducers[typeof(TAction)] = (state, action) => reducer.Reduce(state, (TAction)action);

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
            // Copy-on-write: replace the list reference so concurrent readers can
            // safely snapshot it without locking.
            var updated = new List<IReducer<TState>>(_patternReducers) { reducer };
            _patternReducers = updated;
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

        return RegisterPattern(new FunctionalReducer<TState>(reducer));
    }

    /// <inheritdoc />
    public async Task DispatchAsync<TAction>(TAction action) where TAction : IAction
    {
        ArgumentNullException.ThrowIfNull(action);

        var actionType = action.GetType();
        var actionName = actionType.Name;

        // Resolve the typed reducer, walking up the inheritance chain so derived
        // action types are handled by reducers registered for their base type.
        var reducer = ResolveReducer(actionType);

        // Snapshot the pattern reducer list (copy-on-write reference)
        var patternReducers = _patternReducers;

        if (reducer == null && patternReducers.Count == 0)
        {
            throw new InvalidOperationException(
                $"No reducer is registered for action type '{actionType.Name}'. " +
                $"Register a reducer via Register<{actionType.Name}>(...) or RegisterPattern(...) before dispatching.");
        }

        await _store.UpdateAsync(state =>
        {
            // Try specific reducer first
            if (reducer != null)
            {
                return reducer(state, action);
            }

            // Fall back to pattern reducers
            var currentState = state;
            foreach (var patternReducer in patternReducers)
            {
                currentState = patternReducer.Reduce(currentState, action);
            }

            return currentState;
        }, actionName).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispatch<TAction>(TAction action) where TAction : IAction
    {
        _ = DispatchAsync(action).ContinueWith(
            t => _logger?.LogError(
                t.Exception?.GetBaseException(),
                "Fire-and-forget dispatch of action {ActionType} failed",
                typeof(TAction).Name),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    /// <inheritdoc />
    public bool CanHandle<TAction>() where TAction : IAction
    {
        return ResolveReducer(typeof(TAction)) != null || _patternReducers.Count > 0;
    }

    /// <summary>
    /// Resolves a typed reducer for the action type, walking up the inheritance
    /// chain (base types) so reducers registered for a base action type handle
    /// derived action instances.
    /// </summary>
    private Func<TState, IAction, TState>? ResolveReducer(Type actionType)
    {
        for (var type = actionType; type != null && type != typeof(object); type = type.BaseType)
        {
            if (_reducers.TryGetValue(type, out var reducer))
            {
                return reducer;
            }
        }

        return null;
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
