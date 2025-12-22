// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;

namespace EasyAppDev.Blazor.Store.Security;

/// <summary>
/// Categories of security events for audit logging.
/// </summary>
public enum SecurityEventType
{
    // Authentication & Authorization
    /// <summary>
    /// User authentication attempt.
    /// </summary>
    AuthenticationAttempt,

    /// <summary>
    /// Authorization check performed.
    /// </summary>
    AuthorizationCheck,

    /// <summary>
    /// Access denied to a resource.
    /// </summary>
    AccessDenied,

    // Validation Events
    /// <summary>
    /// State validation was performed.
    /// </summary>
    StateValidation,

    /// <summary>
    /// State validation failed.
    /// </summary>
    StateValidationFailed,

    /// <summary>
    /// Invalid state was rejected.
    /// </summary>
    InvalidStateRejected,

    // Signature & Integrity
    /// <summary>
    /// Message signature verification.
    /// </summary>
    SignatureVerification,

    /// <summary>
    /// Message signature verification failed.
    /// </summary>
    SignatureVerificationFailed,

    /// <summary>
    /// Integrity check failed.
    /// </summary>
    IntegrityCheckFailed,

    // Session Events
    /// <summary>
    /// Session created.
    /// </summary>
    SessionCreated,

    /// <summary>
    /// Session validation performed.
    /// </summary>
    SessionValidation,

    /// <summary>
    /// Session validation failed.
    /// </summary>
    SessionValidationFailed,

    /// <summary>
    /// Session expired.
    /// </summary>
    SessionExpired,

    /// <summary>
    /// Session was invalidated or terminated.
    /// </summary>
    SessionInvalidated,

    // Rate Limiting & DoS Protection
    /// <summary>
    /// Rate limit was exceeded.
    /// </summary>
    RateLimitExceeded,

    /// <summary>
    /// Message size limit was exceeded.
    /// </summary>
    MessageSizeExceeded,

    /// <summary>
    /// Possible denial-of-service attempt detected.
    /// </summary>
    PossibleDoSDetected,

    // Synchronization Security
    /// <summary>
    /// Cross-tab sync message received.
    /// </summary>
    TabSyncMessageReceived,

    /// <summary>
    /// Cross-tab sync message rejected.
    /// </summary>
    TabSyncMessageRejected,

    /// <summary>
    /// Server sync state received.
    /// </summary>
    ServerSyncStateReceived,

    /// <summary>
    /// Server sync state rejected.
    /// </summary>
    ServerSyncStateRejected,

    // Suspicious Activity
    /// <summary>
    /// Suspicious activity detected (e.g., version jumps, unusual patterns).
    /// </summary>
    SuspiciousActivity,

    /// <summary>
    /// Replay attack attempt detected.
    /// </summary>
    ReplayAttackDetected,

    /// <summary>
    /// Future timestamp detected (possible clock manipulation).
    /// </summary>
    FutureTimestampDetected,

    // Configuration Events
    /// <summary>
    /// Security configuration was validated.
    /// </summary>
    ConfigurationValidated,

    /// <summary>
    /// Security configuration warning was raised.
    /// </summary>
    ConfigurationWarning,

    // Key Management
    /// <summary>
    /// Signing key was rotated.
    /// </summary>
    KeyRotation,

    /// <summary>
    /// Key derivation was performed.
    /// </summary>
    KeyDerivation,

    // Sensitive Data
    /// <summary>
    /// Sensitive data was filtered during serialization.
    /// </summary>
    SensitiveDataFiltered,

    /// <summary>
    /// Attempt to access sensitive data was blocked.
    /// </summary>
    SensitiveDataAccessBlocked
}

/// <summary>
/// Represents an entry in the security audit log.
/// </summary>
public sealed record SecurityAuditEntry
{
    /// <summary>
    /// Gets the unique identifier for this audit entry.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the timestamp when this event occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the type of security event.
    /// </summary>
    public SecurityEventType EventType { get; init; }

    /// <summary>
    /// Gets the component that generated this event (e.g., "TabSync", "ServerSync", "Persistence").
    /// </summary>
    public required string Component { get; init; }

    /// <summary>
    /// Gets a human-readable description of the event.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the correlation ID for tracing related events across components.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Gets the user or connection identifier associated with this event.
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// Gets the session identifier associated with this event.
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>
    /// Gets the action that triggered this event.
    /// </summary>
    public string? Action { get; init; }

    /// <summary>
    /// Gets additional details about the event.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Details { get; init; }

    /// <summary>
    /// Gets whether this event represents a security threat or issue.
    /// </summary>
    public bool IsThreat { get; init; }

