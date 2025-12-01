namespace EasyAppDev.Blazor.Store.Core;

/// <summary>
/// Provides write access to state through immutable update operations.
/// </summary>
/// <typeparam name="TState">The type of state managed by the store. Must be a non-nullable reference type.</typeparam>
/// <remarks>
/// Thread-safe state updates with atomic operations and subscriber notifications.
/// Use 'with' expressions for immutable updates.
/// See also: <see cref="IStateReader{TState}"/>, <see cref="IStateObservable{TState}"/>.
/// </remarks>
/// <example>
/// <code>
/// // Asynchronous update (recommended)
/// await stateWriter.UpdateAsync(
///     state => state with { IsLoading = true },
///     "START_LOADING"
/// );
///
/// // Async updater function (e.g., loading data)
/// await stateWriter.UpdateAsync(
///     async state => {
///         var data = await apiService.FetchDataAsync();
///         return state with { Data = data, IsLoading = false };
///     },
///     "LOAD_DATA"
/// );
/// </code>
/// </example>
public interface IStateWriter<TState> where TState : notnull
{
    /// <summary>
    /// Updates the state asynchronously using the provided updater function.
    /// </summary>
    /// <param name="updater">
    /// A pure function that takes the current state and returns the new state.
    /// Must not return null.
    /// </param>
    /// <param name="action">
    /// Optional action name for debugging, logging, and Redux DevTools integration.
    /// </param>
    /// <returns>
    /// Task that completes when update finishes and subscribers are notified.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="updater"/> is null.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// Thrown when the store has been disposed.
    /// </exception>
    /// <remarks>
    /// Async wrapper for synchronous updater. For async updaters, use the Task overload.
    /// </remarks>
    /// <example>
    /// <code>
    /// // In a Blazor component
    /// private async Task IncrementAsync()
    /// {
    ///     await stateWriter.UpdateAsync(
    ///         s => s with { Count = s.Count + 1 },
    ///         "INCREMENT"
    ///     );
    ///     // Can perform additional async work after state update
    ///     await LogToServerAsync("Count incremented");
    /// }
    /// </code>
    /// </example>
    Task UpdateAsync(Func<TState, TState> updater, string? action = null);

    /// <summary>
    /// Updates the state asynchronously using an async updater function.
    /// </summary>
    /// <param name="asyncUpdater">
    /// An async function that takes the current state and returns the new state.
    /// Can perform async operations (e.g., API calls, database queries) before
    /// returning the new state.
    /// </param>
    /// <param name="action">
    /// Optional action name for debugging, logging, and Redux DevTools integration.
    /// </param>
    /// <returns>
    /// Task that completes when async updater finishes and subscribers are notified.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="asyncUpdater"/> is null.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// Thrown when the store has been disposed.
    /// </exception>
    /// <remarks>
    /// For async operations (API calls, database queries). Holds lock during execution.
    /// For long operations, use <see cref="EasyAppDev.Blazor.Store.AsyncActions.IAsyncActionExecutor{TState}"/>.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Loading data from an API
    /// await stateWriter.UpdateAsync(
    ///     async state => {
    ///         var users = await userService.GetUsersAsync();
    ///         return state with { Users = users, IsLoading = false };
    ///     },
    ///     "LOAD_USERS"
    /// );
    ///
    /// // Using state async methods
    /// await stateWriter.UpdateAsync(
    ///     async s => await s.LoadUserAsync(userId, userService),
    ///     "LOAD_USER"
    /// );
    /// </code>
    /// </example>
    Task UpdateAsync(Func<TState, Task<TState>> asyncUpdater, string? action = null);
}
