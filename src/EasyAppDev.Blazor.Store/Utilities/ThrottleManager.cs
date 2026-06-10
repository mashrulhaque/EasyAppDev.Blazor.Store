// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace EasyAppDev.Blazor.Store.Utilities;

/// <summary>
/// Manages throttling of actions with automatic cleanup and thread-safe operation.
/// </summary>
/// <remarks>
/// <para>
/// Throttling limits how often an action can execute by enforcing a minimum interval
/// between executions. Unlike debouncing, throttling guarantees the action executes
/// at regular intervals while events are occurring.
/// </para>
/// <para>
/// This is useful for:
/// - Mouse move/scroll events (limit UI updates)
/// - Window resize handlers (limit reflow calculations)
/// - Real-time data updates (limit refresh rate)
/// </para>
/// <example>
/// <code>
/// using var manager = new ThrottleManager();
///
/// // Mouse moves rapidly - executes at most every 100ms
/// for (int i = 0; i &lt; 100; i++)
/// {
///     await manager.Throttle("mouse", async () => UpdatePosition(i), 100);
///     await Task.Delay(10);  // 10ms between calls
/// }
/// // Will execute ~10 times (every 100ms)
/// </code>
/// </example>
/// </remarks>
public sealed class ThrottleManager : IThrottleManager
{
    private readonly Dictionary<string, ThrottleState> _throttles = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<ThrottleManager>? _logger;
    private int _disposed; // 0 = not disposed, 1 = disposed (use int for Interlocked)

    /// <summary>
    /// Initializes a new instance of the <see cref="ThrottleManager"/> class.
    /// </summary>
    /// <param name="logger">
    /// Optional logger used to report failures of fire-and-forget trailing actions.
    /// </param>
    public ThrottleManager(ILogger<ThrottleManager>? logger = null)
    {
        _logger = logger;
    }

    private sealed class ThrottleState
    {
        public DateTime LastExecutionTime { get; set; } = DateTime.MinValue;
        public CancellationTokenSource? TrailingCts { get; set; }
        public bool HasTrailingExecution { get; set; }
    }

    /// <summary>
    /// Throttles the specified action, limiting execution frequency.
    /// </summary>
    /// <param name="key">Unique identifier for this throttled action.</param>
    /// <param name="action">The action to execute.</param>
    /// <param name="intervalMilliseconds">Minimum interval between executions in milliseconds.</param>
    /// <param name="leading">
    /// If true (default), executes immediately on first call (leading edge).
    /// If false, waits for interval before first execution (trailing edge).
    /// </param>
    /// <returns>A task that represents the throttle operation.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="key"/> or <paramref name="action"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="intervalMilliseconds"/> is negative or zero.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// Thrown when this manager has been disposed.
    /// </exception>
    public async Task Throttle(
        string key,
        Func<Task> action,
        int intervalMilliseconds,
        bool leading = true)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(intervalMilliseconds);

        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();

            if (!_throttles.TryGetValue(key, out var state))
            {
                state = new ThrottleState();
                _throttles[key] = state;
            }

            var now = DateTime.UtcNow;
            var timeSinceLastExecution = (now - state.LastExecutionTime).TotalMilliseconds;

            if (timeSinceLastExecution >= intervalMilliseconds)
            {
                // Enough time passed - execute immediately (leading edge)
                if (leading || state.LastExecutionTime != DateTime.MinValue)
                {
                    state.LastExecutionTime = now;
                    state.HasTrailingExecution = false;

                    // Cancel any pending trailing execution
                    state.TrailingCts?.Cancel();
                    state.TrailingCts?.Dispose();
                    state.TrailingCts = null;

                    // Execute action outside the lock
                    _lock.Release();
                    try
                    {
                        await action().ConfigureAwait(false);
                        return;
                    }
                    finally
                    {
                        await _lock.WaitAsync().ConfigureAwait(false);
                    }
                }
            }

