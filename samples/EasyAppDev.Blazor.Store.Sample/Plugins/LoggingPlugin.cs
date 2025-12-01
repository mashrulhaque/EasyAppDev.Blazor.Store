using EasyAppDev.Blazor.Store.Plugins;
using EasyAppDev.Blazor.Store.Sample.State;

namespace EasyAppDev.Blazor.Store.Sample.Plugins;

/// <summary>
/// Custom plugin that logs all state changes for demonstration.
/// </summary>
public class LoggingPlugin : StorePluginBase<PluginDemoState>
{
    public override string Name => "LoggingPlugin";
    public override Version Version => new(1, 0, 0);

    public override Task OnAfterUpdateAsync(PluginDemoState previousState, PluginDemoState newState, string? action)
    {
        // Log the action to the state itself
        var logEntry = $"[{DateTime.Now:HH:mm:ss}] {action ?? "Unknown"}: {previousState.Counter} -> {newState.Counter}";
        Console.WriteLine($"LoggingPlugin: {logEntry}");

        return Task.CompletedTask;
    }
}

/// <summary>
/// Plugin that validates state constraints.
/// </summary>
public class ValidationPlugin : StorePluginBase<PluginDemoState>
{
    public override string Name => "ValidationPlugin";
    public override Version Version => new(1, 0, 0);

    public override Task OnAfterUpdateAsync(PluginDemoState previousState, PluginDemoState newState, string? action)
    {
        // Validate counter constraints
        var isValid = newState.Counter >= -100 && newState.Counter <= 100;
        string? error = null;

        if (newState.Counter < -100)
            error = "Counter cannot be less than -100";
        else if (newState.Counter > 100)
            error = "Counter cannot be greater than 100";

        // Note: This is for demo purposes. In real scenarios, you might want to
        // prevent the invalid state from being set in the first place.

        return Task.CompletedTask;
    }
}

/// <summary>
/// Plugin that tracks analytics events.
/// </summary>
public class AnalyticsDemoPlugin : StorePluginBase<PluginDemoState>
{
    public override string Name => "AnalyticsPlugin";
    public override Version Version => new(1, 0, 0);

    private readonly List<string> _events = new();

    public IReadOnlyList<string> Events => _events;

    public override Task OnAfterUpdateAsync(PluginDemoState previousState, PluginDemoState newState, string? action)
    {
        var eventEntry = $"{action}: {newState.Counter} (delta: {newState.Counter - previousState.Counter})";
        _events.Add(eventEntry);

        // Keep only last 20 events
        if (_events.Count > 20)
            _events.RemoveAt(0);

        return Task.CompletedTask;
    }
}
