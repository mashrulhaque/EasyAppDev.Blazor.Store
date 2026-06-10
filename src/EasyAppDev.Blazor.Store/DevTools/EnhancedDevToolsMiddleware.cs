// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using System.Diagnostics;
using System.Text.Json;
using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Middleware;
using EasyAppDev.Blazor.Store.Security;

namespace EasyAppDev.Blazor.Store.DevTools;

/// <summary>
/// Enhanced middleware for Redux DevTools with full time-travel debugging,
/// action replay, state editing, and performance tracing.
/// <para>
/// Activation is gated at RUNTIME via <see cref="DevToolsOptions{TState}.Enabled"/>:
/// by default DevTools are only active when a debugger is attached, but they can be
/// explicitly enabled or disabled. The implementation is always compiled, so the
/// published package contains a working middleware (previously it was a dead stub
/// in Release builds).
/// </para>
/// </summary>
/// <typeparam name="TState">The type of state managed by the store.</typeparam>
public class EnhancedDevToolsMiddleware<TState> : IMiddleware<TState>, IStoreAwareMiddleware<TState>, IAsyncDisposable
    where TState : notnull
{
    private IJSRuntime? _jsRuntime;
    private readonly IServiceProvider _serviceProvider;
    private readonly DevToolsOptions<TState> _options;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly JsonSerializerOptions? _filteredJsonOptions;
    private readonly ILogger<EnhancedDevToolsMiddleware<TState>>? _logger;
    private readonly bool _enabled;
    private readonly List<StateHistoryEntry<TState>> _history = new();
    private IJSObjectReference? _devToolsModule;
    private DotNetObjectReference<EnhancedDevToolsMiddleware<TState>>? _dotNetRef;
    private IStore<TState>? _store;
    private bool _initialized;
    private bool _initializationFailed;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private Stopwatch? _actionStopwatch;
    private int _currentIndex = -1;

    // Time-travel bookkeeping: DevTools actionId 0 is @@INIT (the state BEFORE the
    // first recorded action), while _history[0] holds the first action's POST-state.
    // _initialState captures the @@INIT state; _trimOffset tracks how many entries
    // have been trimmed from the front of _history so DevTools actionIds can still
    // be mapped onto list indices.
    private TState? _initialState;
    private bool _hasInitialState;
    private int _trimOffset;

    private string StoreName => _options.Name ?? typeof(TState).Name;

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

        // Runtime gate: enabled when explicitly requested, otherwise only when a
        // debugger is attached.
        _enabled = _options.Enabled ?? Debugger.IsAttached;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = _options.SerializeIndented
        };

        if (!_enabled)
        {
            return;
        }

        // Create filtered JSON options if sensitive data filtering is enabled,
        // mirroring the basic DevToolsMiddleware behavior.
        if (_options.SensitiveDataFilter?.Enabled == true)
        {
            _filteredJsonOptions = SensitiveDataFilterExtensions.CreateFilteredJsonOptions(
                _options.SensitiveDataFilter);
        }
        else
        {
            _logger?.LogWarning(
                "[DevTools] SECURITY: Sensitive data filtering is DISABLED for store '{StoreName}'. " +
                "State containing passwords, tokens, or PII will be exposed in browser DevTools. " +
                "Consider enabling SensitiveDataFilter in DevToolsOptions for production safety.",
                StoreName);
        }
    }

    /// <summary>
    /// Sets the store reference for time-travel operations.
    /// </summary>
    internal void SetStore(IStore<TState> store)
    {
        _store = store;
    }

    /// <summary>
    /// Attaches the store this middleware belongs to. Called automatically by
    /// <see cref="StoreBuilder{TState}.Build"/>. Idempotent.
    /// </summary>
    /// <param name="store">The store this middleware is attached to.</param>
    public void AttachStore(IStore<TState> store)
    {
        ArgumentNullException.ThrowIfNull(store);

        if (ReferenceEquals(_store, store))
            return;

        SetStore(store);
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
                        StoreName);
                    _initializationFailed = true;
                    return;
                }

                _devToolsModule = await _jsRuntime.InvokeAsync<IJSObjectReference>(
                    "import", "./_content/EasyAppDev.Blazor.Store/devtools-enhanced.js")
                    .ConfigureAwait(false);

                _dotNetRef = DotNetObjectReference.Create(this);

                var jsOptions = new
                {
                    name = StoreName,
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
                    StoreName);
            }
            catch (InvalidOperationException ex)
            {
                // JS interop is not available yet (e.g. during Blazor Server
                // prerendering). Do NOT latch the failure flag - the next state
                // update will retry once interop becomes available.
                _logger?.LogDebug(ex,
                    "JS interop not yet available for enhanced DevTools: {StoreName} (prerendering). Will retry on next update.",
                    StoreName);
            }
            catch (Exception ex)
            {
                _initializationFailed = true;
                _logger?.LogWarning(ex,
                    "Enhanced DevTools not available for store: {StoreName}",
                    StoreName);
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
        if (!_enabled)
            return;

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
        if (!_enabled || !_initialized || _devToolsModule == null || _options.Paused)
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

            // Use filtered JSON options if sensitive data filtering is enabled
            var stateJsonOptions = _filteredJsonOptions ?? _jsonOptions;
            var stateJson = JsonSerializer.Serialize(stateToSend, stateJsonOptions);

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
                // Capture the initial (@@INIT) state: the previousState of the
                // first recorded update. DevTools actionId 0 maps to this state.
                if (!_hasInitialState)
                {
                    _initialState = previousState;
                    _hasInitialState = true;
                }

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

                // Trim history if it exceeds max, tracking the offset so DevTools
                // actionIds can still be mapped to list indices.
                while (_history.Count > _options.MaxHistory)
                {
                    _history.RemoveAt(0);
                    _trimOffset++;
                }

                _currentIndex = _history.Count - 1;
            }

            await _devToolsModule.InvokeVoidAsync(
                "sendEnhancedAction",
                StoreName,
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
                StoreName);
        }
    }

    /// <summary>
    /// Called from JavaScript when user clicks time-travel to jump to a state.
    /// </summary>
    /// <param name="actionId">The DevTools action id: 0 is the initial (@@INIT)
    /// state; id N maps to the N-th recorded action's post-state.</param>
    [JSInvokable]
    public async Task JumpToStateAsync(int actionId)
    {
        if (!_options.EnableTimeTravel || _store == null)
            return;

        TState targetState;
        int newIndex;

        if (actionId == 0)
        {
            // actionId 0 is @@INIT: restore the captured initial state
            if (!_hasInitialState)
            {
                _logger?.LogWarning("Cannot jump to initial state: no state recorded yet");
                return;
            }

            targetState = _initialState!;
            newIndex = -1;
        }
        else
        {
            // actionId N maps to _history[N - 1], adjusted by trimmed entries
            var index = actionId - 1 - _trimOffset;
            if (index < 0 || index >= _history.Count)
            {
                _logger?.LogWarning(
                    "Invalid jump actionId {ActionId} (trim offset: {TrimOffset}, history size: {Count})",
                    actionId,
                    _trimOffset,
                    _history.Count);
                return;
            }

            targetState = _history[index].State;
            newIndex = index;
        }

        try
        {
            _currentIndex = newIndex;
            _options.Paused = true; // Temporarily pause to avoid recording the jump

            await _store.UpdateAsync(_ => targetState, "@@JUMP_TO_STATE");

            _options.OnJump?.Invoke(targetState);
            _options.Paused = false;

            _logger?.LogDebug("Time-travel jump to actionId {ActionId}", actionId);
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
        _trimOffset = 0;
        _initialState = default;
        _hasInitialState = false;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_devToolsModule != null)
        {
            try
            {
                await _devToolsModule.InvokeVoidAsync("disconnect", StoreName).ConfigureAwait(false);
            }
            catch
            {
                // Ignore disconnect errors during disposal
            }

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