            // Cancel existing trailing execution and schedule a new one
            state.TrailingCts?.Cancel();
            state.TrailingCts?.Dispose();

            // Create new cancellation token
            var cts = new CancellationTokenSource();
            state.TrailingCts = cts;
            state.HasTrailingExecution = true;

            // Calculate delay - use full interval if first call or if not enough time has passed
            var delay = timeSinceLastExecution >= intervalMilliseconds
                ? intervalMilliseconds
                : (int)(intervalMilliseconds - timeSinceLastExecution);
            _ = ExecuteTrailingAsync(key, action, delay, cts);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task ExecuteTrailingAsync(
        string key,
        Func<Task> action,
        int delayMilliseconds,
        CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(delayMilliseconds, cts.Token).ConfigureAwait(false);

            if (cts.Token.IsCancellationRequested)
            {
                return;
            }

            // Confirm under the lock that this task is still the CURRENT trailing
            // execution. If it was superseded (or cancelled/cleaned up) while waiting
            // for the lock, the action must NOT run - otherwise a stale action would
            // execute alongside its replacement.
            var isCurrentTrailing = false;
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (Volatile.Read(ref _disposed) == 0 &&
                    _throttles.TryGetValue(key, out var state) &&
                    ReferenceEquals(state.TrailingCts, cts))
                {
                    state.LastExecutionTime = DateTime.UtcNow;
                    state.HasTrailingExecution = false;
                    state.TrailingCts = null;
                    isCurrentTrailing = true;
                    // Note: State is intentionally kept in dictionary to maintain
                    // LastExecutionTime for proper throttling of subsequent calls.
                    // States are cleaned up via CancelAsync() or CancelAllAsync().
                }
            }
            finally
            {
                _lock.Release();
            }

            if (isCurrentTrailing)
            {
                cts.Dispose();
                await action().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when throttle is cancelled - ignore
        }
        catch (ObjectDisposedException)
        {
            // Manager was disposed while this trailing execution was pending - ignore
        }
        catch (Exception ex)
        {
            // This task is fire-and-forget; never let action failures go unobserved
            _logger?.LogError(ex, "Throttled action for key '{Key}' failed", key);
        }
    }

    /// <summary>
    /// Cancels a pending throttled action for a specific key.
    /// </summary>
    /// <param name="key">The key identifying the throttled action to cancel.</param>
    public async Task CancelAsync(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        CancellationTokenSource? cts = null;

        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_throttles.TryGetValue(key, out var state))
            {
                cts = state.TrailingCts;
                state.TrailingCts = null;
                _throttles.Remove(key);
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
    /// Cancels all pending throttled actions.
    /// </summary>
    public async Task CancelAllAsync()
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            foreach (var state in _throttles.Values)
            {
                state.TrailingCts?.Cancel();
                state.TrailingCts?.Dispose();
                state.TrailingCts = null;
            }
            _throttles.Clear();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Disposes the manager, cancelling all pending throttled actions.
    /// </summary>
    public void Dispose()
    {
        // Use Interlocked.Exchange for atomic check-and-set to prevent race conditions
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        // Use timeout to prevent indefinite blocking during dispose
        var acquired = _lock.Wait(TimeSpan.FromSeconds(5));
        try
        {
            foreach (var state in _throttles.Values)
            {
                try
                {
                    state.TrailingCts?.Cancel();
                    state.TrailingCts?.Dispose();
                    state.TrailingCts = null;
                }
                catch
                {
                    // Best effort cleanup
                }
            }
            _throttles.Clear();
        }
        finally
        {
            if (acquired)
            {
                _lock.Release();
            }

            // NOTE: _lock (SemaphoreSlim) is intentionally NOT disposed. It holds no
            // unmanaged resources as long as AvailableWaitHandle is never accessed
            // (it is not), and disposing it would make in-flight trailing executions
            // throw ObjectDisposedException when they re-acquire it.
        }
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            throw new ObjectDisposedException(nameof(ThrottleManager));
        }
    }
}
