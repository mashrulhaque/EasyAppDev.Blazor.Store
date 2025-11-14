#if DEBUG
namespace EasyAppDev.Blazor.Store.Diagnostics.Models;

/// <summary>
/// Information about a store subscription for diagnostic purposes.
/// </summary>
public sealed record SubscriptionInfo
{
    /// <summary>
    /// Gets the unique identifier for this subscription.
    /// </summary>
    public required Guid SubscriptionId { get; init; }

    /// <summary>
    /// Gets the type of state this subscription is for.
    /// </summary>
    public required Type StateType { get; init; }

    /// <summary>
    /// Gets the name of the subscriber (component name or identifier).
    /// </summary>
    public required string SubscriberName { get; init; }

    /// <summary>
    /// Gets the type of subscription (Full, Selector, etc.).
    /// </summary>
    public required string SubscriptionType { get; init; }

    /// <summary>
    /// Gets the timestamp when the subscription was created.
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// Gets the timestamp when the subscription was disposed (if applicable).
    /// </summary>
    public DateTime? DisposedAt { get; init; }

    /// <summary>
    /// Gets whether this subscription is still active.
    /// </summary>
    public bool IsActive => DisposedAt is null;

    /// <summary>
    /// Gets the number of times this subscription has been notified.
    /// </summary>
    public int NotificationCount { get; init; }
}
#endif
