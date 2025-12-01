using System.Collections.Immutable;

namespace EasyAppDev.Blazor.Store.Sample.State;

/// <summary>
/// State for demonstrating the plugin system.
/// </summary>
public record PluginDemoState(
    int Counter,
    ImmutableList<string> ActionLog,
    bool IsValid,
    string? ValidationError)
{
    public static PluginDemoState Initial => new(
        0,
        ImmutableList<string>.Empty,
        true,
        null);

    public PluginDemoState Increment() =>
        this with { Counter = Counter + 1 };

    public PluginDemoState Decrement() =>
        this with { Counter = Counter - 1 };

    public PluginDemoState SetCounter(int value) =>
        this with { Counter = value };

    public PluginDemoState LogAction(string action) =>
        this with
        {
            ActionLog = ActionLog.Count >= 10
                ? ActionLog.Skip(1).Append(action).ToImmutableList()
                : ActionLog.Add(action)
        };

    public PluginDemoState SetValidation(bool isValid, string? error = null) =>
        this with { IsValid = isValid, ValidationError = error };
}
