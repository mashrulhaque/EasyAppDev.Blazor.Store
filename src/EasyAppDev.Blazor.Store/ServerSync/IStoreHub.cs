// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

namespace EasyAppDev.Blazor.Store.ServerSync;

/// <summary>
/// Interface for the server-side SignalR hub for state synchronization.
/// Implement this interface in your server project or use StoreHubBase.
/// </summary>
/// <remarks>
/// <para><b>SECURITY REQUIREMENTS:</b></para>
/// <para>
/// Hub implementations MUST add proper authorization to prevent unauthorized access.
/// At minimum, add the [Authorize] attribute to the hub class:
/// <code>
/// [Authorize]
/// public class StoreHub : Hub&lt;IStoreHubClient&gt;, IStoreHub
/// {
///     // Implementation
/// }
/// </code>
/// </para>
/// <para><b>Document Access Validation:</b></para>
/// <para>
/// When using multi-tenant scenarios (DocumentId), you MUST validate that the current user
/// has permission to access the requested document in JoinDocument():
/// <code>
/// public async Task JoinDocument(string documentId, string? displayName)
/// {
///     // REQUIRED: Validate user has access to this document
///     var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
///     if (!await _authService.CanAccessDocument(userId, documentId))
///     {
///         throw new HubException("Access denied to this document");
///     }
///
///     // ... rest of implementation
/// }
/// </code>
/// </para>
/// <para><b>Rate Limiting:</b></para>
/// <para>
/// Consider implementing server-side rate limiting middleware to prevent abuse:
/// <code>
/// builder.Services.AddRateLimiter(options =>
/// {
///     options.AddFixedWindowLimiter("signalr", opt =>
///     {
///         opt.Window = TimeSpan.FromSeconds(1);
///         opt.PermitLimit = 10;
///     });
/// });
/// </code>
/// </para>
/// <para><b>Message Size Limits:</b></para>
/// <para>
/// Configure SignalR to enforce message size limits in your server's Program.cs:
/// <code>
/// builder.Services.AddSignalR(options =>
/// {
///     options.MaximumReceiveMessageSize = 1_048_576; // 1MB
/// });
/// </code>
/// </para>
/// </remarks>
public interface IStoreHub
{
    /// <summary>
    /// Joins a document/room for synchronized state.
    /// </summary>
    /// <param name="documentId">The document identifier.</param>
    /// <param name="displayName">Optional display name for presence.</param>
    Task JoinDocument(string documentId, string? displayName);

    /// <summary>
    /// Leaves a document/room.
    /// </summary>
    /// <param name="documentId">The document identifier.</param>
    Task LeaveDocument(string documentId);

    /// <summary>
    /// Sends a state update to all other clients in the document.
    /// </summary>
    /// <param name="update">The state update.</param>
    Task SendUpdate(StateUpdate update);

    /// <summary>
    /// Sends an operation-based update for fine-grained synchronization.
    /// </summary>
    /// <param name="operation">The operation to apply.</param>
    Task SendOperation(StateOperation operation);

    /// <summary>
    /// Updates the cursor/selection position.
    /// </summary>
    /// <param name="cursor">The cursor information.</param>
    Task UpdateCursor(CursorInfo cursor);

    /// <summary>
    /// Requests the current state from the server.
    /// </summary>
    /// <param name="documentId">The document identifier.</param>
    Task<string?> GetCurrentState(string documentId);

    /// <summary>
    /// Gets operations since a specific version for reconciliation.
    /// </summary>
    /// <param name="documentId">The document identifier.</param>
    /// <param name="sinceVersion">Get operations after this version.</param>
    Task<IReadOnlyList<StateOperation>> GetOperationHistory(string documentId, long sinceVersion);

    /// <summary>
    /// Gets all active cursors in a document.
    /// </summary>
    /// <param name="documentId">The document identifier.</param>
    Task<IReadOnlyList<CursorInfo>> GetCursors(string documentId);
}

/// <summary>
/// Interface for client-side hub callbacks.
/// </summary>
public interface IStoreHubClient
{
    /// <summary>
    /// Receives a state update from another client.
    /// </summary>
    /// <param name="update">The state update.</param>
    Task ReceiveUpdate(StateUpdate update);

    /// <summary>
    /// Receives an operation from another client.
    /// </summary>
    /// <param name="operation">The operation to apply.</param>
    Task ReceiveOperation(StateOperation operation);

    /// <summary>
    /// Receives the full state (usually on join).
    /// </summary>
    /// <param name="stateJson">The serialized state.</param>
    /// <param name="version">The current version.</param>
    Task ReceiveFullState(string stateJson, long version);

    /// <summary>
    /// Receives presence update when user joins.
    /// </summary>
    /// <param name="user">The user info.</param>
    Task UserJoined(PresenceInfo user);

    /// <summary>
    /// Receives presence update when user leaves.
    /// </summary>
    /// <param name="user">The user info.</param>
    Task UserLeft(PresenceInfo user);

    /// <summary>
    /// Receives the full presence list.
    /// </summary>
    /// <param name="users">All connected users.</param>
    Task PresenceUpdated(IReadOnlyList<PresenceInfo> users);

    /// <summary>
    /// Receives a cursor update from another client.
    /// </summary>
    /// <param name="cursor">The cursor information.</param>
    Task CursorUpdated(CursorInfo cursor);

    /// <summary>
    /// Receives notification that a cursor was removed.
    /// </summary>
    /// <param name="connectionId">The connection ID whose cursor was removed.</param>
    Task CursorRemoved(string connectionId);
}
