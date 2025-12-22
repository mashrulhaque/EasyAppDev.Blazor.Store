// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using EasyAppDev.Blazor.Store.Security;

namespace EasyAppDev.Blazor.Store.Middleware;

/// <summary>
/// Options for the logging middleware.
/// </summary>
public sealed class LoggingMiddlewareOptions
{
    /// <summary>
    /// Gets or sets whether to log state details.
    /// When false (default), only action names are logged without state content.
    /// This prevents sensitive data from appearing in logs.
    /// </summary>
    /// <remarks>
    /// <para><b>SECURITY WARNING:</b></para>
    /// <para>
    /// Setting this to true will log full state content, which may include sensitive data
    /// like passwords, tokens, or personal information. Even with sensitive data filtering,
    /// some information may still be exposed.
    /// </para>
    /// <para>Only enable this for debugging in development environments.</para>
    /// </remarks>
    public bool LogStateDetails { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to apply sensitive data filtering when logging state.
    /// Only applies when LogStateDetails is true.
    /// Default is true.
    /// </summary>
    public bool FilterSensitiveData { get; set; } = true;

    /// <summary>
    /// Gets or sets custom filter options for sensitive data.
    /// </summary>
    public SensitiveDataFilterOptions? SensitiveDataFilterOptions { get; set; }

    /// <summary>
    /// Gets or sets the log level for state updates.
    /// Default is Information.
    /// </summary>
    public LogLevel LogLevel { get; set; } = LogLevel.Information;

    /// <summary>
    /// Gets or sets whether to use structured logging with semantic parameters.
    /// When true (default), uses ILogger message templates with named parameters for better
    /// integration with log aggregation systems (Seq, Application Insights, ELK, etc.).
    /// When false, uses plain string messages.
    /// </summary>
    /// <remarks>
    /// Structured logging enables log queries like:
    /// <code>Action == "INCREMENT" and StateType == "CounterState"</code>
    /// </remarks>
    public bool UseStructuredLogging { get; set; } = true;

    /// <summary>
    /// Gets or sets the event ID for state update logs.
    /// Useful for filtering and categorizing logs. Default is 1001.
    /// </summary>
    public int StateUpdateEventId { get; set; } = 1001;

    /// <summary>
    /// Gets or sets the event ID for before-update logs.
    /// Useful for filtering and categorizing logs. Default is 1000.
    /// </summary>
    public int BeforeUpdateEventId { get; set; } = 1000;
}

/// <summary>
/// Middleware that logs state changes with security-aware filtering.
/// </summary>
/// <typeparam name="TState">The type of state.</typeparam>
/// <remarks>
/// <para><b>SECURITY:</b></para>
/// <para>
/// By default, this middleware only logs action names without state content to prevent
/// sensitive data from appearing in logs. To log state details, explicitly enable
/// <see cref="LoggingMiddlewareOptions.LogStateDetails"/> and be aware of the security implications.
/// </para>
/// </remarks>
public class LoggingMiddleware<TState> : IMiddleware<TState> where TState : notnull
{
    private readonly ILogger<LoggingMiddleware<TState>>? _logger;
    private readonly Action<string>? _customLogger;
    private readonly LoggingMiddlewareOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly string _stateTypeName;
    private readonly EventId _beforeUpdateEventId;
    private readonly EventId _stateUpdateEventId;

    /// <summary>
    /// Creates logging middleware using Microsoft.Extensions.Logging.
    /// </summary>
    /// <param name="logger">Optional Microsoft.Extensions.Logging logger.</param>
    /// <param name="options">Logging options. If null, uses secure defaults.</param>
    public LoggingMiddleware(ILogger<LoggingMiddleware<TState>>? logger = null, LoggingMiddlewareOptions? options = null)
    {
        _logger = logger;
        _options = options ?? new LoggingMiddlewareOptions();
        _stateTypeName = typeof(TState).Name;
        _beforeUpdateEventId = new EventId(_options.BeforeUpdateEventId, "StoreBeforeUpdate");
        _stateUpdateEventId = new EventId(_options.StateUpdateEventId, "StoreStateUpdated");

        // Use filtered JSON options when state logging is enabled
        if (_options.LogStateDetails && _options.FilterSensitiveData)
        {
            _jsonOptions = SensitiveDataFilterExtensions.CreateFilteredJsonOptions(
                _options.SensitiveDataFilterOptions ?? new SensitiveDataFilterOptions { Enabled = true });
        }
        else
        {
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true
            };
        }
    }

