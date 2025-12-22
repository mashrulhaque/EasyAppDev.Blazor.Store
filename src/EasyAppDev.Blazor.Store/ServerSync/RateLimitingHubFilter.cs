// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EasyAppDev.Blazor.Store.ServerSync;

/// <summary>
/// Options for the rate limiting hub filter.
/// </summary>
public sealed class RateLimitingOptions
{
    /// <summary>
    /// Gets or sets the maximum number of messages allowed per second per connection.
    /// Default is 10.
    /// </summary>
    public int MaxMessagesPerSecond { get; set; } = 10;

    /// <summary>
    /// Gets or sets the maximum number of messages allowed per second per document/group.
    /// Default is 100.
    /// </summary>
    public int MaxMessagesPerSecondPerDocument { get; set; } = 100;

    /// <summary>
    /// Gets or sets the maximum number of messages allowed in a burst window.
    /// Allows short bursts of activity above the per-second limit.
    /// Default is 20.
    /// </summary>
    public int BurstLimit { get; set; } = 20;

    /// <summary>
    /// Gets or sets the burst window duration in seconds.
    /// Default is 5 seconds.
    /// </summary>
    public int BurstWindowSeconds { get; set; } = 5;

    /// <summary>
    /// Gets or sets whether to block connections that exceed rate limits.
    /// When true, the connection is terminated. When false, messages are simply dropped.
    /// Default is false.
    /// </summary>
    public bool BlockExcessiveConnections { get; set; } = false;

    /// <summary>
    /// Gets or sets the number of rate limit violations before blocking a connection.
    /// Only applies when <see cref="BlockExcessiveConnections"/> is true.
    /// Default is 5.
    /// </summary>
    public int ViolationsBeforeBlock { get; set; } = 5;

    /// <summary>
    /// Gets or sets a callback invoked when a connection is rate limited.
    /// </summary>
    public Action<string, string>? OnRateLimited { get; set; }

    /// <summary>
    /// Gets or sets a callback invoked when a connection is blocked for excessive violations.
    /// </summary>
    public Action<string>? OnConnectionBlocked { get; set; }

    /// <summary>
    /// Gets or sets whether to enable detailed logging of rate limit events.
    /// Default is false.
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;
}

/// <summary>
/// Hub filter that implements rate limiting for SignalR hubs.
/// Prevents DoS attacks by limiting the number of messages per connection and per document.
/// </summary>
/// <remarks>
/// Apply this filter to your hub using ASP.NET Core's hub filter registration:
/// <code>
/// services.AddSignalR(options =>
/// {
///     options.AddFilter&lt;RateLimitingHubFilter&gt;();
/// });
/// </code>
/// </remarks>
public sealed class RateLimitingHubFilter : IHubFilter, IDisposable
{
    private readonly RateLimitingOptions _options;
    private readonly ILogger<RateLimitingHubFilter>? _logger;
    private readonly ConcurrentDictionary<string, ConnectionRateLimit> _connectionLimits = new();
    private readonly ConcurrentDictionary<string, DocumentRateLimit> _documentLimits = new();
    private readonly Timer _cleanupTimer;

    /// <summary>
    /// Creates a new rate limiting hub filter.
    /// </summary>
    /// <param name="options">Rate limiting options.</param>
    /// <param name="logger">Optional logger.</param>
    public RateLimitingHubFilter(
        IOptions<RateLimitingOptions>? options = null,
        ILogger<RateLimitingHubFilter>? logger = null)
    {
        _options = options?.Value ?? new RateLimitingOptions();
        _logger = logger;

        // Cleanup stale entries every minute
        _cleanupTimer = new Timer(
            CleanupStaleEntries,
            null,
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(1));
    }

    /// <inheritdoc />
    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var connectionId = invocationContext.Context.ConnectionId;
        var methodName = invocationContext.HubMethodName;

        // Check connection rate limit
        if (!CheckConnectionRateLimit(connectionId, methodName))
        {
            _options.OnRateLimited?.Invoke(connectionId, methodName);

            if (_options.EnableDetailedLogging)
            {
                _logger?.LogWarning(
                    "Rate limit exceeded for connection {ConnectionId} on method {Method}",
                    connectionId,
                    methodName);
            }

            // Check if we should block the connection
            var connectionLimit = _connectionLimits.GetOrAdd(connectionId, _ => new ConnectionRateLimit());
            if (_options.BlockExcessiveConnections &&
                connectionLimit.ViolationCount >= _options.ViolationsBeforeBlock)
            {
                _logger?.LogWarning(
                    "Blocking connection {ConnectionId} due to excessive rate limit violations ({Count})",
                    connectionId,
                    connectionLimit.ViolationCount);

                _options.OnConnectionBlocked?.Invoke(connectionId);
                invocationContext.Context.Abort();
                return null;
            }

            throw new HubException("Rate limit exceeded. Please slow down.");
        }

