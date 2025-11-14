using System.Text.Json;

namespace EasyAppDev.Blazor.Store.Middleware;

/// <summary>
/// Middleware that logs state changes.
/// </summary>
/// <typeparam name="TState">The type of state.</typeparam>
public class LoggingMiddleware<TState> : IMiddleware<TState> where TState : notnull
{
    private readonly ILogger<LoggingMiddleware<TState>>? _logger;
    private readonly Action<string>? _customLogger;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Creates logging middleware using Microsoft.Extensions.Logging.
    /// </summary>
    /// <param name="logger">Optional Microsoft.Extensions.Logging logger.</param>
    public LoggingMiddleware(ILogger<LoggingMiddleware<TState>>? logger = null)
    {
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };
    }

    /// <summary>
    /// Creates logging middleware with a custom logging function.
    /// </summary>
    /// <param name="customLogger">Custom logging function.</param>
    public LoggingMiddleware(Action<string> customLogger)
    {
        _customLogger = customLogger ?? throw new ArgumentNullException(nameof(customLogger));
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };
    }

    /// <inheritdoc />
    public Task OnBeforeUpdateAsync(TState currentState, string? action)
    {
        var message = $"[Store] Updating state{(action != null ? $" (Action: {action})" : "")}";
        Log(message);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnAfterUpdateAsync(TState previousState, TState currentState, string? action)
    {
        var actionName = action ?? "UNKNOWN_ACTION";
        var message = $@"
[Store] State Updated (Action: {actionName})
Previous State: {SerializeState(previousState)}
Current State:  {SerializeState(currentState)}
";
        Log(message);
        return Task.CompletedTask;
    }

    private string SerializeState(TState state)
    {
        try
        {
            return JsonSerializer.Serialize(state, _jsonOptions);
        }
        catch
        {
            return state.ToString() ?? "null";
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
            _logger.LogInformation(message);
        }
        else
        {
            Console.WriteLine(message);
        }
    }
}
