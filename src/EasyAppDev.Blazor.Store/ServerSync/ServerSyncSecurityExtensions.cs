// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using EasyAppDev.Blazor.Store.Security;

namespace EasyAppDev.Blazor.Store.ServerSync;

/// <summary>
/// Extension methods for configuring secure ServerSync options.
/// </summary>
public static class ServerSyncSecurityExtensions
{
    /// <summary>
    /// Configures ServerSync with all recommended security features enabled.
    /// This includes validation, rate limiting, message size limits, and message signing.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="options">The ServerSync options to configure.</param>
    /// <param name="validator">The state validator to use for incoming state.</param>
    /// <returns>The configured options for method chaining.</returns>
    /// <remarks>
    /// This extension enables the following security features:
    /// - RequireValidation: true
    /// - RejectInvalidState: true
    /// - MaxMessageSize: 1MB
    /// - RateLimitPerSecond: 10
    /// - EnableMessageSigning: true
    /// - FilterSensitiveData: true
    /// </remarks>
    public static ServerSyncOptions<TState> WithSecureDefaults<TState>(
        this ServerSyncOptions<TState> options,
        IStateValidator<TState> validator)
        where TState : notnull
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(validator);

        options.StateValidator = validator;
        options.RequireValidation = true;
        options.RejectInvalidState = true;
        options.MaxMessageSize = 1_048_576; // 1MB
        options.RateLimitPerSecond = 10;
        options.EnableMessageSigning = true;
        options.FilterSensitiveData = true;

        return options;
    }

    /// <summary>
    /// Requires authorization by enforcing validation.
    /// Sets RequireValidation to true and throws if StateValidator is not configured.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="options">The ServerSync options to configure.</param>
    /// <returns>The configured options for method chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if StateValidator is null when RequireValidation is set to true.
    /// </exception>
    public static ServerSyncOptions<TState> RequireAuthorization<TState>(
        this ServerSyncOptions<TState> options)
        where TState : notnull
    {
        ArgumentNullException.ThrowIfNull(options);

        options.RequireValidation = true;

        if (options.StateValidator == null)
        {
            throw new InvalidOperationException(
                "Cannot require authorization without a StateValidator. " +
                "Set StateValidator before calling RequireAuthorization(), or use WithSecureDefaults().");
        }

        return options;
    }

    /// <summary>
    /// Configures rate limiting for ServerSync messages.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="options">The ServerSync options to configure.</param>
    /// <param name="messagesPerSecond">
    /// Maximum number of messages allowed per second.
    /// Set to 0 to disable rate limiting (not recommended for production).
    /// </param>
    /// <returns>The configured options for method chaining.</returns>
    public static ServerSyncOptions<TState> WithRateLimiting<TState>(
        this ServerSyncOptions<TState> options,
        int messagesPerSecond)
        where TState : notnull
    {
        ArgumentNullException.ThrowIfNull(options);

        if (messagesPerSecond < 0)
            throw new ArgumentOutOfRangeException(nameof(messagesPerSecond), "Rate limit must be non-negative");

        options.RateLimitPerSecond = messagesPerSecond;

        return options;
    }

    /// <summary>
    /// Configures maximum message size to prevent DoS attacks.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="options">The ServerSync options to configure.</param>
    /// <param name="maxSizeBytes">
    /// Maximum message size in bytes.
    /// Default is 1MB (1048576 bytes).
    /// </param>
    /// <returns>The configured options for method chaining.</returns>
    public static ServerSyncOptions<TState> WithMaxMessageSize<TState>(
        this ServerSyncOptions<TState> options,
        int maxSizeBytes)
        where TState : notnull
    {
        ArgumentNullException.ThrowIfNull(options);

        if (maxSizeBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxSizeBytes), "Max message size must be positive");

        options.MaxMessageSize = maxSizeBytes;

        return options;
    }

    /// <summary>
    /// Enables HMAC-SHA256 message signing for integrity verification.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="options">The ServerSync options to configure.</param>
    /// <param name="signingKey">
    /// Optional signing key. If null, a random key will be generated.
    /// NOTE: A random key means messages can only be verified within the same session.
    /// </param>
    /// <returns>The configured options for method chaining.</returns>
    public static ServerSyncOptions<TState> WithMessageSigning<TState>(
        this ServerSyncOptions<TState> options,
        byte[]? signingKey = null)
        where TState : notnull
    {
        ArgumentNullException.ThrowIfNull(options);

        if (signingKey != null && signingKey.Length < 32)
            throw new ArgumentException("Signing key must be at least 32 bytes", nameof(signingKey));

        options.EnableMessageSigning = true;
        options.SigningKey = signingKey;

        return options;
    }

    /// <summary>
    /// Enables sensitive data filtering to prevent leaking sensitive information to the server.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="options">The ServerSync options to configure.</param>
    /// <param name="enabled">Whether to enable filtering. Default is true.</param>
    /// <returns>The configured options for method chaining.</returns>
    public static ServerSyncOptions<TState> WithSensitiveDataFiltering<TState>(
        this ServerSyncOptions<TState> options,
        bool enabled = true)
        where TState : notnull
    {
        ArgumentNullException.ThrowIfNull(options);

        options.FilterSensitiveData = enabled;

        return options;
    }

    /// <summary>
    /// Configures validation callbacks for security monitoring.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="options">The ServerSync options to configure.</param>
    /// <param name="onValidationFailed">Called when state validation fails.</param>
    /// <param name="onRateLimitExceeded">Called when rate limit is exceeded.</param>
    /// <param name="onMessageSizeExceeded">Called when message size limit is exceeded.</param>
    /// <param name="onSignatureVerificationFailed">Called when signature verification fails.</param>
    /// <returns>The configured options for method chaining.</returns>
    public static ServerSyncOptions<TState> WithSecurityCallbacks<TState>(
        this ServerSyncOptions<TState> options,
        Action<StateValidationResult>? onValidationFailed = null,
        Action<string>? onRateLimitExceeded = null,
        Action<int>? onMessageSizeExceeded = null,
        Action<string>? onSignatureVerificationFailed = null)
        where TState : notnull
    {
        ArgumentNullException.ThrowIfNull(options);

        if (onValidationFailed != null)
            options.OnValidationFailed = onValidationFailed;

        if (onRateLimitExceeded != null)
            options.OnRateLimitExceeded = onRateLimitExceeded;

        if (onMessageSizeExceeded != null)
            options.OnMessageSizeExceeded = onMessageSizeExceeded;

        if (onSignatureVerificationFailed != null)
            options.OnSignatureVerificationFailed = onSignatureVerificationFailed;

        return options;
    }

    /// <summary>
    /// Creates a production-ready ServerSync configuration with all security features enabled.
    /// </summary>
    /// <typeparam name="TState">The type of state.</typeparam>
    /// <param name="hubUrl">The SignalR hub URL.</param>
    /// <param name="validator">The state validator.</param>
    /// <param name="documentId">Optional document ID for multi-tenant scenarios.</param>
    /// <returns>Fully configured ServerSyncOptions with security enabled.</returns>
    public static ServerSyncOptions<TState> CreateSecure<TState>(
        string hubUrl,
        IStateValidator<TState> validator,
        string? documentId = null)
        where TState : notnull
    {
        ArgumentNullException.ThrowIfNull(hubUrl);
        ArgumentNullException.ThrowIfNull(validator);

        return new ServerSyncOptions<TState>
        {
            HubUrl = hubUrl,
            DocumentId = documentId
        }.WithSecureDefaults(validator);
    }
}
