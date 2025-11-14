using EasyAppDev.Blazor.Store.Utilities;
using FluentAssertions;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.Utilities;

public class ThrottleManagerTests
{
    [Fact]
    public async Task Throttle_FirstCall_ExecutesImmediatelyWithLeading()
    {
        // Arrange
        using var manager = new ThrottleManager();
        var executionCount = 0;
        var firstExecutionTime = DateTime.MinValue;
        async Task Action()
        {
            if (executionCount == 0)
                firstExecutionTime = DateTime.UtcNow;
            executionCount++;
            await Task.CompletedTask;
        }

        var startTime = DateTime.UtcNow;

        // Act
        await manager.Throttle("key1", Action, 200, leading: true);
        await Task.Delay(50); // Give it time to execute

        // Assert
        executionCount.Should().Be(1, "because first call should execute immediately with leading edge");
        (firstExecutionTime - startTime).TotalMilliseconds.Should().BeLessThan(100,
            "because execution should happen immediately");
    }

    [Fact]
    public async Task Throttle_RapidCalls_LimitedByInterval()
    {
        // Arrange
        using var manager = new ThrottleManager();
        var executionCount = 0;
        async Task Action()
        {
            executionCount++;
            await Task.CompletedTask;
        }

        // Act - Call 10 times rapidly with 100ms interval
        for (int i = 0; i < 10; i++)
        {
            await manager.Throttle("key1", Action, 100, leading: true);
            await Task.Delay(20); // 20ms between calls
        }
        await Task.Delay(150); // Wait for any trailing execution

        // Assert
        // With 200ms total (10 * 20ms) and 100ms interval, we should get 2-3 executions
        executionCount.Should().BeInRange(2, 3, "because throttle should limit execution frequency");
    }

    [Fact]
    public async Task Throttle_DifferentKeys_ExecuteIndependently()
    {
        // Arrange
        using var manager = new ThrottleManager();
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
        await manager.Throttle("key1", Action1, 100, leading: true);
        await manager.Throttle("key2", Action2, 100, leading: true);
        await Task.Delay(50);

        // Assert
        count1.Should().Be(1, "because key1 should execute");
        count2.Should().Be(1, "because key2 should execute independently");
    }

    [Fact]
    public async Task Throttle_WithLeadingFalse_WaitsBeforeExecution()
    {
        // Arrange
        using var manager = new ThrottleManager();
        var executionCount = 0;
        async Task Action()
        {
            executionCount++;
            await Task.CompletedTask;
        }

        // Act
        await manager.Throttle("key1", Action, 100, leading: false);
        await Task.Delay(50); // Wait less than interval

        // Assert
        executionCount.Should().Be(0, "because with leading=false, first call should wait");

        // Wait for execution
        await Task.Delay(100);
        executionCount.Should().Be(1, "because action should execute after interval");
    }

    [Fact]
    public async Task Throttle_TrailingEdge_ExecutesLastCall()
    {
        // Arrange
        using var manager = new ThrottleManager();
        var executionCount = 0;
        var lastValue = 0;
        async Task CreateAction(int value)
        {
            lastValue = value;
            executionCount++;
            await Task.CompletedTask;
        }

        // Act
        await manager.Throttle("key1", () => CreateAction(1), 150, leading: true);
        await Task.Delay(50);
        await manager.Throttle("key1", () => CreateAction(2), 150, leading: true);
        await Task.Delay(50);
        await manager.Throttle("key1", () => CreateAction(3), 150, leading: true);
        await Task.Delay(200); // Wait for trailing execution

        // Assert
        executionCount.Should().BeGreaterThan(1, "because both leading and trailing should execute");
        lastValue.Should().Be(3, "because last value should be captured by trailing execution");
    }

    [Fact]
    public async Task Throttle_DisposedWhilePending_CancelsPending()
    {
        // Arrange
        var manager = new ThrottleManager();
        var executionCount = 0;
        async Task Action()
        {
            executionCount++;
            await Task.CompletedTask;
        }

        // Act
        await manager.Throttle("key1", Action, 100, leading: false);
        manager.Dispose(); // Dispose before execution
        await Task.Delay(150);

        // Assert
        executionCount.Should().Be(0, "because pending action should be cancelled on dispose");
    }

