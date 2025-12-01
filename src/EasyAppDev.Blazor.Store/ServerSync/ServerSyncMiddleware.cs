// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Middleware;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace EasyAppDev.Blazor.Store.ServerSync;

/// <summary>
/// Middleware that synchronizes state with a server via SignalR.
/// Supports full state sync, operation-based sync, cursor tracking, and offline queuing.
/// </summary>
/// <typeparam name="TState">The type of state.</typeparam>
public class ServerSyncMiddleware<TState> : IMiddleware<TState>, IAsyncDisposable
    where TState : notnull
{
    private readonly ServerSyncOptions<TState> _options;
    private readonly ILogger<ServerSyncMiddleware<TState>>? _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly List<PresenceInfo> _presenceList = new();
    private readonly Dictionary<string, CursorInfo> _cursors = new();
    private readonly Queue<StateUpdate> _offlineQueue = new();
    private readonly Queue<StateOperation> _offlineOperationQueue = new();

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

    /// <summary>
    /// Creates a new server sync middleware.
    /// </summary>
    public ServerSyncMiddleware(
        ServerSyncOptions<TState> options,
        ILogger<ServerSyncMiddleware<TState>>? logger = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
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
    /// Connects to the server hub.
    /// </summary>
    public async Task ConnectAsync()
    {
        if (_hubConnection != null)
            return;

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
            _hubConnection.On<string, long>("ReceiveFullState", HandleReceiveFullState);
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

            // Join document if specified
            if (_options.DocumentId != null)
            {
                await _hubConnection.InvokeAsync(
                    "JoinDocument",
                    _options.DocumentId,
                    _options.UserDisplayName)
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

        // Flush state updates
        while (true)
        {
            StateUpdate? update;
            lock (_syncLock)
            {
                if (_offlineQueue.Count == 0) break;
                update = _offlineQueue.Dequeue();
            }

            try
            {
                await _hubConnection!.InvokeAsync("SendUpdate", update).ConfigureAwait(false);
                flushedCount++;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to flush offline update");
                // Re-queue failed update
                QueueOfflineUpdate(update);
                break;
            }
        }

        // Flush operations
        while (true)
        {
            StateOperation? operation;
            lock (_syncLock)
            {
                if (_offlineOperationQueue.Count == 0) break;
                operation = _offlineOperationQueue.Dequeue();
            }

            try
            {
                await _hubConnection!.InvokeAsync("SendOperation", operation).ConfigureAwait(false);
                flushedCount++;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to flush offline operation");
                QueueOfflineOperation(operation);
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
            await _hubConnection.InvokeAsync("SendUpdate", update).ConfigureAwait(false);
            _logger?.LogDebug("Sent state update: {Action}", update.Action);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to send state update");
            if (_options.EnableOfflineQueue)
            {
                QueueOfflineUpdate(update);
            }
            _options.OnError?.Invoke(ex);
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
            await _hubConnection.InvokeAsync("SendOperation", operation).ConfigureAwait(false);
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
            _logger?.LogDebug(ex, "Failed to send cursor update");
        }
    }

    private async Task HandleReceiveUpdate(StateUpdate update)
    {
        if (_store == null)
            return;

        // Skip our own updates
        if (update.SenderId == _hubConnection?.ConnectionId)
            return;

        var localState = _store.GetState();
        var remoteState = JsonSerializer.Deserialize<TState>(update.StateJson, _jsonOptions);
        if (remoteState == null)
            return;

        // Apply conflict resolution
        var resolvedState = ResolveConflict(localState, remoteState, update.Version);
        if (resolvedState == null)
            return; // Client wins, keep local

        try
        {
            _isReceivingUpdate = true;
            _currentVersion = Math.Max(_currentVersion, update.Version);

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

        _currentVersion = Math.Max(_currentVersion, operation.Version);
        _options.OnOperationReceived?.Invoke(operation);

        _logger?.LogDebug("Received operation: {Type} at {Path}", operation.OperationType, operation.Path);
        return Task.CompletedTask;
    }

    private TState? ResolveConflict(TState local, TState remote, long remoteVersion)
    {
        if (remoteVersion <= _currentVersion)
        {
            switch (_options.ConflictResolution)
            {
                case ConflictResolution.ClientWins:
                    return default; // Keep local state

                case ConflictResolution.ServerWins:
                    return remote;

                case ConflictResolution.LastWriteWins:
                    // Use remote if it's newer or same version
                    return remote;

                case ConflictResolution.Custom:
                    if (_options.CustomConflictResolver != null)
                    {
                        return _options.CustomConflictResolver.Resolve(local, remote, _previousState);
                    }
                    return remote;

                default:
                    return remote;
            }
        }

        return remote;
    }

    private async Task HandleReceiveFullState(string stateJson, long version)
    {
        if (_store == null)
            return;

        try
        {
            _isReceivingUpdate = true;
            _currentVersion = version;

            var newState = JsonSerializer.Deserialize<TState>(stateJson, _jsonOptions);
            if (newState != null)
            {
                await _store.UpdateAsync(_ => newState, "@@SYNC_FULL").ConfigureAwait(false);
                _logger?.LogDebug("Applied full state sync, version: {Version}", version);
            }
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

        // Rejoin document
        if (_options.DocumentId != null)
        {
            try
            {
                await _hubConnection!.InvokeAsync(
                    "JoinDocument",
                    _options.DocumentId,
                    _options.UserDisplayName)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to rejoin document after reconnect");
            }
        }

        // Flush offline queue
        await FlushOfflineQueueAsync().ConfigureAwait(false);
    }

    private void SetConnectionState(SyncConnectionState state)
    {
        _connectionState = state;
        _options.OnConnectionStateChanged?.Invoke(state);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _debounceTimer?.Dispose();
        _cursorDebounceTimer?.Dispose();
        await DisconnectAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}
