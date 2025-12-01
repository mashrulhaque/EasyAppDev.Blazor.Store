// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using EasyAppDev.Blazor.Store.Core;
using Microsoft.Extensions.Logging;

namespace EasyAppDev.Blazor.Store.Plugins.BuiltInPlugins;

/// <summary>
/// Plugin that tracks analytics for store actions.
/// </summary>
/// <typeparam name="TState">The type of state.</typeparam>
public class AnalyticsPlugin<TState> : ConfigurablePlugin<TState, AnalyticsOptions>
    where TState : notnull
{
    private readonly List<ActionRecord> _actionHistory = new();
    private readonly object _lock = new();
    private long _totalActions;
    private long _totalStateSize;
    private ILogger<AnalyticsPlugin<TState>>? _logger;

    /// <inheritdoc />
    public override string Name => "Analytics";

    /// <inheritdoc />
    public override Version Version => new(1, 0, 0);

    /// <inheritdoc />
    public override void Configure(StoreBuilder<TState> builder, IServiceProvider services)
    {
        _logger = services.GetService(typeof(ILogger<AnalyticsPlugin<TState>>)) as ILogger<AnalyticsPlugin<TState>>;
    }

    /// <inheritdoc />
    public override Task OnAfterUpdateAsync(TState previousState, TState newState, string? action)
    {
        if (!Options.TrackActions)
            return Task.CompletedTask;

        var actionName = action ?? "UNKNOWN";
        var timestamp = DateTime.UtcNow;
        var stateSize = EstimateStateSize(newState);

        lock (_lock)
        {
            _totalActions++;
            _totalStateSize = stateSize;

            // Sample if rate is specified
            if (Options.SampleRate < 1.0 && Random.Shared.NextDouble() > Options.SampleRate)
                return Task.CompletedTask;

            var record = new ActionRecord
            {
                Action = actionName,
                Timestamp = timestamp,
                StateSize = stateSize
            };

            _actionHistory.Add(record);

            // Trim history if needed
            while (_actionHistory.Count > Options.MaxHistorySize)
            {
                _actionHistory.RemoveAt(0);
            }
        }

        Options.OnAction?.Invoke(actionName, timestamp);

        _logger?.LogDebug(
            "Analytics: Action={Action}, StateSize={StateSize}",
            actionName,
            stateSize);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets analytics metrics.
    /// </summary>
    public AnalyticsMetrics GetMetrics()
    {
        lock (_lock)
        {
            return new AnalyticsMetrics
            {
                TotalActions = _totalActions,
                CurrentStateSize = _totalStateSize,
                RecentActions = _actionHistory.ToList(),
                ActionsPerMinute = CalculateActionsPerMinute()
            };
        }
    }

    private static long EstimateStateSize(TState state)
    {
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(state);
            return json.Length * sizeof(char);
        }
        catch
        {
            return 0;
        }
    }

    private double CalculateActionsPerMinute()
    {
        if (_actionHistory.Count < 2) return 0;

        var oldest = _actionHistory[0].Timestamp;
        var newest = _actionHistory[^1].Timestamp;
        var minutes = (newest - oldest).TotalMinutes;

        return minutes > 0 ? _actionHistory.Count / minutes : 0;
    }
}

/// <summary>
/// Options for the analytics plugin.
/// </summary>
public class AnalyticsOptions
{
    /// <summary>
    /// Gets or sets whether to track actions.
    /// Default is true.
    /// </summary>
    public bool TrackActions { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to track state size.
    /// Default is true.
    /// </summary>
    public bool TrackStateSize { get; set; } = true;

    /// <summary>
    /// Gets or sets the sampling rate (0.0 to 1.0).
    /// 1.0 = track all actions.
    /// </summary>
    public double SampleRate { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the maximum history size.
    /// Default is 1000.
    /// </summary>
    public int MaxHistorySize { get; set; } = 1000;

    /// <summary>
    /// Gets or sets a callback for each tracked action.
    /// </summary>
    public Action<string, DateTime>? OnAction { get; set; }
}

/// <summary>
/// Analytics metrics for a store.
/// </summary>
public class AnalyticsMetrics
{
    /// <summary>
    /// Gets the total number of actions processed.
    /// </summary>
    public long TotalActions { get; init; }

    /// <summary>
    /// Gets the current estimated state size in bytes.
    /// </summary>
    public long CurrentStateSize { get; init; }

    /// <summary>
    /// Gets the recent action history.
    /// </summary>
    public IReadOnlyList<ActionRecord> RecentActions { get; init; } = Array.Empty<ActionRecord>();

    /// <summary>
    /// Gets the actions per minute rate.
    /// </summary>
    public double ActionsPerMinute { get; init; }
}

/// <summary>
/// Record of a single action.
/// </summary>
public class ActionRecord
{
    /// <summary>
    /// Gets the action name.
    /// </summary>
    public required string Action { get; init; }

    /// <summary>
    /// Gets when the action occurred.
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Gets the state size after this action.
    /// </summary>
    public long StateSize { get; init; }
}
