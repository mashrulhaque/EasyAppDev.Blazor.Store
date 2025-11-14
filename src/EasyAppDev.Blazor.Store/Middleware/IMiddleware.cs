namespace EasyAppDev.Blazor.Store.Middleware;

/// <summary>
/// Interface for store middleware that can intercept state updates.
/// </summary>
/// <typeparam name="TState">The type of state.</typeparam>
public interface IMiddleware<TState> where TState : notnull
{
    /// <summary>
    /// Called before a state update is applied.
    /// </summary>
    /// <param name="currentState">The current state before the update.</param>
    /// <param name="action">Optional action name for the update.</param>
    Task OnBeforeUpdateAsync(TState currentState, string? action);

    /// <summary>
    /// Called after a state update has been applied.
    /// </summary>
    /// <param name="previousState">The state before the update.</param>
    /// <param name="currentState">The state after the update.</param>
    /// <param name="action">Optional action name for the update.</param>
    Task OnAfterUpdateAsync(TState previousState, TState currentState, string? action);
}
