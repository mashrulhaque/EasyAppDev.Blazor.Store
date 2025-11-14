using EasyAppDev.Blazor.Store.Core;

namespace EasyAppDev.Blazor.Store.AsyncActions;

/// <summary>
/// Helper for executing async actions with automatic loading/error state management.
/// </summary>
/// <typeparam name="TState">The type of state managed by the store.</typeparam>
/// <typeparam name="TResult">The type of result returned by the async action.</typeparam>
public class AsyncAction<TState, TResult> where TState : notnull
{
    private readonly IStore<TState> _store;
    private readonly Func<TState, AsyncActionState<TResult>> _stateSelector;
    private readonly Func<TState, AsyncActionState<TResult>, TState> _stateUpdater;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncAction{TState, TResult}"/> class.
    /// </summary>
    /// <param name="store">The store instance.</param>
    /// <param name="stateSelector">Function to select the action state from store state.</param>
    /// <param name="stateUpdater">Function to update store state with new action state.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when any parameter is null.
    /// </exception>
    public AsyncAction(
        IStore<TState> store,
        Func<TState, AsyncActionState<TResult>> stateSelector,
        Func<TState, AsyncActionState<TResult>, TState> stateUpdater)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _stateSelector = stateSelector ?? throw new ArgumentNullException(nameof(stateSelector));
        _stateUpdater = stateUpdater ?? throw new ArgumentNullException(nameof(stateUpdater));
    }

    /// <summary>
    /// Executes the async action with automatic state management.
    /// </summary>
    /// <param name="action">The async action to execute.</param>
    /// <param name="actionName">Optional action name for debugging.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the action.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="action"/> is null.
    /// </exception>
    /// <remarks>
    /// The method automatically manages the following state transitions:
    /// <list type="number">
    /// <item><description>Sets IsLoading = true before execution</description></item>
    /// <item><description>On success: Sets Data = result, IsLoading = false</description></item>
    /// <item><description>On error: Sets Error = message, IsLoading = false</description></item>
    /// </list>
    /// </remarks>
    public async Task<TResult> ExecuteAsync(
        Func<Task<TResult>> action,
        string? actionName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        var loadingActionName = actionName != null ? $"{actionName}_LOADING" : null;
        var successActionName = actionName != null ? $"{actionName}_SUCCESS" : null;
        var failureActionName = actionName != null ? $"{actionName}_FAILURE" : null;

        // Set loading state
        await _store.UpdateAsync(
            state => _stateUpdater(state, AsyncActionState<TResult>.Loading()),
            loadingActionName).ConfigureAwait(false);

        try
        {
            // Execute action
            var result = await action().ConfigureAwait(false);

            // Set success state
            await _store.UpdateAsync(
                state => _stateUpdater(state, AsyncActionState<TResult>.Success(result)),
                successActionName).ConfigureAwait(false);

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Set error state
            await _store.UpdateAsync(
                state => _stateUpdater(state, AsyncActionState<TResult>.Failed(ex.Message)),
                failureActionName).ConfigureAwait(false);

            throw;
        }
    }

    /// <summary>
    /// Executes the async action with a parameter.
    /// </summary>
    /// <typeparam name="TParam">The type of the parameter.</typeparam>
    /// <param name="action">The async action to execute.</param>
    /// <param name="parameter">The parameter to pass to the action.</param>
    /// <param name="actionName">Optional action name for debugging.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the action.</returns>
    public async Task<TResult> ExecuteAsync<TParam>(
        Func<TParam, Task<TResult>> action,
        TParam parameter,
        string? actionName = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(() => action(parameter), actionName, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the current action state.
    /// </summary>
    /// <returns>The current async action state.</returns>
    public AsyncActionState<TResult> GetState()
    {
        return _stateSelector(_store.GetState());
    }

    /// <summary>
    /// Resets the action state to idle.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ResetAsync()
    {
        await _store.UpdateAsync(
            state => _stateUpdater(state, AsyncActionState<TResult>.Idle()),
            "RESET_ASYNC_ACTION").ConfigureAwait(false);
    }
}
