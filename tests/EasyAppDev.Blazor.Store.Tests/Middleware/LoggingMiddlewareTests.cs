using EasyAppDev.Blazor.Store.Middleware;

namespace EasyAppDev.Blazor.Store.Tests.Middleware;

public class LoggingMiddlewareTests
{
    [Fact]
    public async Task LoggingMiddleware_LogsStateChanges()
    {
        // Arrange
        var logs = new List<string>();
        var middleware = new LoggingMiddleware<TestState>(log => logs.Add(log));

        var store = StoreBuilder<TestState>
            .Create(new TestState(0, "Initial"))
            .WithMiddleware(middleware)
            .Build();

        // Act
        await store.UpdateAsync(
            state => state with { Counter = 1 },
            action: "INCREMENT");

        // Assert
        logs.Should().HaveCount(2); // Before and After
        logs[0].Should().Contain("Updating state");
        logs[0].Should().Contain("INCREMENT");
        logs[1].Should().Contain("State Updated");
        logs[1].Should().Contain("INCREMENT");
    }

    [Fact]
    public void WithLogging_AddsLoggingMiddleware()
    {
        // Arrange
        var logs = new List<string>();

        // Act
        var store = StoreBuilder<TestState>
            .Create(new TestState(0, "Initial"))
            .WithLogging(log => logs.Add(log))
            .Build();

        store.Update(state => state with { Counter = 1 });

        // Assert
        logs.Should().NotBeEmpty();
    }

    [Fact]
    public async Task LoggingMiddleware_SerializesStateProperly()
    {
        // Arrange
        var logs = new List<string>();
        var middleware = new LoggingMiddleware<TestState>(log => logs.Add(log));

        var store = StoreBuilder<TestState>
            .Create(new TestState(0, "Initial"))
            .WithMiddleware(middleware)
            .Build();

        // Act
        await store.UpdateAsync(
            state => state with { Counter = 42, Message = "Updated" },
            action: "SET_STATE");

        // Assert
        var afterLog = logs[1];
        afterLog.Should().Contain("Previous State:");
        afterLog.Should().Contain("Current State:");
        afterLog.Should().Contain("42"); // New counter value
        afterLog.Should().Contain("Updated"); // New message
    }

    [Fact]
    public void LoggingMiddleware_WithoutCustomLogger_UsesConsole()
    {
        // Arrange
        var middleware = new LoggingMiddleware<TestState>();

        // Act & Assert - should not throw
        var act = async () =>
        {
            await middleware.OnBeforeUpdateAsync(new TestState(0, "Test"), "ACTION");
            await middleware.OnAfterUpdateAsync(
                new TestState(0, "Test"),
                new TestState(1, "Test"),
                "ACTION");
        };

        act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task LoggingMiddleware_WithNullAction_DisplaysUnknownAction()
    {
        // Arrange
        var logs = new List<string>();
        var middleware = new LoggingMiddleware<TestState>(log => logs.Add(log));

        var store = StoreBuilder<TestState>
            .Create(new TestState(0, "Initial"))
            .WithMiddleware(middleware)
            .Build();

        // Act
        await store.UpdateAsync(state => state with { Counter = 1 });

        // Assert
        logs[1].Should().Contain("UNKNOWN_ACTION");
    }

    [Fact]
    public async Task WithLogging_CanBeChainedWithOtherMiddleware()
    {
        // Arrange
        var logs = new List<string>();
        var customMiddlewareCalled = false;
        var customMiddleware = new CustomMiddleware<TestState>(() => customMiddlewareCalled = true);

        // Act
        var store = StoreBuilder<TestState>
            .Create(new TestState(0, "Initial"))
            .WithLogging(log => logs.Add(log))
            .WithMiddleware(customMiddleware)
            .Build();

        await store.UpdateAsync(state => state with { Counter = 1 });

        // Assert
        logs.Should().NotBeEmpty();
        customMiddlewareCalled.Should().BeTrue();
    }

    private class CustomMiddleware<TState> : IMiddleware<TState> where TState : notnull
    {
        private readonly Action _onCalled;

        public CustomMiddleware(Action onCalled)
        {
            _onCalled = onCalled;
        }

        public Task OnBeforeUpdateAsync(TState currentState, string? action)
        {
            return Task.CompletedTask;
        }

        public Task OnAfterUpdateAsync(TState previousState, TState currentState, string? action)
        {
            _onCalled();
            return Task.CompletedTask;
        }
    }
}
