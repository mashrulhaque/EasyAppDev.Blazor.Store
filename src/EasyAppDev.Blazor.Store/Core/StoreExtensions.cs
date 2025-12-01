using EasyAppDev.Blazor.Store.AsyncActions;

namespace EasyAppDev.Blazor.Store.Core;

/// <summary>
/// Extension methods for <see cref="IStore{TState}"/> providing convenience patterns
/// for common async data loading scenarios.
/// </summary>
public static class StoreExtensions
{
    /// <summary>
    /// Executes an async data loading operation with automatic state transitions for loading,
    /// success, and failure states. Simplifies the common pattern of loading data into
    /// <see cref="AsyncData{T}"/> fields.
    /// </summary>
    /// <typeparam name="TState">The type of state managed by the store.</typeparam>
    /// <typeparam name="TData">The type of data being loaded.</typeparam>
    /// <param name="store">The store instance.</param>
    /// <param name="selector">Function to select the AsyncData field from state.</param>
    /// <param name="loader">Async function that loads the data.</param>
    /// <param name="updater">Function to update state with the new AsyncData value.</param>
    /// <param name="action">Optional action name for debugging/DevTools.</param>
    /// <returns>A task that completes when the operation finishes.</returns>
    /// <example>
    /// <code>
    /// // Instead of:
    /// await Update(s => s with { Users = s.Users.ToLoading() });
    /// try
    /// {
    ///     var users = await api.LoadUsersAsync();
    ///     await Update(s => s with { Users = AsyncData&lt;List&lt;User&gt;&gt;.Success(users) });
    /// }
    /// catch (Exception ex)
    /// {
    ///     await Update(s => s with { Users = AsyncData&lt;List&lt;User&gt;&gt;.Failure(ex.Message) });
    /// }
    ///
    /// // Use:
    /// await store.UpdateWithAsync(
    ///     s => s.Users,
    ///     () => api.LoadUsersAsync(),
    ///     (s, data) => s with { Users = data }
    /// );
    /// </code>
    /// </example>
    public static async Task UpdateWithAsync<TState, TData>(
        this IStore<TState> store,
        Func<TState, AsyncData<TData>> selector,
        Func<Task<TData>> loader,
        Func<TState, AsyncData<TData>, TState> updater,
        string? action = null)
        where TState : notnull
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(updater);

        var actionName = action ?? "LOAD_ASYNC";

        // Set loading state
        await store.UpdateAsync(
            s => updater(s, AsyncData<TData>.Loading()),
            $"{actionName}_LOADING"
        ).ConfigureAwait(false);

        try
        {
            // Execute loader
            var data = await loader().ConfigureAwait(false);

            // Set success state
            await store.UpdateAsync(
                s => updater(s, AsyncData<TData>.Success(data)),
                $"{actionName}_SUCCESS"
            ).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Set failure state
            await store.UpdateAsync(
                s => updater(s, AsyncData<TData>.Failure(ex.Message)),
                $"{actionName}_FAILURE"
            ).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Executes an async data loading operation with automatic state transitions,
    /// with an optional custom error handler.
    /// </summary>
    /// <typeparam name="TState">The type of state managed by the store.</typeparam>
    /// <typeparam name="TData">The type of data being loaded.</typeparam>
    /// <param name="store">The store instance.</param>
    /// <param name="selector">Function to select the AsyncData field from state.</param>
    /// <param name="loader">Async function that loads the data.</param>
    /// <param name="updater">Function to update state with the new AsyncData value.</param>
    /// <param name="onError">Custom error handler that receives the exception and returns an error message.</param>
    /// <param name="action">Optional action name for debugging/DevTools.</param>
    /// <returns>A task that completes when the operation finishes.</returns>
    public static async Task UpdateWithAsync<TState, TData>(
        this IStore<TState> store,
        Func<TState, AsyncData<TData>> selector,
        Func<Task<TData>> loader,
        Func<TState, AsyncData<TData>, TState> updater,
        Func<Exception, string> onError,
        string? action = null)
        where TState : notnull
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(updater);
        ArgumentNullException.ThrowIfNull(onError);

        var actionName = action ?? "LOAD_ASYNC";

        // Set loading state
        await store.UpdateAsync(
            s => updater(s, AsyncData<TData>.Loading()),
            $"{actionName}_LOADING"
        ).ConfigureAwait(false);

        try
        {
            // Execute loader
            var data = await loader().ConfigureAwait(false);

            // Set success state
            await store.UpdateAsync(
                s => updater(s, AsyncData<TData>.Success(data)),
                $"{actionName}_SUCCESS"
            ).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Set failure state with custom error message
            var errorMessage = onError(ex);
            await store.UpdateAsync(
                s => updater(s, AsyncData<TData>.Failure(errorMessage)),
                $"{actionName}_FAILURE"
            ).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Executes an async operation that transforms state directly without AsyncData wrapper.
    /// Useful for simple async operations that don't need loading/success/failure states.
    /// </summary>
    /// <typeparam name="TState">The type of state managed by the store.</typeparam>
    /// <typeparam name="TData">The type of data being loaded.</typeparam>
    /// <param name="store">The store instance.</param>
    /// <param name="loader">Async function that loads the data.</param>
    /// <param name="onSuccess">Function to update state with the loaded data.</param>
    /// <param name="onError">Optional function to update state on error. If not provided, exception is rethrown.</param>
    /// <param name="action">Optional action name for debugging/DevTools.</param>
    /// <returns>A task that completes when the operation finishes.</returns>
    /// <example>
    /// <code>
    /// await store.LoadAsync(
    ///     () => api.GetUserAsync(userId),
    ///     (s, user) => s with { CurrentUser = user },
    ///     (s, ex) => s with { Error = ex.Message }
    /// );
    /// </code>
    /// </example>
    public static async Task LoadAsync<TState, TData>(
        this IStore<TState> store,
        Func<Task<TData>> loader,
        Func<TState, TData, TState> onSuccess,
        Func<TState, Exception, TState>? onError = null,
        string? action = null)
        where TState : notnull
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(onSuccess);

        var actionName = action ?? "LOAD";

        try
        {
            var data = await loader().ConfigureAwait(false);

            await store.UpdateAsync(
                s => onSuccess(s, data),
                $"{actionName}_SUCCESS"
            ).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (onError != null)
            {
                await store.UpdateAsync(
                    s => onError(s, ex),
                    $"{actionName}_ERROR"
                ).ConfigureAwait(false);
            }
            else
            {
                throw;
            }
        }
    }
}
