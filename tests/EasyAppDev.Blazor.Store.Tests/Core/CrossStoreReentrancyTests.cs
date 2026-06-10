using EasyAppDev.Blazor.Store.Core;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.Core;

/// <summary>
/// Tests for cross-store update scenarios and reentrancy detection.
/// Validates the fix for deadlock prevention when updating multiple stores.
/// </summary>
[Collection("Sequential")]
public class CrossStoreReentrancyTests
{
    private record StoreAState(int Value, string? LastAction = null);
    private record StoreBState(int Value, string? LastAction = null);

    [Fact]
    public async Task CrossStoreUpdate_NoDeadlock_UpdatesSucceed()
    {
        // Arrange
        var storeA = StoreBuilder<StoreAState>.Create(new StoreAState(0)).Build();
        var storeB = StoreBuilder<StoreBState>.Create(new StoreBState(0)).Build();

        var aUpdateCompleted = false;
        var bUpdateTcs = new TaskCompletionSource<bool>();

        // Subscribe StoreA to update StoreB (using fire-and-forget async)
        storeA.Subscribe(_ =>
        {
            // Fire-and-forget async update to avoid blocking
            Task.Run(async () =>
            {
                await storeB.UpdateAsync(s => s with { Value = s.Value + 1 }, "FROM_STORE_A");
                bUpdateTcs.TrySetResult(true);
            }).ContinueWith(t =>
            {
                if (t.IsFaulted) throw t.Exception!;
            }, TaskScheduler.Default);
        });

        // Act - Update StoreA, which should trigger StoreB update
        await storeA.UpdateAsync(s => s with { Value = 10 }, "INITIAL");
        aUpdateCompleted = true;

        // Wait for StoreB update to complete
        await Task.WhenAny(bUpdateTcs.Task, Task.Delay(2000));

        // Assert - Both updates should complete without deadlock
        aUpdateCompleted.Should().BeTrue();
        bUpdateTcs.Task.IsCompleted.Should().BeTrue("because StoreB update should complete");
        storeA.GetState().Value.Should().Be(10);
        storeB.GetState().Value.Should().Be(1);
    }

    [Fact]
    public async Task CircularCrossStoreUpdate_NoDeadlock_EventuallyCompletes()
    {
        // Arrange
        var storeA = StoreBuilder<StoreAState>.Create(new StoreAState(0)).Build();
        var storeB = StoreBuilder<StoreBState>.Create(new StoreBState(0)).Build();

        var maxUpdates = 3;
        var aUpdates = 0;
        var bUpdates = 0;
        var completionTcs = new TaskCompletionSource<bool>();

        // StoreA updates StoreB (with limit to prevent infinite loop)
        storeA.Subscribe(state =>
        {
            if (aUpdates < maxUpdates && state.Value < maxUpdates)
            {
                aUpdates++;
                _ = Task.Run(async () => await storeB.UpdateAsync(s => s with { Value = s.Value + 1 }));
            }
            else if (aUpdates >= maxUpdates && bUpdates >= maxUpdates)
            {
                completionTcs.TrySetResult(true);
            }
        });

        // StoreB updates StoreA (with limit)
        storeB.Subscribe(state =>
        {
            if (bUpdates < maxUpdates && state.Value < maxUpdates)
            {
                bUpdates++;
                _ = Task.Run(async () => await storeA.UpdateAsync(s => s with { Value = s.Value + 1 }));
            }
            else if (aUpdates >= maxUpdates && bUpdates >= maxUpdates)
            {
                completionTcs.TrySetResult(true);
            }
        });

        // Act - Trigger the circular updates
        await storeA.UpdateAsync(s => s with { Value = 1 });

        // Wait for updates to complete (with timeout)
        await Task.WhenAny(completionTcs.Task, Task.Delay(2000));

        // Assert - Should complete without deadlock
        storeA.GetState().Value.Should().BeGreaterOrEqualTo(1, "because at least initial update occurred");
        storeB.GetState().Value.Should().BeGreaterOrEqualTo(1, "because at least one triggered update occurred");
    }

