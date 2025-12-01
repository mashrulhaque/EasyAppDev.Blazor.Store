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
    /// Maximum total estimated memory size for history entries in bytes.
    /// Older entries are removed when this limit is exceeded.
    /// Set to 0 to disable memory-based limiting (default).
    /// </summary>
    /// <remarks>
    /// Memory size is estimated using state serialization size.
    /// For accurate limits with large states, consider setting this value.
    /// Recommended values: 1MB (1_048_576), 5MB (5_242_880), 10MB (10_485_760).
    /// </remarks>
    public long MaxMemoryBytes { get; set; } = 0;

    /// <summary>
    /// Function to estimate the memory size of a state object.
    /// If not provided, uses JSON serialization size as an estimate.
    /// </summary>
    public Func<object, long>? StateSizeEstimator { get; set; }

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
    /// Sets the maximum memory size for history entries.
    /// </summary>
    /// <param name="maxBytes">Maximum memory size in bytes.</param>
    /// <returns>This options instance for chaining.</returns>
    public HistoryOptions WithMaxMemory(long maxBytes)
    {
        if (maxBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(maxBytes), "Max memory must be non-negative.");
        MaxMemoryBytes = maxBytes;
        return this;
    }

    /// <summary>
    /// Sets the maximum memory size for history entries using a friendly unit.
    /// </summary>
    /// <param name="megabytes">Maximum memory size in megabytes.</param>
    /// <returns>This options instance for chaining.</returns>
    public HistoryOptions WithMaxMemoryMB(int megabytes)
    {
        return WithMaxMemory(megabytes * 1024L * 1024L);
    }

    /// <summary>
    /// Sets a custom state size estimator function.
    /// </summary>
    /// <param name="estimator">Function that returns estimated byte size of a state.</param>
    /// <returns>This options instance for chaining.</returns>
    public HistoryOptions WithSizeEstimator(Func<object, long> estimator)
    {
        StateSizeEstimator = estimator ?? throw new ArgumentNullException(nameof(estimator));
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
