using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using System.Text.Json;
using EasyAppDev.Blazor.Store.Middleware;

namespace EasyAppDev.Blazor.Store.DevTools;

/// <summary>
/// Middleware that integrates with Redux DevTools browser extension.
/// Uses lazy IJSRuntime resolution via IServiceProvider for compatibility
/// with Blazor Server, WebAssembly, and Auto render modes.
/// </summary>
/// <typeparam name="TState">The type of state managed by the store.</typeparam>
public class DevToolsMiddleware<TState> : IMiddleware<TState>, IAsyncDisposable
    where TState : notnull
{
    private IJSRuntime? _jsRuntime;
    private readonly IServiceProvider _serviceProvider;
    private readonly string _storeName;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<DevToolsMiddleware<TState>>? _logger;
    private IJSObjectReference? _devToolsModule;
    private bool _initialized;
    private bool _initializationFailed;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    /// <summary>
    /// Initializes a new instance with lazy IJSRuntime resolution.
    /// Works in all render modes: Server, WebAssembly, and Auto.
    /// </summary>
    /// <param name="serviceProvider">Service provider to resolve IJSRuntime on-demand.</param>
    /// <param name="storeName">The name of the store for DevTools display.</param>
    /// <param name="logger">Optional logger for error reporting.</param>
    public DevToolsMiddleware(
        IServiceProvider serviceProvider,
        string storeName = "Store",
        ILogger<DevToolsMiddleware<TState>>? logger = null)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _storeName = storeName;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized || _initializationFailed)
            return;

        await _initLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_initialized || _initializationFailed)
                return;

            try
            {
                // Lazy resolve IJSRuntime from service provider
                _jsRuntime ??= _serviceProvider.GetService(typeof(IJSRuntime)) as IJSRuntime;
                if (_jsRuntime == null)
                {
                    _logger?.LogWarning("IJSRuntime not available in service provider for store: {StoreName}", _storeName);
                    _initializationFailed = true;
                    return;
                }

                _devToolsModule = await _jsRuntime.InvokeAsync<IJSObjectReference>(
                    "import", "./_content/EasyAppDev.Blazor.Store/devtools.js")
                    .ConfigureAwait(false);

                await _devToolsModule.InvokeVoidAsync("initDevTools", _storeName)
                    .ConfigureAwait(false);

                _initialized = true;
                _logger?.LogInformation("Redux DevTools initialized for store: {StoreName}", _storeName);
            }
            catch (Exception ex)
            {
                // DevTools not available, mark as failed and silently continue
                _initializationFailed = true;
                _logger?.LogWarning(ex, "Redux DevTools not available for store: {StoreName}. This is expected in Blazor Server or during prerendering.", _storeName);
            }
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task OnBeforeUpdateAsync(TState currentState, string? action)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task OnAfterUpdateAsync(
        TState previousState,
        TState currentState,
        string? action)
    {
        if (!_initialized || _devToolsModule == null)
            return;

        try
        {
            var actionName = action ?? "UPDATE_STATE";
            var stateJson = JsonSerializer.Serialize(currentState, _jsonOptions);

            await _devToolsModule.InvokeVoidAsync(
                "sendToDevTools",
                actionName,
                stateJson).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error sending action {Action} to DevTools for store: {StoreName}", action, _storeName);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_devToolsModule != null)
        {
            try
            {
                await _devToolsModule.DisposeAsync().ConfigureAwait(false);
            }
            catch
            {
                // Ignore disposal errors
            }
        }

        _initLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
