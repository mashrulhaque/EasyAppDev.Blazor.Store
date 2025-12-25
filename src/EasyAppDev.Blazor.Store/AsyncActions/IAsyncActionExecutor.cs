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

    /// <summary>
    /// Executes an async action with caching and full deduplication of both fetch and state updates.
    /// </summary>
    /// <typeparam name="TResult">The type of result returned by the async action.</typeparam>
    /// <param name="cacheKey">Unique key for deduplication. Concurrent calls with the same key share one execution.</param>
    /// <param name="asyncAction">The async action to execute.</param>
    /// <param name="loading">Pure function to update state to indicate loading has started.</param>
    /// <param name="success">Pure function to update state when the async action succeeds.</param>
    /// <param name="error">Optional pure function to update state when the async action fails.</param>
    /// <param name="cacheFor">Optional duration to cache the result. If null, only deduplicates concurrent requests.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <param name="action">Optional action name for debugging. Defaults to caller member name.</param>
    /// <returns>A task that completes with the result when the operation finishes.</returns>
    /// <remarks>
    /// <para>
    /// Unlike <see cref="ExecuteAsync{TResult}(Func{Task{TResult}}, Func{TState, TState}, Func{TState, TResult, TState}, Func{TState, Exception, TState}?, string?)"/>,
    /// this method deduplicates both the async fetch AND the state updates. If 5 components call
    /// this method with the same cache key concurrently, only 2 state updates occur (one loading,
    /// one success/error) instead of 10.
    /// </para>
    /// <para>
    /// The first caller triggers the loading state and async action. Subsequent callers with the
    /// same key wait for the result without triggering additional state updates. All callers
    /// receive the same result.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Multiple components can call this - only one fetch and 2 state updates occur
    /// var product = await ExecuteCachedAsync(
    ///     $"product-{productId}",
    ///     async () => await productService.GetProductAsync(productId),
    ///     loading: s => s with { Product = s.Product.ToLoading() },
    ///     success: (s, product) => s with { Product = AsyncData.Success(product) },
    ///     error: (s, ex) => s with { Product = AsyncData.Failure(ex.Message) },
    ///     cacheFor: TimeSpan.FromMinutes(5)
    /// );
    /// </code>
    /// </example>
    Task<TResult> ExecuteCachedAsync<TResult>(
        string cacheKey,
        Func<Task<TResult>> asyncAction,
        Func<TState, TState> loading,
        Func<TState, TResult, TState> success,
        Func<TState, Exception, TState>? error = null,
        TimeSpan? cacheFor = null,
        CancellationToken cancellationToken = default,
        [CallerMemberName] string? action = null);

    /// <summary>
    /// Executes an async action with caching and simplified success handler that doesn't need the result.
    /// </summary>
    /// <typeparam name="TResult">The type of result returned by the async action.</typeparam>
    /// <param name="cacheKey">Unique key for deduplication. Concurrent calls with the same key share one execution.</param>
    /// <param name="asyncAction">The async action to execute.</param>
    /// <param name="loading">Pure function to update state to indicate loading has started.</param>
    /// <param name="success">Pure function to update state when the async action succeeds (result discarded).</param>
    /// <param name="error">Optional pure function to update state when the async action fails.</param>
    /// <param name="cacheFor">Optional duration to cache the result. If null, only deduplicates concurrent requests.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <param name="action">Optional action name for debugging. Defaults to caller member name.</param>
    /// <returns>A task that completes when the operation finishes.</returns>
    Task ExecuteCachedAsync<TResult>(
        string cacheKey,
        Func<Task<TResult>> asyncAction,
        Func<TState, TState> loading,
        Func<TState, TState> success,
        Func<TState, Exception, TState>? error = null,
        TimeSpan? cacheFor = null,
        CancellationToken cancellationToken = default,
        [CallerMemberName] string? action = null);
}
