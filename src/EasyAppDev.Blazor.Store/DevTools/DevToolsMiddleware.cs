using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using System.Text.Json;
using EasyAppDev.Blazor.Store.Middleware;

namespace EasyAppDev.Blazor.Store.DevTools;

/// <summary>
/// Middleware that integrates with Redux DevTools browser extension.
/// </summary>
/// <typeparam name="TState">The type of state managed by the store.</typeparam>
public class DevToolsMiddleware<TState> : IMiddleware<TState>, IAsyncDisposable
    where TState : notnull
{
    private readonly IJSRuntime _jsRuntime;
    private readonly string _storeName;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<DevToolsMiddleware<TState>>? _logger;
    private IJSObjectReference? _devToolsModule;
    private bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="DevToolsMiddleware{TState}"/> class.
    /// </summary>
    /// <param name="jsRuntime">The JS runtime for interop.</param>
    /// <param name="storeName">The name of the store for DevTools display.</param>
    /// <param name="logger">Optional logger for error reporting.</param>
    public DevToolsMiddleware(
        IJSRuntime jsRuntime,
        string storeName = "Store",
        ILogger<DevToolsMiddleware<TState>>? logger = null)
    {
        _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
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
        if (_initialized)
            return;

        await _initLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_initialized)
                return;

            try
            {
                _devToolsModule = await _jsRuntime.InvokeAsync<IJSObjectReference>(
                    "import", "./_content/EasyAppDev.Blazor.Store/devtools.js")
                    .ConfigureAwait(false);

                await _devToolsModule.InvokeVoidAsync("initDevTools", _storeName)
                    .ConfigureAwait(false);

                _initialized = true;
            }
            catch (Exception ex)
            {
                // DevTools not available, silently continue
                _logger?.LogWarning(ex, "Redux DevTools not available for store: {StoreName}", _storeName);
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
