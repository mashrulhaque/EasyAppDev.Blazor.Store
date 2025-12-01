using EasyAppDev.Blazor.Store.Core;

namespace EasyAppDev.Blazor.Store.Actions;

/// <summary>
/// Extension methods for dispatching actions to stores.
/// </summary>
public static class ActionExtensions
{
    /// <summary>
    /// Dispatches an action to the store using inline reducer logic.
    /// Useful for simple one-off actions without pre-registered reducers.
    /// </summary>
    /// <typeparam name="TState">The type of state managed by the store.</typeparam>
    /// <typeparam name="TAction">The type of action to dispatch.</typeparam>
    /// <param name="store">The store instance.</param>
    /// <param name="action">The action to dispatch.</param>
    /// <param name="reducer">The reducer function to apply.</param>
    /// <returns>A task that completes when the action is processed.</returns>
    /// <example>
    /// <code>
    /// await store.DispatchAsync(new IncrementBy(5), (s, a) => s with { Count = s.Count + a.Amount });
    /// </code>
    /// </example>
    public static Task DispatchAsync<TState, TAction>(
        this IStore<TState> store,
        TAction action,
        Func<TState, TAction, TState> reducer)
        where TState : notnull
        where TAction : IAction
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(reducer);

        var actionName = action.GetType().Name;
        return store.UpdateAsync(s => reducer(s, action), actionName);
    }

    /// <summary>
    /// Dispatches an action with async reducer logic.
    /// </summary>
    /// <typeparam name="TState">The type of state managed by the store.</typeparam>
    /// <typeparam name="TAction">The type of action to dispatch.</typeparam>
    /// <param name="store">The store instance.</param>
    /// <param name="action">The action to dispatch.</param>
    /// <param name="reducer">The async reducer function to apply.</param>
    /// <returns>A task that completes when the action is processed.</returns>
    public static Task DispatchAsync<TState, TAction>(
        this IStore<TState> store,
        TAction action,
        Func<TState, TAction, Task<TState>> reducer)
        where TState : notnull
        where TAction : IAction
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(reducer);

        var actionName = action.GetType().Name;
        return store.UpdateAsync(async s => await reducer(s, action).ConfigureAwait(false), actionName);
    }

    /// <summary>
    /// Creates an action dispatcher for the store.
    /// </summary>
    /// <typeparam name="TState">The type of state managed by the store.</typeparam>
    /// <param name="store">The store instance.</param>
    /// <returns>A new action dispatcher.</returns>
    /// <example>
    /// <code>
    /// var dispatcher = store.CreateDispatcher()
    ///     .Register&lt;Increment&gt;((s, a) => s with { Count = s.Count + 1 })
    ///     .Register&lt;Decrement&gt;((s, a) => s with { Count = s.Count - 1 })
    ///     .Register&lt;IncrementBy&gt;((s, a) => s with { Count = s.Count + a.Amount });
    ///
    /// await dispatcher.DispatchAsync(new Increment());
    /// await dispatcher.DispatchAsync(new IncrementBy(5));
    /// </code>
    /// </example>
    public static ActionDispatcher<TState> CreateDispatcher<TState>(this IStore<TState> store)
        where TState : notnull
    {
        return new ActionDispatcher<TState>(store);
    }

    /// <summary>
    /// Creates an action dispatcher with reducer registration.
    /// </summary>
    /// <typeparam name="TState">The type of state managed by the store.</typeparam>
    /// <param name="store">The store instance.</param>
    /// <param name="configure">Action to configure reducers.</param>
    /// <returns>A configured action dispatcher.</returns>
    /// <example>
    /// <code>
    /// var dispatcher = store.CreateDispatcher(d => d
    ///     .Register&lt;Increment&gt;((s, a) => s with { Count = s.Count + 1 })
    ///     .Register&lt;Decrement&gt;((s, a) => s with { Count = s.Count - 1 })
    ///     .RegisterPattern((s, action) => action switch
    ///     {
    ///         SetCount sc => s with { Count = sc.Value },
    ///         Reset => new CounterState(0),
    ///         _ => s
    ///     })
    /// );
    /// </code>
    /// </example>
    public static ActionDispatcher<TState> CreateDispatcher<TState>(
        this IStore<TState> store,
        Action<ActionDispatcher<TState>> configure)
        where TState : notnull
    {
        var dispatcher = new ActionDispatcher<TState>(store);
        configure(dispatcher);
        return dispatcher;
    }
}
