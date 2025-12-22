using System.Diagnostics;
using EasyAppDev.Blazor.Store.Middleware;
using EasyAppDev.Blazor.Store.Sample.State;

namespace EasyAppDev.Blazor.Store.Sample.Middleware;

/// <summary>
/// Simple logging middleware that logs state changes to the console.
/// Demonstrates basic middleware implementation.
/// </summary>
public class ConsoleLoggingMiddleware : IMiddleware<MiddlewareDemoState>
{
    public Task OnBeforeUpdateAsync(MiddlewareDemoState currentState, string? action)
    {
        Console.WriteLine($"[BEFORE] Action: {action}, Current Counter: {currentState.Counter}");
        return Task.CompletedTask;
    }

    public Task OnAfterUpdateAsync(MiddlewareDemoState previousState, MiddlewareDemoState newState, string? action)
    {
        Console.WriteLine($"[AFTER] Action: {action}, Counter: {previousState.Counter} -> {newState.Counter}");
        return Task.CompletedTask;
    }
}

/// <summary>
/// Performance tracking middleware that measures update duration.
/// Demonstrates stateful middleware with Stopwatch.
/// </summary>
public class PerformanceMiddleware : IMiddleware<MiddlewareDemoState>
{
    private readonly Stopwatch _stopwatch = new();

    public Action<string?, TimeSpan>? OnActionCompleted { get; set; }

    public Task OnBeforeUpdateAsync(MiddlewareDemoState currentState, string? action)
    {
        _stopwatch.Restart();
        return Task.CompletedTask;
    }

    public Task OnAfterUpdateAsync(MiddlewareDemoState previousState, MiddlewareDemoState newState, string? action)
    {
        _stopwatch.Stop();
        OnActionCompleted?.Invoke(action, _stopwatch.Elapsed);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Validation middleware that prevents invalid state changes.
/// Demonstrates middleware that can affect state updates.
/// </summary>
public class ValidationMiddleware : IMiddleware<MiddlewareDemoState>
{
    public int MinValue { get; set; } = -100;
    public int MaxValue { get; set; } = 100;

    public Action<string>? OnValidationFailed { get; set; }

    public Task OnBeforeUpdateAsync(MiddlewareDemoState currentState, string? action)
    {
        return Task.CompletedTask;
    }

    public Task OnAfterUpdateAsync(MiddlewareDemoState previousState, MiddlewareDemoState newState, string? action)
    {
        if (newState.Counter < MinValue || newState.Counter > MaxValue)
        {
            OnValidationFailed?.Invoke($"Counter {newState.Counter} is out of range [{MinValue}, {MaxValue}]");
        }
        return Task.CompletedTask;
    }
}

/// <summary>
/// Action history middleware that tracks all actions.
/// Demonstrates middleware that can update state with additional info.
/// </summary>
public class ActionHistoryMiddleware : IMiddleware<MiddlewareDemoState>
{
    public List<(DateTime Time, string? Action, int PrevValue, int NewValue)> History { get; } = new();

    public Task OnBeforeUpdateAsync(MiddlewareDemoState currentState, string? action)
    {
        return Task.CompletedTask;
    }

    public Task OnAfterUpdateAsync(MiddlewareDemoState previousState, MiddlewareDemoState newState, string? action)
    {
        History.Add((DateTime.Now, action, previousState.Counter, newState.Counter));

        // Keep only last 50 entries
        while (History.Count > 50)
        {
            History.RemoveAt(0);
        }

        return Task.CompletedTask;
    }
}
