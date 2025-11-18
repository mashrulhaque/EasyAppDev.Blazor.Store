namespace EasyAppDev.Blazor.Store.ServerSample.State;

/// <summary>
/// Scoped counter state - ISOLATED per user/circuit.
/// Each connected client gets their own counter instance.
/// </summary>
public record ScopedCounterState(int Count = 0, string? LastAction = null)
{
    public ScopedCounterState Increment() => this with
    {
        Count = Count + 1,
        LastAction = "INCREMENT"
    };

    public ScopedCounterState Decrement() => this with
    {
        Count = Count - 1,
        LastAction = "DECREMENT"
    };

    public ScopedCounterState Reset() => this with
    {
        Count = 0,
        LastAction = "RESET"
    };
}
