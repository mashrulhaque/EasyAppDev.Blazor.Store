#if DEBUG
using System.Collections.Concurrent;
using EasyAppDev.Blazor.Store.Diagnostics.Models;

namespace EasyAppDev.Blazor.Store.Diagnostics;

/// <summary>
/// Default implementation of <see cref="IDiagnosticsService"/> for diagnostic data collection.
/// </summary>
public sealed class DiagnosticsService : IDiagnosticsService
{
    private const int DefaultHistorySize = 100;

    private readonly object _lock = new();
    private readonly int _maxHistorySize;

    /// <inheritdoc />
    public event EventHandler? DataChanged;

    // Circular buffers for history
    private readonly List<ActionHistoryEntry> _actionHistory = new();
    private readonly List<RenderEvent> _renderHistory = new();

    // Subscriptions tracking
    private readonly ConcurrentDictionary<Guid, SubscriptionInfo> _subscriptions = new();
    private readonly ConcurrentDictionary<Guid, int> _subscriptionNotificationCounts = new();

    // Render counts by component
    private readonly ConcurrentDictionary<string, int> _renderCounts = new();

    // Current state snapshots (for display purposes)
    private readonly ConcurrentDictionary<Type, string> _currentStateSnapshots = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="DiagnosticsService"/> class.
    /// </summary>
    /// <param name="maxHistorySize">The maximum number of history entries to keep. Defaults to 100.</param>
    public DiagnosticsService(int maxHistorySize = DefaultHistorySize)
    {
        _maxHistorySize = maxHistorySize;
    }