    /// <summary>
    /// Gets the severity level of this event for logging purposes.
    /// </summary>
    public LogLevel LogLevel { get; init; } = LogLevel.Information;

    /// <summary>
    /// Gets the source IP address if available.
    /// </summary>
    public string? SourceIp { get; init; }

    /// <summary>
    /// Gets the document or resource ID related to this event.
    /// </summary>
    public string? ResourceId { get; init; }
}

/// <summary>
/// Interface for security audit logging.
/// Implementations can log to various backends (console, file, database, SIEM).
/// </summary>
public interface ISecurityAuditLogger
{
    /// <summary>
    /// Logs a security audit entry.
    /// </summary>
    /// <param name="entry">The audit entry to log.</param>
    void Log(SecurityAuditEntry entry);

    /// <summary>
    /// Logs a security audit entry asynchronously.
    /// </summary>
    /// <param name="entry">The audit entry to log.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    Task LogAsync(SecurityAuditEntry entry, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default implementation of security audit logger that writes to ILogger.
/// </summary>
public sealed class SecurityAuditLogger : ISecurityAuditLogger
{
    private readonly ILogger<SecurityAuditLogger> _logger;
    private readonly SecurityAuditOptions _options;

    /// <summary>
    /// Creates a new security audit logger.
    /// </summary>
    /// <param name="logger">The underlying logger.</param>
    /// <param name="options">Audit logging options.</param>
    public SecurityAuditLogger(ILogger<SecurityAuditLogger> logger, SecurityAuditOptions? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new SecurityAuditOptions();
    }

    /// <inheritdoc />
    public void Log(SecurityAuditEntry entry)
    {
        if (!ShouldLog(entry)) return;

        var state = BuildLogState(entry);

        _logger.Log(
            entry.LogLevel,
            "[SecurityAudit] [{EventType}] {Component}: {Message}",
            entry.EventType,
            entry.Component,
            entry.Message);

        if (_options.IncludeStructuredData && entry.Details != null)
        {
            foreach (var detail in entry.Details)
            {
                _logger.LogDebug("  {Key}: {Value}", detail.Key, detail.Value);
            }
        }
    }

    /// <inheritdoc />
    public Task LogAsync(SecurityAuditEntry entry, CancellationToken cancellationToken = default)
    {
        Log(entry);
        return Task.CompletedTask;
    }

    private bool ShouldLog(SecurityAuditEntry entry)
    {
        if (!_options.Enabled) return false;

        // Check minimum log level
        if (entry.LogLevel < _options.MinimumLogLevel) return false;

        // Check if event type is filtered
        if (_options.ExcludedEventTypes.Contains(entry.EventType)) return false;

        // If whitelist is configured, only log those types
        if (_options.IncludedEventTypes.Count > 0 &&
            !_options.IncludedEventTypes.Contains(entry.EventType))
        {
            return false;
        }

        return true;
    }

    private Dictionary<string, object> BuildLogState(SecurityAuditEntry entry)
    {
        var state = new Dictionary<string, object>
        {
            ["AuditId"] = entry.Id,
            ["Timestamp"] = entry.Timestamp,
            ["EventType"] = entry.EventType.ToString(),
            ["Component"] = entry.Component,
            ["Message"] = entry.Message,
            ["IsThreat"] = entry.IsThreat
        };

        if (entry.CorrelationId != null)
            state["CorrelationId"] = entry.CorrelationId;

        if (entry.UserId != null)
            state["UserId"] = entry.UserId;

        if (entry.SessionId != null)
            state["SessionId"] = entry.SessionId;

        if (entry.Action != null)
            state["Action"] = entry.Action;

        if (entry.SourceIp != null)
            state["SourceIp"] = entry.SourceIp;

        if (entry.ResourceId != null)
            state["ResourceId"] = entry.ResourceId;

        if (entry.Details != null)
        {
            foreach (var detail in entry.Details)
            {
                state[$"Detail_{detail.Key}"] = detail.Value;
            }
        }

        return state;
    }
}

/// <summary>
/// Options for security audit logging.
/// </summary>
public sealed class SecurityAuditOptions
{
    /// <summary>
    /// Gets or sets whether audit logging is enabled.
    /// Default is true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the minimum log level for audit entries.
    /// Default is Information.
    /// </summary>
    public LogLevel MinimumLogLevel { get; set; } = LogLevel.Information;

    /// <summary>
    /// Gets or sets whether to include structured data in log output.
    /// Default is true.
    /// </summary>
    public bool IncludeStructuredData { get; set; } = true;

    /// <summary>
    /// Gets or sets event types to exclude from logging.
    /// </summary>
    public HashSet<SecurityEventType> ExcludedEventTypes { get; set; } = new();

