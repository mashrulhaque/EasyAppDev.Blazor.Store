using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Diagnostics;
using EasyAppDev.Blazor.Store.Diagnostics.Models;
using Microsoft.Extensions.DependencyInjection;

namespace EasyAppDev.Blazor.Store.Blazor;

/// <summary>
/// Base component class that automatically re-renders only when selected state values change.
/// Provides granular update control for optimal performance.
/// </summary>
/// <typeparam name="TState">The type of state managed by the store.</typeparam>
/// <remarks>
/// Unlike <see cref="StoreComponent{TState}"/> which re-renders on any state change,
/// this component only re-renders when the value returned by <see cref="SelectState"/>
/// changes. This enables fine-grained control over component re-rendering for better performance.
/// </remarks>
/// <example>
/// <code>
/// @inherits SelectorStoreComponent&lt;CounterState&gt;
///
/// &lt;p&gt;Count: @State.Count&lt;/p&gt;
///
/// @code {
///     // Only re-render when Count changes (not when other properties change)
///     protected override object SelectState(CounterState state)
///         =&gt; state.Count;
/// }
/// </code>
/// </example>
public abstract class SelectorStoreComponent<TState> : ComponentBase, IDisposable
    where TState : notnull
{
    private IDisposable? _subscription;
    private bool _disposed;
    private volatile object? _selectedValue;
    private readonly object _valueLock = new();
    private Guid _subscriptionId;
    private ILogger? _logger;

    /// <summary>
    /// Gets the injected store instance.
    /// </summary>
    [Inject]
    protected IStore<TState> Store { get; set; } = default!;

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
    /// Gets the currently selected value.
    /// </summary>
    protected object? Selected => _selectedValue;

    /// <summary>
    /// Selects which part of the state should trigger re-renders.
    /// </summary>
    /// <param name="state">The current state.</param>
    /// <returns>
    /// The value to watch for changes. The component will re-render only when
    /// this value changes (using equality comparison).
    /// </returns>
    /// <remarks>
    /// <para>
    /// Return a single property for simple cases:
    /// <code>
    /// protected override object SelectState(MyState state)
    ///     =&gt; state.Count;
    /// </code>
    /// </para>
    /// <para>
    /// Return multiple properties using an anonymous object:
    /// <code>
    /// protected override object SelectState(MyState state)
    ///     =&gt; new { state.Count, state.Name };
    /// </code>
    /// </para>
    /// <para>
    /// Return a computed/derived value:
    /// <code>
    /// protected override object SelectState(MyState state)
    ///     =&gt; state.Items.Where(x =&gt; x.IsActive).ToList();
    /// </code>
    /// </para>
    /// </remarks>
    protected abstract object SelectState(TState state);

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

        // Try to resolve diagnostics service if available
        DiagnosticsService = ServiceProvider.GetService(typeof(IDiagnosticsService)) as IDiagnosticsService;
        _logger = ServiceProvider.GetService(typeof(ILogger<SelectorStoreComponent<TState>>)) as ILogger;

        SubscribeToStore();
    }

    // Note: This component intentionally does NOT override ShouldRender().
    // Render-skipping for unchanged selected values is handled at the subscription
    // level (the selector subscription only fires when the selected value changes).
    // Overriding ShouldRender() here would also suppress legitimate renders caused
    // by parameter changes, event handlers, and manual StateHasChanged() calls.

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender)
    {
        base.OnAfterRender(firstRender);

        // Record render event for diagnostics
        if (DiagnosticsService is not null)
        {
            DiagnosticsService.RecordRender(new RenderEvent
            {
                ComponentName = GetType().Name,
                Timestamp = DateTime.UtcNow,
                IsFirstRender = firstRender,
                StateType = typeof(TState),
                Reason = firstRender ? "Initial Render" : "State or Parameter Change"
            });
        }
    }

    /// <summary>
    /// Subscribes to store changes using the selector pattern.
    /// Can be overridden for custom subscription logic.
    /// </summary>
    protected virtual void SubscribeToStore()
    {
        // Get initial selected value
        _selectedValue = SelectState(Store.GetState());

        _subscriptionId = Guid.NewGuid();

        if (DiagnosticsService is not null)
        {
            DiagnosticsService.RecordSubscription(new SubscriptionInfo
            {
                SubscriptionId = _subscriptionId,
                StateType = typeof(TState),
                SubscriberName = GetType().Name,
                SubscriptionType = "Selector",
                CreatedAt = DateTime.UtcNow,
                NotificationCount = 0
            });
        }

        // Subscribe using the selector
        _subscription = Store.Subscribe(
            selector: SelectState,
            callback: value =>
            {
                // Record subscription notification for diagnostics
                DiagnosticsService?.RecordSubscriptionNotification(_subscriptionId);
                // Thread-safe write to _selectedValue
                lock (_valueLock)
                {
                    _selectedValue = value;
                }

                // The catch only covers synchronous queuing failures (e.g. the renderer
                // was disposed while the notification was in flight). Async failures are
                // observed via the fault continuation below.
                try
                {
                    var task = InvokeAsync(StateHasChanged);
                    _ = task.ContinueWith(
                        t => _logger?.LogWarning(
                            t.Exception?.GetBaseException(),
                            "Error invoking StateHasChanged in {ComponentType}",
                            GetType().Name),
                        CancellationToken.None,
                        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
                }
                catch (ObjectDisposedException)
                {
                    // Component was disposed during notification - this is expected
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
