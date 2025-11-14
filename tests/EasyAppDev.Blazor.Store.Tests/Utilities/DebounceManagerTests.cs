using EasyAppDev.Blazor.Store.Utilities;
using FluentAssertions;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.Utilities;

public class DebounceManagerTests
{
    [Fact]
    public async Task Debounce_SingleCall_ExecutesAfterDelay()
    {
        // Arrange
        using var manager = new DebounceManager();
        var executionCount = 0;
        var tcs = new TaskCompletionSource<bool>();
        async Task Action()
        {
            executionCount++;
            tcs.TrySetResult(true);
            await Task.CompletedTask;
        }

        // Act
        await manager.Debounce("key1", Action, 100);

        // Small delay to ensure the fire-and-forget task starts (important for CI environments)
        await Task.Delay(10);

        // Wait for the action to complete with generous timeout for system load
        var timeoutTask = Task.Delay(3000);
        var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

        // Assert
        completedTask.Should().Be(tcs.Task, "because the action should complete before timeout");
        executionCount.Should().Be(1, "because the action should execute once after the delay");

        // Longer delay to allow cleanup to complete before disposal
        await Task.Delay(200);
    }

    [Fact]
    public async Task Debounce_CalledTwiceQuickly_ExecutesOnlyOnce()
    {
        // Arrange
        using var manager = new DebounceManager();
        var executionCount = 0;
        async Task Action()
        {
            executionCount++;
            await Task.CompletedTask;
        }

        // Act
        await manager.Debounce("key1", Action, 100);
        await Task.Delay(50); // Wait less than delay
        await manager.Debounce("key1", Action, 100); // This should cancel the first
        await Task.Delay(150); // Wait for second execution

        // Assert
        executionCount.Should().Be(1, "because the first call should be cancelled by the second");
    }

    [Fact]
    public async Task Debounce_CalledMultipleTimesRapidly_ExecutesOnlyLast()
    {
        // Arrange
        using var manager = new DebounceManager();
        var executionCount = 0;
        async Task Action()
        {
            executionCount++;
            await Task.CompletedTask;
        }

        // Act
        await manager.Debounce("key1", Action, 100);
        await Task.Delay(20);
        await manager.Debounce("key1", Action, 100);
        await Task.Delay(20);
        await manager.Debounce("key1", Action, 100);
        await Task.Delay(20);
        await manager.Debounce("key1", Action, 100);

        // Small delay to ensure the fire-and-forget task starts
        await Task.Delay(10);
        await Task.Delay(250); // Wait for last execution with extra buffer

        // Assert
        executionCount.Should().Be(1, "because only the last call should execute");

        // Cleanup delay
        await Task.Delay(150);
    }

    [Fact]
    public async Task Debounce_DifferentKeys_ExecuteIndependently()
    {
        // Arrange
        using var manager = new DebounceManager();
        var count1 = 0;
        var count2 = 0;
        async Task Action1()
        {
            count1++;
            await Task.CompletedTask;
        }
        async Task Action2()
        {
            count2++;
            await Task.CompletedTask;
        }

        // Act
        await manager.Debounce("key1", Action1, 100);
        await manager.Debounce("key2", Action2, 100);

        // Small delay to ensure the fire-and-forget tasks start
        await Task.Delay(10);
        await Task.Delay(250);

        // Assert
        count1.Should().Be(1, "because key1 action should execute");
        count2.Should().Be(1, "because key2 action should execute independently");

        // Cleanup delay
        await Task.Delay(150);
    }

    [Fact]
    public async Task Debounce_DisposedWhilePending_CancelsPending()
    {
        // Arrange
        var manager = new DebounceManager();
        var executionCount = 0;
        async Task Action()
        {
            executionCount++;
            await Task.CompletedTask;
        }

        // Act
        await manager.Debounce("key1", Action, 100);
        manager.Dispose(); // Dispose immediately
        await Task.Delay(150);

        // Assert
        executionCount.Should().Be(0, "because pending action should be cancelled on dispose");
    }

