// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

namespace EasyAppDev.Blazor.Store.Utilities;

/// <summary>
/// Manages debouncing of actions with automatic cleanup and thread-safe operation.
/// </summary>
/// <remarks>
/// Debouncing delays execution until a specified time has elapsed since the last invocation.
/// Each key maintains its own independent timer.
/// </remarks>
/// <example>
/// <code>
/// private async Task OnSearchInputChanged(string searchTerm)
/// {
///     await _debouncer.Debounce(
///         "search",
///         async () => await PerformSearchAsync(searchTerm),
///         delayMilliseconds: 300
///     );
/// }
/// </code>
/// </example>
public interface IDebounceManager : IDisposable
{
    /// <summary>
    /// Debounces the specified action, cancelling any pending execution with the same key.
    /// </summary>
    /// <param name="key">Unique identifier for this debounced action.</param>
    /// <param name="action">The async action to execute after the delay expires.</param>
    /// <param name="delayMilliseconds">Delay in milliseconds before executing the action.</param>
    /// <returns>A task that completes when the action executes or is cancelled.</returns>
    /// <example>
    /// <code>
    /// await debouncer.Debounce(
    ///     "search",
    ///     async () => await searchService.SearchAsync(searchTerm),
    ///     300
    /// );
    /// </code>
    /// </example>
    Task Debounce(string key, Func<Task> action, int delayMilliseconds);

    /// <summary>
    /// Cancels a pending debounced action for a specific key.
    /// </summary>
    /// <param name="key">The key identifying the debounced action to cancel.</param>
    /// <returns>A task that completes when the action has been cancelled.</returns>
    Task CancelAsync(string key);

    /// <summary>
    /// Cancels all pending debounced actions across all keys.
    /// </summary>
    /// <returns>A task that completes when all pending actions have been cancelled.</returns>
    Task CancelAllAsync();
}
