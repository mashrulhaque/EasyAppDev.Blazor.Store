// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

// NOTE: This file is excluded from the client library build.
// It is provided as a reference implementation for server projects.
// To use SecureStoreHubBase:
// 1. Copy this file to your server project
// 2. Add: <FrameworkReference Include="Microsoft.AspNetCore.App" />
// 3. Implement the abstract authorization methods

using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace EasyAppDev.Blazor.Store.ServerSync;

/// <summary>
/// Secure base class for SignalR hubs that handle state synchronization.
/// Provides built-in authorization, rate limiting, and audit logging.
/// </summary>
/// <remarks>
/// <para><b>SECURITY:</b> This hub requires authentication by default via the [Authorize] attribute.</para>
/// <para>
/// Implementations MUST override the authorization methods to implement document-level access control:
/// </para>
/// <list type="bullet">
/// <item><see cref="CanAccessDocumentAsync"/> - Check if user can read a document</item>
/// <item><see cref="CanEditDocumentAsync"/> - Check if user can edit a document</item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// public class DocumentHub : SecureStoreHubBase&lt;DocumentState&gt;
/// {
///     private readonly IDocumentAuthorizationService _authService;
///
///     public DocumentHub(
///         IDocumentAuthorizationService authService,
///         ILogger&lt;DocumentHub&gt; logger) : base(logger)
///     {
///         _authService = authService;
///     }
///
///     protected override async Task&lt;bool&gt; CanAccessDocumentAsync(string documentId, ClaimsPrincipal user)
///     {
///         var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
///         return await _authService.HasReadAccessAsync(userId, documentId);
///     }
///
///     protected override async Task&lt;bool&gt; CanEditDocumentAsync(string documentId, ClaimsPrincipal user)
///     {
///         var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
///         return await _authService.HasWriteAccessAsync(userId, documentId);
///     }
/// }
/// </code>
/// </example>
[Authorize]
public abstract class SecureStoreHubBase<TState> : Hub<IStoreHubClient>, IStoreHub
    where TState : notnull
{
    private readonly ILogger? _logger;

    // IMPORTANT: SignalR creates a NEW hub instance for every invocation, so all
    // tracking state MUST be static to survive across invocations. These fields are
    // per closed generic type (one set of state per TState/hub type).
    private static readonly ConcurrentDictionary<string, HashSet<string>> s_documentConnections = new();
    private static readonly ConcurrentDictionary<string, PresenceInfo> s_presenceInfo = new();
    private static readonly ConcurrentDictionary<string, CursorInfo> s_cursorInfo = new();
    private static readonly ConcurrentDictionary<string, long> s_documentVersions = new();
    private static readonly ConcurrentDictionary<string, Queue<DateTime>> s_rateLimitTracking = new();
    private static readonly ConcurrentDictionary<string, SessionInfo> s_sessionTracking = new();
    private static readonly object s_syncLock = new();

    /// <summary>
    /// Gets the maximum number of messages per second per connection.
    /// Override to customize rate limiting. Default is 10.
    /// </summary>
    protected virtual int MaxMessagesPerSecond => 10;

    /// <summary>
    /// Gets the maximum message size in bytes.
    /// Override to customize size limits. Default is 1MB.
    /// </summary>
    protected virtual int MaxMessageSizeBytes => 1_048_576;

    /// <summary>
    /// Creates a new secure store hub.
    /// </summary>
    /// <param name="logger">Optional logger for audit and security events.</param>
    protected SecureStoreHubBase(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Checks if the current user can access (read) the specified document.
    /// </summary>
    /// <param name="documentId">The document identifier.</param>
    /// <param name="user">The user's claims principal.</param>
    /// <returns>True if the user has read access, false otherwise.</returns>
    /// <remarks>
    /// Override this method to implement your authorization logic.
    /// This method is called before JoinDocument and GetCurrentState.
    /// </remarks>
    protected abstract Task<bool> CanAccessDocumentAsync(string documentId, ClaimsPrincipal user);

    /// <summary>
    /// Checks if the current user can edit the specified document.
    /// </summary>
    /// <param name="documentId">The document identifier.</param>
    /// <param name="user">The user's claims principal.</param>
    /// <returns>True if the user has write access, false otherwise.</returns>
    /// <remarks>
    /// Override this method to implement your authorization logic.
    /// This method is called before SendUpdate, SendOperation, and UpdateCursor.
    /// </remarks>
    protected abstract Task<bool> CanEditDocumentAsync(string documentId, ClaimsPrincipal user);

    /// <summary>
    /// Called when a client joins a document. Override to add custom logic.
    /// </summary>
    /// <param name="documentId">The document identifier.</param>
    /// <param name="displayName">The user's display name.</param>
    protected virtual Task OnClientJoinedAsync(string documentId, string? displayName)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when a client leaves a document. Override to add custom logic.
    /// </summary>
    /// <param name="documentId">The document identifier.</param>
    protected virtual Task OnClientLeftAsync(string documentId)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called to get the current state for a document. Override to load from your data store.
    /// </summary>
    /// <param name="documentId">The document identifier.</param>
    /// <returns>Serialized state JSON, or null if not found.</returns>
    protected virtual Task<string?> LoadStateAsync(string documentId)
    {
        return Task.FromResult<string?>(null);
    }

    /// <summary>
    /// Called to get the operation history. Override to load from your data store.
    /// </summary>
    /// <param name="documentId">The document identifier.</param>
    /// <param name="sinceVersion">Get operations after this version.</param>
    /// <returns>List of operations.</returns>
    protected virtual Task<IReadOnlyList<StateOperation>> LoadOperationHistoryAsync(string documentId, long sinceVersion)
    {
        return Task.FromResult<IReadOnlyList<StateOperation>>(Array.Empty<StateOperation>());
    }

    /// <summary>
    /// Joins a document with an optional session token for reconnection validation.
    /// This matches the signature invoked by <see cref="ServerSyncMiddleware{TState}"/>
    /// (sessionToken may be null).
    /// </summary>
    /// <param name="documentId">The document to join.</param>
    /// <param name="displayName">Display name for presence.</param>
    /// <param name="sessionToken">Session token for reconnection validation. May be null.</param>
    public async Task JoinDocument(string documentId, string? displayName, string? sessionToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(documentId);

        // Check authorization
        if (!await CanAccessDocumentAsync(documentId, Context.User!))
        {
            LogSecurityEvent("JoinDocument", documentId, "Access denied");
            throw new HubException("Access denied to document");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, documentId);

        // Track connection and session
        var connections = s_documentConnections.GetOrAdd(documentId, _ => new HashSet<string>());
        lock (connections)
        {
            connections.Add(Context.ConnectionId);
        }

        // Track presence
        s_presenceInfo[Context.ConnectionId] = new PresenceInfo
        {
            ConnectionId = Context.ConnectionId,
            DisplayName = displayName ?? GetUserDisplayName(),
            ConnectedAt = DateTime.UtcNow
        };

        // Track session for reconnection validation
        if (!string.IsNullOrEmpty(sessionToken))
        {
            var userId = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            s_sessionTracking[Context.ConnectionId] = new SessionInfo
            {
                Token = sessionToken,
                UserId = userId,
                DocumentId = documentId,
                CreatedAt = DateTime.UtcNow,
                LastValidated = DateTime.UtcNow
            };
        }

        LogAuditEvent("JoinDocument", documentId, $"User joined: {displayName ?? "anonymous"}");

        // Notify others
        var presenceInfo = GetPresenceInfo();
        await Clients.Group(documentId).UserJoined(presenceInfo);

        // Notify all users of updated presence list
        await BroadcastPresenceListAsync(documentId);

        await OnClientJoinedAsync(documentId, displayName);
    }

    /// <summary>
    /// Validates a session token for reconnection.
    /// </summary>
    /// <param name="sessionToken">The session token to validate.</param>
    /// <param name="documentId">The document ID.</param>
    /// <returns>True if the session is valid, false otherwise.</returns>
    public Task<bool> ValidateSession(string sessionToken, string documentId)
    {
        if (string.IsNullOrEmpty(sessionToken) || string.IsNullOrEmpty(documentId))
        {
            LogSecurityEvent("ValidateSession", documentId, "Missing session token or document ID");
            return Task.FromResult(false);
        }

        var currentUserId = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        lock (s_syncLock)
        {
            // Check if we have a previous session with this token
            var existingSession = s_sessionTracking.Values
                .FirstOrDefault(s => s.Token == sessionToken && s.DocumentId == documentId);

            if (existingSession == null)
            {
                // No previous session found, this could be a new connection
                // Allow it but log for monitoring
                LogAuditEvent("ValidateSession", documentId, "No previous session found, allowing new connection");
                return Task.FromResult(true);
            }

            // Validate the session belongs to the same user
            if (existingSession.UserId != currentUserId)
            {
                LogSecurityEvent("ValidateSession", documentId,
                    $"Session token mismatch: expected user {existingSession.UserId}, got {currentUserId}");
                return Task.FromResult(false);
            }

            // Check session age (optional: enforce session timeout on server)
            var sessionAge = DateTime.UtcNow - existingSession.CreatedAt;
            if (SessionTimeoutMinutes > 0 && sessionAge.TotalMinutes > SessionTimeoutMinutes)
            {
                LogSecurityEvent("ValidateSession", documentId,
                    $"Session expired: age {sessionAge.TotalMinutes:F1} minutes > timeout {SessionTimeoutMinutes} minutes");
                s_sessionTracking.TryRemove(Context.ConnectionId, out _);
                return Task.FromResult(false);
            }

            // Update last validated time
            existingSession.LastValidated = DateTime.UtcNow;

            LogAuditEvent("ValidateSession", documentId, "Session validated successfully");
            return Task.FromResult(true);
        }
    }

    /// <summary>
    /// Gets the session timeout in minutes. Override to customize.
    /// Default is 0 (no timeout).
    /// </summary>
    protected virtual int SessionTimeoutMinutes => 0;

    /// <inheritdoc />
    public async Task LeaveDocument(string documentId)
    {
        ArgumentException.ThrowIfNullOrEmpty(documentId);

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, documentId);

        if (s_documentConnections.TryGetValue(documentId, out var connections))
        {
            lock (connections)
            {
                connections.Remove(Context.ConnectionId);
            }
        }

        s_presenceInfo.TryRemove(Context.ConnectionId, out var presence);
        s_cursorInfo.TryRemove(Context.ConnectionId, out _);

        LogAuditEvent("LeaveDocument", documentId, "User left");

        if (presence != null)
        {
            await Clients.Group(documentId).UserLeft(presence);
        }

        await Clients.Group(documentId).CursorRemoved(Context.ConnectionId);
        await BroadcastPresenceListAsync(documentId);
        await OnClientLeftAsync(documentId);
    }

    /// <inheritdoc />
    public async Task SendUpdate(StateUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);
        ArgumentException.ThrowIfNullOrEmpty(update.DocumentId);

        // Rate limiting
        if (!CheckRateLimit())
        {
            LogSecurityEvent("SendUpdate", update.DocumentId, "Rate limit exceeded");
            throw new HubException("Rate limit exceeded. Please slow down.");
        }

        // Size validation
        if (!ValidateMessageSize(update.StateJson, "StateUpdate"))
        {
            throw new HubException($"Message size exceeds maximum allowed ({MaxMessageSizeBytes} bytes)");
        }

        // Authorization
        if (!await CanEditDocumentAsync(update.DocumentId, Context.User!))
        {
            LogSecurityEvent("SendUpdate", update.DocumentId, "Edit access denied");
            throw new HubException("Edit access denied to document");
        }

        // Set sender info
        update.SenderId = Context.ConnectionId;
        update.Timestamp = DateTime.UtcNow;

        // Update version atomically
        update.Version = s_documentVersions.AddOrUpdate(update.DocumentId, 1, (_, version) => version + 1);

        LogAuditEvent("SendUpdate", update.DocumentId, $"Action: {update.Action}");

        await Clients.OthersInGroup(update.DocumentId).ReceiveUpdate(update);
    }

    /// <inheritdoc />
    public async Task SendOperation(StateOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentException.ThrowIfNullOrEmpty(operation.DocumentId);

        // Rate limiting
        if (!CheckRateLimit())
        {
            LogSecurityEvent("SendOperation", operation.DocumentId, "Rate limit exceeded");
            throw new HubException("Rate limit exceeded. Please slow down.");
        }

        // Authorization
        if (!await CanEditDocumentAsync(operation.DocumentId, Context.User!))
        {
            LogSecurityEvent("SendOperation", operation.DocumentId, "Edit access denied");
            throw new HubException("Edit access denied to document");
        }

        // Set sender info
        operation.SenderId = Context.ConnectionId;
        operation.Timestamp = DateTime.UtcNow;

        // Update version atomically
        operation.Version = s_documentVersions.AddOrUpdate(operation.DocumentId, 1, (_, version) => version + 1);

        LogAuditEvent("SendOperation", operation.DocumentId, $"Type: {operation.OperationType}, Path: {operation.Path}");

        await Clients.OthersInGroup(operation.DocumentId).ReceiveOperation(operation);
    }

    /// <inheritdoc />
    public async Task UpdateCursor(CursorInfo cursor)
    {
        ArgumentNullException.ThrowIfNull(cursor);

        if (string.IsNullOrEmpty(cursor.DocumentId))
        {
            return;
        }

        // Authorization
        if (!await CanAccessDocumentAsync(cursor.DocumentId, Context.User!))
        {
            return;
        }

        cursor.ConnectionId = Context.ConnectionId;
        cursor.Timestamp = DateTime.UtcNow;

        s_cursorInfo[Context.ConnectionId] = cursor;

        await Clients.OthersInGroup(cursor.DocumentId).CursorUpdated(cursor);
    }

    /// <inheritdoc />
    public async Task<string?> GetCurrentState(string documentId)
    {
        ArgumentException.ThrowIfNullOrEmpty(documentId);

        // Authorization
        if (!await CanAccessDocumentAsync(documentId, Context.User!))
        {
            LogSecurityEvent("GetCurrentState", documentId, "Access denied");
            throw new HubException("Access denied to document");
        }

        return await LoadStateAsync(documentId);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StateOperation>> GetOperationHistory(string documentId, long sinceVersion)
    {
        ArgumentException.ThrowIfNullOrEmpty(documentId);

        // Authorization
        if (!await CanAccessDocumentAsync(documentId, Context.User!))
        {
            LogSecurityEvent("GetOperationHistory", documentId, "Access denied");
            throw new HubException("Access denied to document");
        }

        return await LoadOperationHistoryAsync(documentId, sinceVersion);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<CursorInfo>> GetCursors(string documentId)
    {
        var cursors = s_cursorInfo.Values
            .Where(c => c.DocumentId == documentId)
            .ToList();
        return Task.FromResult<IReadOnlyList<CursorInfo>>(cursors);
    }

    /// <inheritdoc />
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Clean up all document associations
        var documentsToLeave = new List<string>();
        foreach (var kvp in s_documentConnections)
        {
            lock (kvp.Value)
            {
                if (kvp.Value.Contains(Context.ConnectionId))
                {
                    documentsToLeave.Add(kvp.Key);
                }
            }
        }

        foreach (var documentId in documentsToLeave)
        {
            try
            {
                await LeaveDocument(documentId);
            }
            catch
            {
                // Best effort cleanup
            }
        }

        // Remove per-connection rate-limit tracking; sessions are kept (bounded by
        // pruning below) so reconnecting clients can still validate their token.
        s_rateLimitTracking.TryRemove(Context.ConnectionId, out _);
        PruneStaleSessions();

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Removes sessions that have not been validated for over 24 hours to bound
    /// growth of the static session tracking dictionary.
    /// </summary>
    private static void PruneStaleSessions()
    {
        var cutoff = DateTime.UtcNow.AddHours(-24);
        foreach (var kvp in s_sessionTracking)
        {
            if (kvp.Value.LastValidated < cutoff)
            {
                s_sessionTracking.TryRemove(kvp.Key, out _);
            }
        }
    }

    private bool CheckRateLimit()
    {
        if (MaxMessagesPerSecond <= 0)
            return true;

        var timestamps = s_rateLimitTracking.GetOrAdd(Context.ConnectionId, _ => new Queue<DateTime>());

        lock (timestamps)
        {
            var now = DateTime.UtcNow;
            var oneSecondAgo = now.AddSeconds(-1);

            // Remove old timestamps
            while (timestamps.Count > 0 && timestamps.Peek() < oneSecondAgo)
            {
                timestamps.Dequeue();
            }

            // Check limit
            if (timestamps.Count >= MaxMessagesPerSecond)
            {
                return false;
            }

            timestamps.Enqueue(now);
            return true;
        }
    }

    private bool ValidateMessageSize(string json, string messageType)
    {
        var size = System.Text.Encoding.UTF8.GetByteCount(json);
        if (size > MaxMessageSizeBytes)
        {
            _logger?.LogWarning(
                "[SecureHub] {MessageType} rejected: size {Size} bytes exceeds limit of {Limit} bytes",
                messageType, size, MaxMessageSizeBytes);
            return false;
        }
        return true;
    }

    private PresenceInfo GetPresenceInfo()
    {
        if (s_presenceInfo.TryGetValue(Context.ConnectionId, out var info))
        {
            return info;
        }

        return new PresenceInfo
        {
            ConnectionId = Context.ConnectionId,
            DisplayName = GetUserDisplayName(),
            ConnectedAt = DateTime.UtcNow
        };
    }

    private async Task BroadcastPresenceListAsync(string documentId)
    {
        if (!s_documentConnections.TryGetValue(documentId, out var connections))
        {
            return;
        }

        List<string> connectionSnapshot;
        lock (connections)
        {
            connectionSnapshot = connections.ToList();
        }

        var presenceList = new List<PresenceInfo>();
        foreach (var connectionId in connectionSnapshot)
        {
            if (s_presenceInfo.TryGetValue(connectionId, out var presence))
            {
                presenceList.Add(presence);
            }
        }

        await Clients.Group(documentId).PresenceUpdated(presenceList);
    }

    private string? GetUserDisplayName()
    {
        return Context.User?.FindFirst(ClaimTypes.Name)?.Value
            ?? Context.User?.FindFirst(ClaimTypes.Email)?.Value
            ?? Context.User?.FindFirst("preferred_username")?.Value;
    }

    private void LogSecurityEvent(string method, string? documentId, string message)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
        _logger?.LogWarning(
            "[SecureHub Security] Method={Method}, Document={DocumentId}, User={UserId}, Message={Message}",
            method, documentId ?? "none", userId, message);
    }

    private void LogAuditEvent(string method, string? documentId, string message)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
        _logger?.LogInformation(
            "[SecureHub Audit] Method={Method}, Document={DocumentId}, User={UserId}, Connection={ConnectionId}, Message={Message}",
            method, documentId ?? "none", userId, Context.ConnectionId, message);
    }
}

/// <summary>
/// Tracks session information for reconnection validation.
/// </summary>
internal sealed class SessionInfo
{
    /// <summary>
    /// The session token.
    /// </summary>
    public required string Token { get; set; }

    /// <summary>
    /// The user ID associated with this session.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// The document ID for this session.
    /// </summary>
    public required string DocumentId { get; set; }

    /// <summary>
    /// When the session was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the session was last validated.
    /// </summary>
    public DateTime LastValidated { get; set; }
}

// SignalRSecurityGuidance has been moved to EasyAppDev.Blazor.Store.Security namespace
// See: src/EasyAppDev.Blazor.Store/Security/SignalRSecurityGuidance.cs
