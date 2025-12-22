using System.Text.Json;
using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Middleware;
using EasyAppDev.Blazor.Store.Security;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<TabSyncMiddleware<TState>>? _logger;
    private readonly IStateValidator<object>? _resolvedValidator;

    private IJSRuntime? _jsRuntime;
    private IJSObjectReference? _jsModule;
    private DotNetObjectReference<TabSyncMiddleware<TState>>? _dotNetRef;
    private IStore<TState>? _store;
    private string? _channelName;
    private int _syncUpdateCount; // Thread-safe counter for concurrent sync operations
    private bool _isInitialized;
    private bool _jsModuleLoaded;
    private CancellationTokenSource? _debounceCts;
    private MessageSigner? _messageSigner;

    /// <summary>
    /// Creates a new tab sync middleware.
    /// </summary>
    /// <param name="serviceProvider">Service provider for lazy IJSRuntime resolution.</param>
    /// <param name="options">Sync configuration options.</param>
    public TabSyncMiddleware(IServiceProvider serviceProvider, TabSyncOptions? options = null)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _options = options ?? new TabSyncOptions();
        _logger = serviceProvider.GetService(typeof(ILogger<TabSyncMiddleware<TState>>)) as ILogger<TabSyncMiddleware<TState>>;

        // Resolve validator from DI if not explicitly configured
        _resolvedValidator = ResolveValidator(serviceProvider);

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            MaxDepth = _options.MaxJsonDepth // Prevent stack overflow from deeply nested payloads
        };
    }

    /// <summary>
    /// Resolves the state validator, preferring explicit configuration over DI resolution.
    /// </summary>
    private IStateValidator<object>? ResolveValidator(IServiceProvider serviceProvider)
    {
        // Use explicitly configured validator first
        if (_options.StateValidator != null)
        {
            return _options.StateValidator;
        }

        // Try to resolve typed validator from DI
        var typedValidator = serviceProvider.GetService(typeof(IStateValidator<TState>)) as IStateValidator<TState>;
        if (typedValidator != null)
        {
            _logger?.LogDebug("[TabSync] Resolved IStateValidator<{StateType}> from DI", typeof(TState).Name);
            return new StateValidatorWrapper<TState>(typedValidator);
        }

        return null;
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

            // Initialize message signer if signing is enabled
            if (_options.EnableMessageSigning)
            {
                await InitializeMessageSignerAsync().ConfigureAwait(false);
            }

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

    private async Task InitializeMessageSignerAsync()
    {
        if (_messageSigner != null)
            return;

        byte[]? signingKey = null;

        // Priority 1: Use explicitly configured signing key
        if (_options.SigningKey != null && _options.SigningKey.Length >= 32)
        {
            signingKey = _options.SigningKey;
        }
        // Priority 2: Derive key from origin if configured
        else if (_options.DeriveKeyFromOrigin && _jsRuntime != null)
        {
            try
            {
                var origin = await _jsRuntime.InvokeAsync<string>("__getTabSyncKeyMaterial").ConfigureAwait(false);
                if (!string.IsNullOrEmpty(origin))
                {
                    signingKey = MessageSigner.DeriveKeyFromSeed(origin);
                    _logger?.LogDebug("[TabSync] Derived signing key from origin: {Origin}", origin);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "[TabSync] Failed to derive key from origin, falling back to random key");
            }
        }

        // Priority 3: Fallback to random key (will cause verification failures across tabs)
        if (signingKey != null)
        {
            _messageSigner = new MessageSigner(signingKey);
        }
        else
        {
            _messageSigner = new MessageSigner();
            _logger?.LogWarning(
                "[TabSync] Using random signing key. Message verification will fail across tabs. " +
                "Set TabSyncOptions.SigningKey or TabSyncOptions.DeriveKeyFromOrigin = true for cross-tab verification.");
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
            // Validate message size before deserialization to prevent DoS
            if (_options.MaxMessageSizeBytes > 0)
            {
                var messageSize = System.Text.Encoding.UTF8.GetByteCount(messageJson);
                if (messageSize > _options.MaxMessageSizeBytes)
                {
                    _logger?.LogWarning(
                        "[TabSync] Message rejected: size {Size} bytes exceeds limit of {Limit} bytes",
                        messageSize,
                        _options.MaxMessageSizeBytes);
                    _options.OnMessageSizeExceeded?.Invoke(messageSize);
                    _options.OnSyncError?.Invoke(new InvalidOperationException(
                        $"Message size ({messageSize} bytes) exceeds maximum allowed ({_options.MaxMessageSizeBytes} bytes)"));
                    return;
                }
            }

            SyncMessage? message;
            try
            {
                message = JsonSerializer.Deserialize<SyncMessage>(messageJson, _jsonOptions);
            }
            catch (JsonException ex)
            {
                _logger?.LogWarning(
                    "[TabSync] Message deserialization failed: {Error}. Possible deeply nested or malformed JSON.",
                    ex.Message);
                _options.OnSyncError?.Invoke(new InvalidOperationException(
                    $"Failed to deserialize message: {ex.Message}. This may indicate a malformed payload or deeply nested structure exceeding MaxJsonDepth ({_options.MaxJsonDepth}).", ex));
                return;
            }

            if (message == null || message.TabId == _tabId) return;

            // Verify message signature if signing is enabled
            if (_options.EnableMessageSigning && _messageSigner != null)
            {
                if (string.IsNullOrEmpty(message.Signature))
                {
                    if (_options.RequireValidSignature)
                    {
                        _options.OnInvalidSignature?.Invoke("Missing signature");
                        return;
                    }
                }
                else
                {
                    // Reconstruct the signed content (message without signature)
                    var signedContent = $"{message.TabId}:{message.Action}:{message.State}:{message.Timestamp:O}";
                    if (!_messageSigner.Verify(signedContent, message.Signature))
                    {
                        _options.OnInvalidSignature?.Invoke("Invalid signature");
                        if (_options.RequireValidSignature)
                        {
                            return;
                        }
                    }
                }
            }

            // Validate message timestamp to prevent replay attacks and future-dated messages
            if (_options.ValidateTimestamp && _options.MaxMessageAgeSeconds > 0)
            {
                var messageAge = DateTime.UtcNow - message.Timestamp;

                // Check for messages too old (replay attack prevention)
                if (messageAge.TotalSeconds > _options.MaxMessageAgeSeconds)
                {
                    _logger?.LogWarning(
                        "[TabSync] Message rejected: too old ({Age:F1}s > max {Max}s)",
                        messageAge.TotalSeconds,
                        _options.MaxMessageAgeSeconds);
                    _options.OnSyncError?.Invoke(new InvalidOperationException(
                        $"Message too old: {messageAge.TotalSeconds:F1}s (max: {_options.MaxMessageAgeSeconds}s)"));
                    return;
                }

                // Check for future-dated messages (clock manipulation prevention)
                if (messageAge.TotalSeconds < -_options.ClockSkewToleranceSeconds)
                {
                    _logger?.LogWarning(
                        "[TabSync] Message rejected: future timestamp ({Age:F1}s in future, tolerance: {Tolerance}s)",
                        -messageAge.TotalSeconds,
                        _options.ClockSkewToleranceSeconds);
                    _options.OnSyncError?.Invoke(new InvalidOperationException(
                        $"Message has future timestamp: {-messageAge.TotalSeconds:F1}s in future (tolerance: {_options.ClockSkewToleranceSeconds}s). " +
                        "Possible clock manipulation attempt."));
                    return;
                }
            }

            _options.OnSyncReceived?.Invoke(message.Action);

            if (_options.SyncFullState && message.State != null)
            {
                var state = JsonSerializer.Deserialize<TState>(message.State, _jsonOptions);
                if (state != null)
                {
                    // Validate state before applying if validator is configured or resolved from DI
                    if (_resolvedValidator != null)
                    {
                        var validationResult = ValidateState(state);
                        if (!validationResult.IsValid)
                        {
                            _logger?.LogWarning(
                                "[TabSync] State validation failed: {Errors}",
                                string.Join(", ", validationResult.Errors));

                            _options.OnValidationFailed?.Invoke(validationResult with { Source = "TabSync" });

                            if (_options.RejectInvalidState)
                            {
                                return;
                            }
                        }
                    }

                    Interlocked.Increment(ref _syncUpdateCount);
                    try
                    {
                        await _store.UpdateAsync(_ => state, $"SYNC_{message.Action ?? "UPDATE"}").ConfigureAwait(false);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref _syncUpdateCount);
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
        // Don't broadcast updates received from other tabs (thread-safe check)
        if (Volatile.Read(ref _syncUpdateCount) > 0) return;

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

            var stateJson = _options.SyncFullState
                ? JsonSerializer.Serialize(currentState, _jsonOptions)
                : null;
            var timestamp = DateTime.UtcNow;

            var message = new SyncMessage
            {
                TabId = _tabId,
                Action = action,
                State = stateJson,
                Timestamp = timestamp
            };

            // Sign the message if signing is enabled
            if (_options.EnableMessageSigning && _messageSigner != null)
            {
                var signedContent = $"{_tabId}:{action}:{stateJson}:{timestamp:O}";
                message.Signature = _messageSigner.Sign(signedContent);
            }

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
        _messageSigner?.Dispose();
    }

    /// <summary>
    /// Validates state using the configured or DI-resolved validator.
    /// </summary>
    private StateValidationResult ValidateState(TState state)
    {
        if (_resolvedValidator == null)
        {
            return StateValidationResult.Success();
        }

        try
        {
            return _resolvedValidator.Validate(state!);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[TabSync] State validator threw exception");
            return StateValidationResult.Failure($"Validator threw exception: {ex.Message}");
        }
    }

    private sealed class SyncMessage
    {
        public string? TabId { get; set; }
        public string? Action { get; set; }
        public string? State { get; set; }
        public DateTime Timestamp { get; set; }
        public string? Signature { get; set; }
    }
}
