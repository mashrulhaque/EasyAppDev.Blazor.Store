namespace EasyAppDev.Blazor.Store.Core;

/// <summary>
/// Exception thrown when a rollback cannot be safely performed due to concurrent state modifications.
/// </summary>
/// <remarks>
/// This exception indicates that the state was modified by another operation during an optimistic
/// update's server action. Rolling back would lose those concurrent changes. Callers should either
/// provide a custom rollback function that handles merging, or handle this exception appropriately.
/// </remarks>
public sealed class ConcurrentModificationException : InvalidOperationException
{
    /// <summary>
    /// Creates a new concurrent modification exception with a default message.
    /// </summary>
    public ConcurrentModificationException()
        : base("Cannot rollback optimistic update: state was modified by concurrent operations. " +
               "Provide a custom rollback function to handle this scenario.")
    {
    }

    /// <summary>
    /// Creates a new concurrent modification exception with a custom message.
    /// </summary>
    /// <param name="message">The exception message.</param>
    public ConcurrentModificationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Creates a new concurrent modification exception with a custom message and inner exception.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The inner exception.</param>
    public ConcurrentModificationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Extension methods for optimistic updates with automatic rollback on failure.
/// </summary>
/// <remarks>
/// Optimistic updates immediately apply state changes for responsive UIs,
/// then automatically rollback if the server action fails.
/// </remarks>
public static class OptimisticUpdateExtensions
{
    /// <summary>
    /// Performs an optimistic update with automatic rollback on failure.
    /// </summary>
    /// <typeparam name="TState">The type of state managed by the store.</typeparam>
    /// <typeparam name="TResult">The type of result returned by the server action.</typeparam>
    /// <param name="store">The store instance.</param>
    /// <param name="optimistic">
    /// Function to apply the optimistic state change immediately.
    /// This runs before the server action.
    /// </param>
    /// <param name="action">
    /// Async function that performs the server operation.
    /// If this throws, the state is rolled back.
    /// </param>
    /// <param name="rollback">
    /// Optional function to rollback state on failure.
    /// If not provided, state is restored to the pre-optimistic value.
    /// </param>
    /// <param name="onSuccess">
    /// Optional function to transform state with the server result.
    /// Useful for adding server-generated IDs or timestamps.
    /// </param>
    /// <param name="onError">
    /// Optional function to handle errors by updating state.
    /// If provided, exceptions are not rethrown.
    /// </param>
    /// <param name="actionName">Optional action name for debugging/DevTools.</param>
    /// <returns>A task that completes when the operation finishes.</returns>
    /// <example>
    /// <code>
    /// await store.UpdateOptimistic(
    ///     optimistic: s => s.AddItem(product),
    ///     action: async () => await api.AddToCartAsync(product.Id),
    ///     onSuccess: (s, result) => s.UpdateItemId(product.Id, result.ServerId),
    ///     onError: (s, ex) => s with { Error = ex.Message }
    /// );
    /// </code>
    /// </example>
    public static async Task UpdateOptimistic<TState, TResult>(
        this IStore<TState> store,
        Func<TState, TState> optimistic,
        Func<Task<TResult>> action,
        Func<TState, TState>? rollback = null,
        Func<TState, TResult, TState>? onSuccess = null,
        Func<TState, Exception, TState>? onError = null,
        string? actionName = null)
        where TState : notnull
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(optimistic);
        ArgumentNullException.ThrowIfNull(action);

        var baseActionName = actionName ?? "OPTIMISTIC";
        TState? previousState = default;
        TState? optimisticState = default;

        // Apply optimistic update and capture both previous and optimistic state atomically
        await store.UpdateAsync(s =>
        {
            previousState = s;
            optimisticState = optimistic(s);
            return optimisticState;
        }, baseActionName).ConfigureAwait(false);

