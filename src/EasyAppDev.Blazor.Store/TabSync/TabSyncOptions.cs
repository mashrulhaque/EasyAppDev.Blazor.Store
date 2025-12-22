using EasyAppDev.Blazor.Store.Security;

namespace EasyAppDev.Blazor.Store.TabSync;

/// <summary>
/// Configuration options for cross-tab state synchronization.
/// </summary>
public sealed class TabSyncOptions
{
    /// <summary>
    /// The BroadcastChannel name for cross-tab communication.
    /// Different stores should use different channel names.
    /// Default is based on the state type name.
    /// </summary>
    public string? ChannelName { get; set; }

    /// <summary>
    /// Specific actions to sync across tabs.
    /// If empty, all actions are synced (except those in ExcludedActions).
    /// </summary>
    /// <remarks>
    /// Action names are matched case-insensitively using contains.
    /// </remarks>
    public HashSet<string> SyncedActions { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Actions to exclude from syncing.
    /// Useful for UI-only state that shouldn't be shared across tabs.
    /// </summary>
    public HashSet<string> ExcludedActions { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Callback invoked when state is received from another tab.
    /// </summary>
    public Action<string?>? OnSyncReceived { get; set; }

    /// <summary>
    /// Callback invoked when sync fails.
    /// </summary>
    public Action<Exception>? OnSyncError { get; set; }

    /// <summary>
    /// Whether to sync the full state or just the action.
    /// Default is true (sync full state).
    /// </summary>
    /// <remarks>
    /// Setting to false requires all tabs to have identical initial state
    /// and deterministic reducers. Recommended to keep true.
    /// </remarks>
    public bool SyncFullState { get; set; } = true;

    /// <summary>
    /// Debounce interval for syncing rapid updates.
    /// Set to 0 for immediate sync.
    /// Default is 0.
    /// </summary>
    public int DebounceMs { get; set; } = 0;

    /// <summary>
    /// Gets or sets whether to enable message signing for cross-tab messages.
    /// When enabled, messages are signed with HMAC to prevent tampering.
    /// Default is false.
    /// </summary>
    /// <remarks>
    /// IMPORTANT: To use message signing for cross-tab synchronization, you MUST configure
    /// a shared signing key using one of these approaches:
    /// 1. Set <see cref="SigningKey"/> to a shared key (use <see cref="Security.MessageSigner.DeriveKeyFromSeed"/>)
    /// 2. Set <see cref="DeriveKeyFromOrigin"/> to true to auto-derive from window.location.origin
    /// 3. Use the extension methods: <c>.WithSharedSigningKey(key)</c> or <c>.WithOriginDerivedKey()</c>
    ///
    /// Without a shared key, each tab will have a different random key and verification will fail.
    /// This default is false to prevent silent verification failures.
    /// </remarks>
    public bool EnableMessageSigning { get; set; }

    /// <summary>
    /// Gets or sets the shared signing key for cross-tab message verification.
    /// If null and <see cref="EnableMessageSigning"/> is true, a random key will be generated
    /// per tab instance (which will cause verification failures).
    /// </summary>
    /// <remarks>
    /// Use <see cref="Security.MessageSigner.DeriveKeyFromSeed"/> to generate a consistent key
    /// from a shared seed (e.g., your application name or domain).
    /// </remarks>
    public byte[]? SigningKey { get; set; }

    /// <summary>
    /// Gets or sets whether to automatically derive the signing key from window.location.origin.
    /// When true, all tabs from the same origin will share the same signing key.
    /// Default is false.
    /// </summary>
    /// <remarks>
    /// This provides a balance between security and convenience for same-origin tabs.
    /// The key is derived using PBKDF2 with the origin as both password and salt source.
    /// For higher security, use <see cref="SigningKey"/> with a server-provided session key.
    /// </remarks>
    public bool DeriveKeyFromOrigin { get; set; }

    /// <summary>
    /// Gets or sets whether to require valid signatures on incoming messages.
    /// Only applies when EnableMessageSigning is true.
    /// Default is true.
    /// </summary>
    public bool RequireValidSignature { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum age of a message in seconds before it's rejected.
    /// Helps prevent replay attacks. Default is 30 seconds.
    /// </summary>
    public int MaxMessageAgeSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets whether to validate message timestamps.
    /// Default is true.
    /// </summary>
    public bool ValidateTimestamp { get; set; } = true;

    /// <summary>
    /// Gets or sets a callback invoked when a message with invalid signature is received.
    /// </summary>
    public Action<string>? OnInvalidSignature { get; set; }

    /// <summary>
    /// Gets or sets a callback invoked when state validation fails.
    /// </summary>
    public Action<StateValidationResult>? OnValidationFailed { get; set; }

    /// <summary>
    /// Gets or sets the state validator for validating incoming state from other tabs.
    /// Default is null (no validation).
    /// </summary>
    /// <remarks>
    /// It is recommended to configure a validator when syncing sensitive state
    /// to prevent accepting malicious or corrupted state from compromised tabs.
    /// </remarks>
    public IStateValidator<object>? StateValidator { get; set; }

    /// <summary>
    /// Gets or sets whether to reject invalid states from other tabs.
    /// If false, invalid states are logged but still applied. Default is true.
    /// </summary>
    public bool RejectInvalidState { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum allowed message size in bytes.
    /// Messages larger than this limit are rejected to prevent DoS attacks.
    /// Default is 1MB (1,048,576 bytes).
    /// </summary>
    public int MaxMessageSizeBytes { get; set; } = 1_048_576;

    /// <summary>
    /// Gets or sets the clock skew tolerance in seconds for timestamp validation.
    /// Messages with timestamps outside the range (now - MaxMessageAgeSeconds, now + ClockSkewToleranceSeconds)
    /// are rejected. Default is 5 seconds.
    /// </summary>
    public int ClockSkewToleranceSeconds { get; set; } = 5;

    /// <summary>
    /// Gets or sets a callback invoked when a message is rejected due to size limits.
    /// </summary>
    public Action<int>? OnMessageSizeExceeded { get; set; }

    /// <summary>
    /// Gets or sets the maximum JSON deserialization depth.
    /// Prevents stack overflow attacks from deeply nested payloads.
    /// Default is 32.
    /// </summary>
    public int MaxJsonDepth { get; set; } = 32;

    /// <summary>
    /// Sets the channel name.
    /// </summary>
    /// <param name="channelName">The channel name.</param>
    /// <returns>This options instance for chaining.</returns>
    public TabSyncOptions Channel(string channelName)
    {
        ChannelName = channelName ?? throw new ArgumentNullException(nameof(channelName));
        return this;
    }

    /// <summary>
    /// Specifies which actions to sync (whitelist mode).
    /// </summary>
    /// <param name="actions">Action names to sync.</param>
    /// <returns>This options instance for chaining.</returns>
    public TabSyncOptions SyncActions(params string[] actions)
    {
        foreach (var action in actions)
        {
            SyncedActions.Add(action);
        }
        return this;
    }

    /// <summary>
    /// Specifies which actions to exclude from syncing.
    /// </summary>
    /// <param name="actions">Action names to exclude.</param>
    /// <returns>This options instance for chaining.</returns>
    public TabSyncOptions ExcludeActions(params string[] actions)
    {
        foreach (var action in actions)
        {
            ExcludedActions.Add(action);
        }
        return this;
    }

    /// <summary>
    /// Sets a callback for when sync is received.
    /// </summary>
    /// <param name="callback">The callback to invoke.</param>
    /// <returns>This options instance for chaining.</returns>
    public TabSyncOptions OnReceived(Action<string?> callback)
    {
        OnSyncReceived = callback;
        return this;
    }

    /// <summary>
    /// Sets a callback for sync errors.
    /// </summary>
    /// <param name="callback">The callback to invoke on error.</param>
    /// <returns>This options instance for chaining.</returns>
    public TabSyncOptions OnError(Action<Exception> callback)
    {
        OnSyncError = callback;
        return this;
    }

    /// <summary>
    /// Sets the debounce interval.
    /// </summary>
    /// <param name="milliseconds">Debounce interval in milliseconds.</param>
    /// <returns>This options instance for chaining.</returns>
    public TabSyncOptions Debounce(int milliseconds)
    {
        DebounceMs = milliseconds;
        return this;
    }

    /// <summary>
    /// Determines if an action should be synced based on configuration.
    /// </summary>
    /// <param name="action">The action name to check.</param>
    /// <returns>True if the action should be synced, false otherwise.</returns>
    internal bool ShouldSyncAction(string? action)
    {
        // If whitelist mode (SyncedActions has items), check if action is in the list
        if (SyncedActions.Count > 0)
        {
            if (action == null) return false;
            var shouldSync = false;
            foreach (var synced in SyncedActions)
            {
                if (action.Contains(synced, StringComparison.OrdinalIgnoreCase))
                {
                    shouldSync = true;
                    break;
                }
            }
            if (!shouldSync) return false;
        }

        // Check excluded actions
        if (action != null && ExcludedActions.Count > 0)
        {
            foreach (var excluded in ExcludedActions)
            {
                if (action.Contains(excluded, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
        }

        return true;
    }
}
