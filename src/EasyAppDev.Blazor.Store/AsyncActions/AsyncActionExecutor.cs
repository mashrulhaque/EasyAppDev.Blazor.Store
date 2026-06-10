// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using EasyAppDev.Blazor.Store.Core;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
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
    private readonly ConcurrentDictionary<string, CachedOperation> _inFlightOperations = new();
    private readonly ConcurrentDictionary<string, CachedResult> _cachedResults = new();
    // Used ONLY for the short check-and-register section of ExecuteCachedAsync.
    // It is never held across state updates or user-provided async actions.
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

        // Fast path: lock-free cache hit
        if (_cachedResults.TryGetValue(cacheKey, out var cached) && !cached.IsExpired)
        {
            return (TResult)cached.Value!;
        }

        TaskCompletionSource<object?>? tcs = null;
        Task<object?> operationTask;

        // Short check-and-register section. The semaphore is released BEFORE any state
        // update runs so subscriber re-entry into this executor cannot deadlock and
        // operations for different keys do not serialize on state updates.
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Re-check the cache now that we hold the lock
            if (_cachedResults.TryGetValue(cacheKey, out cached))
            {
                if (!cached.IsExpired)
                {
                    return (TResult)cached.Value!;
                }
                _cachedResults.TryRemove(cacheKey, out _);
            }

            // Aggressive cleanup when cache is large
            PerformCacheCleanup();

            // Check for in-flight operation (deduplication)
            if (_inFlightOperations.TryGetValue(cacheKey, out var inFlight))
            {
                operationTask = inFlight.Task;
            }
            else
            {
                tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
                operationTask = tcs.Task;
                _inFlightOperations[cacheKey] = new CachedOperation { Task = operationTask };
            }
        }
        finally
        {
            _lock.Release();
        }

        if (tcs != null)
        {
            // First caller - run the operation (the loading-state update happens inside,
            // outside the semaphore). The task completes the shared TCS for all waiters.
            _ = RunCachedOperationAsync(tcs, cacheKey, asyncAction, loading, success, error, cacheFor, action);
        }

        var result = await operationTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        return (TResult)result!;
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

    private async Task RunCachedOperationAsync<TResult>(
        TaskCompletionSource<object?> tcs,
        string cacheKey,
        Func<Task<TResult>> asyncAction,
        Func<TState, TState> loading,
        Func<TState, TResult, TState> success,
        Func<TState, Exception, TState>? error,
        TimeSpan? cacheFor,
        string? action)
    {
        try
        {
            // Set loading state - the executor lock is NOT held here, so subscribers
            // reacting to this update can safely call back into the executor.
            await _stateWriter.UpdateAsync(loading, $"{action}_LOADING");

            var result = await ExecuteCachedOperationAsync(cacheKey, asyncAction, success, error, cacheFor, action)
                .ConfigureAwait(false);
            tcs.TrySetResult(result);
        }
        catch (Exception ex)
        {
            tcs.TrySetException(ex);
        }
        finally
        {
            _inFlightOperations.TryRemove(cacheKey, out _);
        }
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
                var now = DateTime.UtcNow;
                _cachedResults[cacheKey] = new CachedResult
                {
                    Value = result,
                    ExpiresAt = now.Add(cacheFor.Value),
                    CreatedAt = now
                };
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

        _cachedResults.TryRemove(cacheKey, out _);
    }

    /// <inheritdoc />
    public Task InvalidateCacheAsync(string cacheKey)
    {
        InvalidateCache(cacheKey);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void InvalidateCacheByPrefix(string prefix)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(prefix);

        foreach (var key in _cachedResults.Keys)
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal))
            {
                _cachedResults.TryRemove(key, out _);
            }
        }
    }

    /// <inheritdoc />
    public Task InvalidateCacheByPrefixAsync(string prefix)
    {
        InvalidateCacheByPrefix(prefix);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void ClearCache()
    {
        ThrowIfDisposed();

        _cachedResults.Clear();
    }

    /// <inheritdoc />
    public Task ClearCacheAsync()
    {
        ClearCache();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Performs cache cleanup by removing expired entries and enforcing size limits.
    /// </summary>
    private void PerformCacheCleanup()
    {
        // Early exit if cache is small
        if (_cachedResults.Count < 100)
            return;

        // First pass: remove all expired entries
        foreach (var kvp in _cachedResults)
        {
            if (kvp.Value.IsExpired)
            {
                _cachedResults.TryRemove(kvp.Key, out _);
            }
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
                _cachedResults.TryRemove(key, out _);
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

        // NOTE: _lock (SemaphoreSlim) is intentionally NOT disposed. It holds no unmanaged
        // resources as long as AvailableWaitHandle is never accessed (it is not), and
        // disposing it would make in-flight ExecuteCachedAsync calls throw
        // ObjectDisposedException from their finally { _lock.Release(); } blocks.
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(AsyncActionExecutor<TState>));
    }
}
