using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using EasyAppDev.Blazor.Store.Core;
#if DEBUG
using EasyAppDev.Blazor.Store.Diagnostics;
using EasyAppDev.Blazor.Store.Diagnostics.Models;
using Microsoft.Extensions.DependencyInjection;
#endif

namespace EasyAppDev.Blazor.Store.Blazor;

/// <summary>
/// Base component class that automatically re-renders when store state changes.
/// Provides minimal functionality for state management. For utility methods like
/// debouncing, throttling, lazy loading, and async action execution, use
/// <see cref="StoreComponentWithUtilities{TState}"/> instead.
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
    /// Gets the injected logger.
    /// </summary>
    [Inject]
    protected ILogger<StoreComponent<TState>>? Logger { get; set; }

#if DEBUG
    /// <summary>
    /// Gets the injected service provider.
    /// </summary>
    [Inject]
    private IServiceProvider ServiceProvider { get; set; } = default!;

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

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();

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
        }

        _disposed = true;
    }
}
