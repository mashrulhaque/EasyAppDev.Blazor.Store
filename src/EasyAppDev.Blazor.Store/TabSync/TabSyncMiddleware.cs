using System.Text.Json;
using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Middleware;
using Microsoft.JSInterop;

namespace EasyAppDev.Blazor.Store.TabSync;

/// <summary>
/// Middleware that synchronizes state changes across browser tabs
/// using the BroadcastChannel API.
/// </summary>
/// <typeparam name="TState">The type of state being synced.</typeparam>
/// <remarks>
/// Only works in WebAssembly. In Blazor Server, state is already
/// shared at the server level.
/// </remarks>
public sealed class TabSyncMiddleware<TState> : IMiddleware<TState>, IAsyncDisposable
    where TState : notnull
{
    private const string JsModulePath = "./_content/EasyAppDev.Blazor.Store/tabsync.js";

    private readonly IServiceProvider _serviceProvider;
    private readonly TabSyncOptions _options;
    private readonly string _tabId = Guid.NewGuid().ToString("N")[..8];
    private readonly JsonSerializerOptions _jsonOptions;

    private IJSRuntime? _jsRuntime;
    private IJSObjectReference? _jsModule;
    private DotNetObjectReference<TabSyncMiddleware<TState>>? _dotNetRef;
    private IStore<TState>? _store;
    private string? _channelName;
    private bool _isSyncUpdate;
    private bool _isInitialized;
    private bool _jsModuleLoaded;
    private CancellationTokenSource? _debounceCts;

    /// <summary>
    /// Creates a new tab sync middleware.
    /// </summary>
    /// <param name="serviceProvider">Service provider for lazy IJSRuntime resolution.</param>
    /// <param name="options">Sync configuration options.</param>
    public TabSyncMiddleware(IServiceProvider serviceProvider, TabSyncOptions? options = null)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _options = options ?? new TabSyncOptions();

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <summary>
    /// Initializes the middleware with the store reference.
    /// </summary>
    /// <param name="store">The store to sync.</param>
    internal void Initialize(IStore<TState> store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    private async Task EnsureJsModuleLoadedAsync()
    {
        if (_jsModuleLoaded) return;

        try
        {
            _jsRuntime = _serviceProvider.GetService(typeof(IJSRuntime)) as IJSRuntime;
            if (_jsRuntime == null)
            {
                return;
            }

            // Import the JS module
            _jsModule = await _jsRuntime.InvokeAsync<IJSObjectReference>(
                "import", JsModulePath).ConfigureAwait(false);

            _jsModuleLoaded = true;
        }
        catch
        {
            // Module loading not supported (SSR) or failed
            // Fall back to inline script
            _jsModuleLoaded = false;
        }
    }

    private async Task EnsureInitializedAsync()
    {
        if (_isInitialized) return;

        try
        {
            _jsRuntime = _serviceProvider.GetService(typeof(IJSRuntime)) as IJSRuntime;
            if (_jsRuntime == null)
            {
                return;
            }

            _dotNetRef = DotNetObjectReference.Create(this);
            _channelName = _options.ChannelName ?? $"store-{typeof(TState).Name}";

            // Try to load JS module first
            await EnsureJsModuleLoadedAsync().ConfigureAwait(false);

            // Initialize the channel
            var initialized = await _jsRuntime.InvokeAsync<bool>(
                "__initTabSync", _channelName, _dotNetRef).ConfigureAwait(false);

            if (!initialized)
            {
                // BroadcastChannel not supported or init failed
                return;
            }

            _isInitialized = true;
        }
        catch
        {
            // JS interop failed - SSR, prerendering, or unsupported browser
        }
    }

    /// <summary>
    /// Called from JavaScript when a message is received from another tab.
    /// </summary>
    /// <param name="messageJson">The JSON-serialized sync message.</param>
    [JSInvokable]
    public async Task OnMessageReceived(string messageJson)
    {
        if (_store == null) return;

        try
        {
            var message = JsonSerializer.Deserialize<SyncMessage>(messageJson, _jsonOptions);
            if (message == null || message.TabId == _tabId) return;

            _options.OnSyncReceived?.Invoke(message.Action);

            if (_options.SyncFullState && message.State != null)
            {
                var state = JsonSerializer.Deserialize<TState>(message.State, _jsonOptions);
                if (state != null)
                {
                    _isSyncUpdate = true;
                    try
                    {
                        await _store.UpdateAsync(_ => state, $"SYNC_{message.Action ?? "UPDATE"}").ConfigureAwait(false);
                    }
                    finally
                    {
                        _isSyncUpdate = false;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _options.OnSyncError?.Invoke(ex);
        }
    }

    /// <inheritdoc />
    Task IMiddleware<TState>.OnBeforeUpdateAsync(TState currentState, string? action)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    async Task IMiddleware<TState>.OnAfterUpdateAsync(TState previousState, TState currentState, string? action)
    {
        // Don't broadcast updates received from other tabs
        if (_isSyncUpdate) return;

        // Check if action should be synced
        if (!_options.ShouldSyncAction(action)) return;

        // Check if state actually changed
        if (EqualityComparer<TState>.Default.Equals(previousState, currentState))
            return;

        await EnsureInitializedAsync().ConfigureAwait(false);
        if (!_isInitialized || _jsRuntime == null || _channelName == null) return;

        try
        {
            if (_options.DebounceMs > 0)
            {
                // Cancel previous debounced sync
                _debounceCts?.Cancel();
                _debounceCts = new CancellationTokenSource();

                var cts = _debounceCts;
                await Task.Delay(_options.DebounceMs, cts.Token).ConfigureAwait(false);

                if (cts.Token.IsCancellationRequested) return;
            }

            var message = new SyncMessage
            {
                TabId = _tabId,
                Action = action,
                State = _options.SyncFullState
                    ? JsonSerializer.Serialize(currentState, _jsonOptions)
                    : null,
                Timestamp = DateTime.UtcNow
            };

            var messageJson = JsonSerializer.Serialize(message, _jsonOptions);

            await _jsRuntime.InvokeVoidAsync(
                "__postTabSyncMessage", _channelName, messageJson).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _options.OnSyncError?.Invoke(ex);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();

        // Dispose the channel in JavaScript
        if (_jsRuntime != null && _channelName != null && _isInitialized)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("__disposeTabSync", _channelName).ConfigureAwait(false);
            }
            catch
            {
                // Ignore disposal errors
            }
        }

        if (_jsModule != null)
        {
            try
            {
                await _jsModule.DisposeAsync().ConfigureAwait(false);
            }
            catch
            {
                // Ignore disposal errors
            }
        }

        _dotNetRef?.Dispose();
    }

    private sealed class SyncMessage
    {
        public string? TabId { get; set; }
        public string? Action { get; set; }
        public string? State { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
