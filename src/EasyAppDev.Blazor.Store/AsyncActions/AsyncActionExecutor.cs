// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using EasyAppDev.Blazor.Store.Core;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace EasyAppDev.Blazor.Store.AsyncActions;

/// <summary>
/// Default implementation of <see cref="IAsyncActionExecutor{TState}"/>.
/// </summary>
/// <typeparam name="TState">The type of state being managed.</typeparam>
public sealed class AsyncActionExecutor<TState> : IAsyncActionExecutor<TState> where TState : notnull
{
    private readonly IStateWriter<TState> _stateWriter;
    private readonly ILogger<AsyncActionExecutor<TState>>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncActionExecutor{TState}"/> class.
    /// </summary>
    /// <param name="stateWriter">The state writer for updating state.</param>
    /// <param name="logger">Optional logger for error reporting.</param>
    public AsyncActionExecutor(
        IStateWriter<TState> stateWriter,
        ILogger<AsyncActionExecutor<TState>>? logger = null)
    {
        _stateWriter = stateWriter ?? throw new ArgumentNullException(nameof(stateWriter));
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task ExecuteAsync<TResult>(
        Func<Task<TResult>> asyncAction,
        Func<TState, TState> loading,
        Func<TState, TResult, TState> success,
        Func<TState, Exception, TState>? error = null,
        [CallerMemberName] string? action = null)
    {
        ArgumentNullException.ThrowIfNull(asyncAction);
        ArgumentNullException.ThrowIfNull(loading);
        ArgumentNullException.ThrowIfNull(success);

        // Set loading state
        await _stateWriter.UpdateAsync(loading, $"{action}_LOADING");

        try
        {
            // Execute async action
            var result = await asyncAction();

            // Set success state
            await _stateWriter.UpdateAsync(s => success(s, result), $"{action}_SUCCESS");
        }
        catch (Exception ex)
        {
            // Set error state
            if (error != null)
            {
                await _stateWriter.UpdateAsync(s => error(s, ex), $"{action}_ERROR");
            }
            else
            {
                // If no error handler provided, log and keep current state
                _logger?.LogError(ex, "Error in async action: {Action}", action);
            }
        }
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(
        Func<Task> asyncAction,
        Func<TState, TState> loading,
        Func<TState, TState> success,
        Func<TState, Exception, TState>? error = null,
        [CallerMemberName] string? action = null)
    {
        ArgumentNullException.ThrowIfNull(asyncAction);
        ArgumentNullException.ThrowIfNull(loading);
        ArgumentNullException.ThrowIfNull(success);

        await _stateWriter.UpdateAsync(loading, $"{action}_LOADING");

        try
        {
            await asyncAction();
            await _stateWriter.UpdateAsync(success, $"{action}_SUCCESS");
        }
        catch (Exception ex)
        {
            if (error != null)
            {
                await _stateWriter.UpdateAsync(s => error(s, ex), $"{action}_ERROR");
            }
            else
            {
                _logger?.LogError(ex, "Error in async action: {Action}", action);
            }
        }
    }

    /// <inheritdoc />
    public async Task ExecuteAsync<TResult>(
        Func<Task<TResult>> asyncAction,
        Func<TState, TState> loading,
        Func<TState, TState> success,
        Func<TState, Exception, TState>? error = null,
        [CallerMemberName] string? action = null)
    {
        await ExecuteAsync(
            asyncAction,
            loading,
            (s, _) => success(s),  // Discard result
            error,
            action);
    }
}