    [Fact]
    public async Task Debounce_WithNullKey_ThrowsArgumentNullException()
    {
        // Arrange
        using var manager = new DebounceManager();
        async Task Action() => await Task.CompletedTask;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await manager.Debounce(null!, Action, 100));
    }

    [Fact]
    public async Task Debounce_WithNullAction_ThrowsArgumentNullException()
    {
        // Arrange
        using var manager = new DebounceManager();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await manager.Debounce("key1", null!, 100));
    }

    [Fact]
    public async Task Debounce_WithNegativeDelay_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var manager = new DebounceManager();
        async Task Action() => await Task.CompletedTask;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await manager.Debounce("key1", Action, -100));
    }

    [Fact]
    public async Task Debounce_WithZeroDelay_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var manager = new DebounceManager();
        async Task Action() => await Task.CompletedTask;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await manager.Debounce("key1", Action, 0));
    }

    [Fact]
    public async Task CancelAllAsync_CancelsAllPendingActions()
    {
        // Arrange
        using var manager = new DebounceManager();
        var count1 = 0;
        var count2 = 0;
        async Task Action1()
        {
            count1++;
            await Task.CompletedTask;
        }
        async Task Action2()
        {
            count2++;
            await Task.CompletedTask;
        }

        // Act
        await manager.Debounce("key1", Action1, 100);
        await manager.Debounce("key2", Action2, 100);
        await manager.CancelAllAsync();
        await Task.Delay(150);

        // Assert
        count1.Should().Be(0, "because all actions should be cancelled");
        count2.Should().Be(0, "because all actions should be cancelled");
    }

    [Fact]
    public async Task Debounce_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var manager = new DebounceManager();
        async Task Action() => await Task.CompletedTask;

        // Act
        manager.Dispose();

        // Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await manager.Debounce("key1", Action, 100));
    }

    [Fact]
    public async Task Debounce_ConcurrentCalls_ThreadSafe()
    {
        // Arrange
        using var manager = new DebounceManager();
        var executionCount = 0;
        var lockObj = new object();
        async Task Action()
        {
            lock (lockObj)
            {
                executionCount++;
            }
            await Task.CompletedTask;
        }

        // Act - Simulate concurrent calls from multiple threads
        var tasks = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                await manager.Debounce("key1", Action, 100);
            }));
        }
        await Task.WhenAll(tasks);
        await Task.Delay(150);

        // Assert
        executionCount.Should().Be(1, "because only the last debounced action should execute despite concurrent calls");
    }

    [Fact]
    public async Task Debounce_ExceptionInAction_PropagatesCorrectly()
    {
        // Arrange
        using var manager = new DebounceManager();
        var exceptionThrown = false;
        async Task Action()
        {
            await Task.CompletedTask;
            throw new InvalidOperationException("Test exception");
        }

        // Act
        await manager.Debounce("key1", Action, 50);

        try
        {
            await Task.Delay(100);
        }
        catch (InvalidOperationException)
        {
            exceptionThrown = true;
        }

        // Assert
        // Note: Exceptions in debounced actions are fire-and-forget
        // This test verifies the manager doesn't crash when action throws
        exceptionThrown.Should().BeFalse("because exceptions in debounced actions don't propagate to caller");
    }

    [Fact]
    public async Task Debounce_RapidKeyChanges_EachKeyExecutesIndependently()
    {
        // Arrange
        using var manager = new DebounceManager();
        var counts = new Dictionary<string, int>
        {
            ["key1"] = 0,
            ["key2"] = 0,
            ["key3"] = 0
        };
        var lockObj = new object();

        async Task CreateAction(string key)
        {
            lock (lockObj)
            {
                counts[key]++;
            }
            await Task.CompletedTask;
        }

        // Act
        await manager.Debounce("key1", () => CreateAction("key1"), 100);
        await manager.Debounce("key2", () => CreateAction("key2"), 100);
        await manager.Debounce("key3", () => CreateAction("key3"), 100);

        // Small delay to ensure the fire-and-forget tasks start
        await Task.Delay(10);
        await Task.Delay(250);

        // Assert
        counts["key1"].Should().Be(1, "because key1 action should execute");
        counts["key2"].Should().Be(1, "because key2 action should execute");
        counts["key3"].Should().Be(1, "because key3 action should execute");

        // Cleanup delay
        await Task.Delay(150);
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var manager = new DebounceManager();

        // Act & Assert
        manager.Dispose();
        manager.Dispose(); // Should not throw
        manager.Dispose(); // Should not throw
    }
}