        Exception? serverException = null;
        try
        {
            var result = await action().ConfigureAwait(false);

            // onSuccess is wrapped in separate try-catch to prevent rollback on handler failure
            // since the server action already succeeded
            if (onSuccess != null)
            {
                try
                {
                    await store.UpdateAsync(
                        s => onSuccess(s, result),
                        $"{baseActionName}_SUCCESS"
                    ).ConfigureAwait(false);
                }
                catch (Exception successEx)
                {
                    // Server succeeded but onSuccess handler failed. Don't rollback since
                    // server state is already committed. Call onError if provided, otherwise rethrow.
                    if (onError != null)
                    {
                        try
                        {
                            await store.UpdateAsync(
                                s => onError(s, successEx),
                                $"{baseActionName}_SUCCESS_HANDLER_ERROR"
                            ).ConfigureAwait(false);
                        }
                        catch
                        {
                            // onError handler threw - rethrow original success handler exception
                            throw successEx;
                        }
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            return; // Success - no rollback needed
        }
        catch (Exception ex)
        {
            serverException = ex;
        }

        // Rollback only on server action failure
        var rollbackException = await TryRollbackAsync(
            store, previousState!, optimisticState!, rollback, baseActionName
        ).ConfigureAwait(false);

        // Handle error with onError or rethrow
        if (onError != null)
        {
            try
            {
                await store.UpdateAsync(
                    s => onError(s, serverException!),
                    $"{baseActionName}_ERROR"
                ).ConfigureAwait(false);
            }
            catch
            {
                // onError handler threw - rethrow original server exception
                throw serverException!;
            }
        }
        else
        {
            // Throw rollback exception if it occurred, otherwise the original server exception
            if (rollbackException != null)
            {
                throw new AggregateException(
                    "Server action failed and rollback also failed",
                    serverException!, rollbackException);
            }
            throw serverException!;
        }
    }

    /// <summary>
    /// Attempts rollback and returns any exception that occurred during rollback.
    /// </summary>
    /// <exception cref="ConcurrentModificationException">
    /// Thrown when state was modified by concurrent operations during the server action.
    /// </exception>
    private static async Task<Exception?> TryRollbackAsync<TState>(
        IStore<TState> store,
        TState previousState,
        TState optimisticState,
        Func<TState, TState>? rollback,
        string baseActionName)
        where TState : notnull
    {
        try
        {
            if (rollback != null)
            {
                await store.UpdateAsync(rollback, $"{baseActionName}_ROLLBACK").ConfigureAwait(false);
            }
            else
            {
                // Default rollback: detect concurrent updates and throw if detected
                await store.UpdateAsync(currentState =>
                {
                    if (EqualityComparer<TState>.Default.Equals(currentState, optimisticState))
                    {
                        // No concurrent updates - safe to restore previous state
                        return previousState;
                    }
                    // Concurrent update detected - throw to prevent silent data loss
                    throw new ConcurrentModificationException(
                        "Cannot rollback optimistic update: state was modified by concurrent operations " +
                        "during the server action. Provide a custom rollback function to handle merging.");
                }, $"{baseActionName}_ROLLBACK").ConfigureAwait(false);
            }
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    /// <summary>
    /// Performs an optimistic update with automatic rollback on failure (void action version).
    /// </summary>
    /// <typeparam name="TState">The type of state managed by the store.</typeparam>
    /// <param name="store">The store instance.</param>
    /// <param name="optimistic">
    /// Function to apply the optimistic state change immediately.
    /// </param>
    /// <param name="action">
    /// Async function that performs the server operation.
    /// If this throws, the state is rolled back.
    /// </param>
    /// <param name="rollback">
    /// Optional function to rollback state on failure.
    /// If not provided, state is restored to the pre-optimistic value.
    /// </param>
    /// <param name="onError">
    /// Optional function to handle errors by updating state.
    /// If provided, exceptions are not rethrown.
    /// </param>
    /// <param name="actionName">Optional action name for debugging/DevTools.</param>
    /// <returns>A task that completes when the operation finishes.</returns>
    /// <example>
    /// <code>
    /// // Simple form - auto rollback
    /// await store.UpdateOptimistic(
    ///     s => s.AddItem(product),
    ///     async () => await api.AddToCartAsync(product.Id)
    /// );
    ///
    /// // With custom rollback
    /// await store.UpdateOptimistic(
    ///     s => s.AddItem(product),
    ///     async () => await api.AddToCartAsync(product.Id),
    ///     rollback: s => s.RemoveItem(product.Id)
    /// );
    /// </code>
    /// </example>
    public static async Task UpdateOptimistic<TState>(
        this IStore<TState> store,
        Func<TState, TState> optimistic,
        Func<Task> action,
        Func<TState, TState>? rollback = null,
        Func<TState, Exception, TState>? onError = null,
        string? actionName = null)
        where TState : notnull
    {
        await store.UpdateOptimistic<TState, object?>(
            optimistic,
            async () =>
            {
                await action().ConfigureAwait(false);
                return null;
            },
            rollback,
            onSuccess: null,
            onError,
            actionName
        ).ConfigureAwait(false);
    }

    /// <summary>
    /// Performs an optimistic update with result transformation.
    /// Simpler overload when rollback matches the inverse of optimistic update.
    /// </summary>
    /// <typeparam name="TState">The type of state managed by the store.</typeparam>
    /// <typeparam name="TResult">The type of result returned by the server action.</typeparam>
    /// <param name="store">The store instance.</param>
    /// <param name="optimistic">Function to apply the optimistic state change.</param>
    /// <param name="action">Async function that performs the server operation.</param>
    /// <param name="onSuccess">Function to transform state with the server result.</param>
    /// <param name="actionName">Optional action name for debugging/DevTools.</param>
    /// <returns>A task that completes when the operation finishes.</returns>
    /// <exception cref="Exception">Rethrows any exception from the server action after rollback.</exception>
    public static Task UpdateOptimistic<TState, TResult>(
        this IStore<TState> store,
        Func<TState, TState> optimistic,
        Func<Task<TResult>> action,
        Func<TState, TResult, TState> onSuccess,
        string? actionName = null)
        where TState : notnull
    {
        return store.UpdateOptimistic(
            optimistic,
            action,
            rollback: null,
            onSuccess,
            onError: null,
            actionName
        );
    }

    /// <summary>
    /// Performs an optimistic update with a separate confirmation step.
    /// Useful for two-phase operations where the server may return data
    /// needed to confirm or modify the optimistic state.
    /// </summary>
    /// <typeparam name="TState">The type of state managed by the store.</typeparam>
    /// <typeparam name="TResult">The type of result returned by the server action.</typeparam>
    /// <param name="store">The store instance.</param>
    /// <param name="optimistic">Function to apply the optimistic state change.</param>
    /// <param name="action">Async function that performs the server operation.</param>
    /// <param name="confirm">
    /// Function that takes current state and result, returns confirmed state.
    /// Called only on success after the optimistic update.
    /// </param>
    /// <param name="actionName">Optional action name for debugging/DevTools.</param>
    /// <returns>A task containing the result of the server action.</returns>
    /// <exception cref="Exception">
    /// Rethrows any exception from the server action after rollback.
    /// </exception>
    /// <example>
    /// <code>
    /// var result = await store.UpdateOptimisticWithConfirm(
    ///     optimistic: s => s with {
    ///         Items = s.Items.Add(new Item { Id = Guid.NewGuid(), Name = "New" })
    ///     },
    ///     action: () => api.CreateItemAsync("New"),
    ///     confirm: (s, serverItem) => s with {
    ///         // Replace temp item with server-confirmed item
    ///         Items = s.Items.Replace(
    ///             x => x.Name == "New",
    ///             serverItem
    ///         )
    ///     }
    /// );
    /// </code>
    /// </example>
    public static async Task<TResult> UpdateOptimisticWithConfirm<TState, TResult>(
        this IStore<TState> store,
        Func<TState, TState> optimistic,
        Func<Task<TResult>> action,
        Func<TState, TResult, TState> confirm,
        string? actionName = null)
        where TState : notnull
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(optimistic);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(confirm);

        var baseActionName = actionName ?? "OPTIMISTIC";
        TState? previousState = default;
        TState? optimisticState = default;

        // Apply optimistic update and capture both previous and optimistic state
        await store.UpdateAsync(s =>
        {
            previousState = s;
            optimisticState = optimistic(s);
            return optimisticState;
        }, baseActionName).ConfigureAwait(false);

        TResult result;
        try
        {
            result = await action().ConfigureAwait(false);
        }
        catch
        {
            // Server action failed - rollback is appropriate
            await RollbackWithConfirmAsync(store, previousState!, optimisticState!, baseActionName)
                .ConfigureAwait(false);
            throw;
        }

        // Server succeeded - confirm handler failure should NOT trigger rollback
        try
        {
            await store.UpdateAsync(
                s => confirm(s, result),
                $"{baseActionName}_CONFIRM"
            ).ConfigureAwait(false);
        }
        catch
        {
            // Confirm handler failed but server already committed - do NOT rollback
            // Rethrow so caller knows something went wrong, but server state is committed
            throw;
        }

        return result;
    }

    /// <summary>
    /// Performs rollback for UpdateOptimisticWithConfirm with concurrent update detection.
    /// </summary>
    /// <exception cref="ConcurrentModificationException">
    /// Thrown when state was modified by concurrent operations during the server action.
    /// </exception>
    private static async Task RollbackWithConfirmAsync<TState>(
        IStore<TState> store,
        TState previousState,
        TState optimisticState,
        string baseActionName)
        where TState : notnull
    {
        await store.UpdateAsync(currentState =>
        {
            // If no concurrent updates occurred, safely restore previous state
            if (EqualityComparer<TState>.Default.Equals(currentState, optimisticState))
            {
                return previousState;
            }
            // Concurrent update detected - throw to prevent silent data loss
            throw new ConcurrentModificationException(
                "Cannot rollback optimistic update: state was modified by concurrent operations " +
                "during the server action. The server action failed but rollback cannot proceed safely.");
        }, $"{baseActionName}_ROLLBACK").ConfigureAwait(false);
    }
}
