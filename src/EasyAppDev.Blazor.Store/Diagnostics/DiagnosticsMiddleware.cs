#if DEBUG
// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using EasyAppDev.Blazor.Store.Diagnostics.Models;
using EasyAppDev.Blazor.Store.Middleware;
using EasyAppDev.Blazor.Store.Security;

namespace EasyAppDev.Blazor.Store.Diagnostics;

/// <summary>
/// Options for the diagnostics middleware.
/// </summary>
public sealed class DiagnosticsMiddlewareOptions
{
    /// <summary>
    /// Gets or sets whether to filter sensitive data from diagnostic snapshots.
    /// Default is true for security.
    /// </summary>
    /// <remarks>
    /// <para><b>SECURITY:</b></para>
    /// <para>
    /// Even in DEBUG builds, diagnostic snapshots may be captured and shared for debugging.
    /// Sensitive data filtering helps prevent accidental exposure of passwords, tokens, etc.
    /// </para>
    /// </remarks>
    public bool FilterSensitiveData { get; set; } = true;

    /// <summary>
    /// Gets or sets custom filter options for sensitive data.
    /// </summary>
    public SensitiveDataFilterOptions? SensitiveDataFilterOptions { get; set; }
}

/// <summary>
/// Middleware that collects diagnostic data about state updates.
/// </summary>
/// <typeparam name="TState">The type of state being managed.</typeparam>
/// <remarks>
/// <para><b>DEBUG-ONLY:</b></para>
/// <para>
/// This middleware is only compiled in DEBUG builds. It collects full state snapshots
/// for debugging purposes. Sensitive data is filtered by default to prevent accidental
/// exposure in diagnostic outputs.
/// </para>
/// </remarks>
public sealed class DiagnosticsMiddleware<TState> : IMiddleware<TState>
    where TState : notnull
{
    private readonly IDiagnosticsService _diagnosticsService;
    private readonly DiagnosticsMiddlewareOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Stopwatch _stopwatch = new();
    private TState? _previousState;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiagnosticsMiddleware{TState}"/> class.
    /// </summary>
    /// <param name="diagnosticsService">The diagnostics service to record data to.</param>
    /// <param name="options">Optional diagnostics options. Defaults to filtering sensitive data.</param>
    public DiagnosticsMiddleware(IDiagnosticsService diagnosticsService, DiagnosticsMiddlewareOptions? options = null)
    {
        _diagnosticsService = diagnosticsService;
        _options = options ?? new DiagnosticsMiddlewareOptions();

        // Use filtered JSON options by default for security
        if (_options.FilterSensitiveData)
        {
            _jsonOptions = SensitiveDataFilterExtensions.CreateFilteredJsonOptions(
                _options.SensitiveDataFilterOptions ?? new SensitiveDataFilterOptions { Enabled = true });
            _jsonOptions.WriteIndented = true;
        }
        else
        {
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }
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
