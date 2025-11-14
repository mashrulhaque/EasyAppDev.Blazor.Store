// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using System.Runtime.CompilerServices;

namespace EasyAppDev.Blazor.Store.AsyncActions;

/// <summary>
/// Executes async actions with automatic loading, success, and error state management.
/// </summary>
/// <typeparam name="TState">The type of state being managed.</typeparam>
/// <example>
/// <code>
/// // In a Blazor component with StoreComponent&lt;UserState&gt;
/// private async Task LoadUserAsync()
/// {
///     await ExecuteAsync(
///         async () => await userService.GetUserAsync(userId),
///         loading: s => s with { IsLoading = true },
///         success: (s, user) => s with { User = user, IsLoading = false },
///         error: (s, ex) => s with { Error = ex.Message, IsLoading = false }
///     );
/// }
/// </code>
/// </example>
public interface IAsyncActionExecutor<TState> where TState : notnull
{
    /// <summary>
    /// Executes an async action with automatic loading, success, and error state handling.
    /// </summary>
    /// <typeparam name="TResult">The type of result returned by the async action.</typeparam>
    /// <param name="asyncAction">The async action to execute.</param>
    /// <param name="loading">Pure function to update state to indicate loading has started.</param>
    /// <param name="success">Pure function to update state when the async action succeeds.</param>
    /// <param name="error">Optional pure function to update state when the async action fails.</param>
    /// <param name="action">Optional action name for debugging. Defaults to caller member name.</param>
    /// <returns>A task that represents the async operation.</returns>
    /// <example>
    /// <code>
    /// await ExecuteAsync(
    ///     async () => await userService.GetUserAsync(userId),
    ///     loading: s => s with { IsLoading = true, Error = null },
    ///     success: (s, user) => s with { User = user, IsLoading = false },
    ///     error: (s, ex) => s with { Error = ex.Message, IsLoading = false }
    /// );
    /// </code>
    /// </example>
    Task ExecuteAsync<TResult>(
        Func<Task<TResult>> asyncAction,
        Func<TState, TState> loading,
        Func<TState, TResult, TState> success,
        Func<TState, Exception, TState>? error = null,
        [CallerMemberName] string? action = null);

    /// <summary>
    /// Executes an async action without a result value (void async operations).
    /// </summary>
    /// <param name="asyncAction">The async action to execute.</param>
    /// <param name="loading">Pure function to update state to indicate loading has started.</param>
    /// <param name="success">Pure function to update state when the async action succeeds.</param>
    /// <param name="error">Optional pure function to update state when the async action fails.</param>
    /// <param name="action">Optional action name for debugging. Defaults to caller member name.</param>
    /// <returns>A task that represents the async operation.</returns>
    /// <example>
    /// <code>
    /// await ExecuteAsync(
    ///     async () => await userService.SaveUserAsync(user),
    ///     loading: s => s with { IsSaving = true },
    ///     success: s => s with { IsSaving = false, SuccessMessage = "Profile saved!" },
    ///     error: (s, ex) => s with { IsSaving = false, Error = ex.Message }
    /// );
    /// </code>
    /// </example>
    Task ExecuteAsync(
        Func<Task> asyncAction,
        Func<TState, TState> loading,
        Func<TState, TState> success,
        Func<TState, Exception, TState>? error = null,
        [CallerMemberName] string? action = null);

    /// <summary>
    /// Executes an async action with simplified success handler that doesn't need the result.
    /// </summary>
    /// <typeparam name="TResult">The type of result returned by the async action (discarded).</typeparam>
    /// <param name="asyncAction">The async action to execute. Returns a result that is discarded.</param>
    /// <param name="loading">Pure function to update state to indicate loading has started.</param>
    /// <param name="success">Pure function to update state when the async action succeeds.</param>
    /// <param name="error">Optional pure function to update state when the async action fails.</param>
    /// <param name="action">Optional action name for debugging. Defaults to caller member name.</param>
    /// <returns>A task that represents the async operation.</returns>
    /// <remarks>
    /// Use this overload when the async action returns data but you only care about success/failure.
    /// </remarks>
    /// <example>
    /// <code>
    /// await ExecuteAsync&lt;bool&gt;(
    ///     async () => await healthService.PingAsync(),
    ///     loading: s => s with { IsChecking = true },
    ///     success: s => s with { IsChecking = false, ServerAvailable = true },
    ///     error: (s, ex) => s with { IsChecking = false, ServerAvailable = false }
    /// );
    /// </code>
    /// </example>
    Task ExecuteAsync<TResult>(
        Func<Task<TResult>> asyncAction,
        Func<TState, TState> loading,
        Func<TState, TState> success,
        Func<TState, Exception, TState>? error = null,
        [CallerMemberName] string? action = null);
}
