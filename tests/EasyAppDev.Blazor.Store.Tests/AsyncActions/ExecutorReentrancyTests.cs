using System.Diagnostics;
using EasyAppDev.Blazor.Store.AsyncActions;
using EasyAppDev.Blazor.Store.Core;
using FluentAssertions;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.AsyncActions;

public record ReentryState(bool Loading, int Value);

/// <summary>
/// Regression tests for AsyncActionExecutor holding its lock across state updates:
/// subscriber re-entry into the executor's sync cache APIs must not deadlock or
/// silently no-op, and the loading-state update must run outside the executor lock.
/// </summary>
public class ExecutorReentrancyTests
{
    [Fact]
    public async Task ExecuteCachedAsync_SubscriberCallsSyncCacheApis_DoesNotDeadlockOrStall()
    {
        // Arrange
        var store = StoreBuilder<ReentryState>.Create(new ReentryState(false, 0)).Build();
        using var executor = new AsyncActionExecutor<ReentryState>(store);

        // Subscribers re-enter the executor synchronously during the LOADING/SUCCESS
        // notifications. Previously these would block on the executor-wide semaphore
        // (held across UpdateAsync) for 5 seconds each and then silently no-op.
        store.Subscribe(_ =>
        {
            executor.InvalidateCache("unrelated-key");
            executor.InvalidateCacheByPrefix("unrelated-");
            executor.ClearCache();
        });

        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await executor.ExecuteCachedAsync(
            "key",
            async () =>
            {
                await Task.Delay(10);
                return 42;
            },
            loading: s => s with { Loading = true },
            success: (s, r) => s with { Loading = false, Value = r },
            cacheFor: TimeSpan.FromMinutes(1));

        stopwatch.Stop();

        // Assert
        result.Should().Be(42);
        store.GetState().Value.Should().Be(42);
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(3),
            "because subscriber re-entry must not wait on the executor lock");
    }

    [Fact]
    public async Task InvalidateCache_WhileAnotherOperationInFlight_TakesEffectImmediately()
    {
        // Arrange
        var store = StoreBuilder<ReentryState>.Create(new ReentryState(false, 0)).Build();
        using var executor = new AsyncActionExecutor<ReentryState>(store);

        var loadCount = 0;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Cache a value for key-a
        await executor.ExecuteCachedAsync(
            "key-a",
            () => { loadCount++; return Task.FromResult(1); },
            loading: s => s,
            success: (s, r) => s with { Value = r },
            cacheFor: TimeSpan.FromMinutes(1));

        // Start a slow operation for key-b that is still in flight
        var inFlight = executor.ExecuteCachedAsync(
            "key-b",
            async () => { await gate.Task; return 2; },
            loading: s => s,
            success: (s, r) => s with { Value = r },
            cacheFor: TimeSpan.FromMinutes(1));

        // Act - the sync invalidation must work immediately, not wait for key-b
        var stopwatch = Stopwatch.StartNew();
        executor.InvalidateCache("key-a");
        stopwatch.Stop();

        gate.SetResult();
        await inFlight;

        // key-a must reload now that it was invalidated
        await executor.ExecuteCachedAsync(
            "key-a",
            () => { loadCount++; return Task.FromResult(3); },
            loading: s => s,
            success: (s, r) => s with { Value = r },
            cacheFor: TimeSpan.FromMinutes(1));

        // Assert
        loadCount.Should().Be(2, "because the invalidated entry must be reloaded");
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1),
            "because sync invalidation must not block on in-flight operations");
    }
}
