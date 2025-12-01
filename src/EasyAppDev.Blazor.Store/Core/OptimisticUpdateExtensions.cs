namespace EasyAppDev.Blazor.Store.Core;

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
        var previousState = store.GetState();

        // Apply optimistic update immediately
        await store.UpdateAsync(optimistic, baseActionName).ConfigureAwait(false);

        try
        {
            var result = await action().ConfigureAwait(false);

            if (onSuccess != null)
            {
                await store.UpdateAsync(
                    s => onSuccess(s, result),
                    $"{baseActionName}_SUCCESS"
                ).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            // Rollback on failure
            if (rollback != null)
            {
                await store.UpdateAsync(rollback, $"{baseActionName}_ROLLBACK").ConfigureAwait(false);
            }
            else
            {
                // Auto-rollback to previous state
                await store.UpdateAsync(_ => previousState, $"{baseActionName}_ROLLBACK").ConfigureAwait(false);
            }

            if (onError != null)
            {
                await store.UpdateAsync(
                    s => onError(s, ex),
                    $"{baseActionName}_ERROR"
                ).ConfigureAwait(false);
            }
            else
            {
                throw;
            }
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
        var previousState = store.GetState();

        // Apply optimistic update immediately
        await store.UpdateAsync(optimistic, baseActionName).ConfigureAwait(false);

        try
        {
            var result = await action().ConfigureAwait(false);

            // Confirm with server result
            await store.UpdateAsync(
                s => confirm(s, result),
                $"{baseActionName}_CONFIRM"
            ).ConfigureAwait(false);

            return result;
        }
        catch
        {
            // Auto-rollback to previous state
            await store.UpdateAsync(_ => previousState, $"{baseActionName}_ROLLBACK").ConfigureAwait(false);
            throw;
        }
    }
}
