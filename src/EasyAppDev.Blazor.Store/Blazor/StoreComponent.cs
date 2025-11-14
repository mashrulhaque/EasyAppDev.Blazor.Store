using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using EasyAppDev.Blazor.Store.AsyncActions;
using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Utilities;
using System.Runtime.CompilerServices;
#if DEBUG
using EasyAppDev.Blazor.Store.Diagnostics;
using EasyAppDev.Blazor.Store.Diagnostics.Models;
using Microsoft.Extensions.DependencyInjection;
#endif

namespace EasyAppDev.Blazor.Store.Blazor;

/// <summary>
/// Base component class that automatically re-renders when store state changes.
/// </summary>
/// <typeparam name="TState">The type of state managed by the store.</typeparam>
public abstract class StoreComponent<TState> : ComponentBase, IDisposable
    where TState : notnull
{
    private IDisposable? _subscription;
    private bool _disposed;
#if DEBUG
    private Guid _subscriptionId;
#endif

    /// <summary>
    /// Gets the injected store instance.
    /// </summary>
    [Inject]
    protected IStore<TState> Store { get; set; } = default!;

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
    /// Gets the injected async action executor (optional).
    /// </summary>
    protected IAsyncActionExecutor<TState>? AsyncExecutor { get; private set; }

    /// <summary>
    /// Gets the injected logger.
    /// </summary>
    [Inject]
    protected ILogger<StoreComponent<TState>>? Logger { get; set; }

    /// <summary>
    /// Gets the injected service provider.
    /// </summary>
    [Inject]
    private IServiceProvider ServiceProvider { get; set; } = default!;

#if DEBUG

    /// <summary>
    /// Gets the diagnostics service if available (DEBUG only).
    /// </summary>
    protected IDiagnosticsService? DiagnosticsService { get; private set; }
#endif

    /// <summary>
    /// Gets the current state from the store.
    /// </summary>
    protected TState State => Store.GetState();

    /// <summary>
    /// Updates the state using the provided updater function.
    /// </summary>
    /// <param name="updater">Function that transforms current state to new state.</param>
    /// <param name="action">Optional action name for debugging.</param>
    protected void UpdateState(Func<TState, TState> updater, string? action = null)
    {
        Store.Update(updater, action);
    }

    /// <summary>
    /// Updates the state asynchronously.
    /// </summary>
    /// <param name="updater">Function that transforms current state to new state.</param>
    /// <param name="action">Optional action name for debugging.</param>
    protected Task UpdateStateAsync(Func<TState, TState> updater, string? action = null)
    {
        return Store.UpdateAsync(updater, action);
    }

    /// <summary>
    /// Updates the state asynchronously using an async updater.
    /// </summary>
    /// <param name="asyncUpdater">Async function that transforms state.</param>
    /// <param name="action">Optional action name for debugging.</param>
    protected Task UpdateStateAsync(Func<TState, Task<TState>> asyncUpdater, string? action = null)
    {
        return Store.UpdateAsync(asyncUpdater, action);
    }

    /// <summary>
    /// Updates the state using a transformation function (Zustand-style).
    /// </summary>
    /// <param name="updater">Function that transforms current state to new state.</param>
    /// <param name="action">Optional action name for debugging/DevTools.</param>
    protected Task Update(Func<TState, TState> updater, string? action = null)
    {
        return Store.UpdateAsync(updater, action ?? "UPDATE");
    }

    /// <summary>
    /// Updates the state using an async transformation function (Zustand-style).
    /// </summary>
    /// <param name="asyncUpdater">Async function that transforms current state to new state.</param>
    /// <param name="action">Optional action name for debugging/DevTools.</param>
    protected Task UpdateAsync(Func<TState, Task<TState>> asyncUpdater, string? action = null)
    {
        return Store.UpdateAsync(asyncUpdater, action ?? "UPDATE_ASYNC");
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
    /// Executes an async action with simplified success handler.
    /// </summary>
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
    protected Task<T> LazyLoad<T>(
        string cacheKey,
        Func<Task<T>> loader,
        TimeSpan? cacheFor = null)
    {
        ArgumentNullException.ThrowIfNull(cacheKey);
        ArgumentNullException.ThrowIfNull(loader);

        return LazyCache.GetOrLoadAsync(cacheKey, loader, cacheFor);
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();

        // Try to resolve AsyncExecutor - it's optional
        AsyncExecutor = ServiceProvider.GetService(typeof(IAsyncActionExecutor<TState>)) as IAsyncActionExecutor<TState>;

#if DEBUG
        DiagnosticsService = ServiceProvider.GetService(typeof(IDiagnosticsService)) as IDiagnosticsService;
#endif

        SubscribeToStore();
    }

#if DEBUG
    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender)
    {
        base.OnAfterRender(firstRender);

        if (DiagnosticsService is not null)
        {
            DiagnosticsService.RecordRender(new RenderEvent
            {
                ComponentName = GetType().Name,
                Timestamp = DateTime.UtcNow,
                IsFirstRender = firstRender,
                StateType = typeof(TState),
                Reason = firstRender ? "Initial Render" : "State Change"
            });
        }
    }
#endif

    /// <summary>
    /// Subscribes to store changes. Can be overridden for custom subscription logic.
    /// </summary>
    protected virtual void SubscribeToStore()
    {
#if DEBUG
        _subscriptionId = Guid.NewGuid();

        if (DiagnosticsService is not null)
        {
            DiagnosticsService.RecordSubscription(new SubscriptionInfo
            {
                SubscriptionId = _subscriptionId,
                StateType = typeof(TState),
                SubscriberName = GetType().Name,
                SubscriptionType = "Full",
                CreatedAt = DateTime.UtcNow,
                NotificationCount = 0
            });
        }
#endif

        _subscription = Store.Subscribe(_ =>
        {
#if DEBUG
            DiagnosticsService?.RecordSubscriptionNotification(_subscriptionId);
#endif
            InvokeAsync(StateHasChanged);
        });
    }

    /// <summary>
    /// Subscribes to a specific part of the state using a selector.
    /// </summary>
    protected IDisposable SubscribeToSelector<TSelected>(
        Func<TState, TSelected> selector,
        Action<TSelected>? callback = null)
    {
        return Store.Subscribe(
            selector,
            value =>
            {
                callback?.Invoke(value);
                InvokeAsync(StateHasChanged);
            });
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes resources used by the component.
    /// </summary>
    /// <param name="disposing">True if disposing managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
#if DEBUG
            DiagnosticsService?.RecordSubscriptionDisposed(_subscriptionId);
#endif
            _subscription?.Dispose();
            _subscription = null;

            // Note: Injected services (DebounceManager, ThrottleManager, LazyCache)
            // are managed by DI container and will be disposed automatically
        }

        _disposed = true;
    }
}
