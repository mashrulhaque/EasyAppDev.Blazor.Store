#if DEBUG
namespace EasyAppDev.Blazor.Store.Diagnostics.Models;

/// <summary>
/// Represents the differences between two state snapshots.
/// </summary>
public sealed class StateDiff
{
    /// <summary>
    /// Gets the property-level changes between states.
    /// </summary>
    public required IReadOnlyList<PropertyChange> Changes { get; init; }

    /// <summary>
    /// Gets whether the states are identical.
    /// </summary>
    public bool AreIdentical => Changes.Count == 0;

    /// <summary>
    /// Gets a summary description of the changes.
    /// </summary>
    public string Summary
    {
        get
        {
            if (AreIdentical)
                return "No changes";

            if (Changes.Count == 1)
                return $"1 property changed: {Changes[0].PropertyName}";

            return $"{Changes.Count} properties changed: {string.Join(", ", Changes.Take(3).Select(c => c.PropertyName))}{(Changes.Count > 3 ? "..." : "")}";
        }
    }
}

/// <summary>
/// Represents a change to a single property in the state.
/// </summary>
public sealed class PropertyChange
{
    /// <summary>
    /// Gets the name of the property that changed.
    /// </summary>
    public required string PropertyName { get; init; }

    /// <summary>
    /// Gets the previous value as a string.
    /// </summary>
    public string? OldValue { get; init; }

    /// <summary>
    /// Gets the new value as a string.
    /// </summary>
    public string? NewValue { get; init; }

    /// <summary>
    /// Gets whether this property was added (didn't exist before).
    /// </summary>
    public bool IsAdded { get; init; }

    /// <summary>
    /// Gets whether this property was removed (doesn't exist after).
    /// </summary>
    public bool IsRemoved { get; init; }
}
#endif