    /// <summary>
    /// Gets or sets event types to include in logging.
    /// If empty, all types are included (except those in ExcludedEventTypes).
    /// </summary>
    public HashSet<SecurityEventType> IncludedEventTypes { get; set; } = new();
}

/// <summary>
/// Builder for creating security audit entries with fluent API.
/// </summary>
public sealed class SecurityAuditEntryBuilder
{
    private SecurityEventType _eventType;
    private string _component = "Unknown";
    private string _message = "";
    private string? _correlationId;
    private string? _userId;
    private string? _sessionId;
    private string? _action;
    private Dictionary<string, object>? _details;
    private bool _isThreat;
    private LogLevel _logLevel = LogLevel.Information;
    private string? _sourceIp;
    private string? _resourceId;

    /// <summary>
    /// Sets the event type.
    /// </summary>
    public SecurityAuditEntryBuilder WithEventType(SecurityEventType eventType)
    {
        _eventType = eventType;

        // Automatically set log level and threat flag based on event type
        switch (eventType)
        {
            case SecurityEventType.StateValidationFailed:
            case SecurityEventType.SignatureVerificationFailed:
            case SecurityEventType.SessionValidationFailed:
            case SecurityEventType.RateLimitExceeded:
            case SecurityEventType.MessageSizeExceeded:
            case SecurityEventType.TabSyncMessageRejected:
            case SecurityEventType.ServerSyncStateRejected:
                _logLevel = LogLevel.Warning;
                _isThreat = true;
                break;

            case SecurityEventType.AccessDenied:
            case SecurityEventType.InvalidStateRejected:
            case SecurityEventType.IntegrityCheckFailed:
            case SecurityEventType.SuspiciousActivity:
            case SecurityEventType.ReplayAttackDetected:
            case SecurityEventType.FutureTimestampDetected:
            case SecurityEventType.PossibleDoSDetected:
                _logLevel = LogLevel.Warning;
                _isThreat = true;
                break;

            case SecurityEventType.ConfigurationWarning:
                _logLevel = LogLevel.Warning;
                break;
        }

        return this;
    }

    /// <summary>
    /// Sets the component name.
    /// </summary>
    public SecurityAuditEntryBuilder WithComponent(string component)
    {
        _component = component;
        return this;
    }

    /// <summary>
    /// Sets the message.
    /// </summary>
    public SecurityAuditEntryBuilder WithMessage(string message)
    {
        _message = message;
        return this;
    }

    /// <summary>
    /// Sets the correlation ID for tracing.
    /// </summary>
    public SecurityAuditEntryBuilder WithCorrelationId(string? correlationId)
    {
        _correlationId = correlationId;
        return this;
    }

    /// <summary>
    /// Sets the user ID.
    /// </summary>
    public SecurityAuditEntryBuilder WithUserId(string? userId)
    {
        _userId = userId;
        return this;
    }

    /// <summary>
    /// Sets the session ID.
    /// </summary>
    public SecurityAuditEntryBuilder WithSessionId(string? sessionId)
    {
        _sessionId = sessionId;
        return this;
    }

    /// <summary>
    /// Sets the action that triggered this event.
    /// </summary>
    public SecurityAuditEntryBuilder WithAction(string? action)
    {
        _action = action;
        return this;
    }

    /// <summary>
    /// Adds a detail to the entry.
    /// </summary>
    public SecurityAuditEntryBuilder WithDetail(string key, object value)
    {
        _details ??= new Dictionary<string, object>();
        _details[key] = value;
        return this;
    }

    /// <summary>
    /// Sets multiple details at once.
    /// </summary>
    public SecurityAuditEntryBuilder WithDetails(Dictionary<string, object> details)
    {
        _details = details;
        return this;
    }

    /// <summary>
    /// Marks this event as a security threat.
    /// </summary>
    public SecurityAuditEntryBuilder AsThreat(bool isThreat = true)
    {
        _isThreat = isThreat;
        return this;
    }

    /// <summary>
    /// Sets the log level.
    /// </summary>
    public SecurityAuditEntryBuilder WithLogLevel(LogLevel logLevel)
    {
        _logLevel = logLevel;
        return this;
    }

    /// <summary>
    /// Sets the source IP address.
    /// </summary>
    public SecurityAuditEntryBuilder WithSourceIp(string? sourceIp)
    {
        _sourceIp = sourceIp;
        return this;
    }

    /// <summary>
    /// Sets the resource ID.
    /// </summary>
    public SecurityAuditEntryBuilder WithResourceId(string? resourceId)
    {
        _resourceId = resourceId;
        return this;
    }

