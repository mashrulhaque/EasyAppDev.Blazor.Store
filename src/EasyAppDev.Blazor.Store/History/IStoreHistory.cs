namespace EasyAppDev.Blazor.Store.History;

/// <summary>
/// Provides undo/redo functionality for a store.
/// </summary>
/// <typeparam name="TState">The type of state being tracked.</typeparam>
public interface IStoreHistory<TState> where TState : notnull
{
    /// <summary>
    /// Gets whether undo is currently available.
    /// </summary>
    bool CanUndo { get; }

    /// <summary>
    /// Gets whether redo is currently available.
    /// </summary>
    bool CanRedo { get; }

    /// <summary>
    /// Gets the current position in history (0-based index).
    /// </summary>
    int CurrentIndex { get; }

    /// <summary>
    /// Gets the total number of history entries.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets all history entries (read-only).
    /// </summary>
    IReadOnlyList<HistoryEntry<TState>> Entries { get; }

    /// <summary>
    /// Undoes the last action and restores the previous state.
    /// </summary>
    /// <returns>A task that completes when the state is restored.</returns>
    Task UndoAsync();

    /// <summary>
    /// Redoes the last undone action.
    /// </summary>
    /// <returns>A task that completes when the state is restored.</returns>
    Task RedoAsync();

    /// <summary>
    /// Jumps to a specific point in history.
    /// </summary>
    /// <param name="index">The history index to jump to (0-based).</param>
    /// <returns>A task that completes when the state is restored.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when index is out of range.
    /// </exception>
    Task GoToAsync(int index);

    /// <summary>
    /// Clears all history entries.
    /// The current state is preserved as the initial entry.
    /// </summary>
    void Clear();

    /// <summary>
    /// Event raised when history changes (entries added, undo/redo performed).
    /// </summary>
    event Action? OnHistoryChanged;
}
