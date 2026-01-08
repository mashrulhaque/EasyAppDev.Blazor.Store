// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using EasyAppDev.Blazor.Store.Core;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Runtime.CompilerServices;

namespace EasyAppDev.Blazor.Store.AsyncActions;

/// <summary>
/// Default implementation of <see cref="IAsyncActionExecutor{TState}"/>.
/// </summary>
/// <typeparam name="TState">The type of state being managed.</typeparam>
public sealed class AsyncActionExecutor<TState> : IAsyncActionExecutor<TState>, IDisposable where TState : notnull
{
    private const int MaxCacheSize = 1000;
    private readonly IStateWriter<TState> _stateWriter;
    private readonly ILogger<AsyncActionExecutor<TState>>? _logger;
    private readonly Dictionary<string, CachedOperation> _inFlightOperations = new();
    private readonly Dictionary<string, CachedResult> _cachedResults = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private int _disposed; // 0 = not disposed, 1 = disposed (use int for Interlocked)

    /// <summary>
    /// Tracks an in-flight operation for deduplication.
    /// </summary>
    private sealed class CachedOperation
    {
        public required Task<object?> Task { get; init; }
    }

    /// <summary>
    /// Stores a cached successful result with expiration.
    /// </summary>
    private sealed class CachedResult
    {
        public required object? Value { get; init; }
        public required DateTime ExpiresAt { get; init; }
        public required DateTime CreatedAt { get; init; }
        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    }

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

    /// <inheritdoc />
    public async Task<TResult> ExecuteCachedAsync<TResult>(
        string cacheKey,
        Func<Task<TResult>> asyncAction,
        Func<TState, TState> loading,
        Func<TState, TResult, TState> success,
        Func<TState, Exception, TState>? error = null,
        TimeSpan? cacheFor = null,
        CancellationToken cancellationToken = default,
        [CallerMemberName] string? action = null)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(cacheKey);
        ArgumentNullException.ThrowIfNull(asyncAction);
        ArgumentNullException.ThrowIfNull(loading);
        ArgumentNullException.ThrowIfNull(success);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Check for cached result first
            if (_cachedResults.TryGetValue(cacheKey, out var cached))
            {
                if (!cached.IsExpired)
                {
                    return (TResult)cached.Value!;
                }
                _cachedResults.Remove(cacheKey);
            }

            // Aggressive cleanup when cache is large
            PerformCacheCleanup();

            // Check for in-flight operation (deduplication)
            if (_inFlightOperations.TryGetValue(cacheKey, out var inFlight))
            {
                // Release lock while waiting for in-flight operation
                _lock.Release();
                try
                {
                    var result = await inFlight.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                    return (TResult)result!;
                }
                finally
                {
                    await _lock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
                }
            }

            // First caller - set loading state and start the operation
            await _stateWriter.UpdateAsync(loading, $"{action}_LOADING");

            // Create the operation task (captures loading/success/error callbacks)
            var operationTask = ExecuteCachedOperationAsync(
                cacheKey, asyncAction, success, error, cacheFor, action);

            _inFlightOperations[cacheKey] = new CachedOperation { Task = operationTask };

