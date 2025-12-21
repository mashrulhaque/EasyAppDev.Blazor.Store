// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

#if DEBUG
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using System.Text.Json;
using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Middleware;
using System.Diagnostics;

namespace EasyAppDev.Blazor.Store.DevTools;

/// <summary>
/// Enhanced middleware for Redux DevTools with full time-travel debugging,
/// action replay, state editing, and performance tracing.
/// IMPORTANT: This middleware is only available in DEBUG builds for security reasons.
/// DevTools expose your application state and should never be used in production.
/// </summary>
/// <typeparam name="TState">The type of state managed by the store.</typeparam>
public class EnhancedDevToolsMiddleware<TState> : IMiddleware<TState>, IAsyncDisposable
    where TState : notnull
{
    private IJSRuntime? _jsRuntime;
    private readonly IServiceProvider _serviceProvider;
    private readonly DevToolsOptions<TState> _options;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<EnhancedDevToolsMiddleware<TState>>? _logger;
    private readonly List<StateHistoryEntry<TState>> _history = new();
    private IJSObjectReference? _devToolsModule;
    private DotNetObjectReference<EnhancedDevToolsMiddleware<TState>>? _dotNetRef;
    private IStore<TState>? _store;
    private bool _initialized;
    private bool _initializationFailed;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private Stopwatch? _actionStopwatch;
    private int _currentIndex = -1;

    /// <summary>
    /// Initializes a new instance with enhanced DevTools options.
    /// </summary>
    public EnhancedDevToolsMiddleware(
        IServiceProvider serviceProvider,
        DevToolsOptions<TState>? options = null,
        ILogger<EnhancedDevToolsMiddleware<TState>>? logger = null)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _options = options ?? DevToolsOptions<TState>.Default();
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = _options.SerializeIndented
        };
    }

    /// <summary>
    /// Sets the store reference for time-travel operations.
    /// </summary>
    internal void SetStore(IStore<TState> store)
    {
        _store = store;
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
                _jsRuntime ??= _serviceProvider.GetService(typeof(IJSRuntime)) as IJSRuntime;
                if (_jsRuntime == null)
                {
                    _logger?.LogWarning(
                        "IJSRuntime not available for enhanced DevTools: {StoreName}",
                        _options.Name);
                    _initializationFailed = true;
                    return;
                }

                _devToolsModule = await _jsRuntime.InvokeAsync<IJSObjectReference>(
                    "import", "./_content/EasyAppDev.Blazor.Store/devtools-enhanced.js")
                    .ConfigureAwait(false);

                _dotNetRef = DotNetObjectReference.Create(this);

                var jsOptions = new
                {
                    name = _options.Name ?? typeof(TState).Name,
                    maxAge = _options.MaxHistory,
                    features = new
                    {
                        jump = _options.EnableTimeTravel,
                        skip = _options.EnableTimeTravel,
                        dispatch = _options.EnableActionReplay,
                        persist = true,
                        export = true,
                        import = _options.EnableStateEditing
                    }
                };

                await _devToolsModule.InvokeVoidAsync(
                    "initEnhancedDevTools",
                    JsonSerializer.Serialize(jsOptions, _jsonOptions),
                    _dotNetRef)
                    .ConfigureAwait(false);

                _initialized = true;
                _logger?.LogInformation(
                    "Enhanced Redux DevTools initialized for store: {StoreName}",
                    _options.Name);
            }
            catch (Exception ex)
            {
                _initializationFailed = true;
                _logger?.LogWarning(ex,
                    "Enhanced DevTools not available for store: {StoreName}",
                    _options.Name);
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

        if (_options.TracePerformance)
        {
            _actionStopwatch = Stopwatch.StartNew();
        }
    }

    /// <inheritdoc />
    public async Task OnAfterUpdateAsync(
        TState previousState,
        TState currentState,
        string? action)
    {
        if (!_initialized || _devToolsModule == null || _options.Paused)
            return;

        var actionName = action ?? "UPDATE_STATE";

        // Check if action should be filtered
        if (_options.IgnoredActions.Contains(actionName))
            return;

        if (_options.ActionFilter != null && !_options.ActionFilter(actionName))
            return;

        try
        {
            // Sanitize state if sanitizer is provided
            var stateToSend = _options.StateSanitizer != null
                ? _options.StateSanitizer(currentState)
                : currentState;

            var stateJson = JsonSerializer.Serialize(stateToSend, _jsonOptions);

            // Transform action if transformer is provided
            object actionToSend = _options.ActionTransformer != null
                ? _options.ActionTransformer(actionName)
                : new { type = actionName };

            var actionJson = JsonSerializer.Serialize(actionToSend, _jsonOptions);

            // Calculate performance metrics
            var performanceInfo = new Dictionary<string, object>();
            if (_options.TracePerformance && _actionStopwatch != null)
            {
                _actionStopwatch.Stop();
                performanceInfo["duration"] = _actionStopwatch.ElapsedMilliseconds;
                performanceInfo["timestamp"] = DateTime.UtcNow.ToString("o");
            }

            // Add to local history
            if (_options.EnableTimeTravel)
            {
                // Truncate history if we jumped back and made a new action
                if (_currentIndex >= 0 && _currentIndex < _history.Count - 1)
                {
                    _history.RemoveRange(_currentIndex + 1, _history.Count - _currentIndex - 1);
                }

                _history.Add(new StateHistoryEntry<TState>
                {
                    State = currentState,
                    Action = actionName,
                    Timestamp = DateTime.UtcNow
                });

                // Trim history if it exceeds max
                while (_history.Count > _options.MaxHistory)
                {
                    _history.RemoveAt(0);
                }

                _currentIndex = _history.Count - 1;
            }

            await _devToolsModule.InvokeVoidAsync(
                "sendEnhancedAction",
                actionJson,
                stateJson,
                performanceInfo.Count > 0 ? JsonSerializer.Serialize(performanceInfo, _jsonOptions) : null)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex,
                "Error sending action {Action} to enhanced DevTools for store: {StoreName}",
                action,
                _options.Name);
        }
    }

    /// <summary>
    /// Called from JavaScript when user clicks time-travel to jump to a state.
    /// </summary>
    [JSInvokable]
    public async Task JumpToStateAsync(int index)
    {
        if (!_options.EnableTimeTravel || _store == null)
            return;

        if (index < 0 || index >= _history.Count)
        {
            _logger?.LogWarning("Invalid jump index: {Index}", index);
            return;
        }

        try
        {
            var targetState = _history[index].State;
            _currentIndex = index;
            _options.Paused = true; // Temporarily pause to avoid recording the jump

            await _store.UpdateAsync(_ => targetState, "@@JUMP_TO_STATE");

            _options.OnJump?.Invoke(targetState);
            _options.Paused = false;

            _logger?.LogDebug("Time-travel jump to state {Index}", index);
        }
        catch (Exception ex)
        {
            _options.Paused = false;
            _logger?.LogError(ex, "Error during time-travel jump");
        }
    }

    /// <summary>
    /// Called from JavaScript when user wants to replay an action.
    /// </summary>
    [JSInvokable]
    public void ReplayAction(string actionName)
    {
        if (!_options.EnableActionReplay)
            return;

        _options.OnActionReplay?.Invoke(actionName);
        _logger?.LogDebug("Action replay requested: {Action}", actionName);
    }

    /// <summary>
    /// Called from JavaScript when user imports a state.
    /// </summary>
    [JSInvokable]
    public async Task ImportStateAsync(string stateJson)
    {
        if (!_options.EnableStateEditing || _store == null)
            return;

        try
        {
            var importedState = JsonSerializer.Deserialize<TState>(stateJson, _jsonOptions);
            if (importedState == null)
            {
                _logger?.LogWarning("Failed to deserialize imported state");
                return;
            }

            await _store.UpdateAsync(_ => importedState, "@@IMPORT_STATE");
            _options.OnStateImport?.Invoke(importedState);
            _logger?.LogInformation("State imported from DevTools");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error importing state from DevTools");
        }
    }

    /// <summary>
    /// Exports the current state history.
    /// </summary>
    public IReadOnlyList<StateHistoryEntry<TState>> GetHistory() => _history.AsReadOnly();

    /// <summary>
    /// Clears the state history.
    /// </summary>
    public void ClearHistory()
    {
        _history.Clear();
        _currentIndex = -1;
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

        _dotNetRef?.Dispose();
        _initLock.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Represents a single entry in the state history.
/// </summary>
/// <typeparam name="TState">The type of state.</typeparam>
public class StateHistoryEntry<TState>
{
    /// <summary>
    /// Gets or sets the state at this point in history.
    /// </summary>
    public required TState State { get; set; }

    /// <summary>
    /// Gets or sets the action that produced this state.
    /// </summary>
    public required string Action { get; set; }

    /// <summary>
    /// Gets or sets when this entry was created.
    /// </summary>
    public DateTime Timestamp { get; set; }
}

#else

using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Middleware;

namespace EasyAppDev.Blazor.Store.DevTools;

/// <summary>
/// No-op EnhancedDevTools middleware stub for Release builds.
/// DevTools are disabled in production for security reasons.
/// </summary>
/// <typeparam name="TState">The type of state managed by the store.</typeparam>
public class EnhancedDevToolsMiddleware<TState> : IMiddleware<TState>, IAsyncDisposable
    where TState : notnull
{
    /// <summary>
    /// No-op constructor for Release builds.
    /// </summary>
    public EnhancedDevToolsMiddleware(
        IServiceProvider serviceProvider,
        object? options = null,
        object? logger = null)
    {
    }

    internal void SetStore(IStore<TState> store) { }

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

/// <summary>
/// State history entry stub for Release builds.
/// </summary>
/// <typeparam name="TState">The type of state.</typeparam>
public class StateHistoryEntry<TState>
{
    /// <summary>Gets or sets the state.</summary>
    public required TState State { get; set; }
    /// <summary>Gets or sets the action name.</summary>
    public required string Action { get; set; }
    /// <summary>Gets or sets the timestamp.</summary>
    public DateTime Timestamp { get; set; }
}

#endif
