// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EasyAppDev.Blazor.Store.Utilities;

/// <summary>
/// Manages debouncing of actions with automatic cleanup and thread-safe operation.
/// </summary>
/// <remarks>
/// <para>
/// Debouncing delays execution of an action until a specified time period has elapsed
/// since the last invocation. If the action is invoked again before the delay expires,
/// the previous invocation is cancelled and the timer resets.
/// </para>
/// <para>
/// This is useful for:
/// - Search inputs (wait for user to stop typing)
/// - Form validation (wait for user to finish editing)
/// - Auto-save features (wait for user to stop editing)
/// </para>
/// <example>
/// <code>
/// using var manager = new DebounceManager();
///
/// // User types quickly - only last call executes
/// await manager.Debounce("search", async () => await SearchAsync("a"), 300);
/// await manager.Debounce("search", async () => await SearchAsync("ab"), 300);
/// await manager.Debounce("search", async () => await SearchAsync("abc"), 300);
/// // Only SearchAsync("abc") will execute after 300ms
/// </code>
/// </example>
/// </remarks>
public sealed class DebounceManager : IDebounceManager
{
    private readonly Dictionary<string, CancellationTokenSource> _pendingActions = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Debounces the specified action, cancelling any pending execution with the same key.
    /// </summary>
    /// <param name="key">Unique identifier for this debounced action.</param>
    /// <param name="action">The action to execute after the delay.</param>
    /// <param name="delayMilliseconds">Delay in milliseconds before executing the action.</param>
    /// <returns>A task that represents the debounce operation.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="key"/> or <paramref name="action"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="delayMilliseconds"/> is negative or zero.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// Thrown when this manager has been disposed.
    /// </exception>
    public async Task Debounce(string key, Func<Task> action, int delayMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(delayMilliseconds);

        CancellationTokenSource cts;
        CancellationTokenSource? oldCts = null;

        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();

            // Cancel existing debounce for this key (dispose outside lock)
            if (_pendingActions.TryGetValue(key, out var existingCts))
            {
                existingCts.Cancel();
                oldCts = existingCts;
            }

            // Create new cancellation token for this debounce
            cts = new CancellationTokenSource();
            _pendingActions[key] = cts;
        }
        finally
        {
            _lock.Release();
        }

        // Dispose old CTS outside lock to avoid blocking
        oldCts?.Dispose();

        // Schedule execution after delay (outside the lock)
        _ = ExecuteAfterDelayAsync(key, action, delayMilliseconds, cts);
    }

    private async Task ExecuteAfterDelayAsync(
        string key,
        Func<Task> action,
        int delayMilliseconds,
        CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(delayMilliseconds, cts.Token).ConfigureAwait(false);

            // Execute action if not cancelled
            if (!cts.Token.IsCancellationRequested)
            {
                await action().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when debounce is cancelled - ignore
        }
        finally
        {
            // Clean up completed action - only remove and dispose if this is still the current CTS
            bool shouldDispose = false;
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_pendingActions.TryGetValue(key, out var currentCts) &&
                    ReferenceEquals(currentCts, cts))
                {
                    _pendingActions.Remove(key);
                    shouldDispose = true;
                }
            }
            finally
            {
                _lock.Release();
            }

            // Dispose outside lock - only if we owned it
            if (shouldDispose)
            {
                cts.Dispose();
            }
        }
    }

    /// <summary>
    /// Cancels a pending debounced action for a specific key.
    /// </summary>
    /// <param name="key">The key identifying the debounced action to cancel.</param>
    public async Task CancelAsync(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        CancellationTokenSource? cts = null;

        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_pendingActions.TryGetValue(key, out cts))
            {
                _pendingActions.Remove(key);
            }
        }
        finally
        {
            _lock.Release();
        }

        // Cancel and dispose outside lock
        if (cts != null)
        {
            cts.Cancel();
            cts.Dispose();
        }
    }

    /// <summary>
    /// Cancels all pending debounced actions.
    /// </summary>
    public async Task CancelAllAsync()
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            foreach (var cts in _pendingActions.Values)
            {
                cts.Cancel();
                cts.Dispose();
            }
            _pendingActions.Clear();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Disposes the manager, cancelling all pending debounced actions.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _lock.Wait();
        try
        {
            foreach (var cts in _pendingActions.Values)
            {
                cts.Cancel();
                cts.Dispose();
            }
            _pendingActions.Clear();
            _disposed = true;
        }
        finally
        {
            _lock.Release();
            _lock.Dispose();
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DebounceManager));
        }
    }
}
