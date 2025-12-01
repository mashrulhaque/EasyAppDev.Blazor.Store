namespace EasyAppDev.Blazor.Store.Core;

/// <summary>
/// Represents an error that occurred during store operations.
/// </summary>
/// <typeparam name="TState">The type of state.</typeparam>
/// <param name="Exception">The exception that was thrown.</param>
/// <param name="State">The state at the time of the error (may be null if not available).</param>
/// <param name="Action">The action name associated with the operation (if any).</param>
/// <param name="Location">Where in the store lifecycle the error occurred.</param>
public record StoreError<TState>(
    Exception Exception,
    TState? State,
    string? Action,
    ErrorLocation Location) where TState : notnull
{
    /// <summary>
    /// Gets a concise error message including location and action.
    /// </summary>
    public string Message => Action != null
        ? $"[{Location}] Error during '{Action}': {Exception.Message}"
        : $"[{Location}] Error: {Exception.Message}";
}

/// <summary>
/// Indicates where in the store lifecycle an error occurred.
/// </summary>
public enum ErrorLocation
{
    /// <summary>
    /// Error occurred in middleware (OnBeforeUpdate or OnAfterUpdate).
    /// </summary>
    Middleware,

    /// <summary>
    /// Error occurred in the state updater function.
    /// </summary>
    Updater,

    /// <summary>
    /// Error occurred in a subscriber callback.
    /// </summary>
    Subscriber,

    /// <summary>
    /// Error occurred during state persistence (save or load).
    /// </summary>
    Persistence,

    /// <summary>
    /// Error occurred during DevTools integration.
    /// </summary>
    DevTools,

    /// <summary>
    /// Error occurred during state hydration.
    /// </summary>
    Hydration
}

/// <summary>
/// Delegate for handling store errors.
/// </summary>
/// <typeparam name="TState">The type of state.</typeparam>
/// <param name="error">The error that occurred.</param>
public delegate void StoreErrorHandler<TState>(StoreError<TState> error) where TState : notnull;
