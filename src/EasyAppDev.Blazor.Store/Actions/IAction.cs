namespace EasyAppDev.Blazor.Store.Actions;

/// <summary>
/// Marker interface for store actions.
/// Actions represent user intents and are dispatched to the store
/// to trigger state changes via reducers.
/// </summary>
/// <remarks>
/// Actions should be implemented as records for immutability and value equality:
/// <code>
/// public record Increment : IAction;
/// public record IncrementBy(int Amount) : IAction;
/// public record SetCount(int Value) : IAction;
/// </code>
/// </remarks>
public interface IAction
{
}

/// <summary>
/// Marker interface for actions that target a specific state type.
/// Provides compile-time safety when defining reducers.
/// </summary>
/// <typeparam name="TState">The state type this action affects.</typeparam>
/// <remarks>
/// Using typed actions provides IntelliSense support and ensures
/// actions are only dispatched to compatible stores:
/// <code>
/// public record Increment : IAction&lt;CounterState&gt;;
/// public record IncrementBy(int Amount) : IAction&lt;CounterState&gt;;
/// </code>
/// </remarks>
public interface IAction<TState> : IAction where TState : notnull
{
}
