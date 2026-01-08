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
    /// <para>
    /// <strong>Important Callback Behavior:</strong> Only the first caller's callbacks (loading, success, error)
    /// are executed. Concurrent callers waiting for the same cache key receive the result but their callbacks
    /// are NOT invoked. This is intentional to ensure exactly 2 state updates (loading + success/error)
    /// regardless of the number of concurrent callers.
    /// </para>
    /// <para>
    /// When using this method from multiple locations, ensure all concurrent callers provide consistent callbacks
    /// or design your callbacks to be idempotent. If different callbacks are needed for different callers,
    /// use <see cref="ExecuteAsync{TResult}(Func{Task{TResult}}, Func{TState, TState}, Func{TState, TResult, TState}, Func{TState, Exception, TState}?, string?)"/>
    /// instead, or handle state updates separately after receiving the cached result.
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
    /// <remarks>
    /// <para>
    /// <strong>Important:</strong> Only the first caller's callbacks (loading, success, error) are executed.
    /// Concurrent callers waiting for the same cache key do NOT have their callbacks invoked.
    /// See <see cref="ExecuteCachedAsync{TResult}(string, Func{Task{TResult}}, Func{TState, TState}, Func{TState, TResult, TState}, Func{TState, Exception, TState}?, TimeSpan?, CancellationToken, string?)"/>
    /// for detailed explanation of this behavior.
    /// </para>
    /// </remarks>
    Task ExecuteCachedAsync<TResult>(
        string cacheKey,
        Func<Task<TResult>> asyncAction,
        Func<TState, TState> loading,
        Func<TState, TState> success,
        Func<TState, Exception, TState>? error = null,
        TimeSpan? cacheFor = null,
        CancellationToken cancellationToken = default,
        [CallerMemberName] string? action = null);

    /// <summary>
    /// Removes a specific cached result by key.
    /// </summary>
    /// <param name="cacheKey">The cache key to invalidate.</param>
    /// <remarks>
    /// This does not affect in-flight operations. Only cached results are removed.
    /// Prefer <see cref="InvalidateCacheAsync"/> in async contexts to avoid potential deadlocks.
    /// </remarks>
    void InvalidateCache(string cacheKey);

    /// <summary>
    /// Removes a specific cached result by key asynchronously.
    /// </summary>
    /// <param name="cacheKey">The cache key to invalidate.</param>
    /// <returns>A task representing the async operation.</returns>
    /// <remarks>
    /// This does not affect in-flight operations. Only cached results are removed.
    /// Preferred over <see cref="InvalidateCache"/> in async contexts.
    /// </remarks>
    Task InvalidateCacheAsync(string cacheKey);

    /// <summary>
    /// Removes all cached results with keys starting with the specified prefix.
    /// </summary>
    /// <param name="prefix">The prefix to match cache keys against.</param>
    /// <remarks>
    /// Useful for invalidating related cache entries (e.g., "product-" invalidates "product-1", "product-2", etc.).
    /// Prefer <see cref="InvalidateCacheByPrefixAsync"/> in async contexts to avoid potential deadlocks.
    /// </remarks>
    void InvalidateCacheByPrefix(string prefix);

    /// <summary>
    /// Removes all cached results with keys starting with the specified prefix asynchronously.
    /// </summary>
    /// <param name="prefix">The prefix to match cache keys against.</param>
    /// <returns>A task representing the async operation.</returns>
    /// <remarks>
    /// Useful for invalidating related cache entries (e.g., "product-" invalidates "product-1", "product-2", etc.).
    /// Preferred over <see cref="InvalidateCacheByPrefix"/> in async contexts.
    /// </remarks>
    Task InvalidateCacheByPrefixAsync(string prefix);

    /// <summary>
    /// Clears all cached results.
    /// </summary>
    /// <remarks>
    /// This does not affect in-flight operations. Only cached results are removed.
    /// Prefer <see cref="ClearCacheAsync"/> in async contexts to avoid potential deadlocks.
    /// </remarks>
    void ClearCache();

    /// <summary>
    /// Clears all cached results asynchronously.
    /// </summary>
    /// <returns>A task representing the async operation.</returns>
    /// <remarks>
    /// This does not affect in-flight operations. Only cached results are removed.
    /// Preferred over <see cref="ClearCache"/> in async contexts.
    /// </remarks>
    Task ClearCacheAsync();
}
