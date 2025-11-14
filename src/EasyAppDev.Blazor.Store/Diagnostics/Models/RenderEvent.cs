#if DEBUG
namespace EasyAppDev.Blazor.Store.Diagnostics.Models;

/// <summary>
/// Represents a component render event for diagnostic tracking.
/// </summary>
public sealed class RenderEvent
{
    /// <summary>
    /// Gets the name of the component that rendered.
    /// </summary>
    public required string ComponentName { get; init; }

    /// <summary>
    /// Gets the timestamp when the render occurred.
    /// </summary>
    public required DateTime Timestamp { get; init; }

    /// <summary>
    /// Gets whether this was the first render of the component.
    /// </summary>
    public bool IsFirstRender { get; init; }

    /// <summary>
    /// Gets the type of state associated with this component (if any).
    /// </summary>
    public Type? StateType { get; init; }

    /// <summary>
    /// Gets the reason for the render (if known).
    /// </summary>
    public string? Reason { get; init; }
}
#endif
