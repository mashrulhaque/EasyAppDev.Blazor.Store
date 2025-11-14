#if DEBUG
using EasyAppDev.Blazor.Store.Diagnostics.Models;

namespace EasyAppDev.Blazor.Store.Diagnostics;

/// <summary>
/// Service for collecting and querying diagnostic data about store operations.
/// </summary>
public interface IDiagnosticsService
{
    /// <summary>
    /// Event raised when diagnostic data changes (new actions, renders, etc.).
    /// </summary>
    event EventHandler? DataChanged;
    /// <summary>
    /// Records a state update action.
    /// </summary>
    /// <param name="entry">The action history entry to record.</param>
    void RecordUpdate(ActionHistoryEntry entry);

    /// <summary>
    /// Records a component render event.
    /// </summary>
    /// <param name="renderEvent">The render event to record.</param>
    void RecordRender(RenderEvent renderEvent);

    /// <summary>
    /// Records a subscription being created.
    /// </summary>
    /// <param name="subscriptionInfo">The subscription information.</param>
    void RecordSubscription(SubscriptionInfo subscriptionInfo);

    /// <summary>
    /// Records a subscription being disposed.
    /// </summary>
    /// <param name="subscriptionId">The ID of the subscription that was disposed.</param>
    void RecordSubscriptionDisposed(Guid subscriptionId);

    /// <summary>
    /// Records a subscription notification.
    /// </summary>
    /// <param name="subscriptionId">The ID of the subscription that was notified.</param>
    void RecordSubscriptionNotification(Guid subscriptionId);

    /// <summary>
    /// Gets recent actions for a specific state type.
    /// </summary>
    /// <param name="stateType">The state type to get actions for. If null, returns all actions.</param>
    /// <param name="count">The maximum number of actions to return.</param>
    /// <returns>A list of recent actions, most recent first.</returns>
    IReadOnlyList<ActionHistoryEntry> GetRecentActions(Type? stateType = null, int count = 100);

    /// <summary>
    /// Gets recent render events for a specific component.
    /// </summary>
    /// <param name="componentName">The component name to filter by. If null, returns all renders.</param>
    /// <param name="count">The maximum number of events to return.</param>
    /// <returns>A list of recent render events, most recent first.</returns>
    IReadOnlyList<RenderEvent> GetRecentRenders(string? componentName = null, int count = 100);

    /// <summary>
    /// Gets render counts for all components.
    /// </summary>
    /// <returns>A dictionary mapping component names to their render counts.</returns>
    IReadOnlyDictionary<string, int> GetRenderCounts();

    /// <summary>
    /// Gets performance metrics for a specific state type.
    /// </summary>
    /// <param name="stateType">The state type to get metrics for.</param>
    /// <returns>Performance metrics for the state type, or null if no data is available.</returns>
    PerformanceMetrics? GetPerformanceMetrics(Type stateType);

    /// <summary>
    /// Gets performance metrics for all state types.
    /// </summary>
    /// <returns>A dictionary mapping state types to their performance metrics.</returns>
    IReadOnlyDictionary<Type, PerformanceMetrics> GetAllPerformanceMetrics();

    /// <summary>
    /// Gets all active subscriptions.
    /// </summary>
    /// <param name="stateType">Optional filter by state type.</param>
    /// <returns>A list of active subscriptions.</returns>
    IReadOnlyList<SubscriptionInfo> GetActiveSubscriptions(Type? stateType = null);

    /// <summary>
    /// Gets all subscriptions (active and disposed).
    /// </summary>
    /// <param name="stateType">Optional filter by state type.</param>
    /// <returns>A list of all subscriptions.</returns>
    IReadOnlyList<SubscriptionInfo> GetAllSubscriptions(Type? stateType = null);

    /// <summary>
    /// Gets the current state as JSON for a specific state type.
    /// </summary>
    /// <param name="stateType">The state type.</param>
    /// <returns>The current state as a JSON string, or null if not available.</returns>
    string? GetCurrentStateJson(Type stateType);

    /// <summary>
    /// Clears all diagnostic data.
    /// </summary>
    void Clear();

    /// <summary>
    /// Clears diagnostic data for a specific state type.
    /// </summary>
    /// <param name="stateType">The state type to clear data for.</param>
    void Clear(Type stateType);
}
#endif
