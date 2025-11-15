namespace BlazorUnitedTest;

public record CounterState(int Count, string? LastAction = null)
{
    public CounterState Increment() => this with { Count = Count + 1, LastAction = "INCREMENT" };
    public CounterState Decrement() => this with { Count = Count - 1, LastAction = "DECREMENT" };
    public CounterState Reset() => this with { Count = 0, LastAction = "RESET" };
}
