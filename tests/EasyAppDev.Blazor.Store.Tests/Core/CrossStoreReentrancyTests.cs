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
    public async Task ReentrancyDetection_LogsWarning_WhenDetected()
    {
        // Arrange
        var logMessages = new List<string>();
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(new TestLoggerProvider(logMessages));
            builder.SetMinimumLevel(LogLevel.Warning);
        });
        var logger = loggerFactory.CreateLogger<Store<StoreAState>>();

        var store = new Store<StoreAState>(
            new StoreAState(0),
            new SubscriptionManager<StoreAState>(null!),
            logger: logger);

        var updateCount = 0;
        var tcs = new TaskCompletionSource<bool>();

        // Subscribe to trigger reentrancy (only once to avoid infinite loop)
        store.Subscribe(_ =>
        {
            if (updateCount == 0)
            {
                updateCount++;
                // Fire-and-forget to trigger reentrancy
                Task.Run(async () =>
                {
                    await store.UpdateAsync(s => s with { Value = s.Value + 100 });
                    tcs.TrySetResult(true);
                }).ContinueWith(t =>
                {
                    if (t.IsFaulted) throw t.Exception!;
                }, TaskScheduler.Default);
            }
        });

        // Act
        await store.UpdateAsync(s => s with { Value = 1 });

        // Wait for nested update
        await Task.WhenAny(tcs.Task, Task.Delay(1000));

        // Assert
        logMessages.Should().Contain(msg => msg.Contains("Reentrancy detected"),
            "because nested update should trigger reentrancy warning");
    }

    [Fact]
    public async Task DeeplyNestedUpdates_DetectsMultipleLevels()
    {
        // Arrange
        var logMessages = new List<string>();
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(new TestLoggerProvider(logMessages));
            builder.SetMinimumLevel(LogLevel.Warning);
        });
        var logger = loggerFactory.CreateLogger<Store<StoreAState>>();

        var store = new Store<StoreAState>(
            new StoreAState(0),
            new SubscriptionManager<StoreAState>(null!),
            logger: logger);

        var updateCount = 0;
        var tcs = new TaskCompletionSource<bool>();

        store.Subscribe(state =>
        {
            if (state.Value < 2 && updateCount < 2)
            {
                updateCount++;
                // Fire-and-forget to avoid blocking
                _ = Task.Run(async () => await store.UpdateAsync(s => s with { Value = s.Value + 1 }));
            }
            else if (updateCount >= 2)
            {
                tcs.TrySetResult(true);
            }
        });

        // Act
        await store.UpdateAsync(s => s with { Value = 1 });
        await Task.WhenAny(tcs.Task, Task.Delay(1000));

        // Assert
        updateCount.Should().BeGreaterThan(0);
        logMessages.Should().Contain(msg => msg.Contains("Reentrancy detected"));
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
