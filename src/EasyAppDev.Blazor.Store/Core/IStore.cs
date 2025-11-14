namespace EasyAppDev.Blazor.Store.Core;

/// <summary>
/// Represents a reactive state store that manages state of type <typeparamref name="TState"/>.
/// </summary>
/// <typeparam name="TState">The type of state managed by this store.</typeparam>
/// <remarks>
/// Composes three interfaces:
/// <list type="bullet">
/// <item><description><see cref="IStateReader{TState}"/> - Read-only state access</description></item>
/// <item><description><see cref="IStateWriter{TState}"/> - State update operations</description></item>
/// <item><description><see cref="IStateObservable{TState}"/> - State change subscriptions</description></item>
/// </list>
/// </remarks>
public interface IStore<TState> : IStateReader<TState>, IStateWriter<TState>, IStateObservable<TState>, IDisposable
    where TState : notnull
{
}