    /// <summary>
    /// Builds the audit entry.
    /// </summary>
    public SecurityAuditEntry Build()
    {
        return new SecurityAuditEntry
        {
            EventType = _eventType,
            Component = _component,
            Message = _message,
            CorrelationId = _correlationId,
            UserId = _userId,
            SessionId = _sessionId,
            Action = _action,
            Details = _details,
            IsThreat = _isThreat,
            LogLevel = _logLevel,
            SourceIp = _sourceIp,
            ResourceId = _resourceId
        };
    }
}

/// <summary>
/// Extension methods for security audit logging.
/// </summary>
public static class SecurityAuditExtensions
{
    /// <summary>
    /// Creates a new audit entry builder.
    /// </summary>
    public static SecurityAuditEntryBuilder CreateAuditEntry(this ISecurityAuditLogger logger)
    {
        return new SecurityAuditEntryBuilder();
    }

    /// <summary>
    /// Logs a validation failure event.
    /// </summary>
    public static void LogValidationFailed(
        this ISecurityAuditLogger logger,
        string component,
        StateValidationResult result,
        string? action = null,
        string? correlationId = null)
    {
        var entry = new SecurityAuditEntryBuilder()
            .WithEventType(SecurityEventType.StateValidationFailed)
            .WithComponent(component)
            .WithMessage($"State validation failed: {string.Join(", ", result.Errors)}")
            .WithAction(action)
            .WithCorrelationId(correlationId)
            .WithDetail("ErrorCount", result.Errors.Count)
            .WithDetail("Errors", result.Errors)
            .AsThreat()
            .Build();

        logger.Log(entry);
    }

    /// <summary>
    /// Logs a signature verification failure.
    /// </summary>
    public static void LogSignatureVerificationFailed(
        this ISecurityAuditLogger logger,
        string component,
        string reason,
        string? correlationId = null)
    {
        var entry = new SecurityAuditEntryBuilder()
            .WithEventType(SecurityEventType.SignatureVerificationFailed)
            .WithComponent(component)
            .WithMessage($"Message signature verification failed: {reason}")
            .WithCorrelationId(correlationId)
            .WithDetail("Reason", reason)
            .AsThreat()
            .Build();

        logger.Log(entry);
    }

    /// <summary>
    /// Logs a rate limit exceeded event.
    /// </summary>
    public static void LogRateLimitExceeded(
        this ISecurityAuditLogger logger,
        string component,
        int currentRate,
        int limit,
        string? userId = null,
        string? correlationId = null)
    {
        var entry = new SecurityAuditEntryBuilder()
            .WithEventType(SecurityEventType.RateLimitExceeded)
            .WithComponent(component)
            .WithMessage($"Rate limit exceeded: {currentRate}/{limit} requests per second")
            .WithUserId(userId)
            .WithCorrelationId(correlationId)
            .WithDetail("CurrentRate", currentRate)
            .WithDetail("Limit", limit)
            .AsThreat()
            .Build();

        logger.Log(entry);
    }

    /// <summary>
    /// Logs suspicious activity.
    /// </summary>
    public static void LogSuspiciousActivity(
        this ISecurityAuditLogger logger,
        string component,
        string description,
        string? userId = null,
        string? correlationId = null,
        Dictionary<string, object>? details = null)
    {
        var builder = new SecurityAuditEntryBuilder()
            .WithEventType(SecurityEventType.SuspiciousActivity)
            .WithComponent(component)
            .WithMessage(description)
            .WithUserId(userId)
            .WithCorrelationId(correlationId)
            .AsThreat();

        if (details != null)
        {
            builder.WithDetails(details);
        }

        logger.Log(builder.Build());
    }

    /// <summary>
    /// Logs a session event.
    /// </summary>
    public static void LogSessionEvent(
        this ISecurityAuditLogger logger,
        SecurityEventType eventType,
        string component,
        string sessionId,
        string? userId = null,
        string? message = null)
    {
        var defaultMessage = eventType switch
        {
            SecurityEventType.SessionCreated => "Session created",
            SecurityEventType.SessionValidation => "Session validated",
            SecurityEventType.SessionValidationFailed => "Session validation failed",
            SecurityEventType.SessionExpired => "Session expired",
            SecurityEventType.SessionInvalidated => "Session invalidated",
            _ => eventType.ToString()
        };

        var entry = new SecurityAuditEntryBuilder()
            .WithEventType(eventType)
            .WithComponent(component)
            .WithMessage(message ?? defaultMessage)
            .WithSessionId(sessionId)
            .WithUserId(userId)
            .Build();

        logger.Log(entry);
    }
}
