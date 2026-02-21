namespace EasyAppDev.Blazor.Store.Diagnostics.Models;

/// <summary>
/// Performance metrics for a specific state type.
/// </summary>
public sealed class PerformanceMetrics
{
    /// <summary>
    /// Gets the type of state these metrics are for.
    /// </summary>
    public required Type StateType { get; init; }

    /// <summary>
    /// Gets the total number of updates.
    /// </summary>
    public required int TotalUpdates { get; init; }

    /// <summary>
    /// Gets the average update duration.
    /// </summary>
    public required TimeSpan AverageDuration { get; init; }

    /// <summary>
    /// Gets the median (P50) update duration.
    /// </summary>
    public required TimeSpan MedianDuration { get; init; }

    /// <summary>
    /// Gets the 95th percentile update duration.
    /// </summary>
    public required TimeSpan P95Duration { get; init; }

    /// <summary>
    /// Gets the 99th percentile update duration.
    /// </summary>
    public required TimeSpan P99Duration { get; init; }

    /// <summary>
    /// Gets the minimum update duration.
    /// </summary>
    public required TimeSpan MinDuration { get; init; }

    /// <summary>
    /// Gets the maximum update duration.
    /// </summary>
    public required TimeSpan MaxDuration { get; init; }

    /// <summary>
    /// Gets the slowest actions with their durations.
    /// </summary>
    public required IReadOnlyList<(string? Action, TimeSpan Duration)> SlowestActions { get; init; }

    /// <summary>
    /// Gets the action frequency distribution.
    /// </summary>
    public required IReadOnlyDictionary<string, int> ActionFrequency { get; init; }

    /// <summary>
    /// Gets the current state size in bytes (JSON).
    /// </summary>
    public int CurrentStateSize { get; init; }

    /// <summary>
    /// Gets the average state size in bytes.
    /// </summary>
    public double AverageStateSize { get; init; }
}