    [Fact]
    public async Task SubscriberInitiatedUpdate_IsNotTreatedAsNestedUpdate()
    {
        // A subscriber-initiated update is SAFE (the store lock is already released when
        // subscribers are notified) and must NOT be reported/rejected as a nested update.
        var store = new Store<StoreAState>(
            new StoreAState(0),
            new SubscriptionManager<StoreAState>(null!));

        var updateCount = 0;
        var tcs = new TaskCompletionSource<Exception?>();

        store.Subscribe(_ =>
        {
            if (updateCount == 0)
            {
                updateCount++;
                // Subscriber-initiated follow-up update (flows the same execution context)
                Task.Run(async () =>
                {
                    try
                    {
                        await store.UpdateAsync(s => s with { Value = s.Value + 100 });
                        tcs.TrySetResult(null);
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetResult(ex);
                    }
                });
            }
        });

        // Act
        await store.UpdateAsync(s => s with { Value = 1 });

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(2000)) == tcs.Task;

        // Assert - the follow-up update must complete without being rejected as nested
        completed.Should().BeTrue("because the subscriber-initiated update should not deadlock");
        tcs.Task.Result.Should().BeNull("because subscriber-initiated updates are safe and must not throw");
        store.GetState().Value.Should().Be(101);
    }

    [Fact]
    public async Task NestedUpdate_FromMiddleware_ThrowsInsteadOfDeadlocking()
    {
        // A nested UpdateAsync from middleware would await the non-reentrant store lock
        // that the outer update still holds - a guaranteed deadlock. The store must throw
        // a clear InvalidOperationException instead of hanging.
        Store<StoreAState>? store = null;
        var middleware = new NestedUpdateMiddleware(() => store!);

        store = new Store<StoreAState>(
            new StoreAState(0),
            new SubscriptionManager<StoreAState>(null!),
            middlewares: new[] { middleware },
            middlewareOptions: new EasyAppDev.Blazor.Store.Middleware.MiddlewarePipelineOptions
            {
                StopOnError = true,
                MaxRetries = 0
            });

        // Act
        var updateTask = store.UpdateAsync(s => s with { Value = 1 });
        var completed = await Task.WhenAny(updateTask, Task.Delay(5000)) == updateTask;

        // Assert - must fail fast with a clear message, not deadlock
        completed.Should().BeTrue("because nested updates must fail fast instead of deadlocking");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => updateTask);
        ex.Message.Should().Contain("Nested UpdateAsync");
    }

    private class NestedUpdateMiddleware : EasyAppDev.Blazor.Store.Middleware.IMiddleware<StoreAState>
    {
        private readonly Func<Store<StoreAState>> _storeAccessor;

        public NestedUpdateMiddleware(Func<Store<StoreAState>> storeAccessor)
        {
            _storeAccessor = storeAccessor;
        }

        public async Task OnBeforeUpdateAsync(StoreAState currentState, string? action)
        {
            // Nested update from middleware - must be detected and rejected
            await _storeAccessor().UpdateAsync(s => s with { Value = s.Value + 1 });
        }

        public Task OnAfterUpdateAsync(StoreAState previousState, StoreAState currentState, string? action)
            => Task.CompletedTask;
    }

    [Fact]
    public async Task MultipleStores_ConcurrentUpdates_NoDeadlock()
    {
        // Arrange
        var stores = Enumerable.Range(0, 5)
            .Select(i => StoreBuilder<StoreAState>.Create(new StoreAState(i)).Build())
            .ToList();

        // Act - Update all stores concurrently
        var tasks = stores.Select((store, index) =>
            store.UpdateAsync(s => s with { Value = s.Value * 2 }));

        // Should complete without deadlock
        await Task.WhenAll(tasks);

        // Assert
        stores[0].GetState().Value.Should().Be(0);
        stores[1].GetState().Value.Should().Be(2);
        stores[2].GetState().Value.Should().Be(4);
        stores[3].GetState().Value.Should().Be(6);
        stores[4].GetState().Value.Should().Be(8);
    }

    // Test logger provider for capturing log messages
    private class TestLoggerProvider : ILoggerProvider
    {
        private readonly List<string> _logMessages;

        public TestLoggerProvider(List<string> logMessages)
        {
            _logMessages = logMessages;
        }

        public ILogger CreateLogger(string categoryName) => new TestLogger(_logMessages);

        public void Dispose() { }

        private class TestLogger : ILogger
        {
            private readonly List<string> _logMessages;

            public TestLogger(List<string> logMessages)
            {
                _logMessages = logMessages;
            }

            public IDisposable BeginScope<TState>(TState state) where TState : notnull => null!;

            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (IsEnabled(logLevel))
                {
                    _logMessages.Add(formatter(state, exception));
                }
            }
        }
    }
}
