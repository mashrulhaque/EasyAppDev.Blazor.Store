// Copyright (c) EasyAppDev. All rights reserved.
// Licensed under the MIT License.

using EasyAppDev.Blazor.Store.AsyncActions;
using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Tests.TestUtilities;

namespace EasyAppDev.Blazor.Store.Tests.AsyncActions;

public record CachedTestState(int UpdateCount, string? Data, bool IsLoading, string? Error);

public class ExecuteCachedAsyncTests
{
    [Fact]
    public async Task ExecuteCachedAsync_SetsLoadingAndSuccessStates()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(new CachedTestState(0, null, false, null));
        var executor = new AsyncActionExecutor<CachedTestState>(store);

        // Act
        var result = await executor.ExecuteCachedAsync(
            "test-key",
            async () =>
            {
                await Task.Delay(10);
                return "success data";
            },
            loading: s => s with { IsLoading = true, UpdateCount = s.UpdateCount + 1 },
            success: (s, data) => s with { Data = data, IsLoading = false, UpdateCount = s.UpdateCount + 1 }
        );

        // Assert
        result.Should().Be("success data");
        var state = store.GetState();
        state.Data.Should().Be("success data");
        state.IsLoading.Should().BeFalse();
        state.UpdateCount.Should().Be(2); // One loading + one success
    }

    [Fact]
    public async Task ExecuteCachedAsync_WithConcurrentCalls_OnlyUpdatesStateOnce()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(new CachedTestState(0, null, false, null));
        var executor = new AsyncActionExecutor<CachedTestState>(store);

        var tcs = new TaskCompletionSource<string>();

        // Act - start 5 concurrent calls
        var tasks = Enumerable.Range(0, 5).Select(_ =>
            executor.ExecuteCachedAsync(
                "same-key",
                () => tcs.Task,
                loading: s => s with { IsLoading = true, UpdateCount = s.UpdateCount + 1 },
                success: (s, data) => s with { Data = data, IsLoading = false, UpdateCount = s.UpdateCount + 1 }
            )
        ).ToList();

        // Allow all tasks to start
        await Task.Delay(50);

        // Verify loading state was set only once
        store.GetState().UpdateCount.Should().Be(1);
        store.GetState().IsLoading.Should().BeTrue();

        // Complete the operation
        tcs.SetResult("shared result");
        var results = await Task.WhenAll(tasks);

        // Assert - all callers get the same result, but state only updated twice total
        results.Should().AllBeEquivalentTo("shared result");
        var finalState = store.GetState();
        finalState.UpdateCount.Should().Be(2); // One loading + one success
        finalState.Data.Should().Be("shared result");
    }

    [Fact]
    public async Task ExecuteCachedAsync_WithCaching_ReturnsCachedResultWithoutStateUpdate()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(new CachedTestState(0, null, false, null));
        var executor = new AsyncActionExecutor<CachedTestState>(store);
        var callCount = 0;

        // Act - first call
        var result1 = await executor.ExecuteCachedAsync(
            "cached-key",
            async () =>
            {
                Interlocked.Increment(ref callCount);
                await Task.Delay(10);
                return "cached data";
            },
            loading: s => s with { IsLoading = true, UpdateCount = s.UpdateCount + 1 },
            success: (s, data) => s with { Data = data, IsLoading = false, UpdateCount = s.UpdateCount + 1 },
            cacheFor: TimeSpan.FromMinutes(5)
        );

        var updateCountAfterFirst = store.GetState().UpdateCount;

        // Second call - should return cached result without any state updates
        var result2 = await executor.ExecuteCachedAsync(
            "cached-key",
            async () =>
            {
                Interlocked.Increment(ref callCount);
                await Task.Delay(10);
                return "new data";
            },
            loading: s => s with { IsLoading = true, UpdateCount = s.UpdateCount + 1 },
            success: (s, data) => s with { Data = data, IsLoading = false, UpdateCount = s.UpdateCount + 1 },
            cacheFor: TimeSpan.FromMinutes(5)
        );

        // Assert
        callCount.Should().Be(1);
        result1.Should().Be("cached data");
        result2.Should().Be("cached data");
        store.GetState().UpdateCount.Should().Be(updateCountAfterFirst); // No additional updates
    }

    [Fact]
    public async Task ExecuteCachedAsync_WithDifferentKeys_ExecutesSeparately()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(new CachedTestState(0, null, false, null));
        var executor = new AsyncActionExecutor<CachedTestState>(store);

        // Act - two calls with different keys
        var task1 = executor.ExecuteCachedAsync(
            "key-1",
            async () =>
            {
                await Task.Delay(10);
                return "data-1";
            },
            loading: s => s with { IsLoading = true, UpdateCount = s.UpdateCount + 1 },
            success: (s, data) => s with { Data = data, IsLoading = false, UpdateCount = s.UpdateCount + 1 }
        );

        var task2 = executor.ExecuteCachedAsync(
            "key-2",
            async () =>
            {
                await Task.Delay(10);
                return "data-2";
            },
            loading: s => s with { IsLoading = true, UpdateCount = s.UpdateCount + 1 },
            success: (s, data) => s with { Data = data, IsLoading = false, UpdateCount = s.UpdateCount + 1 }
        );

        var results = await Task.WhenAll(task1, task2);

        // Assert - both executed separately
        results.Should().Contain("data-1");
        results.Should().Contain("data-2");
        store.GetState().UpdateCount.Should().Be(4); // 2 loading + 2 success
    }

    [Fact]
    public async Task ExecuteCachedAsync_OnError_SetsErrorStateOnce()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(new CachedTestState(0, null, false, null));
        var executor = new AsyncActionExecutor<CachedTestState>(store);

        var tcs = new TaskCompletionSource<string>();

        // Act - start 3 concurrent calls
        var tasks = Enumerable.Range(0, 3).Select(_ =>
            executor.ExecuteCachedAsync(
                "error-key",
                () => tcs.Task,
                loading: s => s with { IsLoading = true, UpdateCount = s.UpdateCount + 1 },
                success: (s, data) => s with { Data = data, IsLoading = false, UpdateCount = s.UpdateCount + 1 },
                error: (s, ex) => s with { Error = ex.Message, IsLoading = false, UpdateCount = s.UpdateCount + 1 }
            )
        ).ToList();

        await Task.Delay(50);

        // Complete with error
        tcs.SetException(new InvalidOperationException("Test error"));

        // Assert - all tasks throw, but error state only set once
        foreach (var task in tasks)
        {
            await task.Invoking(t => t).Should().ThrowAsync<InvalidOperationException>();
        }

        var state = store.GetState();
        state.UpdateCount.Should().Be(2); // One loading + one error
        state.Error.Should().Be("Test error");
    }

    [Fact]
    public async Task ExecuteCachedAsync_WithNullCacheKey_ThrowsArgumentNullException()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(new CachedTestState(0, null, false, null));
        var executor = new AsyncActionExecutor<CachedTestState>(store);

        // Act
        var act = async () => await executor.ExecuteCachedAsync<string>(
            null!,
            () => Task.FromResult("data"),
            loading: s => s,
            success: (s, _) => s
        );

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("cacheKey");
    }

    [Fact]
    public async Task ExecuteCachedAsync_SimplifiedSuccess_WorksCorrectly()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(new CachedTestState(0, null, false, null));
        var executor = new AsyncActionExecutor<CachedTestState>(store);

        // Act - using the simplified overload that discards the result
        await executor.ExecuteCachedAsync<string>(
            "simple-key",
            async () =>
            {
                await Task.Delay(10);
                return "ignored result";
            },
            loading: s => s with { IsLoading = true },
            success: s => s with { Data = "loaded", IsLoading = false }
        );

        // Assert
        var state = store.GetState();
        state.Data.Should().Be("loaded");
        state.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteCachedAsync_AfterCacheExpiry_ExecutesAgain()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(new CachedTestState(0, null, false, null));
        var executor = new AsyncActionExecutor<CachedTestState>(store);
        var callCount = 0;

        // Act - first call with very short cache
        await executor.ExecuteCachedAsync(
            "expiring-key",
            async () =>
            {
                Interlocked.Increment(ref callCount);
                return $"call-{callCount}";
            },
            loading: s => s with { IsLoading = true },
            success: (s, data) => s with { Data = data, IsLoading = false },
            cacheFor: TimeSpan.FromMilliseconds(50)
        );

        callCount.Should().Be(1);

        // Wait for cache to expire
        await Task.Delay(100);

        // Second call - should execute again
        var result = await executor.ExecuteCachedAsync(
            "expiring-key",
            async () =>
            {
                Interlocked.Increment(ref callCount);
                return $"call-{callCount}";
            },
            loading: s => s with { IsLoading = true },
            success: (s, data) => s with { Data = data, IsLoading = false },
            cacheFor: TimeSpan.FromMinutes(5)
        );

        // Assert
        callCount.Should().Be(2);
        result.Should().Be("call-2");
    }

    [Fact]
    public async Task ExecuteCachedAsync_WithoutCacheFor_OnlyDeduplicatesConcurrent()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(new CachedTestState(0, null, false, null));
        var executor = new AsyncActionExecutor<CachedTestState>(store);
        var callCount = 0;

        // First call (no cacheFor)
        await executor.ExecuteCachedAsync(
            "no-cache-key",
            async () =>
            {
                Interlocked.Increment(ref callCount);
                await Task.Delay(10);
                return "first";
            },
            loading: s => s with { IsLoading = true },
            success: (s, data) => s with { Data = data, IsLoading = false }
        );

        // Second call immediately after - should execute again (no caching without cacheFor)
        await executor.ExecuteCachedAsync(
            "no-cache-key",
            async () =>
            {
                Interlocked.Increment(ref callCount);
                await Task.Delay(10);
                return "second";
            },
            loading: s => s with { IsLoading = true },
            success: (s, data) => s with { Data = data, IsLoading = false }
        );

        // Assert
        callCount.Should().Be(2);
        store.GetState().Data.Should().Be("second");
    }

    [Fact]
    public async Task ExecuteCachedAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(new CachedTestState(0, null, false, null));
        var executor = new AsyncActionExecutor<CachedTestState>(store);
        var cts = new CancellationTokenSource();

        // Act
        var task = executor.ExecuteCachedAsync(
            "cancel-key",
            async () =>
            {
                await Task.Delay(5000); // Long delay
                return "data";
            },
            loading: s => s with { IsLoading = true },
            success: (s, data) => s with { Data = data, IsLoading = false },
            cancellationToken: cts.Token
        );

        // Cancel after a short delay
        await Task.Delay(50);
        cts.Cancel();

        // Assert
        await task.Invoking(t => t).Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ExecuteCachedAsync_CancellationBeforeStart_ThrowsImmediately()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(new CachedTestState(0, null, false, null));
        var executor = new AsyncActionExecutor<CachedTestState>(store);
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel before calling

        // Act
        var act = async () => await executor.ExecuteCachedAsync(
            "already-cancelled-key",
            () => Task.FromResult("data"),
            loading: s => s with { IsLoading = true },
            success: (s, data) => s with { Data = data, IsLoading = false },
            cancellationToken: cts.Token
        );

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void Dispose_ReleasesResources()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(new CachedTestState(0, null, false, null));
        var executor = new AsyncActionExecutor<CachedTestState>(store);

        // Act
        executor.Dispose();

        // Assert - calling after dispose should throw
        var act = async () => await executor.ExecuteCachedAsync(
            "disposed-key",
            () => Task.FromResult("data"),
            loading: s => s with { IsLoading = true },
            success: (s, data) => s with { Data = data, IsLoading = false }
        );

        act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(new CachedTestState(0, null, false, null));
        var executor = new AsyncActionExecutor<CachedTestState>(store);

        // Act & Assert - should not throw
        executor.Dispose();
        executor.Dispose();
        executor.Dispose();
    }

    #region Edge Cases

    [Fact]
    public async Task ExecuteCachedAsync_CancellationDuringWait_DoesNotCorruptExecutorState()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(new CachedTestState(0, null, false, null));
        var executor = new AsyncActionExecutor<CachedTestState>(store);
        var tcs = new TaskCompletionSource<string>();
        var cts = new CancellationTokenSource();

        // Start a long-running operation
        var task1 = executor.ExecuteCachedAsync(
            "lock-test-key",
            () => tcs.Task,
            loading: s => s with { IsLoading = true, UpdateCount = s.UpdateCount + 1 },
            success: (s, data) => s with { Data = data, IsLoading = false, UpdateCount = s.UpdateCount + 1 }
        );

        await Task.Delay(50);

        // Start a second call that will wait for the first, then cancel it
        var task2 = executor.ExecuteCachedAsync(
            "lock-test-key",
            () => tcs.Task,
            loading: s => s with { IsLoading = true, UpdateCount = s.UpdateCount + 1 },
            success: (s, data) => s with { Data = data, IsLoading = false, UpdateCount = s.UpdateCount + 1 },
            cancellationToken: cts.Token
        );

        await Task.Delay(50);
        cts.Cancel();

        // Task2 should be cancelled
        await task2.Invoking(t => t).Should().ThrowAsync<OperationCanceledException>();

        // Complete the original operation
        tcs.SetResult("completed");
        var result1 = await task1;

        // Assert - state should be consistent after cancellation
        result1.Should().Be("completed");
        store.GetState().UpdateCount.Should().Be(2); // Only loading + success from task1

        // Verify executor still works correctly with a new call
        var result3 = await executor.ExecuteCachedAsync(
            "new-key-after-cancel",
            async () =>
            {
                await Task.Delay(10);
                return "new data";
            },
            loading: s => s with { IsLoading = true, UpdateCount = s.UpdateCount + 1 },
            success: (s, data) => s with { Data = data, IsLoading = false, UpdateCount = s.UpdateCount + 1 }
        );

        result3.Should().Be("new data");
        store.GetState().UpdateCount.Should().Be(4); // Previous 2 + loading + success
    }

    [Fact]
    public async Task ExecuteCachedAsync_ConcurrentCalls_AsyncActionExecutesOnlyOnce()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(new CachedTestState(0, null, false, null));
        var executor = new AsyncActionExecutor<CachedTestState>(store);
        var executeCount = 0;
        var tcs = new TaskCompletionSource<string>();

        // Act - start 10 concurrent calls
        var tasks = Enumerable.Range(0, 10).Select(_ =>
            executor.ExecuteCachedAsync(
                "dedup-key",
                async () =>
                {
                    Interlocked.Increment(ref executeCount);
                    return await tcs.Task;
                },
                loading: s => s with { IsLoading = true, UpdateCount = s.UpdateCount + 1 },
                success: (s, data) => s with { Data = data, IsLoading = false, UpdateCount = s.UpdateCount + 1 }
            )
        ).ToList();

        await Task.Delay(100);

        // Complete the operation
        tcs.SetResult("deduped result");
        var results = await Task.WhenAll(tasks);

        // Assert - async action executed exactly once
        executeCount.Should().Be(1, "the async action should only execute once despite concurrent calls");
        results.Should().AllBeEquivalentTo("deduped result");
        store.GetState().UpdateCount.Should().Be(2); // One loading + one success
    }

    [Fact]
    public async Task ExecuteCachedAsync_ExpiredEntries_AreCleanedUpProperly()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(new CachedTestState(0, null, false, null));
        var executor = new AsyncActionExecutor<CachedTestState>(store);

        // Create several cached entries with short expiration
        for (int i = 0; i < 5; i++)
        {
            await executor.ExecuteCachedAsync(
                $"expiring-key-{i}",
                async () =>
                {
                    await Task.Delay(5);
                    return $"data-{i}";
                },
                loading: s => s with { IsLoading = true },
                success: (s, data) => s with { Data = data, IsLoading = false },
                cacheFor: TimeSpan.FromMilliseconds(50)
            );
        }

        // Wait for cache entries to expire
        await Task.Delay(100);

        // Trigger cleanup by accessing with new keys (implementation should clean up expired entries)
        var result = await executor.ExecuteCachedAsync(
            "new-key-trigger-cleanup",
            async () =>
            {
                await Task.Delay(5);
                return "new data";
            },
            loading: s => s with { IsLoading = true },
            success: (s, data) => s with { Data = data, IsLoading = false }
        );

        // Assert - should complete without issues
        result.Should().Be("new data");

        // Verify old keys execute again (not cached anymore)
        var callCount = 0;
        await executor.ExecuteCachedAsync(
            "expiring-key-0",
            async () =>
            {
                Interlocked.Increment(ref callCount);
                return "re-executed";
            },
            loading: s => s with { IsLoading = true },
            success: (s, data) => s with { Data = data, IsLoading = false }
        );

        callCount.Should().Be(1, "expired entry should have been cleaned up and action re-executed");
    }

    [Fact]
    public async Task ExecuteCachedAsync_DisposalDuringActiveOperation_DoesNotDeadlock()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(new CachedTestState(0, null, false, null));
        var executor = new AsyncActionExecutor<CachedTestState>(store);
        var tcs = new TaskCompletionSource<string>();

        // Start a long-running operation
        var task = executor.ExecuteCachedAsync(
            "disposal-test-key",
            () => tcs.Task,
            loading: s => s with { IsLoading = true },
            success: (s, data) => s with { Data = data, IsLoading = false }
        );

        await Task.Delay(50);

        // Dispose while operation is in-flight
        var disposeTask = Task.Run(() => executor.Dispose());

        // Complete the operation
        tcs.SetResult("data");

        // Wait for disposal to complete - should not deadlock
        await Task.WhenAny(disposeTask, Task.Delay(2000)).ConfigureAwait(false);

        // Assert - disposal should have completed
        disposeTask.IsCompleted.Should().BeTrue("disposal should complete without deadlock");
    }

    [Fact]
    public async Task ExecuteCachedAsync_ErrorsAreNotCached_AllowsRetry()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(new CachedTestState(0, null, false, null));
        var executor = new AsyncActionExecutor<CachedTestState>(store);
        var callCount = 0;

        // First call fails
        var act1 = async () => await executor.ExecuteCachedAsync<string>(
            "error-retry-key",
            async () =>
            {
                Interlocked.Increment(ref callCount);
                await Task.Delay(10);
                throw new InvalidOperationException("First attempt failed");
            },
            loading: s => s with { IsLoading = true, UpdateCount = s.UpdateCount + 1 },
            success: (s, data) => s with { Data = data, IsLoading = false, UpdateCount = s.UpdateCount + 1 },
            error: (s, ex) => s with { Error = ex.Message, IsLoading = false, UpdateCount = s.UpdateCount + 1 },
            cacheFor: TimeSpan.FromMinutes(5)
        );

        await act1.Should().ThrowAsync<InvalidOperationException>();
        callCount.Should().Be(1);

        // Second call should retry (errors are not cached)
        var result = await executor.ExecuteCachedAsync(
            "error-retry-key",
            async () =>
            {
                Interlocked.Increment(ref callCount);
                await Task.Delay(10);
                return "success on retry";
            },
            loading: s => s with { IsLoading = true, UpdateCount = s.UpdateCount + 1 },
            success: (s, data) => s with { Data = data, IsLoading = false, UpdateCount = s.UpdateCount + 1 },
            error: (s, ex) => s with { Error = ex.Message, IsLoading = false, UpdateCount = s.UpdateCount + 1 },
            cacheFor: TimeSpan.FromMinutes(5)
        );

        // Assert - action executed twice (error not cached)
        callCount.Should().Be(2);
        result.Should().Be("success on retry");
    }

    #endregion

    #region Cache Invalidation Tests

    [Fact]
    public async Task InvalidateCache_RemovesSpecificEntry()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(new CachedTestState(0, null, false, null));
        var executor = new AsyncActionExecutor<CachedTestState>(store);
        var callCount = 0;

        // Create cached entry
        await executor.ExecuteCachedAsync(
            "invalidate-test-key",
            async () =>
            {
                Interlocked.Increment(ref callCount);
                return "original data";
            },
            loading: s => s with { IsLoading = true },
            success: (s, data) => s with { Data = data, IsLoading = false },
            cacheFor: TimeSpan.FromMinutes(5)
        );

        callCount.Should().Be(1);

        // Act - invalidate the cache
        executor.InvalidateCache("invalidate-test-key");

        // Next call should execute again
        var result = await executor.ExecuteCachedAsync(
            "invalidate-test-key",
            async () =>
            {
                Interlocked.Increment(ref callCount);
                return "new data after invalidation";
            },
            loading: s => s with { IsLoading = true },
            success: (s, data) => s with { Data = data, IsLoading = false },
            cacheFor: TimeSpan.FromMinutes(5)
        );

        // Assert
        callCount.Should().Be(2);
        result.Should().Be("new data after invalidation");
    }

    [Fact]
    public async Task InvalidateCacheByPrefix_RemovesMatchingEntries()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(new CachedTestState(0, null, false, null));
        var executor = new AsyncActionExecutor<CachedTestState>(store);
        var productCallCount = 0;
        var userCallCount = 0;

        // Create multiple cached entries
        await executor.ExecuteCachedAsync(
            "product-123",
            async () => { Interlocked.Increment(ref productCallCount); return "product data"; },
            loading: s => s,
            success: (s, _) => s,
            cacheFor: TimeSpan.FromMinutes(5)
        );

        await executor.ExecuteCachedAsync(
            "product-456",
            async () => { Interlocked.Increment(ref productCallCount); return "product data"; },
            loading: s => s,
            success: (s, _) => s,
            cacheFor: TimeSpan.FromMinutes(5)
        );

        await executor.ExecuteCachedAsync(
            "user-789",
            async () => { Interlocked.Increment(ref userCallCount); return "user data"; },
            loading: s => s,
            success: (s, _) => s,
            cacheFor: TimeSpan.FromMinutes(5)
        );

        productCallCount.Should().Be(2);
        userCallCount.Should().Be(1);

        // Act - invalidate all product entries
        executor.InvalidateCacheByPrefix("product-");

        // Trigger re-execution
        await executor.ExecuteCachedAsync<string>("product-123", async () => { Interlocked.Increment(ref productCallCount); return "new"; }, s => s, s => s, cacheFor: TimeSpan.FromMinutes(5));
        await executor.ExecuteCachedAsync<string>("product-456", async () => { Interlocked.Increment(ref productCallCount); return "new"; }, s => s, s => s, cacheFor: TimeSpan.FromMinutes(5));
        await executor.ExecuteCachedAsync<string>("user-789", async () => { Interlocked.Increment(ref userCallCount); return "new"; }, s => s, s => s, cacheFor: TimeSpan.FromMinutes(5));

        // Assert - product entries re-executed, user entry still cached
        productCallCount.Should().Be(4, "product entries should have been invalidated and re-executed");
        userCallCount.Should().Be(1, "user entry should still be cached");
    }

    [Fact]
    public async Task ClearCache_RemovesAllEntries()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(new CachedTestState(0, null, false, null));
        var executor = new AsyncActionExecutor<CachedTestState>(store);
        var callCount1 = 0;
        var callCount2 = 0;

        // Create multiple cached entries
        await executor.ExecuteCachedAsync(
            "key-1",
            async () => { Interlocked.Increment(ref callCount1); return "data-1"; },
            loading: s => s,
            success: (s, _) => s,
            cacheFor: TimeSpan.FromMinutes(5)
        );

        await executor.ExecuteCachedAsync(
            "key-2",
            async () => { Interlocked.Increment(ref callCount2); return "data-2"; },
            loading: s => s,
            success: (s, _) => s,
            cacheFor: TimeSpan.FromMinutes(5)
        );

        // Act - clear all cache
        executor.ClearCache();

        // Trigger re-execution
        await executor.ExecuteCachedAsync<string>("key-1", async () => { Interlocked.Increment(ref callCount1); return "new"; }, s => s, s => s, error: null, cacheFor: TimeSpan.FromMinutes(5));
        await executor.ExecuteCachedAsync<string>("key-2", async () => { Interlocked.Increment(ref callCount2); return "new"; }, s => s, s => s, error: null, cacheFor: TimeSpan.FromMinutes(5));

        // Assert - all entries re-executed
        callCount1.Should().Be(2);
        callCount2.Should().Be(2);
    }

    [Fact]
    public async Task InvalidateCache_NonexistentKey_DoesNotThrow()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(new CachedTestState(0, null, false, null));
        var executor = new AsyncActionExecutor<CachedTestState>(store);

        // Act & Assert - should not throw
        var act = () => executor.InvalidateCache("nonexistent-key");
        act.Should().NotThrow();
    }

    #endregion
}