            // Release lock before awaiting
            _lock.Release();
            try
            {
                var result = await operationTask.WaitAsync(cancellationToken).ConfigureAwait(false);
                return (TResult)result!;
            }
            finally
            {
                // Re-acquire lock to clean up - don't use cancellation token here
                // since cleanup must happen even if cancelled
                await _lock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
                _inFlightOperations.Remove(cacheKey);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task ExecuteCachedAsync<TResult>(
        string cacheKey,
        Func<Task<TResult>> asyncAction,
        Func<TState, TState> loading,
        Func<TState, TState> success,
        Func<TState, Exception, TState>? error = null,
        TimeSpan? cacheFor = null,
        CancellationToken cancellationToken = default,
        [CallerMemberName] string? action = null)
    {
        await ExecuteCachedAsync(
            cacheKey,
            asyncAction,
            loading,
            (s, _) => success(s),  // Discard result
            error,
            cacheFor,
            cancellationToken,
            action);
    }

    private async Task<object?> ExecuteCachedOperationAsync<TResult>(
        string cacheKey,
        Func<Task<TResult>> asyncAction,
        Func<TState, TResult, TState> success,
        Func<TState, Exception, TState>? error,
        TimeSpan? cacheFor,
        string? action)
    {
        try
        {
            // Execute the async action
            var result = await asyncAction().ConfigureAwait(false);

            // Set success state (only once for all waiting callers)
            await _stateWriter.UpdateAsync(s => success(s, result), $"{action}_SUCCESS");

            // Cache the result if cacheFor is specified
            if (cacheFor.HasValue)
            {
                await _lock.WaitAsync().ConfigureAwait(false);
                try
                {
                    var now = DateTime.UtcNow;
                    _cachedResults[cacheKey] = new CachedResult
                    {
                        Value = result,
                        ExpiresAt = now.Add(cacheFor.Value),
                        CreatedAt = now
                    };
                }
                finally
                {
                    _lock.Release();
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            // Set error state (only once for all waiting callers)
            if (error != null)
            {
                await _stateWriter.UpdateAsync(s => error(s, ex), $"{action}_ERROR");
            }
            else
            {
                _logger?.LogError(ex, "Error in cached async action: {Action}", action);
            }
            throw;
        }
    }

    /// <inheritdoc />
    public void InvalidateCache(string cacheKey)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(cacheKey);

        // Use timeout to prevent indefinite blocking in edge cases
        if (!_lock.Wait(TimeSpan.FromSeconds(5)))
        {
            _logger?.LogWarning("InvalidateCache timed out waiting for lock. Consider using InvalidateCacheAsync instead.");
            return;
        }
        try
        {
            _cachedResults.Remove(cacheKey);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task InvalidateCacheAsync(string cacheKey)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(cacheKey);

        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            _cachedResults.Remove(cacheKey);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public void InvalidateCacheByPrefix(string prefix)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(prefix);

        // Use timeout to prevent indefinite blocking in edge cases
        if (!_lock.Wait(TimeSpan.FromSeconds(5)))
        {
            _logger?.LogWarning("InvalidateCacheByPrefix timed out waiting for lock. Consider using InvalidateCacheByPrefixAsync instead.");
            return;
        }
        try
        {
            var keysToRemove = _cachedResults.Keys
                .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
                .ToList();

            foreach (var key in keysToRemove)
            {
                _cachedResults.Remove(key);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task InvalidateCacheByPrefixAsync(string prefix)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(prefix);

        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            var keysToRemove = _cachedResults.Keys
                .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
                .ToList();

            foreach (var key in keysToRemove)
            {
                _cachedResults.Remove(key);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public void ClearCache()
    {
        ThrowIfDisposed();

        // Use timeout to prevent indefinite blocking in edge cases
        if (!_lock.Wait(TimeSpan.FromSeconds(5)))
        {
            _logger?.LogWarning("ClearCache timed out waiting for lock. Consider using ClearCacheAsync instead.");
            return;
        }
        try
        {
            _cachedResults.Clear();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task ClearCacheAsync()
    {
        ThrowIfDisposed();

        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            _cachedResults.Clear();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Performs cache cleanup by removing expired entries and enforcing size limits.
    /// Must be called while holding the lock.
    /// </summary>
    private void PerformCacheCleanup()
    {
        // Early exit if cache is small
        if (_cachedResults.Count < 100)
            return;

        // First pass: remove all expired entries
        var expiredKeys = _cachedResults
            .Where(kvp => kvp.Value.IsExpired)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _cachedResults.Remove(key);
        }

        // Second pass: if still at or over capacity, remove oldest entries (FIFO)
        if (_cachedResults.Count >= MaxCacheSize)
        {
            var entriesToRemove = _cachedResults.Count - MaxCacheSize + 1;
            var oldestKeys = _cachedResults
                .OrderBy(kvp => kvp.Value.CreatedAt)
                .Take(entriesToRemove)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in oldestKeys)
            {
                _cachedResults.Remove(key);
            }
        }
    }

    /// <summary>
    /// Disposes the executor and releases resources.
    /// </summary>
    public void Dispose()
    {
        // Use Interlocked.Exchange for atomic check-and-set to prevent race conditions
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _inFlightOperations.Clear();
        _cachedResults.Clear();
        _lock.Dispose();
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(AsyncActionExecutor<TState>));
    }
}
