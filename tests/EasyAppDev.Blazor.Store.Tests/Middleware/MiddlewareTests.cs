using EasyAppDev.Blazor.Store.Middleware;

namespace EasyAppDev.Blazor.Store.Tests.Middleware;

public record TestState(int Counter, string Message);

public class MiddlewareTests
{
    [Fact]
    public async Task OnBeforeUpdateAsync_CalledBeforeStateUpdate()
    {
        // Arrange
        var middleware = new TestMiddleware<TestState>();
        var store = StoreBuilder<TestState>
            .Create(new TestState(0, "Initial"))
            .WithMiddleware(middleware)
            .Build();

        // Act
        await store.UpdateAsync(state => state with { Counter = 1 });

        // Assert
        middleware.BeforeUpdateCalls.Should().Be(1);
        middleware.BeforeUpdateState.Should().NotBeNull();
        middleware.BeforeUpdateState!.Counter.Should().Be(0);
    }

    [Fact]
    public async Task OnAfterUpdateAsync_CalledAfterStateUpdate()
    {
        // Arrange
        var middleware = new TestMiddleware<TestState>();
        var store = StoreBuilder<TestState>
            .Create(new TestState(0, "Initial"))
            .WithMiddleware(middleware)
            .Build();

        // Act
        await store.UpdateAsync(state => state with { Counter = 1 });

        // Assert
        middleware.AfterUpdateCalls.Should().Be(1);
        middleware.PreviousState.Should().NotBeNull();
        middleware.PreviousState!.Counter.Should().Be(0);
        middleware.CurrentState.Should().NotBeNull();
        middleware.CurrentState!.Counter.Should().Be(1);
    }

    [Fact]
    public async Task Middleware_ReceivesActionName()
    {
        // Arrange
        var middleware = new TestMiddleware<TestState>();
        var store = StoreBuilder<TestState>
            .Create(new TestState(0, "Initial"))
            .WithMiddleware(middleware)
            .Build();

        // Act
        await store.UpdateAsync(
            state => state with { Counter = 1 },
            action: "INCREMENT");

        // Assert
        middleware.Action.Should().Be("INCREMENT");
    }

    [Fact]
    public async Task MultipleMiddlewares_ExecuteInOrder()
    {
        // Arrange
        var executionOrder = new List<string>();
        var middleware1 = new OrderTrackingMiddleware<TestState>("M1", executionOrder);
        var middleware2 = new OrderTrackingMiddleware<TestState>("M2", executionOrder);

        var store = StoreBuilder<TestState>
            .Create(new TestState(0, "Initial"))
            .WithMiddlewares(middleware1, middleware2)
            .Build();

        // Act
        await store.UpdateAsync(state => state with { Counter = 1 });

        // Assert
        executionOrder.Should().Equal("M1-Before", "M2-Before", "M1-After", "M2-After");
    }

    [Fact]
    public async Task MiddlewareException_DoesNotStopOtherMiddleware()
    {
        // Arrange
        var goodMiddleware = new TestMiddleware<TestState>();
        var badMiddleware = new ThrowingMiddleware<TestState>();

        var store = StoreBuilder<TestState>
            .Create(new TestState(0, "Initial"))
            .WithMiddlewares(badMiddleware, goodMiddleware)
            .Build();

        // Act
        await store.UpdateAsync(state => state with { Counter = 1 });

        // Assert - good middleware should still execute
        goodMiddleware.AfterUpdateCalls.Should().Be(1);
    }

    [Fact]
    public async Task AsyncUpdate_CallsMiddleware()
    {
        // Arrange
        var middleware = new TestMiddleware<TestState>();
        var store = StoreBuilder<TestState>
            .Create(new TestState(0, "Initial"))
            .WithMiddleware(middleware)
            .Build();

        // Act
        await store.UpdateAsync(state => state with { Counter = 1 });

        // Assert
        middleware.BeforeUpdateCalls.Should().Be(1);
        middleware.AfterUpdateCalls.Should().Be(1);
    }

    [Fact]
    public async Task NoStateChange_DoesNotCallAfterUpdate()
    {
        // Arrange
        var middleware = new TestMiddleware<TestState>();
        var store = StoreBuilder<TestState>
            .Create(new TestState(0, "Initial"))
            .WithMiddleware(middleware)
            .Build();

        // Act - update to same state
        await store.UpdateAsync(state => state);

        // Assert - before is called, but after is not because state didn't change
        middleware.BeforeUpdateCalls.Should().Be(1);
        middleware.AfterUpdateCalls.Should().Be(0);
    }

    private class TestMiddleware<TState> : IMiddleware<TState> where TState : notnull
    {
        public int BeforeUpdateCalls { get; private set; }
        public int AfterUpdateCalls { get; private set; }
        public TState? BeforeUpdateState { get; private set; }
        public TState? PreviousState { get; private set; }
        public TState? CurrentState { get; private set; }
        public string? Action { get; private set; }

        public Task OnBeforeUpdateAsync(TState currentState, string? action)
        {
            BeforeUpdateCalls++;
            BeforeUpdateState = currentState;
            Action = action;
            return Task.CompletedTask;
        }

        public Task OnAfterUpdateAsync(TState previousState, TState currentState, string? action)
        {
            AfterUpdateCalls++;
            PreviousState = previousState;
            CurrentState = currentState;
            Action = action;
            return Task.CompletedTask;
        }
    }

    private class OrderTrackingMiddleware<TState> : IMiddleware<TState>
        where TState : notnull
    {
        private readonly string _name;
        private readonly List<string> _executionOrder;

        public OrderTrackingMiddleware(string name, List<string> executionOrder)
        {
            _name = name;
            _executionOrder = executionOrder;
        }

        public Task OnBeforeUpdateAsync(TState currentState, string? action)
        {
            _executionOrder.Add($"{_name}-Before");
            return Task.CompletedTask;
        }

        public Task OnAfterUpdateAsync(TState previousState, TState currentState, string? action)
        {
            _executionOrder.Add($"{_name}-After");
            return Task.CompletedTask;
        }
    }

    private class ThrowingMiddleware<TState> : IMiddleware<TState>
        where TState : notnull
    {
        public Task OnBeforeUpdateAsync(TState currentState, string? action)
        {
            throw new InvalidOperationException("Test exception");
        }

        public Task OnAfterUpdateAsync(TState previousState, TState currentState, string? action)
        {
            throw new InvalidOperationException("Test exception");
        }
    }
}
