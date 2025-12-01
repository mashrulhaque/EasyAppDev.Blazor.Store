namespace EasyAppDev.Blazor.Store.History;

/// <summary>
/// Configuration options for store history tracking.
/// </summary>
public sealed class HistoryOptions
{
    /// <summary>
    /// Maximum number of history entries to keep.
    /// Older entries are automatically removed when this limit is exceeded.
    /// Default is 50.
    /// </summary>
    public int MaxSize { get; set; } = 50;

    /// <summary>
    /// Actions that should not be tracked in history.
    /// Useful for excluding high-frequency updates like cursor movements.
    /// </summary>
    /// <remarks>
    /// Action names are matched case-insensitively using contains.
    /// For example, "CURSOR" would exclude both "CURSOR_MOVE" and "UPDATE_CURSOR".
    /// </remarks>
    public HashSet<string> ExcludedActions { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Time window for grouping rapid consecutive actions.
    /// Actions within this window are combined into a single history entry.
    /// Set to <see cref="TimeSpan.Zero"/> to disable grouping.
    /// Default is 0 (disabled).
    /// </summary>
    /// <remarks>
    /// Useful for text editing where multiple keystrokes should be a single undo step.
    /// </remarks>
    public TimeSpan GroupWindow { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Predicate to determine if an action should be tracked.
    /// Return false to exclude the action from history.
    /// </summary>
    /// <remarks>
    /// More powerful than <see cref="ExcludedActions"/> for complex filtering logic.
    /// Both filters are applied - action must pass both to be recorded.
    /// </remarks>
    public Func<string?, bool>? ShouldTrack { get; set; }

    /// <summary>
    /// Sets the maximum history size.
    /// </summary>
    /// <param name="maxSize">Maximum number of entries.</param>
    /// <returns>This options instance for chaining.</returns>
    public HistoryOptions WithMaxSize(int maxSize)
    {
        if (maxSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxSize), "Max size must be positive.");
        MaxSize = maxSize;
        return this;
    }

    /// <summary>
    /// Excludes specific actions from history tracking.
    /// </summary>
    /// <param name="actions">Action names to exclude.</param>
    /// <returns>This options instance for chaining.</returns>
    public HistoryOptions ExcludeActions(params string[] actions)
    {
        foreach (var action in actions)
        {
            ExcludedActions.Add(action);
        }
        return this;
    }

    /// <summary>
    /// Sets the time window for grouping rapid actions.
    /// </summary>
    /// <param name="window">Time window for grouping.</param>
    /// <returns>This options instance for chaining.</returns>
    public HistoryOptions GroupActions(TimeSpan window)
    {
        GroupWindow = window;
        return this;
    }

    /// <summary>
    /// Sets a predicate to filter which actions are tracked.
    /// </summary>
    /// <param name="shouldTrack">Predicate returning true for actions to track.</param>
    /// <returns>This options instance for chaining.</returns>
    public HistoryOptions TrackWhen(Func<string?, bool> shouldTrack)
    {
        ShouldTrack = shouldTrack;
        return this;
    }

    /// <summary>
    /// Determines if an action should be tracked based on configuration.
    /// </summary>
    /// <param name="action">The action name to check.</param>
    /// <returns>True if the action should be tracked, false otherwise.</returns>
    internal bool ShouldTrackAction(string? action)
    {
        // Check excluded actions list
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

        // Check custom predicate
        if (ShouldTrack != null && !ShouldTrack(action))
        {
            return false;
        }

        return true;
    }
}
