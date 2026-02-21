using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Diagnostics;
using EasyAppDev.Blazor.Store.Diagnostics.Models;
using Microsoft.Extensions.DependencyInjection;

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
    private volatile bool _disposed;
    private Guid _subscriptionId;

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

    /// <summary>
    /// Gets the injected service provider.
    /// </summary>
    [Inject]
    private IServiceProvider ServiceProvider { get; set; } = default!;

    /// <summary>
    /// Gets the diagnostics service if available.
    /// </summary>
    protected IDiagnosticsService? DiagnosticsService { get; private set; }

    /// <summary>
    /// Gets the current state from the store.
    /// </summary>
    protected TState State => Store.GetState();

    /// <summary>
    /// Gets whether the component has been disposed.
    /// </summary>
    protected bool IsDisposed => _disposed;

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

        DiagnosticsService = ServiceProvider.GetService(typeof(IDiagnosticsService)) as IDiagnosticsService;

        SubscribeToStore();
    }

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

    /// <summary>
    /// Subscribes to store changes. Can be overridden for custom subscription logic.
    /// </summary>
    protected virtual void SubscribeToStore()
    {
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

        _subscription = Store.Subscribe(state =>
        {
            DiagnosticsService?.RecordSubscriptionNotification(_subscriptionId);
            // Use try-catch to handle component disposal during async invoke
            try
            {
                InvokeAsync(StateHasChanged);
            }
            catch (ObjectDisposedException)
            {
                // Component was disposed during notification - this is expected
            }
            catch (Exception ex)
            {
                Logger?.LogWarning(ex, "Error invoking StateHasChanged in {ComponentType}", GetType().Name);
            }
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
                // Use try-catch to handle component disposal during async invoke
                try
                {
                    InvokeAsync(StateHasChanged);
                }
                catch (ObjectDisposedException)
                {
                    // Component was disposed during notification - this is expected
                }
                catch (Exception ex)
                {
                    Logger?.LogWarning(ex, "Error invoking StateHasChanged in {ComponentType}", GetType().Name);
                }
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
            DiagnosticsService?.RecordSubscriptionDisposed(_subscriptionId);
            _subscription?.Dispose();
            _subscription = null;
        }

        _disposed = true;
    }
}
