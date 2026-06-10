// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Middleware;
using EasyAppDev.Blazor.Store.Security;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace EasyAppDev.Blazor.Store.ServerSync;

/// <summary>
/// Middleware that synchronizes state with a server via SignalR.
/// Supports full state sync, operation-based sync, cursor tracking, and offline queuing.
/// </summary>
/// <typeparam name="TState">The type of state.</typeparam>
public class ServerSyncMiddleware<TState> : IMiddleware<TState>, IStoreAwareMiddleware<TState>, IAsyncDisposable
    where TState : notnull
{
    private readonly ServerSyncOptions<TState> _options;
    private readonly ILogger<ServerSyncMiddleware<TState>>? _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly List<PresenceInfo> _presenceList = new();
    private readonly Dictionary<string, CursorInfo> _cursors = new();
    private readonly Queue<StateUpdate> _offlineQueue = new();
    private readonly Queue<StateOperation> _offlineOperationQueue = new();
    private readonly MessageSigner? _messageSigner;
    private readonly Queue<DateTime> _messageTimestamps = new();

    private HubConnection? _hubConnection;
    private IStore<TState>? _store;
    private SyncConnectionState _connectionState = SyncConnectionState.Disconnected;
    private long _currentVersion;
    private bool _isReceivingUpdate;
    private Timer? _debounceTimer;
    private Timer? _cursorDebounceTimer;
    private StateUpdate? _pendingUpdate;
    private CursorInfo? _pendingCursor;
    private TState? _previousState;
    private readonly object _syncLock = new();
    private readonly object _cursorLock = new();
    private readonly object _rateLimitLock = new();
    private string? _sessionToken;
    private DateTime _sessionCreatedAt;

    /// <summary>
    /// Creates a new server sync middleware.
    /// </summary>
    public ServerSyncMiddleware(
        ServerSyncOptions<TState> options,
        ILogger<ServerSyncMiddleware<TState>>? logger = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;

        // Validate security configuration
        if (_options.RequireValidation && _options.StateValidator == null)
        {
            throw new InvalidOperationException(
                "ServerSync requires a StateValidator to be configured for security. " +
                "Either provide a StateValidator or set RequireValidation to false (not recommended for production). " +
                "Configure via: options.StateValidator = new YourValidator()");
        }

        // Initialize message signer if enabled
        if (_options.EnableMessageSigning)
        {
            _messageSigner = _options.SigningKey != null
                ? new MessageSigner(_options.SigningKey)
                : new MessageSigner();

            _logger?.LogInformation(
                "ServerSync message signing enabled. Using {KeyType} key.",
                _options.SigningKey != null ? "provided" : "generated");
        }

        // Configure JSON options with optional sensitive data filtering
        if (_options.FilterSensitiveData)
        {
            _jsonOptions = SensitiveDataFilterExtensions.CreateFilteredJsonOptions(
                new SensitiveDataFilterOptions { Enabled = true });
            _jsonOptions.MaxDepth = _options.MaxJsonDepth; // Prevent stack overflow from deeply nested payloads
        }
        else
        {
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                MaxDepth = _options.MaxJsonDepth // Prevent stack overflow from deeply nested payloads
            };
        }

        _logger?.LogInformation(
            "ServerSync middleware initialized. Validation: {Validation}, Rate Limit: {RateLimit}/s, Max Size: {MaxSize} bytes, Signing: {Signing}",
            _options.StateValidator != null ? "Enabled" : "Disabled",
            _options.RateLimitPerSecond,
            _options.MaxMessageSize,
            _options.EnableMessageSigning);
    }

    /// <summary>
    /// Gets the current connection state.
    /// </summary>
    public SyncConnectionState ConnectionState => _connectionState;

    /// <summary>
    /// Gets the current presence list.
    /// </summary>
    public IReadOnlyList<PresenceInfo> Presence => _presenceList.AsReadOnly();

    /// <summary>
    /// Gets the current cursors from other users.
    /// </summary>
    public IReadOnlyDictionary<string, CursorInfo> Cursors
    {
        get { lock (_cursorLock) return new Dictionary<string, CursorInfo>(_cursors); }
    }

    /// <summary>
    /// Gets the number of items in the offline queue.
    /// </summary>
    public int OfflineQueueCount
    {
        get { lock (_syncLock) return _offlineQueue.Count + _offlineOperationQueue.Count; }
    }

    /// <summary>
    /// Sets the store reference for receiving updates.
    /// </summary>
    internal void SetStore(IStore<TState> store)
    {
        _store = store;
    }

    /// <summary>
    /// Attaches the store this middleware belongs to. Called automatically by
    /// <see cref="StoreBuilder{TState}.Build"/>. Idempotent: a second call with
    /// the same store is a no-op. When <see cref="ServerSyncOptions{TState}.AutoConnect"/>
    /// is true and a HubUrl is configured, the connection is started in the background.
    /// </summary>
    /// <param name="store">The store this middleware is attached to.</param>
    public void AttachStore(IStore<TState> store)
    {
        ArgumentNullException.ThrowIfNull(store);

        if (ReferenceEquals(_store, store))
            return;

        SetStore(store);

        if (_options.AutoConnect && !string.IsNullOrEmpty(_options.HubUrl))
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await ConnectAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // ConnectAsync already invokes OnError and logs; this guard prevents
                    // the background task from surfacing an unobserved exception.
                    _logger?.LogWarning(ex, "Auto-connect to server sync hub failed: {Url}", _options.HubUrl);
                }
            });
        }
    }

    /// <summary>
    /// Connects to the server hub. If a previous connection exists but has been
    /// permanently closed, it is disposed and a fresh connection is established.
    /// </summary>
    public async Task ConnectAsync()
    {
        if (_hubConnection != null)
        {
            // Already connected, connecting, or reconnecting - nothing to do
            if (_hubConnection.State != HubConnectionState.Disconnected)
                return;

            // The previous connection was permanently closed (e.g., reconnect
            // attempts exhausted). Dispose it and build a fresh connection.
            var staleConnection = _hubConnection;
            _hubConnection = null;
            try
            {
                await staleConnection.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Error disposing stale hub connection before reconnect");
            }
        }

        try
        {
            SetConnectionState(SyncConnectionState.Connecting);

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(_options.HubUrl)
                .WithAutomaticReconnect(_options.AutoReconnect ? _options.ReconnectDelays : Array.Empty<TimeSpan>())
                .Build();

            // Register handlers
            _hubConnection.On<StateUpdate>("ReceiveUpdate", HandleReceiveUpdate);
            _hubConnection.On<StateOperation>("ReceiveOperation", HandleReceiveOperation);
            _hubConnection.On<string, long, string?>("ReceiveFullState", HandleReceiveFullState);
            _hubConnection.On<PresenceInfo>("UserJoined", HandleUserJoined);
            _hubConnection.On<PresenceInfo>("UserLeft", HandleUserLeft);
            _hubConnection.On<IReadOnlyList<PresenceInfo>>("PresenceUpdated", HandlePresenceUpdated);
            _hubConnection.On<CursorInfo>("CursorUpdated", HandleCursorUpdated);
            _hubConnection.On<string>("CursorRemoved", HandleCursorRemoved);

            _hubConnection.Closed += OnConnectionClosed;
            _hubConnection.Reconnecting += OnReconnecting;
            _hubConnection.Reconnected += OnReconnected;

            await _hubConnection.StartAsync().ConfigureAwait(false);
            SetConnectionState(SyncConnectionState.Connected);

            // Generate session token for this connection
            _sessionToken = GenerateSessionToken();
            _sessionCreatedAt = DateTime.UtcNow;

            // Join document if specified
            if (_options.DocumentId != null)
            {
                await _hubConnection.InvokeAsync(
                    "JoinDocument",
                    _options.DocumentId,
                    _options.UserDisplayName,
                    _sessionToken)
                    .ConfigureAwait(false);
            }

            // Flush offline queue on connect
            await FlushOfflineQueueAsync().ConfigureAwait(false);

            _logger?.LogInformation("Connected to server sync hub: {Url}", _options.HubUrl);
        }
        catch (Exception ex)
        {
            SetConnectionState(SyncConnectionState.Disconnected);
            _options.OnError?.Invoke(ex);
            _logger?.LogError(ex, "Failed to connect to server sync hub");
            throw;
        }
    }

    /// <summary>
    /// Disconnects from the server hub.
    /// </summary>
    public async Task DisconnectAsync()
    {
        if (_hubConnection == null)
            return;

        try
        {
            if (_options.DocumentId != null && _hubConnection.State == HubConnectionState.Connected)
            {
                await _hubConnection.InvokeAsync("LeaveDocument", _options.DocumentId)
                    .ConfigureAwait(false);
            }

            await _hubConnection.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error during disconnect");
        }
        finally
        {
            _hubConnection = null;
            SetConnectionState(SyncConnectionState.Disconnected);
        }
    }

    /// <summary>
    /// Updates the local cursor position and broadcasts to other clients.
    /// </summary>
    /// <param name="cursor">The cursor information to broadcast.</param>
    public void UpdateCursor(CursorInfo cursor)
    {
        if (!_options.EnableCursorTracking)
            return;

        cursor.DocumentId = _options.DocumentId;
        cursor.DisplayName = _options.UserDisplayName;
        cursor.Color = _options.UserCursorColor;
        cursor.Timestamp = DateTime.UtcNow;

        lock (_cursorLock)
        {
            _pendingCursor = cursor;

            _cursorDebounceTimer?.Dispose();
            _cursorDebounceTimer = new Timer(
                _ => SendPendingCursor(),
                null,
                _options.CursorDebounce,
                Timeout.InfiniteTimeSpan);
        }
    }

    /// <inheritdoc />
    public Task OnBeforeUpdateAsync(TState currentState, string? action)
    {
        _previousState = currentState;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnAfterUpdateAsync(TState previousState, TState currentState, string? action)
    {
        // Don't sync if we're receiving an update
        if (_isReceivingUpdate)
            return Task.CompletedTask;

        // Don't sync excluded actions
        if (action != null && _options.ExcludedActions.Contains(action))
            return Task.CompletedTask;

        // Use operation-based sync if enabled
        if (_options.UseOperationBasedSync)
        {
            return HandleOperationBasedSync(previousState, currentState, action);
        }

        // Full state sync
        return HandleFullStateSync(currentState, action);
    }

    private Task HandleFullStateSync(TState currentState, string? action)
    {
        // Prepare update
        object stateToSync = _options.SyncSelector != null
            ? _options.SyncSelector(currentState)
            : currentState;

        var update = new StateUpdate
        {
            StateJson = JsonSerializer.Serialize(stateToSync, _jsonOptions),
            Action = action,
            Timestamp = DateTime.UtcNow,
            DocumentId = _options.DocumentId,
            Version = Interlocked.Increment(ref _currentVersion)
        };

        // Queue offline or schedule sync
        if (_hubConnection?.State != HubConnectionState.Connected)
        {
            if (_options.EnableOfflineQueue)
            {
                QueueOfflineUpdate(update);
            }
            return Task.CompletedTask;
        }

        ScheduleSync(update);
        return Task.CompletedTask;
    }

    private Task HandleOperationBasedSync(TState previousState, TState currentState, string? action)
    {
        IEnumerable<StateOperation> operations;

        if (_options.StateToOperations != null)
        {
            operations = _options.StateToOperations(previousState, currentState, action);
        }
        else
        {
            // Default: create a single SET operation for the whole state
            operations = new[]
            {
                StateOperation.Set(
                    "/",
                    JsonSerializer.Serialize(currentState, _jsonOptions),
                    _options.DocumentId)
            };
        }

        foreach (var operation in operations)
        {
            operation.DocumentId = _options.DocumentId;
            operation.Timestamp = DateTime.UtcNow;
            operation.Version = Interlocked.Increment(ref _currentVersion);

            if (_hubConnection?.State != HubConnectionState.Connected)
            {
                if (_options.EnableOfflineQueue)
                {
                    QueueOfflineOperation(operation);
                }
            }
            else
            {
                _ = SendOperationAsync(operation);
            }
        }

        return Task.CompletedTask;
    }

    private void QueueOfflineUpdate(StateUpdate update)
    {
        lock (_syncLock)
        {
            if (_offlineQueue.Count >= _options.MaxOfflineQueueSize)
            {
                _offlineQueue.Dequeue(); // Remove oldest
            }
            _offlineQueue.Enqueue(update);
            _logger?.LogDebug("Queued offline update. Queue size: {Size}", _offlineQueue.Count);
        }
    }

    private void QueueOfflineOperation(StateOperation operation)
    {
        lock (_syncLock)
        {
            if (_offlineOperationQueue.Count >= _options.MaxOfflineQueueSize)
            {
                _offlineOperationQueue.Dequeue(); // Remove oldest
            }
            _offlineOperationQueue.Enqueue(operation);
            _logger?.LogDebug("Queued offline operation. Queue size: {Size}", _offlineOperationQueue.Count);
        }
    }

    private async Task FlushOfflineQueueAsync()
    {
        var flushedCount = 0;

        // Take snapshots of queues while holding lock to prevent race conditions
        StateUpdate[] updates;
        StateOperation[] operations;
        lock (_syncLock)
        {
            updates = _offlineQueue.ToArray();
            _offlineQueue.Clear();
            operations = _offlineOperationQueue.ToArray();
            _offlineOperationQueue.Clear();
        }

        // Flush state updates sequentially (order matters)
        for (var i = 0; i < updates.Length; i++)
        {
            try
            {
                await SendUpdateCoreAsync(updates[i]).ConfigureAwait(false);
                flushedCount++;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to flush offline update at index {Index}", i);
                // Re-queue remaining updates (including the failed one) to preserve order
                lock (_syncLock)
                {
                    for (var j = i; j < updates.Length; j++)
                    {
                        _offlineQueue.Enqueue(updates[j]);
                    }
                }
                break;
            }
        }

        // Flush operations sequentially
        for (var i = 0; i < operations.Length; i++)
        {
            try
            {
                await SendOperationCoreAsync(operations[i]).ConfigureAwait(false);
                flushedCount++;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to flush offline operation at index {Index}", i);
                // Re-queue remaining operations to preserve order
                lock (_syncLock)
                {
                    for (var j = i; j < operations.Length; j++)
                    {
                        _offlineOperationQueue.Enqueue(operations[j]);
                    }
                }
                break;
            }
        }

        if (flushedCount > 0)
        {
            _logger?.LogInformation("Flushed {Count} items from offline queue", flushedCount);
            _options.OnOfflineQueueFlushed?.Invoke(flushedCount);
        }
    }

    private void ScheduleSync(StateUpdate update)
    {
        lock (_syncLock)
        {
            _pendingUpdate = update;

            _debounceTimer?.Dispose();
            _debounceTimer = new Timer(
                _ => SendPendingUpdate(),
                null,
                _options.SyncDebounce,
                Timeout.InfiniteTimeSpan);
        }
    }

    private async void SendPendingUpdate()
    {
        StateUpdate? update;
        lock (_syncLock)
        {
            update = _pendingUpdate;
            _pendingUpdate = null;
        }

        if (update == null || _hubConnection?.State != HubConnectionState.Connected)
            return;

        try
        {
            await SendUpdateCoreAsync(update).ConfigureAwait(false);
            _logger?.LogDebug("Sent state update: {Action}", update.Action);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to send state update");

            // Guard QueueOfflineUpdate to prevent exceptions escaping async void
            try
            {
                if (_options.EnableOfflineQueue)
                {
                    QueueOfflineUpdate(update);
                }
            }
            catch (Exception queueEx)
            {
                _logger?.LogError(queueEx, "Failed to queue offline update");
            }

            // Guard OnError callback to prevent user code exceptions escaping async void
            try
            {
                _options.OnError?.Invoke(ex);
            }
            catch (Exception callbackEx)
            {
                _logger?.LogError(callbackEx, "OnError callback threw an exception");
            }
        }
    }

    private async Task SendOperationAsync(StateOperation operation)
    {
        if (_hubConnection?.State != HubConnectionState.Connected)
        {
            if (_options.EnableOfflineQueue)
            {
                QueueOfflineOperation(operation);
            }
            return;
        }

        try
        {
            await SendOperationCoreAsync(operation).ConfigureAwait(false);
            _logger?.LogDebug("Sent operation: {Type} at {Path}", operation.OperationType, operation.Path);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to send operation");
            if (_options.EnableOfflineQueue)
            {
                QueueOfflineOperation(operation);
            }
            _options.OnError?.Invoke(ex);
        }
    }

    /// <summary>
    /// Signs (when signing is enabled) and sends a state update over the hub connection.
    /// Used by both the live send path and the offline-queue flush path so that
    /// queued updates are signed exactly like directly-sent ones.
    /// </summary>
    private async Task SendUpdateCoreAsync(StateUpdate update)
    {
        if (_options.EnableMessageSigning && _messageSigner != null)
        {
            update.Signature = _messageSigner.Sign(update.StateJson);
        }

        await _hubConnection!.InvokeAsync("SendUpdate", update).ConfigureAwait(false);
    }

    /// <summary>
    /// Signs (when signing is enabled) and sends an operation over the hub connection.
    /// Used by both the live send path and the offline-queue flush path.
    /// </summary>
    private async Task SendOperationCoreAsync(StateOperation operation)
    {
        if (_options.EnableMessageSigning && _messageSigner != null)
        {
            operation.Signature = _messageSigner.Sign(operation.GetSignaturePayload());
        }

        await _hubConnection!.InvokeAsync("SendOperation", operation).ConfigureAwait(false);
    }

    private async void SendPendingCursor()
    {
        CursorInfo? cursor;
        lock (_cursorLock)
        {
            cursor = _pendingCursor;
            _pendingCursor = null;
        }

        if (cursor == null || _hubConnection?.State != HubConnectionState.Connected)
            return;

        try
        {
            await _hubConnection.InvokeAsync("UpdateCursor", cursor).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Guard logger call to prevent exceptions escaping async void
            try
            {
                _logger?.LogDebug(ex, "Failed to send cursor update");
            }
            catch
            {
                // Swallow logging exceptions to prevent async void crash
            }
        }
    }

    private bool CheckRateLimit()
    {
        if (_options.RateLimitPerSecond <= 0)
            return true;

        lock (_rateLimitLock)
        {
            var now = DateTime.UtcNow;
            var oneSecondAgo = now.AddSeconds(-1);

            // Remove timestamps older than 1 second
            while (_messageTimestamps.Count > 0 && _messageTimestamps.Peek() < oneSecondAgo)
            {
                _messageTimestamps.Dequeue();
            }

            // Check if we're at the limit
            if (_messageTimestamps.Count >= _options.RateLimitPerSecond)
            {
                _logger?.LogWarning(
                    "Rate limit exceeded: {Count} messages in the last second (limit: {Limit})",
                    _messageTimestamps.Count,
                    _options.RateLimitPerSecond);

                _options.OnRateLimitExceeded?.Invoke(
                    $"Rate limit of {_options.RateLimitPerSecond} messages/second exceeded");

                return false;
            }

            _messageTimestamps.Enqueue(now);
            return true;
        }
    }

    private bool ValidateMessageSize(string json, string messageType)
    {
        var messageSize = System.Text.Encoding.UTF8.GetByteCount(json);

        if (messageSize > _options.MaxMessageSize)
        {
            _logger?.LogWarning(
                "{MessageType} rejected: size {Size} bytes exceeds limit of {Limit} bytes",
                messageType,
                messageSize,
                _options.MaxMessageSize);

            _options.OnMessageSizeExceeded?.Invoke(messageSize);

            return false;
        }

        return true;
    }

    private bool VerifyMessageSignature(StateUpdate update)
        => VerifySignature(update.StateJson, update.Signature, "StateUpdate");

    private bool VerifyOperationSignature(StateOperation operation)
        => VerifySignature(operation.GetSignaturePayload(), operation.Signature, "StateOperation");

    private bool VerifySignature(string payload, string? signature, string messageType)
    {
        if (!_options.EnableMessageSigning || _messageSigner == null)
            return true; // Signing not enabled

        if (string.IsNullOrEmpty(signature))
        {
            _logger?.LogWarning("{MessageType} signature missing but signing is enabled", messageType);
            _options.OnSignatureVerificationFailed?.Invoke($"Missing signature ({messageType})");
            return false;
        }

        var isValid = _messageSigner.Verify(payload, signature);

        if (!isValid)
        {
            _logger?.LogWarning("{MessageType} signature verification failed", messageType);
            _options.OnSignatureVerificationFailed?.Invoke($"Invalid signature ({messageType})");
        }

        return isValid;
    }

    private async Task HandleReceiveUpdate(StateUpdate update)
    {
        if (_store == null)
            return;

        // Skip our own updates
        if (update.SenderId == _hubConnection?.ConnectionId)
            return;

        // Check rate limit
        if (!CheckRateLimit())
            return;

        // Validate message size before deserialization
        if (!ValidateMessageSize(update.StateJson, "StateUpdate"))
            return;

        // Verify message signature
        if (!VerifyMessageSignature(update))
            return;

        // Capture current local state BEFORE any processing for accurate conflict resolution
        var localStateAtReceive = _store.GetState();

        var remoteState = JsonSerializer.Deserialize<TState>(update.StateJson, _jsonOptions);
        if (remoteState == null)
            return;

        // Validate incoming state if validator is configured
        if (_options.StateValidator != null)
        {
            var validationResult = _options.StateValidator.Validate(remoteState);
            if (!validationResult.IsValid)
            {
                _logger?.LogWarning(
                    "Server state validation failed: {Errors}",
                    string.Join(", ", validationResult.Errors));

                _options.OnValidationFailed?.Invoke(validationResult with { Source = "ServerSync" });

                if (_options.RejectInvalidState)
                {
                    return;
                }
            }
        }

        // Apply conflict resolution with the captured local state
        var resolvedState = ResolveConflict(localStateAtReceive, remoteState, update.Version, localStateAtReceive);
        if (resolvedState == null)
            return; // Client wins, keep local

        try
        {
            _isReceivingUpdate = true;
            UpdateVersionMax(update.Version);

            await _store.UpdateAsync(_ => resolvedState, "@@SYNC").ConfigureAwait(false);
            _logger?.LogDebug("Applied state update from: {Sender}", update.SenderId);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to apply state update");
            _options.OnError?.Invoke(ex);
        }
        finally
        {
            _isReceivingUpdate = false;
        }
    }

    private Task HandleReceiveOperation(StateOperation operation)
    {
        if (_store == null)
            return Task.CompletedTask;

        // Skip our own operations
        if (operation.SenderId == _hubConnection?.ConnectionId)
            return Task.CompletedTask;

        // Check rate limit
        if (!CheckRateLimit())
            return Task.CompletedTask;

        // Validate payload size before any further processing
        if (operation.ValueJson != null && !ValidateMessageSize(operation.ValueJson, "StateOperation"))
            return Task.CompletedTask;

        // Verify message signature when signing is enabled
        if (!VerifyOperationSignature(operation))
            return Task.CompletedTask;

        // Never move the version backwards
        UpdateVersionMax(operation.Version);
        _options.OnOperationReceived?.Invoke(operation);

        _logger?.LogDebug("Received operation: {Type} at {Path}", operation.OperationType, operation.Path);
        return Task.CompletedTask;
    }

    private TState? ResolveConflict(TState local, TState remote, long remoteVersion, TState? baseState = default)
    {
        // Detect suspicious version jumps (potential attack or corruption)
        var versionDelta = remoteVersion - _currentVersion;
        if (versionDelta > _options.MaxVersionJump)
        {
            _logger?.LogWarning(
                "Suspicious version jump detected: current={Current}, remote={Remote}, delta={Delta} (max allowed: {Max})",
                _currentVersion,
                remoteVersion,
                versionDelta,
                _options.MaxVersionJump);

            _options.OnSuspiciousActivity?.Invoke(
                $"Version jump: {_currentVersion} → {remoteVersion} (delta: {versionDelta}, max: {_options.MaxVersionJump})");

            if (_options.RejectSuspiciousVersions)
            {
                _logger?.LogWarning("Rejecting state update due to suspicious version jump");
                return default; // Reject the update
            }
        }

        // Always apply conflict resolution, regardless of version
        // This prevents attackers from using high version numbers to bypass conflict resolution
        switch (_options.ConflictResolution)
        {
            case ConflictResolution.ClientWins:
                // Client always wins - reject remote state
                return default;

            case ConflictResolution.ServerWins:
                // Server always wins - use remote state
                return remote;

            case ConflictResolution.LastWriteWins:
                // Use remote if it's newer (higher version)
                if (remoteVersion > _currentVersion)
                {
                    return remote;
                }
                return default; // Keep local

            case ConflictResolution.Custom:
                if (_options.CustomConflictResolver != null)
                {
                    // Pass the captured base state (state at receive time) instead of potentially stale _previousState
                    return _options.CustomConflictResolver.Resolve(local, remote, baseState ?? local);
                }
                // Fallback to ServerWins if no custom resolver
                return remote;

            default:
                return remote;
        }
    }

    private async Task HandleReceiveFullState(string stateJson, long version, string? signature)
    {
        if (_store == null)
            return;

        // Check rate limit
        if (!CheckRateLimit())
            return;

        // Validate message size BEFORE deserialization to prevent DoS
        if (!ValidateMessageSize(stateJson, "FullState"))
            return;

        // Verify message signature when signing is enabled
        if (!VerifySignature(stateJson, signature, "FullState"))
            return;

        // Never move the version backwards: only apply full state when it is
        // at least as new as our current version.
        var currentVersion = Volatile.Read(ref _currentVersion);
        if (version < currentVersion)
        {
            _logger?.LogWarning(
                "Ignoring full state with stale version {Remote} (current: {Current})",
                version,
                currentVersion);
            return;
        }

        try
        {
            var newState = JsonSerializer.Deserialize<TState>(stateJson, _jsonOptions);
            if (newState == null)
                return;

            // Validate incoming state if validator is configured
            if (_options.StateValidator != null)
            {
                var validationResult = _options.StateValidator.Validate(newState);
                if (!validationResult.IsValid)
                {
                    _logger?.LogWarning(
                        "Full state validation failed: {Errors}",
                        string.Join(", ", validationResult.Errors));

                    _options.OnValidationFailed?.Invoke(validationResult with { Source = "ServerSync_FullState" });

                    if (_options.RejectInvalidState)
                    {
                        return;
                    }
                }
            }

            _isReceivingUpdate = true;
            UpdateVersionMax(version);

            await _store.UpdateAsync(_ => newState, "@@SYNC_FULL").ConfigureAwait(false);
            _logger?.LogDebug("Applied full state sync, version: {Version}", version);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to apply full state");
            _options.OnError?.Invoke(ex);
        }
        finally
        {
            _isReceivingUpdate = false;
        }
    }

    private Task HandleUserJoined(PresenceInfo user)
    {
        lock (_presenceList)
        {
            _presenceList.Add(user);
        }
        _options.OnUserJoined?.Invoke(user);
        return Task.CompletedTask;
    }

    private Task HandleUserLeft(PresenceInfo user)
    {
        lock (_presenceList)
        {
            _presenceList.RemoveAll(p => p.ConnectionId == user.ConnectionId);
        }
        lock (_cursorLock)
        {
            _cursors.Remove(user.ConnectionId);
        }
        _options.OnUserLeft?.Invoke(user);
        _options.OnCursorRemoved?.Invoke(user.ConnectionId);
        return Task.CompletedTask;
    }

    private Task HandlePresenceUpdated(IReadOnlyList<PresenceInfo> users)
    {
        lock (_presenceList)
        {
            _presenceList.Clear();
            _presenceList.AddRange(users);
        }
        _options.OnPresenceChanged?.Invoke(users);
        return Task.CompletedTask;
    }

    private Task HandleCursorUpdated(CursorInfo cursor)
    {
        lock (_cursorLock)
        {
            _cursors[cursor.ConnectionId] = cursor;
        }
        _options.OnCursorUpdated?.Invoke(cursor);
        return Task.CompletedTask;
    }

    private Task HandleCursorRemoved(string connectionId)
    {
        lock (_cursorLock)
        {
            _cursors.Remove(connectionId);
        }
        _options.OnCursorRemoved?.Invoke(connectionId);
        return Task.CompletedTask;
    }

    private Task OnConnectionClosed(Exception? ex)
    {
        SetConnectionState(SyncConnectionState.Disconnected);
        if (ex != null)
        {
            _options.OnError?.Invoke(ex);
        }
        return Task.CompletedTask;
    }

    private Task OnReconnecting(Exception? ex)
    {
        SetConnectionState(SyncConnectionState.Reconnecting);
        return Task.CompletedTask;
    }

    private async Task OnReconnected(string? connectionId)
    {
        SetConnectionState(SyncConnectionState.Connected);

        // Validate session before rejoining to prevent session hijacking
        if (_options.DocumentId != null && _sessionToken != null)
        {
            try
            {
                // Check if session has expired
                if (_options.SessionTimeoutMinutes > 0)
                {
                    var sessionAge = DateTime.UtcNow - _sessionCreatedAt;
                    if (sessionAge.TotalMinutes > _options.SessionTimeoutMinutes)
                    {
                        _logger?.LogWarning(
                            "Session expired after {Age:F1} minutes (timeout: {Timeout} minutes). Disconnecting.",
                            sessionAge.TotalMinutes,
                            _options.SessionTimeoutMinutes);
                        _options.OnSessionExpired?.Invoke();
                        await DisconnectAsync().ConfigureAwait(false);
                        return;
                    }
                }

                // Validate session with server before rejoining with timeout to prevent deadlock
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                bool isValid;
                try
                {
                    isValid = await _hubConnection!.InvokeAsync<bool>(
                        "ValidateSession",
                        _sessionToken,
                        _options.DocumentId,
                        cts.Token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    _logger?.LogWarning("Session validation timed out after 30 seconds. Disconnecting to prevent deadlock.");
                    _options.OnSessionValidationFailed?.Invoke("Session validation timeout");
                    await DisconnectAsync().ConfigureAwait(false);
                    return;
                }

                if (!isValid)
                {
                    _logger?.LogWarning("Session validation failed on reconnect. Possible session hijacking attempt.");
                    _options.OnSessionValidationFailed?.Invoke("Session validation failed on reconnect");
                    await DisconnectAsync().ConfigureAwait(false);
                    return;
                }

                await _hubConnection!.InvokeAsync(
                    "JoinDocument",
                    _options.DocumentId,
                    _options.UserDisplayName,
                    _sessionToken)
                    .ConfigureAwait(false);

                // Refresh session token and reset creation time after successful reconnection
                // This prevents session expiry from accumulating across reconnects
                _sessionToken = GenerateSessionToken();
                _sessionCreatedAt = DateTime.UtcNow;
                _logger?.LogDebug("Session token refreshed after successful reconnection");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to validate session or rejoin document after reconnect");
                // If session validation is required and fails, disconnect
                if (_options.RequireSessionValidation)
                {
                    await DisconnectAsync().ConfigureAwait(false);
                    return;
                }
            }
        }

        // Only flush offline queue after successful session validation
        await FlushOfflineQueueAsync().ConfigureAwait(false);
    }

    private void SetConnectionState(SyncConnectionState state)
    {
        _connectionState = state;
        _options.OnConnectionStateChanged?.Invoke(state);
    }

    /// <summary>
    /// Generates a cryptographically secure session token.
    /// </summary>
    private static string GenerateSessionToken()
    {
        var bytes = new byte[32];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// Atomically updates _currentVersion to the maximum of its current value and the provided value.
    /// Uses Interlocked.CompareExchange for thread-safe operation.
    /// </summary>
    private void UpdateVersionMax(long newVersion)
    {
        long current;
        do
        {
            current = Volatile.Read(ref _currentVersion);
            if (newVersion <= current)
                return; // New version is not greater, no update needed
        }
        while (Interlocked.CompareExchange(ref _currentVersion, newVersion, current) != current);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _debounceTimer?.Dispose();
        _cursorDebounceTimer?.Dispose();
        _messageSigner?.Dispose();
        await DisconnectAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}
