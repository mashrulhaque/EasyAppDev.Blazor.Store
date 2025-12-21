// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

namespace EasyAppDev.Blazor.Store.ServerSync;

/// <summary>
/// Represents a state update for synchronization.
/// </summary>
public class StateUpdate
{
    /// <summary>
    /// Gets or sets the serialized state.
    /// </summary>
    public required string StateJson { get; set; }

    /// <summary>
    /// Gets or sets the action that caused the update.
    /// </summary>
    public string? Action { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the update.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the connection ID of the sender.
    /// </summary>
    public string? SenderId { get; set; }

    /// <summary>
    /// Gets or sets the document ID for multi-tenant scenarios.
    /// </summary>
    public string? DocumentId { get; set; }

    /// <summary>
    /// Gets or sets a version number for conflict detection.
    /// </summary>
    public long Version { get; set; }

    /// <summary>
    /// Gets or sets the HMAC-SHA256 signature for message integrity verification.
    /// Only set when EnableMessageSigning is true in ServerSyncOptions.
    /// </summary>
    public string? Signature { get; set; }
}

/// <summary>
/// Represents an operation-based update for fine-grained synchronization.
/// Operations are smaller, more precise updates compared to full state replacement.
/// </summary>
public class StateOperation
{
    private static readonly HashSet<string> ValidOperationTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "SET", "INSERT", "DELETE", "UPDATE"
    };

    private string _operationType = string.Empty;
    private string _path = string.Empty;

    /// <summary>
    /// Gets or sets a unique identifier for this operation.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the type of operation (e.g., "SET", "INSERT", "DELETE", "UPDATE").
    /// Only predefined operation types are allowed to prevent injection attacks.
    /// </summary>
    public required string OperationType
    {
        get => _operationType;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("OperationType cannot be null or empty", nameof(OperationType));

            if (!ValidOperationTypes.Contains(value))
                throw new ArgumentException(
                    $"Invalid OperationType '{value}'. Allowed values: {string.Join(", ", ValidOperationTypes)}",
                    nameof(OperationType));

            _operationType = value;
        }
    }

    /// <summary>
    /// Gets or sets the path to the property being modified (e.g., "user.profile.name").
    /// Path traversal patterns (../) are not allowed for security.
    /// </summary>
    public required string Path
    {
        get => _path;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Path cannot be null or empty", nameof(Path));

            // Prevent path traversal attacks
            if (value.Contains(".."))
                throw new ArgumentException("Path cannot contain '..' for security reasons", nameof(Path));

            _path = value;
        }
    }

    /// <summary>
    /// Gets or sets the new value as JSON.
    /// </summary>
    public string? ValueJson { get; set; }

    /// <summary>
    /// Gets or sets the previous value as JSON (for undo support).
    /// </summary>
    public string? PreviousValueJson { get; set; }

    /// <summary>
    /// Gets or sets the document ID.
    /// </summary>
    public string? DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the connection ID of the sender.
    /// </summary>
    public string? SenderId { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the operation.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the version number for ordering.
    /// </summary>
    public long Version { get; set; }

    /// <summary>
    /// Gets or sets optional metadata for the operation.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Creates a SET operation.
    /// </summary>
    public static StateOperation Set(string path, string valueJson, string? documentId = null)
        => new() { OperationType = "SET", Path = path, ValueJson = valueJson, DocumentId = documentId };

    /// <summary>
    /// Creates an INSERT operation for collections.
    /// </summary>
    public static StateOperation Insert(string path, int index, string valueJson, string? documentId = null)
        => new()
        {
            OperationType = "INSERT",
            Path = path,
            ValueJson = valueJson,
            DocumentId = documentId,
            Metadata = new Dictionary<string, object> { ["index"] = index }
        };

    /// <summary>
    /// Creates a DELETE operation.
    /// </summary>
    public static StateOperation Delete(string path, string? documentId = null)
        => new() { OperationType = "DELETE", Path = path, DocumentId = documentId };

    /// <summary>
    /// Creates an UPDATE operation for transforming values.
    /// </summary>
    public static StateOperation Update(string path, string valueJson, string? previousValueJson = null, string? documentId = null)
        => new()
        {
            OperationType = "UPDATE",
            Path = path,
            ValueJson = valueJson,
            PreviousValueJson = previousValueJson,
            DocumentId = documentId
        };
}

/// <summary>
/// Represents cursor/selection information for collaborative editing.
/// </summary>
public class CursorInfo
{
    /// <summary>
    /// Gets or sets the connection ID of the user.
    /// </summary>
    public string ConnectionId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the document ID.
    /// </summary>
    public string? DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the display name of the user.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the cursor color for visual distinction.
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// Gets or sets the cursor position (for text editing).
    /// </summary>
    public int? Position { get; set; }

    /// <summary>
    /// Gets or sets the selection start position.
    /// </summary>
    public int? SelectionStart { get; set; }

    /// <summary>
    /// Gets or sets the selection end position.
    /// </summary>
    public int? SelectionEnd { get; set; }

    /// <summary>
    /// Gets or sets the path to the element the cursor is in.
    /// </summary>
    public string? ElementPath { get; set; }

    /// <summary>
    /// Gets or sets the X coordinate (for canvas/visual editors).
    /// </summary>
    public double? X { get; set; }

    /// <summary>
    /// Gets or sets the Y coordinate (for canvas/visual editors).
    /// </summary>
    public double? Y { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last update.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets custom metadata.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Gets whether this cursor has a selection.
    /// </summary>
    public bool HasSelection => SelectionStart.HasValue && SelectionEnd.HasValue && SelectionStart != SelectionEnd;
}

/// <summary>
/// Interface for custom conflict resolution.
/// </summary>
/// <typeparam name="TState">The type of state.</typeparam>
public interface IConflictResolver<TState> where TState : notnull
{
    /// <summary>
    /// Resolves a conflict between local and remote state.
    /// </summary>
    /// <param name="local">The local state.</param>
    /// <param name="remote">The remote state.</param>
    /// <param name="common">The common ancestor state, if available.</param>
    /// <returns>The resolved state.</returns>
    TState Resolve(TState local, TState remote, TState? common);
}

/// <summary>
/// Last-write-wins conflict resolver. The most recent state wins.
/// </summary>
public class LastWriteWinsResolver<TState> : IConflictResolver<TState> where TState : notnull
{
    /// <inheritdoc />
    public TState Resolve(TState local, TState remote, TState? common) => remote;
}

/// <summary>
/// Server-wins conflict resolver. Server state always takes precedence.
/// </summary>
public class ServerWinsResolver<TState> : IConflictResolver<TState> where TState : notnull
{
    /// <inheritdoc />
    public TState Resolve(TState local, TState remote, TState? common) => remote;
}

/// <summary>
/// Client-wins conflict resolver. Local state always takes precedence.
/// </summary>
public class ClientWinsResolver<TState> : IConflictResolver<TState> where TState : notnull
{
    /// <inheritdoc />
    public TState Resolve(TState local, TState remote, TState? common) => local;
}