    /// <summary>
    /// Creates logging middleware with a custom logging function.
    /// </summary>
    /// <param name="customLogger">Custom logging function.</param>
    /// <param name="options">Logging options. If null, uses secure defaults.</param>
    /// <remarks>
    /// When using a custom logger, structured logging features (event IDs, semantic parameters)
    /// are not available. Use the ILogger-based constructor for full structured logging support.
    /// </remarks>
    public LoggingMiddleware(Action<string> customLogger, LoggingMiddlewareOptions? options = null)
    {
        _customLogger = customLogger ?? throw new ArgumentNullException(nameof(customLogger));
        _options = options ?? new LoggingMiddlewareOptions();
        _stateTypeName = typeof(TState).Name;
        _beforeUpdateEventId = new EventId(_options.BeforeUpdateEventId, "StoreBeforeUpdate");
        _stateUpdateEventId = new EventId(_options.StateUpdateEventId, "StoreStateUpdated");

        if (_options.LogStateDetails && _options.FilterSensitiveData)
        {
            _jsonOptions = SensitiveDataFilterExtensions.CreateFilteredJsonOptions(
                _options.SensitiveDataFilterOptions ?? new SensitiveDataFilterOptions { Enabled = true });
        }
        else
        {
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true
            };
        }
    }

    /// <inheritdoc />
    public Task OnBeforeUpdateAsync(TState currentState, string? action)
    {
        if (_customLogger != null)
        {
            var message = $"[Store] Updating state{(action != null ? $" (Action: {action})" : "")}";
            _customLogger(message);
        }
        else if (_logger != null)
        {
            if (_options.UseStructuredLogging)
            {
                LogStructured(_options.LogLevel, _beforeUpdateEventId,
                    "Store updating {StateType} with action {Action}",
                    _stateTypeName, action ?? "UNKNOWN");
            }
            else
            {
                Log($"[Store] Updating state{(action != null ? $" (Action: {action})" : "")}");
            }
        }
        else
        {
            Console.WriteLine($"[Store] Updating state{(action != null ? $" (Action: {action})" : "")}");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnAfterUpdateAsync(TState previousState, TState currentState, string? action)
    {
        var actionName = action ?? "UNKNOWN_ACTION";

        if (_customLogger != null)
        {
            LogAfterUpdatePlain(previousState, currentState, actionName);
        }
        else if (_logger != null)
        {
            if (_options.UseStructuredLogging)
            {
                LogAfterUpdateStructured(previousState, currentState, actionName);
            }
            else
            {
                LogAfterUpdatePlain(previousState, currentState, actionName);
            }
        }
        else
        {
            LogAfterUpdatePlain(previousState, currentState, actionName);
        }

        return Task.CompletedTask;
    }

    private void LogAfterUpdatePlain(TState previousState, TState currentState, string actionName)
    {
        string message;
        if (_options.LogStateDetails)
        {
            message = $@"
[Store] State Updated (Action: {actionName})
Previous State: {SerializeState(previousState)}
Current State:  {SerializeState(currentState)}
";
        }
        else
        {
            message = $"[Store] State Updated (Action: {actionName})";
        }

        Log(message);
    }

    private void LogAfterUpdateStructured(TState previousState, TState currentState, string actionName)
    {
        if (_options.LogStateDetails)
        {
            var prevState = SerializeState(previousState);
            var currState = SerializeState(currentState);

            LogStructured(_options.LogLevel, _stateUpdateEventId,
                "Store {StateType} updated with action {Action}. Previous: {PreviousState}, Current: {CurrentState}",
                _stateTypeName, actionName, prevState, currState);
        }
        else
        {
            LogStructured(_options.LogLevel, _stateUpdateEventId,
                "Store {StateType} updated with action {Action}",
                _stateTypeName, actionName);
        }
    }

    private string SerializeState(TState state)
    {
        try
        {
            return JsonSerializer.Serialize(state, _jsonOptions);
        }
        catch (JsonException)
        {
            return "[Serialization failed]";
        }
        catch (NotSupportedException)
        {
            // Type is not supported for serialization
            return "[Serialization not supported]";
        }
    }

    private void LogStructured(LogLevel level, EventId eventId, string messageTemplate, params object?[] args)
    {
        if (_logger == null) return;

        switch (level)
        {
            case LogLevel.Trace:
                _logger.Log(LogLevel.Trace, eventId, messageTemplate, args);
                break;
            case LogLevel.Debug:
                _logger.Log(LogLevel.Debug, eventId, messageTemplate, args);
                break;
            case LogLevel.Warning:
                _logger.Log(LogLevel.Warning, eventId, messageTemplate, args);
                break;
            case LogLevel.Error:
                _logger.Log(LogLevel.Error, eventId, messageTemplate, args);
                break;
            case LogLevel.Critical:
                _logger.Log(LogLevel.Critical, eventId, messageTemplate, args);
                break;
            default:
                _logger.Log(LogLevel.Information, eventId, messageTemplate, args);
                break;
        }
    }

    private void Log(string message)
    {
        if (_customLogger != null)
        {
            _customLogger(message);
        }
        else if (_logger != null)
        {
            switch (_options.LogLevel)
            {
                case LogLevel.Trace:
                    _logger.LogTrace(message);
                    break;
                case LogLevel.Debug:
                    _logger.LogDebug(message);
                    break;
                case LogLevel.Warning:
                    _logger.LogWarning(message);
                    break;
                case LogLevel.Error:
                    _logger.LogError(message);
                    break;
                case LogLevel.Critical:
                    _logger.LogCritical(message);
                    break;
                default:
                    _logger.LogInformation(message);
                    break;
            }
        }
        else
        {
            Console.WriteLine(message);
        }
    }
}
