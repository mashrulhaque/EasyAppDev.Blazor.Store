using EasyAppDev.Blazor.Store.Actions;

namespace EasyAppDev.Blazor.Store.Sample.State;

/// <summary>
/// State for demonstrating Redux-style actions with type-safe dispatching.
/// </summary>
public record ActionsDemoState(
    int Count,
    string LastAction,
    DateTime? LastActionTime)
{
    public static ActionsDemoState Initial => new(0, "None", null);
}

// Action definitions - each action is a record implementing IAction
public record Increment : IAction;
public record Decrement : IAction;
public record IncrementBy(int Amount) : IAction;
public record DecrementBy(int Amount) : IAction;
public record Reset : IAction;
public record SetCount(int Value) : IAction;