        // Check document rate limit if this is a document-scoped operation
        var documentId = GetDocumentId(invocationContext);
        if (documentId != null && !CheckDocumentRateLimit(documentId, methodName))
        {
            if (_options.EnableDetailedLogging)
            {
                _logger?.LogWarning(
                    "Document rate limit exceeded for document {DocumentId} on method {Method}",
                    documentId,
                    methodName);
            }

            throw new HubException("Document rate limit exceeded. Too many updates to this document.");
        }

        return await next(invocationContext).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task OnConnectedAsync(
        HubLifetimeContext context,
        Func<HubLifetimeContext, Task> next)
    {
        // Initialize rate limit tracking for new connection
        _connectionLimits.TryAdd(context.Context.ConnectionId, new ConnectionRateLimit());
        return next(context);
    }

    /// <inheritdoc />
    public Task OnDisconnectedAsync(
        HubLifetimeContext context,
        Exception? exception,
        Func<HubLifetimeContext, Exception?, Task> next)
    {
        // Clean up rate limit tracking for disconnected connection
        _connectionLimits.TryRemove(context.Context.ConnectionId, out _);
        return next(context, exception);
    }

    private bool CheckConnectionRateLimit(string connectionId, string methodName)
    {
        var limit = _connectionLimits.GetOrAdd(connectionId, _ => new ConnectionRateLimit());
        var now = DateTime.UtcNow;

        lock (limit)
        {
            // Clean old timestamps
            var oneSecondAgo = now.AddSeconds(-1);
            while (limit.RecentMessages.Count > 0 && limit.RecentMessages.Peek() < oneSecondAgo)
            {
                limit.RecentMessages.Dequeue();
            }

            // Clean burst window
            var burstWindowStart = now.AddSeconds(-_options.BurstWindowSeconds);
            while (limit.BurstMessages.Count > 0 && limit.BurstMessages.Peek() < burstWindowStart)
            {
                limit.BurstMessages.Dequeue();
            }

            // Check per-second limit
            if (limit.RecentMessages.Count >= _options.MaxMessagesPerSecond)
            {
                limit.ViolationCount++;
                return false;
            }

            // Check burst limit
            if (limit.BurstMessages.Count >= _options.BurstLimit)
            {
                limit.ViolationCount++;
                return false;
            }

            // Record this message
            limit.RecentMessages.Enqueue(now);
            limit.BurstMessages.Enqueue(now);
            limit.LastActivity = now;

            return true;
        }
    }

    private bool CheckDocumentRateLimit(string documentId, string methodName)
    {
        var limit = _documentLimits.GetOrAdd(documentId, _ => new DocumentRateLimit());
        var now = DateTime.UtcNow;

        lock (limit)
        {
            // Clean old timestamps
            var oneSecondAgo = now.AddSeconds(-1);
            while (limit.RecentMessages.Count > 0 && limit.RecentMessages.Peek() < oneSecondAgo)
            {
                limit.RecentMessages.Dequeue();
            }

            // Check per-second limit for document
            if (limit.RecentMessages.Count >= _options.MaxMessagesPerSecondPerDocument)
            {
                return false;
            }

            // Record this message
            limit.RecentMessages.Enqueue(now);
            limit.LastActivity = now;

            return true;
        }
    }

    private static string? GetDocumentId(HubInvocationContext context)
    {
        // Try to extract document ID from method arguments
        foreach (var arg in context.HubMethodArguments)
        {
            if (arg is StateUpdate stateUpdate)
            {
                return stateUpdate.DocumentId;
            }

            if (arg is StateOperation stateOperation)
            {
                return stateOperation.DocumentId;
            }
        }

        // Check hub items for stored document ID
        if (context.Context.Items.TryGetValue("DocumentId", out var docId))
        {
            return docId?.ToString();
        }

        return null;
    }

    private void CleanupStaleEntries(object? state)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-5);

        // Remove stale connection entries
        foreach (var (connectionId, limit) in _connectionLimits)
        {
            if (limit.LastActivity < cutoff)
            {
                _connectionLimits.TryRemove(connectionId, out _);
            }
        }

        // Remove stale document entries
        foreach (var (documentId, limit) in _documentLimits)
        {
            if (limit.LastActivity < cutoff)
            {
                _documentLimits.TryRemove(documentId, out _);
            }
        }
    }

    private sealed class ConnectionRateLimit
    {
        public Queue<DateTime> RecentMessages { get; } = new();
        public Queue<DateTime> BurstMessages { get; } = new();
        public DateTime LastActivity { get; set; } = DateTime.UtcNow;
        public int ViolationCount { get; set; }
    }

    private sealed class DocumentRateLimit
    {
        public Queue<DateTime> RecentMessages { get; } = new();
        public DateTime LastActivity { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Disposes the rate limiting hub filter and stops the cleanup timer.
    /// </summary>
    public void Dispose()
    {
        _cleanupTimer.Dispose();
        _connectionLimits.Clear();
        _documentLimits.Clear();
    }
}
