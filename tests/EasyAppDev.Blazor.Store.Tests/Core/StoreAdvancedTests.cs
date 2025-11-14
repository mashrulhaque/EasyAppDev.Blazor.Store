using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Tests.TestUtilities;
using FluentAssertions;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.Core;

/// <summary>
/// Advanced test scenarios for edge cases and concurrency.
/// These tests validate thread-safety and error handling.
/// </summary>
public class StoreAdvancedTests : IDisposable
{
    private readonly IStore<TestState> _store;

    public StoreAdvancedTests()
    {
        _store = StoreTestHelpers.CreateStore(new TestState(0, "Initial"));
    }

    [Fact]
    public async Task Update_ConcurrentUpdates_AreSerializedCorrectly()
    {
        // Arrange
        const int updateCount = 100;
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < updateCount; i++)
        {
            tasks.Add(Task.Run(() => _store.Update(state => state with { Counter = state.Counter + 1 })));
        }

        await Task.WhenAll(tasks);

        // Assert
        _store.GetState().Counter.Should().Be(updateCount);
    }

    [Fact]
    public async Task UpdateAsync_ConcurrentAsyncUpdates_AreSerializedCorrectly()
    {
        // Arrange
        const int updateCount = 50;
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < updateCount; i++)
        {
            tasks.Add(_store.UpdateAsync(async state =>
            {
                await Task.Delay(1); // Simulate async work
                return state with { Counter = state.Counter + 1 };
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        _store.GetState().Counter.Should().Be(updateCount);
    }

    [Fact]
    public void Update_WhenSubscriberThrows_OtherSubscribersStillNotified()
    {
        // Arrange
        var firstCallbackExecuted = false;
        var secondCallbackExecuted = false;
        var thirdCallbackExecuted = false;

        _store.Subscribe(_ => { firstCallbackExecuted = true; });
        _store.Subscribe(_ => throw new InvalidOperationException("Test exception"));
        _store.Subscribe(_ => { secondCallbackExecuted = true; });
        _store.Subscribe(_ => { thirdCallbackExecuted = true; });

        // Act
        _store.Update(state => state with { Counter = 1 });

        // Assert
        firstCallbackExecuted.Should().BeTrue("first subscriber should be notified");
        secondCallbackExecuted.Should().BeTrue("third subscriber should be notified despite second throwing");
        thirdCallbackExecuted.Should().BeTrue("fourth subscriber should be notified despite second throwing");
    }

    [Fact]
    public void Subscribe_MultipleSubscribeUnsubscribe_ManagesMemoryCorrectly()
    {
        // Arrange & Act
        for (int i = 0; i < 1000; i++)
        {
            var subscription = _store.Subscribe(_ => { });
            subscription.Dispose();
        }

        // Trigger update to ensure no dangling subscriptions
        var callbackCount = 0;
        using var testSubscription = _store.Subscribe(_ => callbackCount++);
        _store.Update(state => state with { Counter = 1 });

        // Assert
        callbackCount.Should().Be(1, "only active subscription should be notified");
    }

    [Fact]
    public void Update_WithSameState_DoesNotNotifySubscribers()
    {
        // Arrange
        var callbackCount = 0;
        _store.Subscribe(_ => callbackCount++);

        // Act - update with same state
        _store.Update(state => state);

        // Assert
        callbackCount.Should().Be(0, "subscribers should not be notified when state doesn't change");
    }

    [Fact]
    public void Subscribe_WithCustomComparer_UsesComparerForEquality()
    {
        // Arrange
        var comparer = new TestStateComparer();
        var store = StoreTestHelpers.CreateStore(new TestState(0, "Initial"), comparer);

        var callbackCount = 0;
        store.Subscribe(_ => callbackCount++);

        // Act - update counter (comparer considers states equal if counter and message are same)
        store.Update(state => state with { Counter = 0, Message = "Initial" });

        // Assert
        callbackCount.Should().Be(0, "comparer should prevent notification for equal states");
    }

    [Fact]
    public async Task GetState_DuringConcurrentUpdates_ReturnsConsistentState()
    {
        // Arrange
        var updateTask = Task.Run(async () =>
        {
            for (int i = 0; i < 100; i++)
            {
                await _store.UpdateAsync(state => state with { Counter = i });
                await Task.Delay(1);
            }
        });

        var readTask = Task.Run(() =>
        {
            for (int i = 0; i < 200; i++)
            {
                var state = _store.GetState();
                // Assert - state should always be valid (non-negative counter)
                state.Counter.Should().BeGreaterOrEqualTo(0);
            }
        });

        // Act
        await Task.WhenAll(updateTask, readTask);

        // Assert
        // If we get here without exceptions, GetState() was safe during concurrent updates
        _store.GetState().Counter.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public void Subscribe_FromWithinCallback_DoesNotDeadlock()
    {
        // Arrange
        IDisposable? innerSubscription = null;
        var innerCallbackCount = 0;

        _store.Subscribe(state =>
        {
            // Subscribe from within callback (potential deadlock scenario)
            innerSubscription = _store.Subscribe(_ => innerCallbackCount++);
        });

        // Act
        _store.Update(state => state with { Counter = 1 });
        _store.Update(state => state with { Counter = 2 });

        // Assert
        innerCallbackCount.Should().Be(1, "inner subscription should be notified on second update");

        // Cleanup
        innerSubscription?.Dispose();
    }

    [Fact]
    public void Dispose_WhileSubscriberExecuting_HandlesGracefully()
    {
        // Arrange
        var callbackStarted = new TaskCompletionSource<bool>();
        var canComplete = new TaskCompletionSource<bool>();

        var subscription = _store.Subscribe(_ =>
        {
            callbackStarted.SetResult(true);
            canComplete.Task.Wait(TimeSpan.FromSeconds(5));
        });

        // Act
        var updateTask = Task.Run(() => _store.Update(state => state with { Counter = 1 }));
        callbackStarted.Task.Wait(TimeSpan.FromSeconds(5));

        // Dispose while callback is executing
        _store.Dispose();
        canComplete.SetResult(true);

        // Assert
        Action act = () => _store.GetState();
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Subscribe_WithSelector_HandlesNullSelectedValues()
    {
        // Arrange
        var store = StoreTestHelpers.CreateStore(new TestState(0, null!));
        var callbackCount = 0;

        // Act & Assert - should not throw when selector returns null
        using var subscription = store.Subscribe(
            state => state.Message,
            message => callbackCount++);

        store.Update(state => state with { Message = "Changed" });
        callbackCount.Should().Be(1);
    }

    public void Dispose()
    {
        _store.Dispose();
    }

    private class TestStateComparer : IEqualityComparer<TestState>
    {
        public bool Equals(TestState? x, TestState? y)
        {
            if (x is null || y is null) return x == y;
            return x.Counter == y.Counter && x.Message == y.Message;
        }

        public int GetHashCode(TestState obj) =>
            HashCode.Combine(obj.Counter, obj.Message);
    }
}
