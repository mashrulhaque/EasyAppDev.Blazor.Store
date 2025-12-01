// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using EasyAppDev.Blazor.Store.Security;

namespace EasyAppDev.Blazor.Store.ServerSync;

/// <summary>
/// Options for server-side state synchronization.
/// </summary>
/// <typeparam name="TState">The type of state.</typeparam>
public class ServerSyncOptions<TState> where TState : notnull
{
    /// <summary>
    /// Gets or sets the SignalR hub URL.
    /// </summary>
    public required string HubUrl { get; set; }

    /// <summary>
    /// Gets or sets an optional document or room identifier for multi-tenant scenarios.
    /// </summary>
    public string? DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the conflict resolution strategy.
    /// Default is LastWriteWins.
    /// </summary>
    public ConflictResolution ConflictResolution { get; set; } = ConflictResolution.LastWriteWins;

    /// <summary>
    /// Gets or sets a custom conflict resolver. Used when ConflictResolution is set to Custom.
    /// </summary>
    public IConflictResolver<TState>? CustomConflictResolver { get; set; }

    /// <summary>
    /// Gets or sets a selector function to choose which parts of state to sync.
    /// By default, the entire state is synced.
    /// </summary>
    public Func<TState, object>? SyncSelector { get; set; }

    /// <summary>
    /// Gets or sets whether to enable presence tracking.
    /// Default is false.
    /// </summary>
    public bool EnablePresence { get; set; } = false;

    /// <summary>
    /// Gets or sets the callback when a user joins.
    /// </summary>
    public Action<PresenceInfo>? OnUserJoined { get; set; }

    /// <summary>
    /// Gets or sets the callback when a user leaves.
    /// </summary>
    public Action<PresenceInfo>? OnUserLeft { get; set; }

    /// <summary>
    /// Gets or sets the callback when presence list changes.
    /// </summary>
    public Action<IReadOnlyList<PresenceInfo>>? OnPresenceChanged { get; set; }

    /// <summary>
    /// Gets or sets the current user's display name for presence.
    /// </summary>
    public string? UserDisplayName { get; set; }

    /// <summary>
    /// Gets or sets the user's cursor color for collaborative editing.
    /// </summary>
    public string? UserCursorColor { get; set; }

    /// <summary>
    /// Gets or sets whether to automatically reconnect on disconnection.
    /// Default is true.
    /// </summary>
    public bool AutoReconnect { get; set; } = true;

    /// <summary>
    /// Gets or sets the reconnection delays.
    /// </summary>
    public TimeSpan[] ReconnectDelays { get; set; } = new[]
    {
        TimeSpan.FromSeconds(0),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30)
    };

    /// <summary>
    /// Gets or sets the callback when connection state changes.
    /// </summary>
    public Action<SyncConnectionState>? OnConnectionStateChanged { get; set; }

    /// <summary>
    /// Gets or sets the callback when an error occurs.
    /// </summary>
    public Action<Exception>? OnError { get; set; }

    /// <summary>
    /// Gets or sets actions to exclude from sync.
    /// </summary>
    public HashSet<string> ExcludedActions { get; set; } = new()
    {
        "@@INIT",
        "@@SYNC",
        "@@SYNC_FULL",
        "@@JUMP_TO_STATE"
    };

    /// <summary>
    /// Gets or sets the debounce delay for outgoing sync messages.
    /// Default is 100ms.
    /// </summary>
    public TimeSpan SyncDebounce { get; set; } = TimeSpan.FromMilliseconds(100);

    // --- Cursor tracking options ---

    /// <summary>
    /// Gets or sets whether to enable cursor/selection tracking.
    /// Default is false.
    /// </summary>
    public bool EnableCursorTracking { get; set; } = false;

    /// <summary>
    /// Gets or sets the debounce delay for cursor updates.
    /// Default is 50ms.
    /// </summary>
    public TimeSpan CursorDebounce { get; set; } = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// Gets or sets the callback when a cursor is updated.
    /// </summary>
    public Action<CursorInfo>? OnCursorUpdated { get; set; }

    /// <summary>
    /// Gets or sets the callback when a cursor is removed.
    /// </summary>
    public Action<string>? OnCursorRemoved { get; set; }

    // --- Offline support options ---

    /// <summary>
    /// Gets or sets whether to enable offline queue.
    /// When enabled, updates made while offline are queued and sent when reconnected.
    /// Default is false.
    /// </summary>
    public bool EnableOfflineQueue { get; set; } = false;

    /// <summary>
    /// Gets or sets the maximum number of operations to queue offline.
    /// Default is 100.
    /// </summary>
    public int MaxOfflineQueueSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets whether to persist the offline queue to local storage.
    /// Default is false.
    /// </summary>
    public bool PersistOfflineQueue { get; set; } = false;

    /// <summary>
    /// Gets or sets the callback when offline queue is flushed on reconnect.
    /// </summary>
    public Action<int>? OnOfflineQueueFlushed { get; set; }

    // --- Operation-based sync options ---

    /// <summary>
    /// Gets or sets whether to use operation-based sync instead of full state sync.
    /// Operation-based sync sends smaller, more precise updates.
    /// Default is false.
    /// </summary>
    public bool UseOperationBasedSync { get; set; } = false;

    /// <summary>
    /// Gets or sets the callback when an operation is received.
    /// </summary>
    public Action<StateOperation>? OnOperationReceived { get; set; }

    /// <summary>
    /// Gets or sets a function to convert state changes to operations.
    /// </summary>
    public Func<TState, TState, string?, IEnumerable<StateOperation>>? StateToOperations { get; set; }

    // --- Security options ---

    /// <summary>
    /// Gets or sets the state validator for validating incoming state from the server.
    /// Default is null (no validation).
    /// </summary>
    public IStateValidator<TState>? StateValidator { get; set; }

    /// <summary>
    /// Gets or sets whether to reject invalid states from the server.
    /// If true, invalid states are not applied.
    /// If false, invalid states are logged but still applied.
    /// Default is true.
    /// </summary>
    public bool RejectInvalidState { get; set; } = true;

    /// <summary>
    /// Gets or sets a callback invoked when state validation fails.
    /// </summary>
    public Action<StateValidationResult>? OnValidationFailed { get; set; }
}

/// <summary>
/// Conflict resolution strategies.
/// </summary>
public enum ConflictResolution
{
    /// <summary>
    /// Last write wins - the most recent update takes precedence.
    /// </summary>
    LastWriteWins,

    /// <summary>
    /// Server wins - server state always takes precedence.
    /// </summary>
    ServerWins,

    /// <summary>
    /// Client wins - client state always takes precedence.
    /// </summary>
    ClientWins,

    /// <summary>
    /// Custom - use a custom resolver function.
    /// </summary>
    Custom
}

/// <summary>
/// Connection state for server sync.
/// </summary>
public enum SyncConnectionState
{
    /// <summary>
    /// Not connected.
    /// </summary>
    Disconnected,

    /// <summary>
    /// Attempting to connect.
    /// </summary>
    Connecting,

    /// <summary>
    /// Connected and syncing.
    /// </summary>
    Connected,

    /// <summary>
    /// Reconnecting after disconnection.
    /// </summary>
    Reconnecting
}

/// <summary>
/// Information about a connected user.
/// </summary>
public class PresenceInfo
{
    /// <summary>
    /// Gets or sets the user's connection ID.
    /// </summary>
    public required string ConnectionId { get; set; }

    /// <summary>
    /// Gets or sets the user's display name.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets when the user connected.
    /// </summary>
    public DateTime ConnectedAt { get; set; }

    /// <summary>
    /// Gets or sets custom metadata about the user.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}
