// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace EasyAppDev.Blazor.Store.Persistence;

/// <summary>
/// Wrapper for persisted state that includes metadata for integrity verification.
/// Supports format versioning for backward compatibility.
/// </summary>
public sealed class PersistedStateWrapper
{
    /// <summary>
    /// Gets or sets the version of the persistence format.
    /// Current version is 1.
    /// </summary>
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    /// <summary>
    /// Gets or sets the serialized state data (JSON string).
    /// </summary>
    [JsonPropertyName("state")]
    public required string State { get; set; }

    /// <summary>
    /// Gets or sets the HMAC signature of the state (base64-encoded).
    /// Used for integrity verification to detect tampering.
    /// </summary>
    [JsonPropertyName("signature")]
    public string? Signature { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the state was persisted (UTC).
    /// Useful for staleness detection and debugging.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the size of the state in bytes (before wrapping).
    /// Used for diagnostics and monitoring.
    /// </summary>
    [JsonPropertyName("size")]
    public int Size { get; set; }
}

/// <summary>
/// Exception thrown when persisted state fails integrity verification.
/// </summary>
public sealed class StateIntegrityException : InvalidOperationException
{
    /// <summary>
    /// Creates a new state integrity exception.
    /// </summary>
    public StateIntegrityException()
        : base("State integrity check failed. The persisted state may have been tampered with.")
    {
    }

    /// <summary>
    /// Creates a new state integrity exception with a custom message.
    /// </summary>
    /// <param name="message">The exception message.</param>
    public StateIntegrityException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Creates a new state integrity exception with a custom message and inner exception.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The inner exception.</param>
    public StateIntegrityException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when persisted state exceeds size limits.
/// </summary>
public sealed class StateSizeExceededException : InvalidOperationException
{
    /// <summary>
    /// Gets the actual size of the state in bytes.
    /// </summary>
    public int ActualSize { get; }

    /// <summary>
    /// Gets the maximum allowed size in bytes.
    /// </summary>
    public int MaxSize { get; }

    /// <summary>
    /// Creates a new state size exceeded exception.
    /// </summary>
    /// <param name="actualSize">The actual size of the state.</param>
    /// <param name="maxSize">The maximum allowed size.</param>
    public StateSizeExceededException(int actualSize, int maxSize)
        : base($"State size ({actualSize:N0} bytes) exceeds maximum allowed size ({maxSize:N0} bytes).")
    {
        ActualSize = actualSize;
        MaxSize = maxSize;
    }

    /// <summary>
    /// Creates a new state size exceeded exception with an inner exception.
    /// </summary>
    /// <param name="actualSize">The actual size of the state.</param>
    /// <param name="maxSize">The maximum allowed size.</param>
    /// <param name="innerException">The inner exception.</param>
    public StateSizeExceededException(int actualSize, int maxSize, Exception innerException)
        : base($"State size ({actualSize:N0} bytes) exceeds maximum allowed size ({maxSize:N0} bytes).", innerException)
    {
        ActualSize = actualSize;
        MaxSize = maxSize;
    }
}
