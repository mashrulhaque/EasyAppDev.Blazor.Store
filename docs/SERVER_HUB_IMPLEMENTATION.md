# Server Hub Implementation for ServerSync

This document provides a reference implementation for the SignalR hub required for server-side state synchronization. Copy this code to your ASP.NET Core server project.

## Prerequisites

Your server project should reference:
- `Microsoft.AspNetCore.SignalR`
- `EasyAppDev.Blazor.Store` (for the types like `StateUpdate`, `StateOperation`, etc.)

## Hub Implementation

Create a new file `StoreHub.cs` in your server project:

```csharp
using Microsoft.AspNetCore.SignalR;
using EasyAppDev.Blazor.Store.ServerSync;
using System.Collections.Concurrent;
using System.Text.Json;

namespace YourApp.Server.Hubs;

/// <summary>
/// SignalR hub for server-side state synchronization.
/// </summary>
public class StoreHub : Hub<IStoreHubClient>
{
    private static readonly ConcurrentDictionary<string, DocumentRoom> _rooms = new();
    private readonly ILogger<StoreHub> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public StoreHub(ILogger<StoreHub> logger)
    {
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task JoinDocument(string documentId, string? displayName)
    {
        var room = _rooms.GetOrAdd(documentId, _ => new DocumentRoom(documentId));
        var user = new PresenceInfo
        {
            ConnectionId = Context.ConnectionId,
            DisplayName = displayName,
            ConnectedAt = DateTime.UtcNow
        };

        await Groups.AddToGroupAsync(Context.ConnectionId, documentId);
        room.AddUser(user);

        _logger.LogInformation("User {ConnectionId} joined document {DocumentId}",
            Context.ConnectionId, documentId);

        // Send current state to the new user
        var currentState = room.GetStateJson();
        if (currentState != null)
        {
            await Clients.Caller.ReceiveFullState(currentState, room.Version);
        }

        // Notify others
        await Clients.OthersInGroup(documentId).UserJoined(user);
        await Clients.Caller.PresenceUpdated(room.GetPresence());
    }

    public async Task LeaveDocument(string documentId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, documentId);

        if (_rooms.TryGetValue(documentId, out var room))
        {
            var user = room.RemoveUser(Context.ConnectionId);
            if (user != null)
            {
                await Clients.OthersInGroup(documentId).UserLeft(user);
            }

            if (room.IsEmpty)
            {
                _rooms.TryRemove(documentId, out _);
            }
        }
    }

    public async Task SendUpdate(StateUpdate update)
    {
        if (string.IsNullOrEmpty(update.DocumentId))
            return;

        var room = _rooms.GetOrAdd(update.DocumentId, _ => new DocumentRoom(update.DocumentId));
        update.SenderId = Context.ConnectionId;
        room.UpdateState(update.StateJson, update.Version);

        await Clients.OthersInGroup(update.DocumentId).ReceiveUpdate(update);
    }

    public async Task SendOperation(StateOperation operation)
    {
        if (string.IsNullOrEmpty(operation.DocumentId))
            return;

        operation.SenderId = Context.ConnectionId;
        operation.Timestamp = DateTime.UtcNow;

        var room = _rooms.GetOrAdd(operation.DocumentId, _ => new DocumentRoom(operation.DocumentId));
        room.AddOperation(operation);

        await Clients.OthersInGroup(operation.DocumentId).ReceiveOperation(operation);
    }

    public async Task UpdateCursor(CursorInfo cursor)
    {
        if (string.IsNullOrEmpty(cursor.DocumentId))
            return;

        cursor.ConnectionId = Context.ConnectionId;
        cursor.Timestamp = DateTime.UtcNow;

        var room = _rooms.GetOrAdd(cursor.DocumentId, _ => new DocumentRoom(cursor.DocumentId));
        room.UpdateCursor(cursor);

        await Clients.OthersInGroup(cursor.DocumentId).CursorUpdated(cursor);
    }

    public Task<string?> GetCurrentState(string documentId)
    {
        if (_rooms.TryGetValue(documentId, out var room))
        {
            return Task.FromResult(room.GetStateJson());
        }
        return Task.FromResult<string?>(null);
    }

    public Task<IReadOnlyList<StateOperation>> GetOperationHistory(string documentId, long sinceVersion)
    {
        if (_rooms.TryGetValue(documentId, out var room))
        {
            return Task.FromResult(room.GetOperationsSince(sinceVersion));
        }
        return Task.FromResult<IReadOnlyList<StateOperation>>(Array.Empty<StateOperation>());
    }

    public Task<IReadOnlyList<CursorInfo>> GetCursors(string documentId)
    {
        if (_rooms.TryGetValue(documentId, out var room))
        {
            return Task.FromResult(room.GetCursors());
        }
        return Task.FromResult<IReadOnlyList<CursorInfo>>(Array.Empty<CursorInfo>());
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var room in _rooms.Values)
        {
            var user = room.RemoveUser(Context.ConnectionId);
            if (user != null)
            {
                await Clients.Group(room.DocumentId).UserLeft(user);
                room.RemoveCursor(Context.ConnectionId);
            }

            if (room.IsEmpty)
            {
                _rooms.TryRemove(room.DocumentId, out _);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }
}

/// <summary>
/// Manages a document room for collaboration.
/// </summary>
public class DocumentRoom
{
    private readonly object _lock = new();
    private readonly List<PresenceInfo> _users = new();
    private readonly List<StateOperation> _operations = new();
    private readonly Dictionary<string, CursorInfo> _cursors = new();
    private string? _stateJson;
    private long _version;

    public string DocumentId { get; }
    public long Version => _version;
    public bool IsEmpty { get { lock (_lock) return _users.Count == 0; } }

    public DocumentRoom(string documentId) => DocumentId = documentId;

    public void AddUser(PresenceInfo user)
    {
        lock (_lock)
        {
            _users.RemoveAll(u => u.ConnectionId == user.ConnectionId);
            _users.Add(user);
        }
    }

    public PresenceInfo? RemoveUser(string connectionId)
    {
        lock (_lock)
        {
            var user = _users.FirstOrDefault(u => u.ConnectionId == connectionId);
            if (user != null) _users.Remove(user);
            return user;
        }
    }

    public IReadOnlyList<PresenceInfo> GetPresence()
    {
        lock (_lock) return _users.ToList();
    }

    public string? GetStateJson()
    {
        lock (_lock) return _stateJson;
    }

    public void UpdateState(string stateJson, long version)
    {
        lock (_lock)
        {
            _stateJson = stateJson;
            _version = Math.Max(_version, version);
        }
    }

    public void AddOperation(StateOperation operation)
    {
        lock (_lock)
        {
            operation.Version = ++_version;
            _operations.Add(operation);
            while (_operations.Count > 1000) _operations.RemoveAt(0);
        }
    }

    public IReadOnlyList<StateOperation> GetOperationsSince(long sinceVersion)
    {
        lock (_lock) return _operations.Where(op => op.Version > sinceVersion).ToList();
    }

    public void UpdateCursor(CursorInfo cursor)
    {
        lock (_lock) _cursors[cursor.ConnectionId] = cursor;
    }

    public void RemoveCursor(string connectionId)
    {
        lock (_lock) _cursors.Remove(connectionId);
    }

    public IReadOnlyList<CursorInfo> GetCursors()
    {
        lock (_lock) return _cursors.Values.ToList();
    }
}
```

## Registration in Program.cs

```csharp
// Add SignalR services
builder.Services.AddSignalR();

// Map the hub endpoint
app.MapHub<StoreHub>("/hubs/store");
```

## Client-Side Usage

```csharp
// In your Blazor component or service
builder.Services.AddStore(
    new AppState(),
    (store, sp) => store
        .WithServerSync(sp, options =>
        {
            options.HubUrl = "/hubs/store";
            options.DocumentId = "my-document";
            options.EnablePresence = true;
            options.EnableCursorTracking = true;
            options.EnableOfflineQueue = true;
        })
);
```

## Features

The hub implementation supports:

- **Full State Sync**: Sends complete state on each update
- **Operation-Based Sync**: Sends smaller, granular operations
- **Presence Tracking**: See who is connected to a document
- **Cursor Tracking**: Real-time cursor positions for collaborative editing
- **Reconnection**: Automatic rejoining of documents on reconnect
- **Version Tracking**: Conflict detection via version numbers
