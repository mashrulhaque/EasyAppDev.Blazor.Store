namespace EasyAppDev.Blazor.Store.ServerSample.State;

/// <summary>
/// Singleton counter state - SHARED across all users/circuits.
/// Demonstrates the problem: all connected clients see the same counter.
/// </summary>
public record SingletonCounterState(int Count = 0, string? LastAction = null)
{
    public SingletonCounterState Increment() => this with
    {
        Count = Count + 1,
        LastAction = "INCREMENT"
    };

    public SingletonCounterState Decrement() => this with
    {
        Count = Count - 1,
        LastAction = "DECREMENT"
    };

    public SingletonCounterState Reset() => this with
    {
        Count = 0,
        LastAction = "RESET"
    };
}
