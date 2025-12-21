#if DEBUG
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using System.Text.Json;
using EasyAppDev.Blazor.Store.Middleware;
using EasyAppDev.Blazor.Store.Security;

namespace EasyAppDev.Blazor.Store.DevTools;

/// <summary>
/// Middleware that integrates with Redux DevTools browser extension.
/// Uses lazy IJSRuntime resolution via IServiceProvider for compatibility
/// with Blazor Server, WebAssembly, and Auto render modes.
/// IMPORTANT: This middleware is only available in DEBUG builds for security reasons.
/// DevTools expose your application state and should never be used in production.
/// </summary>
/// <typeparam name="TState">The type of state managed by the store.</typeparam>
public class DevToolsMiddleware<TState> : IMiddleware<TState>, IAsyncDisposable
    where TState : notnull
{
    private IJSRuntime? _jsRuntime;
    private readonly IServiceProvider _serviceProvider;
    private readonly string _storeName;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly JsonSerializerOptions? _filteredJsonOptions;
    private readonly DevToolsOptions<TState>? _options;
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
        : this(serviceProvider, storeName, null, logger)
    {
    }

    /// <summary>
    /// Initializes a new instance with options for sensitive data filtering.
    /// </summary>
    /// <param name="serviceProvider">Service provider to resolve IJSRuntime on-demand.</param>
    /// <param name="storeName">The name of the store for DevTools display.</param>
    /// <param name="options">DevTools configuration options.</param>
    /// <param name="logger">Optional logger for error reporting.</param>
    public DevToolsMiddleware(
        IServiceProvider serviceProvider,
        string storeName,
        DevToolsOptions<TState>? options,
        ILogger<DevToolsMiddleware<TState>>? logger = null)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _storeName = storeName;
        _options = options;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = options?.SerializeIndented ?? false
        };

        // Create filtered JSON options if sensitive data filtering is enabled
        if (options?.SensitiveDataFilter?.Enabled == true)
        {
            _filteredJsonOptions = SensitiveDataFilterExtensions.CreateFilteredJsonOptions(
                options.SensitiveDataFilter);
        }
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

        // Check if action should be filtered
        if (_options?.ActionFilter != null && action != null && !_options.ActionFilter(action))
            return;

        // Check if action is in ignored list
        if (_options?.IgnoredActions != null && action != null && _options.IgnoredActions.Contains(action))
            return;

        // Check if paused
        if (_options?.Paused == true)
            return;

        try
        {
            var actionName = action ?? "UPDATE_STATE";

            // Apply state sanitizer if configured
            var stateToSerialize = _options?.StateSanitizer != null
                ? _options.StateSanitizer(currentState)
                : currentState;

            // Use filtered JSON options if sensitive data filtering is enabled
            var jsonOptions = _filteredJsonOptions ?? _jsonOptions;
            var stateJson = JsonSerializer.Serialize(stateToSerialize, jsonOptions);

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

#else

using EasyAppDev.Blazor.Store.Middleware;

namespace EasyAppDev.Blazor.Store.DevTools;

/// <summary>
/// No-op DevTools middleware stub for Release builds.
/// DevTools are disabled in production for security reasons.
/// </summary>
/// <typeparam name="TState">The type of state managed by the store.</typeparam>
public class DevToolsMiddleware<TState> : IMiddleware<TState>, IAsyncDisposable
    where TState : notnull
{
    /// <summary>
    /// No-op constructor for Release builds.
    /// </summary>
    public DevToolsMiddleware(
        IServiceProvider serviceProvider,
        string storeName = "Store",
        object? logger = null)
    {
    }

    /// <summary>
    /// No-op constructor for Release builds.
    /// </summary>
    public DevToolsMiddleware(
        IServiceProvider serviceProvider,
        string storeName,
        object? options,
        object? logger = null)
    {
    }

    /// <inheritdoc />
    public Task OnBeforeUpdateAsync(TState currentState, string? action) => Task.CompletedTask;

    /// <inheritdoc />
    public Task OnAfterUpdateAsync(TState previousState, TState currentState, string? action) => Task.CompletedTask;

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}

#endif
