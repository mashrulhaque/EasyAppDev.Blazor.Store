namespace EasyAppDev.Blazor.Store.Core;

/// <summary>
/// Provides read-only access to the current state.
/// </summary>
/// <typeparam name="TState">The type of state managed by the store. Must be a non-nullable reference type.</typeparam>
/// <remarks>
/// Thread-safe read-only access to state snapshots.
/// See also: <see cref="IStateWriter{TState}"/>, <see cref="IStateObservable{TState}"/>.
/// </remarks>
/// <example>
/// <code>
/// public class UserService
/// {
///     private readonly IStateReader&lt;UserState&gt; _stateReader;
///
///     public UserService(IStateReader&lt;UserState&gt; stateReader)
///     {
///         _stateReader = stateReader;
///     }
///
///     public string GetCurrentUsername()
///     {
///         var state = _stateReader.GetState();
///         return state.User?.Name ?? "Anonymous";
///     }
/// }
/// </code>
/// </example>
public interface IStateReader<TState> where TState : notnull
{
    /// <summary>
    /// Gets the current state snapshot.
    /// </summary>
    /// <returns>
    /// An immutable snapshot of the current state. Never returns null.
    /// </returns>
    /// <remarks>
    /// Returns a reference to the current state. Thread-safe and lightweight.
    /// </remarks>
    /// <example>
    /// <code>
    /// var currentState = stateReader.GetState();
    /// Console.WriteLine($"Current count: {currentState.Count}");
    ///
    /// // State snapshot remains unchanged even after store updates
    /// var snapshot = stateReader.GetState();
    /// store.Update(s => s with { Count = s.Count + 1 });
    /// // snapshot.Count still has the original value
    /// </code>
    /// </example>
    TState GetState();
}
