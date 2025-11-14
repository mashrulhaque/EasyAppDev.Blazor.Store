#if DEBUG
namespace EasyAppDev.Blazor.Store.Diagnostics.Models;

/// <summary>
/// Represents a single state update action in the diagnostic history.
/// </summary>
public sealed class ActionHistoryEntry
{
    /// <summary>
    /// Gets the type of state that was updated.
    /// </summary>
    public required Type StateType { get; init; }

    /// <summary>
    /// Gets the action name/description (if provided).
    /// </summary>
    public string? Action { get; init; }

    /// <summary>
    /// Gets the timestamp when the update occurred.
    /// </summary>
    public required DateTime Timestamp { get; init; }

    /// <summary>
    /// Gets the duration of the update operation.
    /// </summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets the previous state as a JSON string (if available).
    /// </summary>
    public string? PreviousStateJson { get; init; }

    /// <summary>
    /// Gets the new state as a JSON string (if available).
    /// </summary>
    public string? NewStateJson { get; init; }

    /// <summary>
    /// Gets the state diff information (if available).
    /// </summary>
    public StateDiff? Diff { get; init; }

    /// <summary>
    /// Gets the size of the previous state in bytes.
    /// </summary>
    public int PreviousStateSize { get; init; }

    /// <summary>
    /// Gets the size of the new state in bytes.
    /// </summary>
    public int NewStateSize { get; init; }
}
#endif