    [Fact]
    public async Task Throttle_WithNullKey_ThrowsArgumentNullException()
    {
        // Arrange
        using var manager = new ThrottleManager();
        async Task Action() => await Task.CompletedTask;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await manager.Throttle(null!, Action, 100));
    }

    [Fact]
    public async Task Throttle_WithNullAction_ThrowsArgumentNullException()
    {
        // Arrange
        using var manager = new ThrottleManager();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await manager.Throttle("key1", null!, 100));
    }

    [Fact]
    public async Task Throttle_WithNegativeInterval_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var manager = new ThrottleManager();
        async Task Action() => await Task.CompletedTask;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await manager.Throttle("key1", Action, -100));
    }

    [Fact]
    public async Task Throttle_WithZeroInterval_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var manager = new ThrottleManager();
        async Task Action() => await Task.CompletedTask;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await manager.Throttle("key1", Action, 0));
    }

    [Fact]
    public async Task CancelAllAsync_CancelsAllPendingActions()
    {
        // Arrange
        using var manager = new ThrottleManager();
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
        await manager.Throttle("key1", Action1, 100, leading: false);
        await manager.Throttle("key2", Action2, 100, leading: false);
        await manager.CancelAllAsync();
        await Task.Delay(150);

        // Assert
        count1.Should().Be(0, "because all actions should be cancelled");
        count2.Should().Be(0, "because all actions should be cancelled");
    }

    [Fact]
    public async Task Throttle_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var manager = new ThrottleManager();
        async Task Action() => await Task.CompletedTask;

        // Act
        manager.Dispose();

        // Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await manager.Throttle("key1", Action, 100));
    }

    [Fact]
    public async Task Throttle_ConcurrentCalls_ThreadSafe()
    {
        // Arrange
        using var manager = new ThrottleManager();
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
        for (int i = 0; i < 20; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                await manager.Throttle("key1", Action, 100, leading: true);
                await Task.Delay(10);
            }));
        }
        await Task.WhenAll(tasks);
        await Task.Delay(250); // Wait for any trailing executions

        // Assert
        executionCount.Should().BeGreaterThan(0, "because at least one execution should occur");
        executionCount.Should().BeLessThan(20, "because throttling should limit executions");
    }

    [Fact]
    public async Task Throttle_LongRunningAction_DoesNotBlockOthers()
    {
        // Arrange
        using var manager = new ThrottleManager();
        var count1 = 0;
        var count2 = 0;
        async Task LongAction()
        {
            count1++;
            await Task.Delay(200); // Simulate long-running operation
        }
        async Task QuickAction()
        {
            count2++;
            await Task.CompletedTask;
        }

        // Act
        await manager.Throttle("key1", LongAction, 100, leading: true);
        await Task.Delay(50);
        await manager.Throttle("key2", QuickAction, 100, leading: true);
        await Task.Delay(50);

        // Assert
        count1.Should().Be(1, "because long action should start");
        count2.Should().Be(1, "because quick action should not be blocked by long action");
    }

    [Fact]
    public async Task Throttle_RepeatedCallsWithInterval_ExecutesMultipleTimes()
    {
        // Arrange
        using var manager = new ThrottleManager();
        var executionCount = 0;
        async Task Action()
        {
            executionCount++;
            await Task.CompletedTask;
        }

        // Act - Call multiple times with enough delay between calls
        await manager.Throttle("key1", Action, 50, leading: true);
        await Task.Delay(100); // Wait longer than interval
        await manager.Throttle("key1", Action, 50, leading: true);
        await Task.Delay(100); // Wait longer than interval
        await manager.Throttle("key1", Action, 50, leading: true);
        await Task.Delay(50);

        // Assert
        executionCount.Should().Be(3, "because each call is spaced by more than the interval");
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var manager = new ThrottleManager();

        // Act & Assert
        manager.Dispose();
        manager.Dispose(); // Should not throw
        manager.Dispose(); // Should not throw
    }

    [Fact]
    public async Task Throttle_MixedLeadingTrailing_WorksCorrectly()
    {
        // Arrange
        using var manager = new ThrottleManager();
        var key1Count = 0;
        var key2Count = 0;
        async Task Action1()
        {
            key1Count++;
            await Task.CompletedTask;
        }
        async Task Action2()
        {
            key2Count++;
            await Task.CompletedTask;
        }

        // Act
        await manager.Throttle("key1", Action1, 100, leading: true);
        await manager.Throttle("key2", Action2, 100, leading: false);
        await Task.Delay(50);

        // Assert
        key1Count.Should().Be(1, "because key1 has leading edge");
        key2Count.Should().Be(0, "because key2 has no leading edge");

        // Wait for trailing execution
        await Task.Delay(100);
        key2Count.Should().Be(1, "because key2 should execute after interval");
    }
}