    /// <inheritdoc />
    public void RecordUpdate(ActionHistoryEntry entry)
    {
        lock (_lock)
        {
            _actionHistory.Add(entry);

            // Maintain circular buffer
            if (_actionHistory.Count > _maxHistorySize)
            {
                _actionHistory.RemoveAt(0);
            }

            // Update current state snapshot
            if (entry.NewStateJson is not null)
            {
                _currentStateSnapshots[entry.StateType] = entry.NewStateJson;
            }
        }

        // Notify listeners that data has changed
        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void RecordRender(RenderEvent renderEvent)
    {
        lock (_lock)
        {
            _renderHistory.Add(renderEvent);

            // Maintain circular buffer
            if (_renderHistory.Count > _maxHistorySize)
            {
                _renderHistory.RemoveAt(0);
            }

            // Update render counts
            _renderCounts.AddOrUpdate(
                renderEvent.ComponentName,
                1,
                (_, count) => count + 1
            );
        }

        // Notify listeners that data has changed
        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void RecordSubscription(SubscriptionInfo subscriptionInfo)
    {
        _subscriptions[subscriptionInfo.SubscriptionId] = subscriptionInfo;
        _subscriptionNotificationCounts[subscriptionInfo.SubscriptionId] = 0;

        // Notify listeners that data has changed
        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void RecordSubscriptionDisposed(Guid subscriptionId)
    {
        if (_subscriptions.TryGetValue(subscriptionId, out var info))
        {
            var updatedInfo = info with { DisposedAt = DateTime.UtcNow };
            _subscriptions[subscriptionId] = updatedInfo;

            // Notify listeners that data has changed
            DataChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc />
    public void RecordSubscriptionNotification(Guid subscriptionId)
    {
        _subscriptionNotificationCounts.AddOrUpdate(subscriptionId, 1, (_, count) => count + 1);

        // Update the subscription info with the new notification count
        if (_subscriptions.TryGetValue(subscriptionId, out var info))
        {
            var notificationCount = _subscriptionNotificationCounts[subscriptionId];
            var updatedInfo = info with { NotificationCount = notificationCount };
            _subscriptions[subscriptionId] = updatedInfo;

            // Notify listeners that data has changed
            DataChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ActionHistoryEntry> GetRecentActions(Type? stateType = null, int count = 100)
    {
        lock (_lock)
        {
            var query = _actionHistory.AsEnumerable();

            if (stateType is not null)
            {
                query = query.Where(a => a.StateType == stateType);
            }

            return query
                .OrderByDescending(a => a.Timestamp)
                .Take(count)
                .ToList();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<RenderEvent> GetRecentRenders(string? componentName = null, int count = 100)
    {
        lock (_lock)
        {
            var query = _renderHistory.AsEnumerable();

            if (componentName is not null)
            {
                query = query.Where(r => r.ComponentName == componentName);
            }

            return query
                .OrderByDescending(r => r.Timestamp)
                .Take(count)
                .ToList();
        }
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, int> GetRenderCounts()
    {
        return _renderCounts.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    /// <inheritdoc />
    public PerformanceMetrics? GetPerformanceMetrics(Type stateType)
    {
        lock (_lock)
        {
            var actions = _actionHistory.Where(a => a.StateType == stateType).ToList();

            if (actions.Count == 0)
                return null;

            return CalculateMetrics(stateType, actions);
        }
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<Type, PerformanceMetrics> GetAllPerformanceMetrics()
    {
        lock (_lock)
        {
            var stateTypes = _actionHistory.Select(a => a.StateType).Distinct();
            var metrics = new Dictionary<Type, PerformanceMetrics>();

            foreach (var stateType in stateTypes)
            {
                var actions = _actionHistory.Where(a => a.StateType == stateType).ToList();
                if (actions.Count > 0)
                {
                    metrics[stateType] = CalculateMetrics(stateType, actions);
                }
            }

            return metrics;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<SubscriptionInfo> GetActiveSubscriptions(Type? stateType = null)
    {
        var query = _subscriptions.Values.Where(s => s.IsActive);

        if (stateType is not null)
        {
            query = query.Where(s => s.StateType == stateType);
        }

        return query.OrderBy(s => s.CreatedAt).ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<SubscriptionInfo> GetAllSubscriptions(Type? stateType = null)
    {
        var query = _subscriptions.Values.AsEnumerable();

        if (stateType is not null)
        {
            query = query.Where(s => s.StateType == stateType);
        }

        return query.OrderBy(s => s.CreatedAt).ToList();
    }

    /// <inheritdoc />
    public string? GetCurrentStateJson(Type stateType)
    {
        return _currentStateSnapshots.TryGetValue(stateType, out var json) ? json : null;
    }

    /// <inheritdoc />
    public void Clear()
    {
        lock (_lock)
        {
            _actionHistory.Clear();
            _renderHistory.Clear();
        }

        _subscriptions.Clear();
        _subscriptionNotificationCounts.Clear();
        _renderCounts.Clear();
        _currentStateSnapshots.Clear();
    }

    /// <inheritdoc />
    public void Clear(Type stateType)
    {
        lock (_lock)
        {
            _actionHistory.RemoveAll(a => a.StateType == stateType);
            _renderHistory.RemoveAll(r => r.StateType == stateType);
        }

        _currentStateSnapshots.TryRemove(stateType, out _);

        // Clear subscriptions for this state type
        var subscriptionsToRemove = _subscriptions
            .Where(kvp => kvp.Value.StateType == stateType)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var id in subscriptionsToRemove)
        {
            _subscriptions.TryRemove(id, out _);
            _subscriptionNotificationCounts.TryRemove(id, out _);
        }
    }

    private PerformanceMetrics CalculateMetrics(Type stateType, List<ActionHistoryEntry> actions)
    {
        var durations = actions.Select(a => a.Duration).OrderBy(d => d).ToList();

        var totalUpdates = actions.Count;
        var averageDuration = TimeSpan.FromTicks((long)durations.Average(d => d.Ticks));
        var medianDuration = durations[durations.Count / 2];
        var p95Duration = durations[(int)(durations.Count * 0.95)];
        var p99Duration = durations[(int)(durations.Count * 0.99)];
        var minDuration = durations[0];
        var maxDuration = durations[^1];

        var slowestActions = actions
            .OrderByDescending(a => a.Duration)
            .Take(10)
            .Select(a => (a.Action, a.Duration))
            .ToList();

        var actionFrequency = actions
            .GroupBy(a => a.Action ?? "(unnamed)")
            .ToDictionary(g => g.Key, g => g.Count());

        var currentStateSize = actions.LastOrDefault()?.NewStateSize ?? 0;
        var averageStateSize = actions.Any() ? actions.Average(a => a.NewStateSize) : 0;

        return new PerformanceMetrics
        {
            StateType = stateType,
            TotalUpdates = totalUpdates,
            AverageDuration = averageDuration,
            MedianDuration = medianDuration,
            P95Duration = p95Duration,
            P99Duration = p99Duration,
            MinDuration = minDuration,
            MaxDuration = maxDuration,
            SlowestActions = slowestActions,
            ActionFrequency = actionFrequency,
            CurrentStateSize = currentStateSize,
            AverageStateSize = averageStateSize
        };
    }
}
#endif
