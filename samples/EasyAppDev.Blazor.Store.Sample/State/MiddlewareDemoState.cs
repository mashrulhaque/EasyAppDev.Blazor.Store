using System.Collections.Immutable;

namespace EasyAppDev.Blazor.Store.Sample.State;

/// <summary>
/// State for demonstrating custom middleware.
/// </summary>
public record MiddlewareDemoState(
    int Counter,
    ImmutableList<string> ActionLog,
    DateTime? LastAction,
    int TotalActions,
    TimeSpan? LastActionDuration)
{
    public static MiddlewareDemoState Initial => new(
        Counter: 0,
        ActionLog: ImmutableList<string>.Empty,
        LastAction: null,
        TotalActions: 0,
        LastActionDuration: null);

    public MiddlewareDemoState Increment() => this with { Counter = Counter + 1 };
    public MiddlewareDemoState Decrement() => this with { Counter = Counter - 1 };
    public MiddlewareDemoState SetCounter(int value) => this with { Counter = value };
    public MiddlewareDemoState Reset() => this with { Counter = 0 };

    public MiddlewareDemoState LogAction(string action) => this with
    {
        ActionLog = ActionLog.Count >= 20
            ? ActionLog.RemoveAt(0).Add(action)
            : ActionLog.Add(action),
        LastAction = DateTime.Now,
        TotalActions = TotalActions + 1
    };

    public MiddlewareDemoState SetDuration(TimeSpan duration) => this with
    {
        LastActionDuration = duration
    };
}
