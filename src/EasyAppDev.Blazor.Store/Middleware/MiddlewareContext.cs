namespace EasyAppDev.Blazor.Store.Middleware;

/// <summary>
/// Context passed to functional middleware, containing state information and services.
/// </summary>
/// <typeparam name="TState">The type of state.</typeparam>
/// <param name="CurrentState">The current state before the update.</param>
/// <param name="NewState">The new state after the update (null during OnBefore phase).</param>
/// <param name="Action">The action name associated with the update.</param>
/// <param name="Services">The service provider for resolving dependencies.</param>
/// <param name="Phase">The current middleware execution phase.</param>
public record MiddlewareContext<TState>(
    TState CurrentState,
    TState? NewState,
    string? Action,
    IServiceProvider? Services,
    MiddlewarePhase Phase) where TState : notnull
{
    /// <summary>
    /// Gets a value indicating whether this is the "before update" phase.
    /// </summary>
    public bool IsBefore => Phase == MiddlewarePhase.Before;

    /// <summary>
    /// Gets a value indicating whether this is the "after update" phase.
    /// </summary>
    public bool IsAfter => Phase == MiddlewarePhase.After;
}

/// <summary>
/// Represents the phase of middleware execution.
/// </summary>
public enum MiddlewarePhase
{
    /// <summary>
    /// Before the state update is applied.
    /// </summary>
    Before,

    /// <summary>
    /// After the state update has been applied.
    /// </summary>
    After
}
