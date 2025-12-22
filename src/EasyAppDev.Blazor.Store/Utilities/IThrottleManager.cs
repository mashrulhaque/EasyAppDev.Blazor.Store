// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

namespace EasyAppDev.Blazor.Store.Utilities;

/// <summary>
/// Manages throttling of actions with automatic cleanup and thread-safe operation.
/// </summary>
/// <remarks>
/// Throttling limits how often an action can execute by enforcing a minimum interval between executions.
/// Unlike debouncing (waits for events to stop), throttling executes at regular intervals while events occur.
/// </remarks>
/// <example>
/// <code>
/// private async Task OnScroll()
/// {
///     await _throttler.Throttle(
///         "scroll-position",
///         async () => await UpdateScrollPositionAsync(),
///         intervalMilliseconds: 100,
///         leading: true
///     );
/// }
/// </code>
/// </example>
public interface IThrottleManager : IDisposable
{
    /// <summary>
    /// Throttles the specified action, limiting its execution frequency to once per interval.
    /// </summary>
    /// <param name="key">Unique identifier for this throttled action.</param>
    /// <param name="action">The async action to execute.</param>
    /// <param name="intervalMilliseconds">Minimum interval between executions in milliseconds.</param>
    /// <param name="leading">
    /// When true (default), executes immediately on first call (leading edge).
    /// When false, waits for interval to expire before executing (trailing edge).
    /// </param>
    /// <returns>A task that completes when the throttle operation finishes.</returns>
    /// <example>
    /// <code>
    /// await throttler.Throttle(
    ///     "scroll",
    ///     async () => await UpdateScrollIndicatorAsync(position),
    ///     100,
    ///     leading: true
    /// );
    /// </code>
    /// </example>
    Task Throttle(
        string key,
        Func<Task> action,
        int intervalMilliseconds,
        bool leading = true);

    /// <summary>
    /// Cancels a pending throttled action for a specific key.
    /// </summary>
    /// <param name="key">The key identifying the throttled action to cancel.</param>
    /// <returns>A task that completes when the action has been cancelled.</returns>
    Task CancelAsync(string key);

    /// <summary>
    /// Cancels all pending throttled actions across all keys.
    /// </summary>
    /// <returns>A task that completes when all pending throttled actions have been cancelled.</returns>
    Task CancelAllAsync();
}
