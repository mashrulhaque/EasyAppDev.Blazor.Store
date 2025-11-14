#if DEBUG
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using EasyAppDev.Blazor.Store.Diagnostics.Models;
using EasyAppDev.Blazor.Store.Middleware;

namespace EasyAppDev.Blazor.Store.Diagnostics;

/// <summary>
/// Middleware that collects diagnostic data about state updates.
/// </summary>
/// <typeparam name="TState">The type of state being managed.</typeparam>
public sealed class DiagnosticsMiddleware<TState> : IMiddleware<TState>
    where TState : notnull
{
    private readonly IDiagnosticsService _diagnosticsService;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Stopwatch _stopwatch = new();
    private TState? _previousState;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiagnosticsMiddleware{TState}"/> class.
    /// </summary>
    /// <param name="diagnosticsService">The diagnostics service to record data to.</param>
    public DiagnosticsMiddleware(IDiagnosticsService diagnosticsService)
    {
        _diagnosticsService = diagnosticsService;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <inheritdoc />
    public Task OnBeforeUpdateAsync(TState currentState, string? action)
    {
        _previousState = currentState;
        _stopwatch.Restart();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnAfterUpdateAsync(TState previousState, TState currentState, string? action)
    {
        _stopwatch.Stop();

        try
        {
            var previousJson = JsonSerializer.Serialize(previousState, _jsonOptions);
            var currentJson = JsonSerializer.Serialize(currentState, _jsonOptions);

            var previousSize = System.Text.Encoding.UTF8.GetByteCount(previousJson);
            var currentSize = System.Text.Encoding.UTF8.GetByteCount(currentJson);

            var diff = CalculateStateDiff(previousJson, currentJson);

            var entry = new ActionHistoryEntry
            {
                StateType = typeof(TState),
                Action = action,
                Timestamp = DateTime.UtcNow,
                Duration = _stopwatch.Elapsed,
                PreviousStateJson = previousJson,
                NewStateJson = currentJson,
                Diff = diff,
                PreviousStateSize = previousSize,
                NewStateSize = currentSize
            };

            _diagnosticsService.RecordUpdate(entry);
        }
        catch
        {
            // Silently fail to avoid disrupting the application
            // Could log this in production scenarios
        }

        return Task.CompletedTask;
    }

    private StateDiff? CalculateStateDiff(string previousJson, string currentJson)
    {
        try
        {
            var previousObj = JsonNode.Parse(previousJson);
            var currentObj = JsonNode.Parse(currentJson);

            if (previousObj is null || currentObj is null)
                return null;

            var changes = new List<PropertyChange>();

            // Compare properties
            if (previousObj is JsonObject prevObject && currentObj is JsonObject currObject)
            {
                // Find modified and removed properties
                foreach (var (key, prevValue) in prevObject)
                {
                    if (currObject.TryGetPropertyValue(key, out var currValue))
                    {
                        var prevStr = prevValue?.ToJsonString() ?? "null";
                        var currStr = currValue?.ToJsonString() ?? "null";

                        if (prevStr != currStr)
                        {
                            changes.Add(new PropertyChange
                            {
                                PropertyName = key,
                                OldValue = prevStr,
                                NewValue = currStr,
                                IsAdded = false,
                                IsRemoved = false
                            });
                        }
                    }
                    else
                    {
                        changes.Add(new PropertyChange
                        {
                            PropertyName = key,
                            OldValue = prevValue?.ToJsonString() ?? "null",
                            NewValue = null,
                            IsAdded = false,
                            IsRemoved = true
                        });
                    }
                }

                // Find added properties
                foreach (var (key, currValue) in currObject)
                {
                    if (!prevObject.ContainsKey(key))
                    {
                        changes.Add(new PropertyChange
                        {
                            PropertyName = key,
                            OldValue = null,
                            NewValue = currValue?.ToJsonString() ?? "null",
                            IsAdded = true,
                            IsRemoved = false
                        });
                    }
                }
            }

            return new StateDiff
            {
                Changes = changes
            };
        }
        catch
        {
            // If diff calculation fails, return null
            return null;
        }
    }
}
#endif
