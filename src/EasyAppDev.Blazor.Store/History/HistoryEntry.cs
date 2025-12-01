namespace EasyAppDev.Blazor.Store.History;

/// <summary>
/// Represents a single entry in the store's history.
/// </summary>
/// <typeparam name="TState">The type of state being tracked.</typeparam>
/// <param name="State">The state snapshot at this point in history.</param>
/// <param name="Action">The action name that caused this state change.</param>
/// <param name="Timestamp">When this entry was created.</param>
public record HistoryEntry<TState>(
    TState State,
    string? Action,
    DateTime Timestamp) where TState : notnull;
