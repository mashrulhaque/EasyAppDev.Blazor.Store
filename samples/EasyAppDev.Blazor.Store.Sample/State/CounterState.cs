namespace EasyAppDev.Blazor.Store.Sample.State;

/// <summary>
/// State for the counter example.
/// </summary>
/// <param name="Count">The current count value.</param>
/// <param name="LastAction">The last action that modified the state.</param>
public record CounterState(int Count, string? LastAction = null)
{
    /// <summary>
    /// Increments the counter by 1.
    /// </summary>
    public CounterState Increment() => this with
    {
        Count = Count + 1,
        LastAction = "INCREMENT"
    };

    /// <summary>
    /// Decrements the counter by 1.
    /// </summary>
    public CounterState Decrement() => this with
    {
        Count = Count - 1,
        LastAction = "DECREMENT"
    };

    /// <summary>
    /// Increments the counter by a specified amount.
    /// </summary>
    /// <param name="amount">The amount to increment by.</param>
    public CounterState IncrementBy(int amount) => this with
    {
        Count = Count + amount,
        LastAction = $"INCREMENT_BY_{amount}"
    };

    /// <summary>
    /// Resets the counter to 0.
    /// </summary>
    public CounterState Reset() => this with
    {
        Count = 0,
        LastAction = "RESET"
    };
}
