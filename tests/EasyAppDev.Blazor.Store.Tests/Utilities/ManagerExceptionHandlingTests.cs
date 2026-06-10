using EasyAppDev.Blazor.Store.Utilities;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace EasyAppDev.Blazor.Store.Tests.Utilities;

/// <summary>
/// Regression tests for DebounceManager/ThrottleManager: fire-and-forget action
/// exceptions must be logged (not silently swallowed), and disposal with pending
/// actions must not blow up in-flight cleanup.
/// </summary>
public class ManagerExceptionHandlingTests
{
    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = new();
        public List<Exception?> Exceptions { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (Messages)
            {
                Messages.Add(formatter(state, exception));
                Exceptions.Add(exception);
            }
        }
    }

    [Fact]
    public async Task Debounce_ActionThrows_LogsException()
    {
        // Arrange
        var logger = new ListLogger<DebounceManager>();
        using var manager = new DebounceManager(logger);

        // Act
        await manager.Debounce("key", () => throw new InvalidOperationException("boom"), 10);
        await Task.Delay(300);

        // Assert
        lock (logger.Messages)
        {
            logger.Messages.Should().Contain(m => m.Contains("failed"));
            logger.Exceptions.Should().Contain(e => e is InvalidOperationException);
        }
    }

    [Fact]
    public async Task Debounce_AfterActionThrew_StillWorksForSubsequentActions()
    {
        // Arrange
        using var manager = new DebounceManager();
        var executed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Act - first action throws, second must still run
        await manager.Debounce("key", () => throw new InvalidOperationException("boom"), 10);
        await Task.Delay(100);
        await manager.Debounce("key", () =>
        {
            executed.TrySetResult();
            return Task.CompletedTask;
        }, 10);

        var completed = await Task.WhenAny(executed.Task, Task.Delay(2000)) == executed.Task;

        // Assert
        completed.Should().BeTrue("because a previous action failure must not poison the key");
    }

    [Fact]
    public async Task Throttle_TrailingActionThrows_LogsException()
    {
        // Arrange
        var logger = new ListLogger<ThrottleManager>();
        using var manager = new ThrottleManager(logger);

        // Act - leading=false schedules a trailing (fire-and-forget) execution
        await manager.Throttle(
            "key",
            () => throw new InvalidOperationException("boom"),
            10,
            leading: false);
        await Task.Delay(300);

        // Assert
        lock (logger.Messages)
        {
            logger.Messages.Should().Contain(m => m.Contains("failed"));
            logger.Exceptions.Should().Contain(e => e is InvalidOperationException);
        }
    }

    [Fact]
    public async Task DebounceManager_Dispose_WithPendingAction_DoesNotThrow()
    {
        // Arrange
        var manager = new DebounceManager();
        await manager.Debounce("key", () => Task.CompletedTask, 200);

        // Act + Assert - disposing with a pending action must not throw, and the
        // pending action's cleanup must not crash after dispose.
        var act = () => manager.Dispose();
        act.Should().NotThrow();
        await Task.Delay(300);
    }

    [Fact]
    public async Task ThrottleManager_Dispose_WithPendingTrailingAction_DoesNotThrow()
    {
        // Arrange
        var manager = new ThrottleManager();
        await manager.Throttle("key", () => Task.CompletedTask, 200, leading: false);

        // Act + Assert
        var act = () => manager.Dispose();
        act.Should().NotThrow();
        await Task.Delay(300);
    }
}
