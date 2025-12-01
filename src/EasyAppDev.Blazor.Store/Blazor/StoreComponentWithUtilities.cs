using Microsoft.AspNetCore.Components;
using EasyAppDev.Blazor.Store.AsyncActions;
using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Utilities;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;

namespace EasyAppDev.Blazor.Store.Blazor;

/// <summary>
/// Extended base component class that includes utility methods for debouncing,
/// throttling, lazy loading, and async action execution.
/// Use this when you need the full feature set. For a leaner component,
/// use <see cref="StoreComponent{TState}"/> instead.
/// </summary>
/// <typeparam name="TState">The type of state managed by the store.</typeparam>
public abstract class StoreComponentWithUtilities<TState> : StoreComponent<TState>
    where TState : notnull
{
    /// <summary>
    /// Gets the injected debounce manager.
    /// </summary>
    [Inject]
    protected IDebounceManager DebounceManager { get; set; } = default!;

    /// <summary>
    /// Gets the injected throttle manager.
    /// </summary>
    [Inject]
    protected IThrottleManager ThrottleManager { get; set; } = default!;

    /// <summary>
    /// Gets the injected lazy cache.
    /// </summary>
    [Inject]
    protected ILazyCache LazyCache { get; set; } = default!;

    /// <summary>
    /// Gets the injected service provider.
    /// </summary>
    [Inject]
    private IServiceProvider ServiceProvider { get; set; } = default!;

    /// <summary>
    /// Gets the injected async action executor (optional).
    /// </summary>
    protected IAsyncActionExecutor<TState>? AsyncExecutor { get; private set; }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();

        // Try to resolve AsyncExecutor - it's optional
        AsyncExecutor = ServiceProvider.GetService(typeof(IAsyncActionExecutor<TState>)) as IAsyncActionExecutor<TState>;
    }

    /// <summary>
    /// Updates state with debouncing, cancelling any pending updates with the same action name.
    /// </summary>
    /// <param name="updater">Function to transform the current state.</param>
    /// <param name="delayMilliseconds">Delay in milliseconds before applying the update.</param>
    /// <param name="action">Optional action name for debugging (auto-filled with caller method name).</param>
    protected Task UpdateDebounced(
        Func<TState, TState> updater,
        int delayMilliseconds,
        [CallerMemberName] string? action = null)
    {
        ArgumentNullException.ThrowIfNull(updater);

        var key = $"{GetType().Name}_{action ?? "update"}";
        return DebounceManager.Debounce(key, async () =>
        {
            await InvokeAsync(async () =>
            {
                await Store.UpdateAsync(updater, action);
            });
        }, delayMilliseconds);
    }

    /// <summary>
    /// Updates state asynchronously with debouncing.
    /// </summary>
    /// <param name="asyncUpdater">Async function to transform the current state.</param>
    /// <param name="delayMilliseconds">Delay in milliseconds before applying the update.</param>
    /// <param name="action">Optional action name for debugging (auto-filled with caller method name).</param>
    protected Task UpdateDebouncedAsync(
        Func<TState, Task<TState>> asyncUpdater,
        int delayMilliseconds,
        [CallerMemberName] string? action = null)
    {
        ArgumentNullException.ThrowIfNull(asyncUpdater);

        var key = $"{GetType().Name}_{action ?? "update"}";
        return DebounceManager.Debounce(key, async () =>
        {
            await InvokeAsync(async () =>
            {
                await Store.UpdateAsync(asyncUpdater, action);
            });
        }, delayMilliseconds);
    }

    /// <summary>
    /// Updates state with throttling, limiting update frequency.
    /// </summary>
    /// <param name="updater">Function to transform the current state.</param>
    /// <param name="intervalMilliseconds">Minimum interval in milliseconds between updates.</param>
    /// <param name="action">Optional action name for debugging (auto-filled with caller method name).</param>
    protected Task UpdateThrottled(
        Func<TState, TState> updater,
        int intervalMilliseconds,
        [CallerMemberName] string? action = null)
    {
        ArgumentNullException.ThrowIfNull(updater);

        var key = $"{GetType().Name}_{action ?? "update"}";
        return ThrottleManager.Throttle(key, async () =>
        {
            await InvokeAsync(async () =>
            {
                await Store.UpdateAsync(updater, action);
            });
        }, intervalMilliseconds);
    }

    /// <summary>
    /// Updates state asynchronously with throttling.
    /// </summary>
    /// <param name="asyncUpdater">Async function to transform the current state.</param>
    /// <param name="intervalMilliseconds">Minimum interval in milliseconds between updates.</param>
    /// <param name="action">Optional action name for debugging (auto-filled with caller method name).</param>
    protected Task UpdateThrottledAsync(
        Func<TState, Task<TState>> asyncUpdater,
        int intervalMilliseconds,
        [CallerMemberName] string? action = null)
    {
        ArgumentNullException.ThrowIfNull(asyncUpdater);

        var key = $"{GetType().Name}_{action ?? "update"}";
        return ThrottleManager.Throttle(key, async () =>
        {
            await InvokeAsync(async () =>
            {
                await Store.UpdateAsync(asyncUpdater, action);
            });
        }, intervalMilliseconds);
    }

    /// <summary>
    /// Executes an async action with automatic loading, success, and error state handling.
    /// </summary>
    /// <typeparam name="TResult">The type of result returned by the async action.</typeparam>
    /// <param name="asyncAction">The async action to execute.</param>
    /// <param name="loading">Function to transform state to loading state.</param>
    /// <param name="success">Function to transform state with the result on success.</param>
    /// <param name="error">Optional function to transform state on error.</param>
    /// <param name="action">Optional action name for debugging (auto-filled with caller method name).</param>
    protected Task ExecuteAsync<TResult>(
        Func<Task<TResult>> asyncAction,
        Func<TState, TState> loading,
        Func<TState, TResult, TState> success,
        Func<TState, Exception, TState>? error = null,
        [CallerMemberName] string? action = null)
    {
        if (AsyncExecutor == null)
        {
            throw new InvalidOperationException(
                $"IAsyncActionExecutor<{typeof(TState).Name}> is not registered. " +
                $"Call builder.Services.AddAsyncActionExecutor<{typeof(TState).Name}>() before using ExecuteAsync.");
        }
        return AsyncExecutor.ExecuteAsync(asyncAction, loading, success, error, action);
    }

    /// <summary>
    /// Executes an async action without a result value.
    /// </summary>
    /// <param name="asyncAction">The async action to execute.</param>
    /// <param name="loading">Function to transform state to loading state.</param>
    /// <param name="success">Function to transform state on success.</param>
    /// <param name="error">Optional function to transform state on error.</param>
    /// <param name="action">Optional action name for debugging (auto-filled with caller method name).</param>
    protected Task ExecuteAsync(
        Func<Task> asyncAction,
        Func<TState, TState> loading,
        Func<TState, TState> success,
        Func<TState, Exception, TState>? error = null,
        [CallerMemberName] string? action = null)
    {
        if (AsyncExecutor == null)
        {
            throw new InvalidOperationException(
                $"IAsyncActionExecutor<{typeof(TState).Name}> is not registered. " +
                $"Call builder.Services.AddAsyncActionExecutor<{typeof(TState).Name}>() before using ExecuteAsync.");
        }
        return AsyncExecutor.ExecuteAsync(asyncAction, loading, success, error, action);
    }

    /// <summary>
    /// Executes an async action with simplified success handler that ignores the result.
    /// </summary>
    /// <typeparam name="TResult">The type of result returned by the async action.</typeparam>
    /// <param name="asyncAction">The async action to execute.</param>
    /// <param name="loading">Function to transform state to loading state.</param>
    /// <param name="success">Function to transform state on success (ignores result).</param>
    /// <param name="error">Optional function to transform state on error.</param>
    /// <param name="action">Optional action name for debugging (auto-filled with caller method name).</param>
    protected Task ExecuteAsync<TResult>(
        Func<Task<TResult>> asyncAction,
        Func<TState, TState> loading,
        Func<TState, TState> success,
        Func<TState, Exception, TState>? error = null,
        [CallerMemberName] string? action = null)
    {
        if (AsyncExecutor == null)
        {
            throw new InvalidOperationException(
                $"IAsyncActionExecutor<{typeof(TState).Name}> is not registered. " +
                $"Call builder.Services.AddAsyncActionExecutor<{typeof(TState).Name}>() before using ExecuteAsync.");
        }
        return AsyncExecutor.ExecuteAsync(asyncAction, loading, success, error, action);
    }

    /// <summary>
    /// Loads data with automatic caching and request deduplication.
    /// </summary>
    /// <typeparam name="T">The type of data to load.</typeparam>
    /// <param name="cacheKey">Unique key for caching the result.</param>
    /// <param name="loader">Function that loads the data.</param>
    /// <param name="cacheFor">Optional cache duration. If null, data is cached indefinitely.</param>
    /// <returns>The loaded or cached data.</returns>
    protected Task<T> LazyLoad<T>(
        string cacheKey,
        Func<Task<T>> loader,
        TimeSpan? cacheFor = null)
    {
        ArgumentNullException.ThrowIfNull(cacheKey);
        ArgumentNullException.ThrowIfNull(loader);

        return LazyCache.GetOrLoadAsync(cacheKey, loader, cacheFor);
    }
}
