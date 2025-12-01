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
